using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Numeração automática de documentos internos (DDS Semanal, APR, PT, PCMSO, edital do Processo
// Eleitoral da CIPA) — pedido do usuário (03/09): formato "PREFIXO-ANO-0001", sequencial reiniciando
// a cada ano. Um registro por (Prefixo, Ano) — ver índice único em ContadorDocumentoConfiguracao e a
// geração em GeradorNumeroDocumentoService. NUNCA usado para números que vêm de fora do sistema (CAT
// do INSS/eSocial, CA do EPI, certificado de treinamento emitido pela instituição) — esses continuam
// sendo digitados manualmente, porque inventar um valor ali seria um risco de compliance real.
public class ContadorDocumento : AuditableEntity
{
    public string Prefixo { get; set; } = string.Empty;
    public int Ano { get; set; }
    public int UltimoNumero { get; set; }
}
