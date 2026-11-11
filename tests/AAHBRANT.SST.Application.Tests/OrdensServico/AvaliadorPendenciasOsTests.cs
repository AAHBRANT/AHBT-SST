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

    // ---- Atividades por função (Entrega 4, 10/11/2026) ----

    private static readonly string[] TiposPorAtividade = { "RiscoSemAvaliacao", "MedidasNaoSeparadas", "SugestoesPendentes", "SemProcedimento" };

    private static AvaliadorPendenciasOs.AtividadeAvaliada AtividadeComTudoPendente(string nome) =>
        new(Guid.NewGuid(), nome, new() { new(Guid.NewGuid(), "Queda", false, 0, 0) }, 1, 0);

    [Fact]
    public void Transicao_TrocaPendenciasPorAtividadePorUmaSo()
    {
        var p = AvaliadorPendenciasOs.Avaliar(EntradaOk() with
        {
            Atividades = Enumerable.Range(1, 18).Select(i => AtividadeComTudoPendente($"Etapa {i}")).ToList(),
            AtividadesDaFuncaoDefinidas = false,
            SemNenhumaDecisao = true,
            AtividadesSemDecisao = new(),
            FuncaoNome = "Armador",
            GhesNumeros = new() { 3 },
        });

        var unica = Assert.Single(p);
        Assert.Equal("AtividadesFuncaoNaoConfirmadas", unica.Tipo);
        Assert.Equal("As atividades da função Armador no GHE 03 ainda não foram confirmadas pelo engenheiro", unica.Titulo);
        Assert.Equal("SST", unica.QuemResolve);
        Assert.Equal("pgr", unica.Destino);
        Assert.Equal(Pgr, unica.DestinoId);
    }

    [Fact]
    public void Transicao_NaoMexeEmTreinamentoAsoEPgr()
    {
        var p = AvaliadorPendenciasOs.Avaliar(EntradaOk() with
        {
            PgrVigenteId = null,
            Aso = null,
            Treinamentos = new() { new TreinamentoOsDto("NR-35", "NR-35", null, "Vencido") },
            AtividadesDaFuncaoDefinidas = false,
            SemNenhumaDecisao = true,
        });

        Assert.Contains(p, x => x.Tipo == "PgrNaoVigente");
        Assert.Contains(p, x => x.Tipo == "SemAso");
        Assert.Contains(p, x => x.Tipo == "TreinamentoPendente");
        Assert.Contains(p, x => x.Tipo == "AtividadesFuncaoNaoConfirmadas");
    }

    [Fact]
    public void Parcial_AtividadeSemDecisaoViraPendenciaAgrupada_EConfirmadaSegueAsRegrasNormais()
    {
        var p = AvaliadorPendenciasOs.Avaliar(EntradaOk() with
        {
            Atividades = new() { AtividadeComTudoPendente("Corte") },
            AtividadesDaFuncaoDefinidas = false,
            AtividadesSemDecisao = new() { "Concretagem", "Desforma" },
            FuncaoNome = "Armador",
        });

        var agrupada = Assert.Single(p, x => x.Tipo == "AtividadesFuncaoSemDecisao");
        Assert.Contains("2 atividade(s)", agrupada.Titulo);
        Assert.Contains("Concretagem, Desforma", agrupada.Titulo);
        Assert.DoesNotContain(p, x => x.Tipo == "AtividadesFuncaoNaoConfirmadas");
        // A atividade confirmada continua com as regras normais (sem procedimento bloqueia).
        Assert.Contains(p, x => x.Tipo == "SemProcedimento" && x.Titulo.Contains("Corte"));
        Assert.All(p.Where(x => TiposPorAtividade.Contains(x.Tipo)), x => Assert.Contains("Corte", x.Titulo));
    }

    [Fact]
    public void Completo_SoAtividadesConfirmadasValem_SemPendenciaDeFuncao()
    {
        var p = AvaliadorPendenciasOs.Avaliar(EntradaOk() with
        {
            AtividadesDaFuncaoDefinidas = true,
            AtividadesSemDecisao = new(),
            FuncaoNome = "Armador",
        });
        Assert.Empty(p);
    }

    [Fact]
    public void TodasDescartadas_BloqueiaComTextoDeNenhumaAtividadeConfirmada()
    {
        var p = AvaliadorPendenciasOs.Avaliar(EntradaOk() with
        {
            Atividades = new(),
            AtividadesDaFuncaoDefinidas = true,
            TodasDescartadas = true,
            FuncaoNome = "Armador",
        });

        var item = Assert.Single(p);
        Assert.Equal("AtividadesFuncaoNenhumaConfirmada", item.Tipo);
        Assert.Equal("Nenhuma atividade confirmada para a função Armador", item.Titulo);
    }

    [Fact]
    public void GheSemAtividadesNoInventario_ContinuaComOTextoAntigo()
    {
        var item = Assert.Single(AvaliadorPendenciasOs.Avaliar(EntradaOk() with { Atividades = new() }));
        Assert.Equal("SemAtividades", item.Tipo);
        Assert.Equal("O GHE da função não tem atividades com riscos no inventário", item.Titulo);
    }

    [Fact]
    public void DecisoesSemNenhumaConfirmada_ComAtividadeSemDecisao_MostraSoASemDecisao()
    {
        var p = AvaliadorPendenciasOs.Avaliar(EntradaOk() with
        {
            Atividades = new(),
            AtividadesDaFuncaoDefinidas = false,
            AtividadesSemDecisao = new() { "Concretagem" },
            FuncaoNome = "Armador",
        });

        var item = Assert.Single(p);
        Assert.Equal("AtividadesFuncaoSemDecisao", item.Tipo);
    }
}
