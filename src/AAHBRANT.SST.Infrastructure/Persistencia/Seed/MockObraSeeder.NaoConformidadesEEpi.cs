using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Application.Common;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

public static partial class MockObraSeeder
{
    private static (List<NaoConformidade> NaoConformidades, List<CatalogoEpi> CatalogosEpi, List<EntregaEpi> EntregasEpi, List<EstoqueEpi> EstoquesEpi)
        ConstruirNaoConformidadesEEpi(Obra obra, List<Trabalhador> trabalhadores, List<AreaSst> areas, DateTime referenciaUtc)
    {
        var naoConformidades = new List<NaoConformidade>();
        var indiceNc = 0;
        foreach (var (status, quantidade) in DistribuicaoNaoConformidades)
        {
            for (var i = 0; i < quantidade; i++)
            {
                var area = areas[indiceNc % areas.Count];
                var prazo = DistribuidorFaixaVencimento.CalcularData(indiceNc, referenciaUtc);
                naoConformidades.Add(new NaoConformidade
                {
                    OrigemDeteccao = (OrigemNaoConformidade)((indiceNc % 5) + 1),
                    Descricao = $"Não conformidade identificada em {area.Nome}: uso incorreto de EPI / condição insegura de acesso.",
                    Local = area.Nome,
                    Prazo = prazo,
                    Status = status,
                    DataConclusao = status == StatusNaoConformidade.Encerrada
                        ? (prazo < referenciaUtc ? prazo.AddDays(-2) : referenciaUtc.AddDays(-2))
                        : null,
                });
                indiceNc++;
            }
        }

        var catalogosEpi = CatalogoEpisPadrao
            .Select(e => new CatalogoEpi
            {
                Nome = e.Nome,
                Fabricante = e.Fabricante,
                CertificadoAprovacaoNumero = e.CertificadoAprovacaoNumero,
                CertificadoAprovacaoValidade = referenciaUtc.AddYears(2),
                VidaUtilEmMeses = e.VidaUtilEmMeses,
            })
            .ToList();

        // Fase 3 — estoque segmentado por Obra: a obra mocada é única, então cada CatalogoEpi
        // recebe exatamente uma linha de EstoqueEpi com o saldo inicial definido em CatalogoEpisPadrao.
        var estoquesEpi = catalogosEpi
            .Zip(CatalogoEpisPadrao, (catalogo, dados) => new EstoqueEpi
            {
                Obra = obra,
                CatalogoEpi = catalogo,
                Saldo = dados.SaldoEstoque,
            })
            .ToList();

        var epiCapacete = catalogosEpi.Single(e => e.Nome.Contains("Capacete"));
        var epiBota = catalogosEpi.Single(e => e.Nome.Contains("Bota"));

        var entregasEpi = new List<EntregaEpi>();
        var indiceEntrega = 0;
        foreach (var trabalhador in trabalhadores)
        {
            entregasEpi.Add(NovaEntrega(trabalhador, epiCapacete, referenciaUtc, indiceEntrega++));
            entregasEpi.Add(NovaEntrega(trabalhador, epiBota, referenciaUtc, indiceEntrega++));
        }

        return (naoConformidades, catalogosEpi, entregasEpi, estoquesEpi);
    }

    private static EntregaEpi NovaEntrega(Trabalhador trabalhador, CatalogoEpi epi, DateTime referenciaUtc, int indice) => new()
    {
        Trabalhador = trabalhador,
        CatalogoEpi = epi,
        DataEntrega = referenciaUtc.AddDays(-(30 + indice % 60)),
        DataValidade = referenciaUtc.AddMonths(epi.VidaUtilEmMeses).AddDays(-(indice % 30)),
        Quantidade = 1,
        Motivo = "Entrega inicial",
    };
}
