using System.Text.Json;
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.GeracaoPgr;
using AAHBRANT.SST.Application.Ghes.Commands;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Ghes;

public class PreservacaoAtividadesOsTests
{
    private sealed class Numerador : IGeradorNumeroDocumentoService
    {
        public Task<string> GerarAsync(string prefixo, CancellationToken ct) => Task.FromResult($"{prefixo}-TESTE");
    }

    private static async Task<(SstDbContext Db, Obra Obra, Funcao Funcao)> CriarAsync(bool eap)
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB", Nome = "Obra" };
        var funcao = new Funcao { Nome = "Armador" };
        var matriz = new MatrizRiscoConfig { Nome = "5x5", NumNiveisProbabilidade = 5, NumNiveisSeveridade = 5 };
        matriz.Celulas.Add(new MatrizRiscoCelula { Probabilidade = 2, Severidade = 3, NivelRisco = NivelRisco.Moderado });
        matriz.Celulas.Add(new MatrizRiscoCelula { Probabilidade = 3, Severidade = 3, NivelRisco = NivelRisco.Alto });
        db.AddRange(obra, funcao, matriz);
        await db.SaveChangesAsync();
        await GerarAsync(db, obra, funcao, eap);
        return (db, obra, funcao);
    }

    private static async Task GerarAsync(SstDbContext db, Obra obra, Funcao funcao, bool eap,
        string ambiente = "Laje", string descricaoFuncao = "Monta armaduras", int probabilidade = 2,
        string atividadeNome = "Armação", bool atividadeNova = false)
    {
        if (eap)
        {
            var atividade = new AtividadeGerada
            {
                CodigoEap = "1.1", Nome = atividadeNome, RiscosGerados = true,
                Riscos = new()
                {
                    new RiscoGerado
                    {
                        Tipo = "Acidente", Perigo = "Queda", Probabilidade = probabilidade, Severidade = 3,
                        Revisado = true, Medidas = new()
                        {
                            new MedidaGerada { Fase = 1, Texto = "Instale guarda-corpo", Categoria = CategoriasMedida.Epc, Decisao = "Aceita" },
                        },
                    },
                },
            };
            var estrutura = new EstruturaGeracaoPgr
            {
                DataTermino = new DateTime(2028, 1, 1),
                Complementares = new DadosComplementaresPgr
                {
                    Cnae = "4120", GrauRisco = 3, ResponsavelTecnicoRegistro = "CREA TESTE", ResponsavelImplantacaoNome = "Responsável",
                },
                Ghes = new()
                {
                    new GheGerado
                    {
                        Numero = 1, Nome = "Estrutura", Ambiente = ambiente,
                        Funcoes = new() { new FuncaoGerada { FuncaoId = funcao.Id, Nome = funcao.Nome, Descricao = descricaoFuncao } },
                        Atividades = new() { atividade },
                    },
                },
            };
            if (atividadeNova) estrutura.Ghes[0].Atividades.Add(new AtividadeGerada { CodigoEap = "1.2", Nome = "Concretagem", RiscosGerados = true });
            var geracao = new GeracaoPgrEap
            {
                ObraId = obra.Id, Nome = "PGR", DataElaboracao = new DateTime(2026, 11, 11), NomeArquivoEap = "eap.xlsx",
                Status = StatusGeracaoPgrEap.AguardandoAprovacao,
                EstruturaJson = JsonSerializer.Serialize(estrutura, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            };
            db.GeracoesPgrEap.Add(geracao);
            await db.SaveChangesAsync();
            await new AprovarGeracaoPgrCommandHandler(db).Handle(new AprovarGeracaoPgrCommand(geracao.Id, null, "Engenheira"), default);
        }
        else
        {
            var pgrId = await db.Pgrs.Where(p => p.ObraId == obra.Id).Select(p => (Guid?)p.Id).FirstOrDefaultAsync();
            await new ImportarEstruturaSstObraCommandHandler(db, new Numerador()).Handle(new ImportarEstruturaSstObraCommand(
                obra.Id, pgrId is null ? new ImportarEstruturaPgr("PGR", new DateTime(2026, 11, 11), null, null, null) : null,
                null, new()
                {
                    new ImportarEstruturaGhe(1, "Estrutura", null, ambiente, null, null, null,
                        new() { new ImportarEstruturaFuncao(funcao.Nome, null, descricaoFuncao, null, null, funcao.Id) },
                        new() { new ImportarEstruturaRisco("Acidente", "Queda", null, null, "Guarda-corpo", null, probabilidade, 3, null, null) }),
                }, null, null, null, PgrIdExistente: pgrId, SubstituirEstruturaAtual: pgrId is not null), default);
        }
        db.ChangeTracker.Clear();
    }

    private static async Task<(Guid AtividadeId, Guid DecisaoId, Guid DocumentoId)> PrepararDetalhesAsync(SstDbContext db, Funcao funcao,
        StatusMedidaPrevencao status = StatusMedidaPrevencao.Validada)
    {
        var atividade = await db.Atividades.EmUso(db).SingleAsync();
        atividade.CondicoesExecucao = "Sem chuva";
        atividade.Ferramentas = "Torquês";
        var decisao = new AtividadeFuncao
        {
            ObraId = atividade.ObraId, AtividadeId = atividade.Id, FuncaoId = funcao.Id,
            Status = status, OrigemVinculo = OrigemMedidaPrevencao.SugestaoIa, JustificativaIa = "Atividade da função",
            ValidadoPorUsuarioId = Guid.NewGuid(), ValidadoPorNome = "Engenheira", ValidadoEmUtc = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc),
        };
        var documento = new AtividadeDocumento { AtividadeId = atividade.Id, MaterialApoioId = Guid.NewGuid() };
        db.AddRange(decisao, documento,
            new AtividadeEpc { AtividadeId = atividade.Id, CatalogoEpcId = Guid.NewGuid() },
            new AtividadeItemOs
            {
                AtividadeId = atividade.Id, Tipo = TipoItemAtividadeOs.Restricao, Texto = "Não iniciar sem liberação",
                Requisito = "Autorização", Status = StatusMedidaPrevencao.Pendente, OrigemItem = OrigemMedidaPrevencao.SugestaoIa,
            },
            new OrdemServico { ObraId = atividade.ObraId, TrabalhadorId = Guid.NewGuid(), Numero = "OS-TESTE", ConteudoJson = "{\"imutavel\":true}" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (atividade.Id, decisao.Id, documento.Id);
    }

    [Theory]
    [InlineData(true, StatusMedidaPrevencao.Validada)]
    [InlineData(false, StatusMedidaPrevencao.Validada)]
    [InlineData(true, StatusMedidaPrevencao.Descartada)]
    [InlineData(false, StatusMedidaPrevencao.Descartada)]
    [InlineData(true, StatusMedidaPrevencao.Pendente)]
    [InlineData(false, StatusMedidaPrevencao.Pendente)]
    public async Task RevisaoEquivalente_PreservaDetalhesDecisaoAutoriaEHistorico(bool eap, StatusMedidaPrevencao status)
    {
        var (db, obra, funcao) = await CriarAsync(eap);
        var anterior = await PrepararDetalhesAsync(db, funcao, status);
        var decisaoAnterior = await db.AtividadesFuncoes.AsNoTracking().SingleAsync();

        await GerarAsync(db, obra, funcao, eap);

        var atual = await db.Atividades.EmUso(db).SingleAsync();
        Assert.NotEqual(anterior.AtividadeId, atual.Id);
        Assert.Equal("Sem chuva", atual.CondicoesExecucao);
        Assert.Equal("Torquês", atual.Ferramentas);
        var decisao = await db.AtividadesFuncoes.SingleAsync(x => x.AtividadeId == atual.Id);
        Assert.Equal(status, decisao.Status);
        Assert.Equal(decisaoAnterior.ValidadoEmUtc, decisao.ValidadoEmUtc);
        Assert.Equal(decisaoAnterior.ValidadoPorUsuarioId, decisao.ValidadoPorUsuarioId);
        Assert.Equal(decisaoAnterior.ValidadoPorNome, decisao.ValidadoPorNome);
        Assert.Equal(decisaoAnterior.JustificativaIa, decisao.JustificativaIa);
        var documento = await db.AtividadeDocumentos.SingleAsync(x => x.AtividadeId == atual.Id);
        var documentoAnterior = await db.AtividadeDocumentos.SingleAsync(x => x.Id == anterior.DocumentoId);
        Assert.NotEqual(documentoAnterior.Id, documento.Id);
        Assert.Equal(documentoAnterior.MaterialApoioId, documento.MaterialApoioId);
        Assert.Single(await db.AtividadeEpcs.Where(x => x.AtividadeId == atual.Id).ToListAsync());
        Assert.Equal(StatusMedidaPrevencao.Pendente, (await db.AtividadeItensOs.SingleAsync(x => x.AtividadeId == atual.Id)).Status);
        Assert.Equal(status, (await db.AtividadesFuncoes.SingleAsync(x => x.Id == anterior.DecisaoId)).Status);
        Assert.Equal("Sem chuva", (await db.Atividades.SingleAsync(x => x.Id == anterior.AtividadeId)).CondicoesExecucao);
        Assert.Equal("{\"imutavel\":true}", (await db.OrdensServico.SingleAsync()).ConteudoJson);
    }

    [Theory]
    [InlineData(true, "ambiente")]
    [InlineData(false, "ambiente")]
    [InlineData(true, "risco")]
    [InlineData(false, "risco")]
    [InlineData(true, "atividade")]
    public async Task MudancaDeConteudo_NaoReaproveitaConfirmacaoNemProcedimento(bool eap, string mudanca)
    {
        var (db, obra, funcao) = await CriarAsync(eap);
        await PrepararDetalhesAsync(db, funcao);
        await GerarAsync(db, obra, funcao, eap, ambiente: mudanca == "ambiente" ? "Subsolo" : "Laje",
            probabilidade: mudanca == "risco" ? 3 : 2, atividadeNome: mudanca == "atividade" ? "Desforma" : "Armação");
        var atual = await db.Atividades.EmUso(db).SingleAsync();
        Assert.False(await db.AtividadesFuncoes.AnyAsync(x => x.AtividadeId == atual.Id));
        Assert.False(await db.AtividadeDocumentos.AnyAsync(x => x.AtividadeId == atual.Id));
        Assert.Null(atual.CondicoesExecucao);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FuncaoComDescricaoAlterada_NaoHerdaDecisao(bool eap)
    {
        var (db, obra, funcao) = await CriarAsync(eap);
        await PrepararDetalhesAsync(db, funcao);
        await GerarAsync(db, obra, funcao, eap, descricaoFuncao: "Supervisiona sem executar");
        var atual = await db.Atividades.EmUso(db).SingleAsync();
        Assert.False(await db.AtividadesFuncoes.AnyAsync(x => x.AtividadeId == atual.Id));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OrigemComAtividadeDuplicada_NaoEscolheUmaArbitrariamente(bool eap)
    {
        var (db, obra, funcao) = await CriarAsync(eap);
        await PrepararDetalhesAsync(db, funcao);
        var anterior = await db.Atividades.EmUso(db).SingleAsync();
        db.Atividades.Add(new Atividade { ObraId = obra.Id, GheId = anterior.GheId, Nome = anterior.Nome, Descricao = anterior.Descricao });
        await db.SaveChangesAsync();
        await GerarAsync(db, obra, funcao, eap);
        var atual = await db.Atividades.EmUso(db).SingleAsync();
        Assert.False(await db.AtividadesFuncoes.AnyAsync(x => x.AtividadeId == atual.Id));
        Assert.False(await db.AtividadeDocumentos.AnyAsync(x => x.AtividadeId == atual.Id));
    }

    [Fact]
    public async Task AtividadeNova_FicaSemDecisaoEExistentePreservaConfirmacao()
    {
        var (db, obra, funcao) = await CriarAsync(true);
        await PrepararDetalhesAsync(db, funcao);
        await GerarAsync(db, obra, funcao, true, atividadeNova: true);
        var atividades = await db.Atividades.EmUso(db).ToListAsync();
        Assert.Equal(2, atividades.Count);
        var antiga = atividades.Single(a => a.Nome == "1.1 Armação");
        var nova = atividades.Single(a => a.Nome == "1.2 Concretagem");
        Assert.True(await db.AtividadesFuncoes.AnyAsync(x => x.AtividadeId == antiga.Id && x.Status == StatusMedidaPrevencao.Validada));
        Assert.False(await db.AtividadesFuncoes.AnyAsync(x => x.AtividadeId == nova.Id));
    }

    [Fact]
    public async Task FuncaoSubstituida_NaoHerdaDecisaoDaFuncaoRemovida()
    {
        var (db, obra, funcao) = await CriarAsync(true);
        await PrepararDetalhesAsync(db, funcao);
        var novaFuncao = new Funcao { Nome = "Supervisor" };
        db.Funcoes.Add(novaFuncao);
        await db.SaveChangesAsync();
        await GerarAsync(db, obra, novaFuncao, true);
        var atual = await db.Atividades.EmUso(db).SingleAsync();
        Assert.False(await db.AtividadesFuncoes.AnyAsync(x => x.AtividadeId == atual.Id));
        Assert.True(await db.AtividadeDocumentos.AnyAsync(x => x.AtividadeId == atual.Id));
    }
}
