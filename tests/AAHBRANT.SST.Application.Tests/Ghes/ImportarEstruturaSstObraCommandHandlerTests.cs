using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Ghes.Commands;
using AAHBRANT.SST.Application.Ghes.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Ghes;

public class ImportarEstruturaSstObraCommandHandlerTests
{
    private sealed class GeradorNumeroFake : IGeradorNumeroDocumentoService
    {
        public Task<string> GerarAsync(string prefixo, CancellationToken ct) => Task.FromResult($"{prefixo}-TESTE-001");
    }

    private static SstDbContext CriarDb(string nome, CurrentUserService? usuario = null) =>
        new(new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nome).Options, usuario ?? new CurrentUserService());

    private static async Task<Obra> SemearAsync(IAppDbContext db)
    {
        var obra = new Obra { Codigo = "ROGER", Nome = "Parque Roger" };
        db.Obras.Add(obra);
        var matriz = new MatrizRiscoConfig { Nome = "5x5", NumNiveisProbabilidade = 5, NumNiveisSeveridade = 5 };
        for (var p = 1; p <= 5; p++)
            for (var s = 1; s <= 5; s++)
                matriz.Celulas.Add(new MatrizRiscoCelula { Probabilidade = p, Severidade = s, NivelRisco = p * s <= 4 ? NivelRisco.Baixo : NivelRisco.Moderado });
        db.MatrizRiscoConfigs.Add(matriz);
        // Função já criada pelo G-RH, em caixa alta e sem acento.
        db.Funcoes.Add(new Funcao { Nome = "PEDREIRO" });
        await db.SaveChangesAsync();
        return obra;
    }

    private static ImportarEstruturaSstObraCommand Comando(Guid obraId) => new(
        obraId,
        new ImportarEstruturaPgr("PGR Parque Roger", new DateTime(2024, 10, 10), new DateTime(2025, 10, 10), new DateTime(2026, 10, 10), null),
        new ImportarEstruturaPcmso("PCMSO Parque Roger", new DateTime(2024, 11, 11), null, null, "Médica", "CRM PB/1"),
        new List<ImportarEstruturaGhe>
        {
            new(1, "Canteiro", "44h", "Céu aberto", "Trabalho em altura", null, null,
                new List<ImportarEstruturaFuncao>
                {
                    new("Pedreiro", "715210", "Alvenaria", 10, null),
                    new("Ajudante/Servente", "717020", null, 16, new List<string> { "Servente" }),
                },
                new List<ImportarEstruturaRisco>
                {
                    new("Físico", "Ruído do ambiente", "Perda auditiva", "Qualitativa", "Sinalização", "Protetor auricular", 1, 3, "3 - Baixo Risco", "LTCAT"),
                    new("Acidentes", "Trabalho em altura", "Queda", "Qualitativa", "Guarda-corpo", "Cinto", 3, 2, "6 - Médio Risco", null),
                    new("Químico", "Fumos metálicos", null, null, null, null, 0, 0, "Não há exposição", null),
                }),
        },
        new List<ImportarEstruturaExamesFuncao>
        {
            new("Pedreiro", new List<ImportarEstruturaExame>
            {
                new("AUDIOMETRIA TONAL", "0281", true, true, false, true, true, 12, null),
                new("RAIO-X DE TORAX PADRAO OIT", "1415", true, true, false, true, false, 24, null),
            }),
        },
        new List<string> { "Divulgação do PGR" },
        new List<string> { "GHE 14: P=3 x G=4 = 12, impresso 8" });

    [Fact]
    public async Task Importa_ghe_funcoes_riscos_exames_e_plano_reaproveitando_funcao_do_grh()
    {
        var db = CriarDb(nameof(Importa_ghe_funcoes_riscos_exames_e_plano_reaproveitando_funcao_do_grh));
        var obra = await SemearAsync(db);

        var r = await new ImportarEstruturaSstObraCommandHandler(db, new GeradorNumeroFake()).Handle(Comando(obra.Id), CancellationToken.None);

        Assert.Equal(1, r.Ghes);
        Assert.Equal(2, r.Riscos);
        Assert.Equal(1, r.RiscosSemExposicaoIgnorados);
        Assert.Equal(2, r.Exames);
        Assert.Equal(2, r.ItensPlanoAcao);
        Assert.Equal(new[] { "PEDREIRO" }, r.FuncoesReaproveitadas);
        Assert.Equal(new[] { "Ajudante/Servente" }, r.FuncoesCriadas);
        Assert.Equal(2, await db.Funcoes.CountAsync());
        Assert.Equal("715210", (await db.Funcoes.SingleAsync(f => f.Nome == "PEDREIRO")).CboCodigo);

        var ghe = Assert.Single(await new ListarGhesObraQueryHandler(db).Handle(new ListarGhesObraQuery(obra.Id), CancellationToken.None));
        Assert.Equal(2, ghe.Funcoes.Count);
        Assert.Equal(NivelRisco.Moderado, ghe.Riscos[0].NivelRisco);
        Assert.Equal("Trabalho em altura", ghe.Riscos[0].Perigo);

        var plano = await db.Pgrs.Include(p => p.PlanoDeAcao).SingleAsync();
        Assert.Contains(plano.PlanoDeAcao, i => i.Descricao.StartsWith(ImportarEstruturaSstObraCommandHandler.PrefixoPendencia));
        Assert.All(plano.PlanoDeAcao, i => Assert.Null(i.Prazo));
        Assert.All(await db.ExamesFuncaoObra.ToListAsync(), e => Assert.NotNull(e.PcmsoDetalheId));
    }

    [Fact]
    public async Task Recusa_reimportar_obra_que_ja_tem_ghe()
    {
        var db = CriarDb(nameof(Recusa_reimportar_obra_que_ja_tem_ghe));
        var obra = await SemearAsync(db);
        var handler = new ImportarEstruturaSstObraCommandHandler(db, new GeradorNumeroFake());
        await handler.Handle(Comando(obra.Id), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(Comando(obra.Id), CancellationToken.None));
        Assert.Equal(1, await db.Ghes.CountAsync());
    }

    [Fact]
    public async Task Usuario_restrito_nao_importa_em_obra_de_fora()
    {
        var nome = nameof(Usuario_restrito_nao_importa_em_obra_de_fora);
        var obra = await SemearAsync(CriarDb(nome));
        var restrito = new CurrentUserService();
        restrito.DefinirEscopo(false, new[] { Guid.NewGuid() });

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new ImportarEstruturaSstObraCommandHandler(CriarDb(nome, restrito), new GeradorNumeroFake())
                .Handle(Comando(obra.Id), CancellationToken.None));
    }
}
