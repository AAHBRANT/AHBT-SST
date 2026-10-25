using AAHBRANT.SST.Application.MatrizRisco;
using AAHBRANT.SST.Application.MatrizRisco.Commands;
using AAHBRANT.SST.Application.MatrizRisco.Queries;
using AAHBRANT.SST.Application.Riscos;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.MatrizRisco;

// Matriz de risco versionada (25/10/2026): a vigente só se consulta; mudar é publicar uma nova versão.
public class NovaVersaoMatrizRiscoTests
{
    private static List<NovaVersaoMatrizRiscoCelula> Grade(int p, int s, NivelRisco padrao, params (int P, int S, NivelRisco N)[] excecoes)
    {
        var lista = new List<NovaVersaoMatrizRiscoCelula>();
        for (var i = 1; i <= p; i++)
            for (var j = 1; j <= s; j++)
                lista.Add(new(i, j, excecoes.Where(e => e.P == i && e.S == j).Select(e => e.N).FirstOrDefault(padrao)));
        return lista;
    }

    private static NovaVersaoMatrizRiscoCommand Comando(List<NovaVersaoMatrizRiscoCelula> celulas, string? motivo, int p = 3, int s = 3, Guid? usuario = null) =>
        new("Matriz da empresa", p, s, celulas, motivo, usuario, "Eng. Carlos");

    [Fact]
    public async Task PrimeiraMatriz_ViraVersao1Vigente_SemExigirMotivo()
    {
        var db = DbContextFactory.Criar();

        var id = await new NovaVersaoMatrizRiscoCommandHandler(db).Handle(Comando(Grade(3, 3, NivelRisco.Baixo), null), default);

        var vigente = await new ObterMatrizRiscoVigenteQueryHandler(db).Handle(new ObterMatrizRiscoVigenteQuery(), default);
        Assert.NotNull(vigente);
        Assert.Equal(id, vigente!.Id);
        Assert.Equal(1, vigente.Versao);
        Assert.True(vigente.Vigente);
        Assert.Equal(9, vigente.Celulas.Count);
        Assert.Equal("Eng. Carlos", vigente.RevisadaPorNome);
    }

    [Fact]
    public async Task SemMatriz_ConsultaDaVigenteDevolveNulo()
    {
        var db = DbContextFactory.Criar();

        Assert.Null(await new ObterMatrizRiscoVigenteQueryHandler(db).Handle(new ObterMatrizRiscoVigenteQuery(), default));
    }

    [Fact]
    public async Task NovaVersao_ExigeMotivo_SubstituiAVigente_EGuardaOHistorico()
    {
        var db = DbContextFactory.Criar();
        var handler = new NovaVersaoMatrizRiscoCommandHandler(db);
        var v1 = await handler.Handle(Comando(Grade(3, 3, NivelRisco.Baixo), null), default);

        // A partir da versão 2 o motivo é obrigatório.
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(Comando(Grade(3, 3, NivelRisco.Moderado), "  "), default));

        var v2 = await handler.Handle(Comando(Grade(3, 3, NivelRisco.Moderado), "Revisão do PGR de 2026"), default);

        var versoes = await new ListarMatrizRiscoConfigsQueryHandler(db).Handle(new ListarMatrizRiscoConfigsQuery(), default);
        Assert.Equal(new[] { 2, 1 }, versoes.Select(v => v.Versao).ToArray()); // da mais nova para a mais antiga
        var nova = versoes[0];
        var antiga = versoes[1];
        Assert.Equal(v2, nova.Id);
        Assert.True(nova.Vigente);
        Assert.Equal("Revisão do PGR de 2026", nova.MotivoRevisao);
        Assert.Equal(v1, antiga.Id);
        Assert.False(antiga.Vigente);
        Assert.NotNull(antiga.SubstituidaEmUtc);
        Assert.All(antiga.Celulas, c => Assert.Equal(NivelRisco.Baixo, c.NivelRisco)); // a antiga não muda: é histórico
        Assert.Single(versoes, v => v.Vigente); // nunca duas vigentes
    }

    [Fact]
    public async Task ClassificacaoDosRiscos_UsaSempreAMatrizVigente()
    {
        var db = DbContextFactory.Criar();
        var handler = new NovaVersaoMatrizRiscoCommandHandler(db);
        await handler.Handle(Comando(Grade(3, 3, NivelRisco.Baixo), null), default);
        Assert.Equal(NivelRisco.Baixo, await NivelRiscoLookup.ResolverAsync(db, Guid.NewGuid(), 2, 2, default));

        await handler.Handle(Comando(Grade(3, 3, NivelRisco.Baixo, (2, 2, NivelRisco.Alto)), "Célula P2 × S2 subiu para Alto"), default);

        Assert.Equal(NivelRisco.Alto, await NivelRiscoLookup.ResolverAsync(db, Guid.NewGuid(), 2, 2, default));
    }

    [Fact]
    public void Validacao_ExigeUmaCelulaParaCadaCruzamento_SemRepetirNemSairDaEscala()
    {
        var validador = new NovaVersaoMatrizRiscoCommandValidator();

        Assert.True(validador.Validate(Comando(Grade(3, 3, NivelRisco.Baixo), null)).IsValid);

        var faltando = Grade(3, 3, NivelRisco.Baixo);
        faltando.RemoveAt(0);
        Assert.False(validador.Validate(Comando(faltando, null)).IsValid);

        var repetida = Grade(3, 3, NivelRisco.Baixo);
        repetida[1] = repetida[0];
        Assert.False(validador.Validate(Comando(repetida, null)).IsValid);

        var foraDaEscala = Grade(3, 3, NivelRisco.Baixo);
        foraDaEscala[0] = new(9, 9, NivelRisco.Baixo);
        Assert.False(validador.Validate(Comando(foraDaEscala, null)).IsValid);

        var nivelInvalido = Grade(3, 3, NivelRisco.Baixo);
        nivelInvalido[0] = new(1, 1, (NivelRisco)99);
        Assert.False(validador.Validate(Comando(nivelInvalido, null)).IsValid);

        Assert.False(validador.Validate(Comando(Grade(3, 3, NivelRisco.Baixo), null, p: 0)).IsValid);
    }
}
