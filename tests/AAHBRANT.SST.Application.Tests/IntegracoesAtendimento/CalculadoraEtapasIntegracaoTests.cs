using AAHBRANT.SST.Application.IntegracoesAtendimento;
using AAHBRANT.SST.Application.IntegracoesAtendimento.Queries;
using AAHBRANT.SST.Domain.Entidades;

namespace AAHBRANT.SST.Application.Tests.IntegracoesAtendimento;

// Regra das etapas (decisão de 11/11/2026): ordem fixa NR-06, NR-12, NR-18, NR-35 e OS.
public class CalculadoraEtapasIntegracaoTests
{
    private static readonly IReadOnlySet<string> Nenhuma = new HashSet<string>();

    private static IReadOnlyDictionary<TipoEtapaIntegracao, CursoEtapaIntegracao> Cursos(int cargaNr12 = 0) =>
        new Dictionary<TipoEtapaIntegracao, CursoEtapaIntegracao>
        {
            [TipoEtapaIntegracao.Nr06] = new(Guid.NewGuid(), 0),
            [TipoEtapaIntegracao.Nr12] = new(Guid.NewGuid(), cargaNr12),
            [TipoEtapaIntegracao.Nr18] = new(Guid.NewGuid(), 4),
            [TipoEtapaIntegracao.Nr35] = new(Guid.NewGuid(), 8),
        };

    private static PessoaEtapasIntegracao Pessoa(Guid? id = null, bool isento = false, string[]? validas = null, string[]? exigidas = null) =>
        new(id ?? Guid.NewGuid(), isento, (validas ?? []).ToHashSet(), (exigidas ?? []).ToHashSet());

    [Fact]
    public void OrdemFixa_ENr06ENr18SaoDeTodos_Nr12ENr35SoPeloPgr()
    {
        var a = Pessoa(exigidas: ["NR-12", "NR-35"]);
        var b = Pessoa();

        var etapas = CalculadoraEtapasIntegracao.Calcular([a, b], Cursos());

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, etapas.Select(e => e.Ordem));
        Assert.Equal(
            new[] { TipoEtapaIntegracao.Nr06, TipoEtapaIntegracao.Nr12, TipoEtapaIntegracao.Nr18, TipoEtapaIntegracao.Nr35, TipoEtapaIntegracao.OrdemServico },
            etapas.Select(e => e.Tipo));
        Assert.Equal(2, etapas[0].Participantes.Count);
        Assert.Equal(new[] { a.TrabalhadorId }, etapas[1].Participantes);
        Assert.Equal(2, etapas[2].Participantes.Count);
        Assert.Equal(new[] { a.TrabalhadorId }, etapas[3].Participantes);
        Assert.Equal(2, etapas[4].Participantes.Count);
    }

    [Fact]
    public void QuemJaTemANrValida_FicaForaDaEtapaDela()
    {
        var comNr06 = Pessoa(validas: ["NR-06"]);
        var semNada = Pessoa();

        var etapas = CalculadoraEtapasIntegracao.Calcular([comNr06, semNada], Cursos());

        Assert.Equal(new[] { semNada.TrabalhadorId }, etapas[0].Participantes);
        Assert.Equal(2, etapas[2].Participantes.Count);
    }

    [Fact]
    public void EtapaDeCursoSemParticipante_EPulada_ENaoPulaAOs()
    {
        var b = Pessoa(validas: ["NR-06", "NR-18"]);

        var etapas = CalculadoraEtapasIntegracao.Calcular([b], Cursos());

        Assert.All(etapas.Where(e => e.Tipo != TipoEtapaIntegracao.OrdemServico), e => Assert.True(e.Pulada));
        var os = etapas.Single(e => e.Tipo == TipoEtapaIntegracao.OrdemServico);
        Assert.False(os.Pulada);
        Assert.Equal(new[] { b.TrabalhadorId }, os.Participantes);
    }

    [Fact]
    public void TecnicoDeSeguranca_EIsentoDosCursosMasFazAOs()
    {
        var tecnico = Pessoa(isento: true, exigidas: ["NR-12", "NR-35"]);

        var etapas = CalculadoraEtapasIntegracao.Calcular([tecnico], Cursos());

        Assert.All(etapas.Where(e => e.Tipo != TipoEtapaIntegracao.OrdemServico), e => Assert.Empty(e.Participantes));
        Assert.Equal(new[] { tecnico.TrabalhadorId }, etapas[4].Participantes);
    }

    [Fact]
    public void CargaMinima_UsaOPisoDecidido_ESobeComOPadraoDoCurso()
    {
        var a = Pessoa(exigidas: ["NR-12", "NR-35"]);

        var padrao = CalculadoraEtapasIntegracao.Calcular([a], Cursos());
        var catalogoMaior = CalculadoraEtapasIntegracao.Calcular([a], Cursos(cargaNr12: 16));

        Assert.Equal(0, padrao[0].CargaMinimaHoras);   // NR-06: sem trava (o QSMS configura)
        Assert.Equal(8, padrao[1].CargaMinimaHoras);   // NR-12: piso de 8 h
        Assert.Equal(4, padrao[2].CargaMinimaHoras);   // NR-18: 4 h
        Assert.Equal(8, padrao[3].CargaMinimaHoras);   // NR-35: 8 h
        Assert.Equal(16, catalogoMaior[1].CargaMinimaHoras);
    }
}

// Regra Q2: concluir libera quando todas as OS emitíveis estão assinadas; "OS pendente" não segura os demais.
public class AvaliadorConclusaoIntegracaoTests
{
    private static LinhaOsIntegracaoDto Linha(string situacao) =>
        new(Guid.NewGuid(), "Pessoa", "Função", situacao, null, null, null, situacao != SituacaoOsIntegracao.Pendente, []);

    [Fact]
    public void TodasEmitiveisAssinadas_LiberaMesmoComOsPendente() =>
        Assert.True(AvaliadorConclusaoIntegracao.PodeConcluir(
            [Linha(SituacaoOsIntegracao.Assinada), Linha(SituacaoOsIntegracao.Pendente)]));

    [Theory]
    [InlineData(SituacaoOsIntegracao.NaoEmitida)]
    [InlineData(SituacaoOsIntegracao.AguardandoFuncionario)]
    [InlineData(SituacaoOsIntegracao.AguardandoTecnico)]
    public void OsEmitivelNaoAssinada_Segura(string situacao) =>
        Assert.False(AvaliadorConclusaoIntegracao.PodeConcluir([Linha(SituacaoOsIntegracao.Assinada), Linha(situacao)]));
}
