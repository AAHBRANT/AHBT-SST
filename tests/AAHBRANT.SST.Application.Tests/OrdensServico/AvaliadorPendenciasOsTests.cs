using AAHBRANT.SST.Application.OrdensServico;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.OrdensServico;

// Regra da emissão da OS (20/10/2026): qualquer pendência bloqueia. Treinamento vencido e ASO não apto
// bloqueiam (decisão do usuário).
public class AvaliadorPendenciasOsTests
{
    private static readonly Guid Pgr = Guid.NewGuid();

    private static AvaliadorPendenciasOs.AtividadeAvaliada AtividadeOk(string nome = "Armação") =>
        new(Guid.NewGuid(), nome, new() { new(Guid.NewGuid(), "Queda", true, 3, 0) }, 0, 1);

    private static AvaliadorPendenciasOs.Entrada EntradaOk() => new(
        Pgr, true, true,
        new() { AtividadeOk() },
        new() { new TreinamentoOsDto("NR-35", "NR-35", DateTime.UtcNow.AddMonths(6), "Valido") },
        new AsoOsDto(ResultadoAso.Apto, DateTime.UtcNow.AddMonths(6), true));

    [Fact]
    public void TudoValidado_SemPendencias()
        => Assert.Empty(AvaliadorPendenciasOs.Avaliar(EntradaOk()));

    [Fact]
    public void SemPgrVigente_Bloqueia()
        => Assert.Contains(AvaliadorPendenciasOs.Avaliar(EntradaOk() with { PgrVigenteId = null }), p => p.Tipo == "PgrNaoVigente");

    [Fact]
    public void FuncaoSemGhe_Bloqueia_ESemDescricaoNaoDuplica()
    {
        var p = AvaliadorPendenciasOs.Avaliar(EntradaOk() with { FuncaoTemGhe = false, TemDescricaoAtividades = false });
        Assert.Contains(p, x => x.Tipo == "FuncaoSemGhe");
        Assert.DoesNotContain(p, x => x.Tipo == "SemDescricaoAtividades");
    }

    [Fact]
    public void RiscoSemAvaliacao_MedidasNaoSeparadas_SugestaoPendente_SemProcedimento()
    {
        var atividade = new AvaliadorPendenciasOs.AtividadeAvaliada(Guid.NewGuid(), "Corte", new()
        {
            new(Guid.NewGuid(), "Corte", false, 0, 0),
            new(Guid.NewGuid(), "Ruído", true, 2, 1),
        }, 2, 0);

        var p = AvaliadorPendenciasOs.Avaliar(EntradaOk() with { Atividades = new() { atividade } });

        Assert.Contains(p, x => x.Tipo == "RiscoSemAvaliacao");
        Assert.Contains(p, x => x.Tipo == "MedidasNaoSeparadas");
        Assert.Contains(p, x => x.Tipo == "SugestoesPendentes" && x.Titulo.StartsWith("3 "));
        Assert.Contains(p, x => x.Tipo == "SemProcedimento");
        Assert.All(p, x => Assert.Equal("SST", x.QuemResolve));
    }

    [Theory]
    [InlineData("Vencido")]
    [InlineData("NaoRealizado")]
    public void TreinamentoNaoValido_Bloqueia(string situacao)
    {
        var p = AvaliadorPendenciasOs.Avaliar(EntradaOk() with
        {
            Treinamentos = new() { new TreinamentoOsDto("NR-35", "NR-35", null, situacao) },
        });
        var item = Assert.Single(p);
        Assert.Equal("TreinamentoPendente", item.Tipo);
        Assert.Equal("RH", item.QuemResolve);
    }

    [Fact]
    public void AsoInvalidoOuAusente_Bloqueia()
    {
        Assert.Contains(AvaliadorPendenciasOs.Avaliar(EntradaOk() with { Aso = null }), x => x.Tipo == "SemAso");
        Assert.Contains(AvaliadorPendenciasOs.Avaliar(EntradaOk() with { Aso = new AsoOsDto(ResultadoAso.Inapto, DateTime.UtcNow.AddMonths(1), false) }),
            x => x.Tipo == "AsoInvalido");
    }
}
