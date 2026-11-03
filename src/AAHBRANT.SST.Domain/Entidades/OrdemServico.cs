using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Ordem de Serviço de SST emitida (NR-01 1.4.1 "c"), etapa 4 — 03/11/2026. Nasce da minuta sem
// pendências (ObterMinutaOrdemServicoQuery) e guarda uma cópia congelada do conteúdo no momento da
// emissão: o PGR, a matriz e os treinamentos podem mudar depois, a OS assinada não.
//
// Uma OS por trabalhador por obra (decisão de 20/10); emitir de novo gera a revisão seguinte e a
// anterior deixa de ser a vigente. Assinam só o funcionário (digital ou facial) e depois o técnico de
// segurança com o próprio login (decisão de 03/11 — saiu a assinatura do responsável da obra). As
// assinaturas ficam no Motor de Assinatura (DocumentoAssinatura com EntidadeTipo "OrdemServico").
public class OrdemServico : AuditableEntity
{
    public const string EntidadeTipoAssinatura = "OrdemServico";

    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }

    public Guid ObraId { get; set; }

    // Ex.: "OS-2026-0001" (GeradorNumeroDocumentoService, prefixo "OS").
    public string Numero { get; set; } = string.Empty;

    // 0 na primeira OS do trabalhador na obra; cada nova emissão soma 1.
    public int Revisao { get; set; }

    public DateTime EmitidaEm { get; set; }

    // MinutaOrdemServicoDto serializado no momento da emissão — é o que sai no PDF.
    public string ConteudoJson { get; set; } = string.Empty;
}
