using AAHBRANT.SST.Application.GeracaoPgr;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.GeracaoPgr;

// Nova revisão do PGR (07/11/2026): parte da última geração aprovada; mudança só acrescenta, periódica exige a
// confirmação de reavaliação (NR-01).
public class RevisaoGeracaoPgrTests
{
    private sealed class IaFalsa : IGeradorPgrIa
    {
        public List<GheGerado> Ghes { get; set; } = new();
        public List<string> AtividadesEnviadas { get; private set; } = new();
        public bool ChamouGhes { get; private set; }

        public Task<List<ItemEapGerado>> LerEapAsync(string textoEap, CancellationToken ct) => Task.FromResult(new List<ItemEapGerado>());
        public Task<List<GheGerado>> SugerirGhesAsync(IReadOnlyList<ItemEapGerado> atividades, IReadOnlyList<string> funcoesCadastradas, CancellationToken ct)
        {
            ChamouGhes = true;
            AtividadesEnviadas = atividades.Select(a => a.Codigo).ToList();
            return Task.FromResult(Ghes);
        }
        public Task<List<RiscoGerado>> GerarRiscosAsync(ContextoRiscosAtividade contexto, CancellationToken ct) => Task.FromResult(new List<RiscoGerado>());
        public Task<List<AcompanhamentoSugeridoIa>> SugerirAcompanhamentoAsync(ContextoAcompanhamentoPlano contexto, CancellationToken ct) =>
            Task.FromResult(new List<AcompanhamentoSugeridoIa>());
        public Task<List<CboSugeridoIa>> SugerirCboAsync(IReadOnlyList<FuncaoParaCbo> funcoes, CancellationToken ct) => Task.FromResult(new List<CboSugeridoIa>());
        public Task<SugestaoEmergenciaIa> SugerirEmergenciaAsync(ContextoEmergenciaIa contexto, CancellationToken ct) =>
            Task.FromResult(new SugestaoEmergenciaIa(new List<RecomendacaoEmergenciaIa>(), new List<string>()));
    }

    private static async Task<(Infrastructure.Persistencia.SstDbContext Db, Obra Obra)> CriarComAprovadaAsync()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB-01", Nome = "Obra Teste" };
        var estrutura = new EstruturaGeracaoPgr
        {
            Itens = new()
            {
                new ItemEapGerado { Codigo = "1", Nome = "Estrutura", Nivel = 1 },
                new ItemEapGerado { Codigo = "1.1", Nome = "Concretagem", Nivel = 2 },
            },
            Ghes = new()
            {
                new GheGerado
                {
                    Numero = 1,
                    Nome = "Estrutura",
                    Funcoes = new() { new FuncaoGerada { Nome = "Armador", Descricao = "Monta armaduras" } },
                    Atividades = new()
                    {
                        new AtividadeGerada
                        {
                            CodigoEap = "1.1", Nome = "Concretagem", RiscosGerados = true,
                            Riscos = new() { new RiscoGerado { Tipo = "Acidente", Perigo = "Queda de altura", Probabilidade = 2, Severidade = 3, Revisado = true } },
                        },
                    },
                },
            },
        };
        db.AddRange(obra, new GeracaoPgrEap
        {
            ObraId = obra.Id, Nome = "PGR Obra Teste", DataElaboracao = new DateTime(2026, 1, 1), NomeArquivoEap = "eap.xlsx",
            Status = StatusGeracaoPgrEap.Aprovado, AprovadoEmUtc = new DateTime(2026, 1, 2), Passo = 5,
            EstruturaJson = System.Text.Json.JsonSerializer.Serialize(estrutura, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),
        });
        await db.SaveChangesAsync();
        return (db, obra);
    }

    private static CriarRevisaoGeracaoPgrCommand Comando(Guid obraId, string tipo, bool confirmou = false) =>
        new(obraId, tipo, new DateTime(2027, 1, 1), new DateTime(2029, 1, 1), new DateTime(2029, 1, 1), "obs", confirmou, null, "Eng. Ana");

    [Fact]
    public async Task Periodica_SemConfirmarReavaliacao_Recusa()
    {
        var (db, obra) = await CriarComAprovadaAsync();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CriarRevisaoGeracaoPgrCommandHandler(db).Handle(Comando(obra.Id, RevisaoGeracaoPgr.Periodica), default));
        Assert.Contains("reavaliou", ex.Message);
    }

    [Fact]
    public async Task Periodica_CopiaInventario_AbreNoEnvio_ERegistraQuemReavaliou()
    {
        var (db, obra) = await CriarComAprovadaAsync();
        var id = await new CriarRevisaoGeracaoPgrCommandHandler(db).Handle(Comando(obra.Id, RevisaoGeracaoPgr.Periodica, true), default);

        var g = await db.GeracoesPgrEap.SingleAsync(x => x.Id == id);
        Assert.Equal(4, g.Passo);
        Assert.Equal(StatusGeracaoPgrEap.Rascunho, g.Status);
        var e = SerializacaoGeracaoTeste.Ler(g.EstruturaJson);
        Assert.True(e.Revisao!.ReavaliacaoConfirmada);
        Assert.Equal("Eng. Ana", e.Revisao.ReavaliadoPorNome);
        var atividade = Assert.Single(e.Ghes.Single().Atividades);
        Assert.True(atividade.Herdada);
        Assert.True(Assert.Single(atividade.Riscos).Revisado);
    }

    [Fact]
    public async Task SoUmaRevisaoEmAndamentoPorObra()
    {
        var (db, obra) = await CriarComAprovadaAsync();
        await new CriarRevisaoGeracaoPgrCommandHandler(db).Handle(Comando(obra.Id, RevisaoGeracaoPgr.Mudanca), default);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CriarRevisaoGeracaoPgrCommandHandler(db).Handle(Comando(obra.Id, RevisaoGeracaoPgr.Mudanca), default));
    }

    [Fact]
    public async Task Mudanca_SoItemNovoVaiParaIa_EMantemGheAprovado()
    {
        var (db, obra) = await CriarComAprovadaAsync();
        var id = await new CriarRevisaoGeracaoPgrCommandHandler(db).Handle(Comando(obra.Id, RevisaoGeracaoPgr.Mudanca), default);

        // O técnico inclui dois itens novos: um cai no GHE que já existe, o outro abre um GHE novo.
        var g = await db.GeracoesPgrEap.SingleAsync(x => x.Id == id);
        Assert.Equal(2, g.Passo);
        var e = SerializacaoGeracaoTeste.Ler(g.EstruturaJson);
        Assert.All(e.Itens, i => Assert.True(i.Herdado));
        e.Itens.Add(new ItemEapGerado { Codigo = "1.2", Nome = "Armação", Nivel = 2, Manual = true });
        e.Itens.Add(new ItemEapGerado { Codigo = "2.1", Nome = "Impermeabilização", Nivel = 2, Manual = true });
        g.EstruturaJson = System.Text.Json.JsonSerializer.Serialize(e, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        await db.SaveChangesAsync();

        var ia = new IaFalsa
        {
            Ghes = new()
            {
                new GheGerado { Nome = "Estrutura", Funcoes = new() { new FuncaoGerada { Nome = "Armador" } }, Atividades = new() { new AtividadeGerada { CodigoEap = "1.2" } } },
                new GheGerado { Nome = "Impermeabilização", Funcoes = new() { new FuncaoGerada { Nome = "Impermeabilizador", Descricao = "x" } }, Atividades = new() { new AtividadeGerada { CodigoEap = "2.1" } } },
            },
        };
        await new SugerirGhesGeracaoPgrCommandHandler(db, ia).Handle(new SugerirGhesGeracaoPgrCommand(id), default);

        Assert.DoesNotContain("1.1", ia.AtividadesEnviadas);
        Assert.Contains("1.2", ia.AtividadesEnviadas);
        Assert.Contains("2.1", ia.AtividadesEnviadas);

        var final = SerializacaoGeracaoTeste.Ler((await db.GeracoesPgrEap.SingleAsync(x => x.Id == id)).EstruturaJson);
        var estrutura = final.Ghes.Single(x => x.Nome == "Estrutura");
        Assert.Equal(1, estrutura.Numero);
        Assert.Single(estrutura.Funcoes); // Armador não duplica
        var mantida = estrutura.Atividades.Single(a => a.CodigoEap == "1.1");
        Assert.True(mantida.Herdada);
        Assert.True(mantida.Riscos.Single().Revisado);
        Assert.Contains(estrutura.Atividades, a => a.CodigoEap == "1.2" && !a.Herdada);
        Assert.True(final.Ghes.Single(x => x.Nome == "Impermeabilização").Numero > 1);
    }

    [Fact]
    public async Task Mudanca_SemItemNovo_NaoChamaIa()
    {
        var (db, obra) = await CriarComAprovadaAsync();
        var id = await new CriarRevisaoGeracaoPgrCommandHandler(db).Handle(Comando(obra.Id, RevisaoGeracaoPgr.Mudanca), default);
        var ia = new IaFalsa();
        await new SugerirGhesGeracaoPgrCommandHandler(db, ia).Handle(new SugerirGhesGeracaoPgrCommand(id), default);
        Assert.False(ia.ChamouGhes);
        Assert.Equal(3, (await db.GeracoesPgrEap.SingleAsync(x => x.Id == id)).Passo);
    }
}
