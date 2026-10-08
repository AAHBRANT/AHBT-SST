using AAHBRANT.SST.Application.Ideias;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.Ideias;

public class IdeiasLogicaTests
{
    [Fact]
    public void Pontuacao_SemTodosOsCriterios_RetornaNulo()
        => Assert.Null(PontuacaoIdeia.Calcular(NivelIdeia.Alto, NivelIdeia.Alto, null, NivelIdeia.Baixo, NivelIdeia.Baixo));

    [Fact]
    public void Pontuacao_MelhorCaso_E100_PiorCaso_E0_PrioridadeSugerida()
    {
        var melhor = PontuacaoIdeia.Calcular(NivelIdeia.Alto, NivelIdeia.Alto, NivelIdeia.Alto, NivelIdeia.Baixo, NivelIdeia.Baixo);
        var pior = PontuacaoIdeia.Calcular(NivelIdeia.Baixo, NivelIdeia.Baixo, NivelIdeia.Baixo, NivelIdeia.Alto, NivelIdeia.Alto);
        Assert.Equal(100, melhor);
        Assert.Equal(0, pior);
        Assert.Equal(PrioridadeIdeia.P1, PontuacaoIdeia.SugerirPrioridade(melhor));
        Assert.Equal(PrioridadeIdeia.P3, PontuacaoIdeia.SugerirPrioridade(pior));
        Assert.Equal(PrioridadeIdeia.P2, PontuacaoIdeia.SugerirPrioridade(50));
        Assert.Null(PontuacaoIdeia.SugerirPrioridade(null));
    }

    [Fact]
    public void Fluxo_NaoPermitePularEtapas_EImplantadaEhTerminal()
    {
        Assert.True(FluxoStatusIdeia.Permite(StatusIdeia.NovaIdeia, StatusIdeia.EmAnalise));
        Assert.False(FluxoStatusIdeia.Permite(StatusIdeia.NovaIdeia, StatusIdeia.Aprovada));
        Assert.False(FluxoStatusIdeia.Permite(StatusIdeia.EmAnalise, StatusIdeia.EmDesenvolvimento));
        Assert.Empty(FluxoStatusIdeia.Destinos(StatusIdeia.Implantada));
        Assert.True(FluxoStatusIdeia.Permite(StatusIdeia.Descartada, StatusIdeia.EmAnalise));
    }

    [Theory]
    [InlineData(StatusIdeia.EmAnalise, "ideia:analisar")]
    [InlineData(StatusIdeia.Aprovada, "ideia:decidir")]
    [InlineData(StatusIdeia.Descartada, "ideia:decidir")]
    [InlineData(StatusIdeia.EmDesenvolvimento, "ideia:desenvolver")]
    [InlineData(StatusIdeia.Implantada, "ideia:desenvolver")]
    public void Fluxo_PermissaoExigidaPorDestino(StatusIdeia destino, string esperado)
        => Assert.Equal(esperado, FluxoStatusIdeia.PermissaoExigida(destino));

    [Fact]
    public async Task Heuristica_IdentificaModuloCategoriaEIa()
    {
        var servico = new HeuristicaIdeiaEstruturacaoService();
        var r = await servico.EstruturarAsync(
            "Seria interessante o sistema de não conformidades analisar automaticamente uma ocorrência e sugerir a causa raiz e as ações corretivas.", default);
        Assert.Equal("Não Conformidades", r.Modulo);
        Assert.Equal("Inteligência Artificial", r.Categoria);
        Assert.True(r.NecessidadeIa);
        Assert.StartsWith("Seria interessante", r.Titulo);
    }

    [Fact]
    public async Task Heuristica_SemModulo_ApontaInformacaoFaltante()
    {
        var r = await new HeuristicaIdeiaEstruturacaoService().EstruturarAsync("Melhorar isto aqui", default);
        Assert.Null(r.Modulo);
        Assert.Contains("Módulo", r.InformacoesFaltantes);
    }

    [Fact]
    public void Similaridade_EpiVencido_ReconheceIdeiasParecidas_EIgnoraAssuntosDiferentes()
    {
        var nova = SimilaridadeIdeias.Palavras("O sistema deveria avisar quando um EPI estiver vencido.");
        var existente = SimilaridadeIdeias.Palavras("Criar alerta para vencimento de EPI. Avisar quando o EPI vencer.");
        var outra = SimilaridadeIdeias.Palavras("Relatório mensal de horas trabalhadas por obra em PDF.");

        Assert.True(SimilaridadeIdeias.Calcular(nova, existente) >= SimilaridadeIdeias.Limiar);
        Assert.True(SimilaridadeIdeias.Calcular(nova, outra) < SimilaridadeIdeias.Limiar);
    }
}
