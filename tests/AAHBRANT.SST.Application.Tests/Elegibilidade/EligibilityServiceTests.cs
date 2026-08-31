using AAHBRANT.SST.Application.Elegibilidade;
using AAHBRANT.SST.Domain.Interfaces;

namespace AAHBRANT.SST.Application.Tests.Elegibilidade;

// Testa só a agregação (EligibilityService), com regras falsas — as regras de verdade (ASO,
// Treinamento) já têm teste próprio. O que importa aqui: um único requisito crítico não atendido
// deve bloquear tudo, mesmo com as demais regras atendidas.
public class EligibilityServiceTests
{
    private sealed class RegraFalsa : IEligibilityRule
    {
        private readonly bool _atendido;
        private readonly bool _critico;

        public RegraFalsa(string nome, bool atendido, bool critico = true)
        {
            NomeRequisito = nome;
            _atendido = atendido;
            _critico = critico;
        }

        public string NomeRequisito { get; }

        public Task<EligibilityCheckItem> AvaliarAsync(EligibilityRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(new EligibilityCheckItem
            {
                Requisito = NomeRequisito,
                Atendido = _atendido,
                Critico = _critico,
                Detalhe = _atendido ? null : $"{NomeRequisito} não atendido.",
            });
        }
    }

    private static EligibilityRequest RequestQualquer() => new() { TrabalhadorId = Guid.NewGuid(), ObraId = Guid.NewGuid() };

    [Fact]
    public async Task Todas_as_regras_atendidas_libera()
    {
        var servico = new EligibilityService(new IEligibilityRule[]
        {
            new RegraFalsa("ASO válido", atendido: true),
            new RegraFalsa("Treinamento válido", atendido: true),
        });

        var resultado = await servico.AvaliarAsync(RequestQualquer());

        Assert.True(resultado.Liberado);
        Assert.Null(resultado.MotivoBloqueioResumo);
    }

    [Fact]
    public async Task Uma_regra_critica_nao_atendida_bloqueia_mesmo_com_as_outras_ok()
    {
        var servico = new EligibilityService(new IEligibilityRule[]
        {
            new RegraFalsa("ASO válido", atendido: false),
            new RegraFalsa("Treinamento válido", atendido: true),
        });

        var resultado = await servico.AvaliarAsync(RequestQualquer());

        Assert.False(resultado.Liberado);
        Assert.Contains("ASO válido", resultado.MotivoBloqueioResumo);
    }

    [Fact]
    public async Task Regra_nao_critica_nao_atendida_nao_bloqueia()
    {
        var servico = new EligibilityService(new IEligibilityRule[]
        {
            new RegraFalsa("Recomendação não crítica", atendido: false, critico: false),
        });

        var resultado = await servico.AvaliarAsync(RequestQualquer());

        Assert.True(resultado.Liberado);
    }

    [Fact]
    public async Task Sem_nenhuma_regra_registrada_libera_por_vacuidade()
    {
        var servico = new EligibilityService(Array.Empty<IEligibilityRule>());

        var resultado = await servico.AvaliarAsync(RequestQualquer());

        Assert.True(resultado.Liberado);
        Assert.Empty(resultado.Itens);
    }
}
