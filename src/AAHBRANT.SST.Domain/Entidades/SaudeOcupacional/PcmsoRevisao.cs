using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Revisão do PCMSO com o PDF daquela versão — mesmo modelo de PgrRevisao (10/10/2026): enviar um
// documento novo cria uma revisão em vez de apagar o anterior. PcmsoDetalhe.DocumentoConteudo é a
// cópia da revisão atual (o visualizador continua lendo de lá).
public class PcmsoRevisao : AuditableEntity
{
    public Guid PcmsoDetalheId { get; set; }
    public PcmsoDetalhe? PcmsoDetalhe { get; set; }

    public int NumeroRevisao { get; set; }
    public DateTime DataRevisao { get; set; }
    public string Motivo { get; set; } = string.Empty;

    public byte[]? DocumentoConteudo { get; set; }
    public string? DocumentoContentType { get; set; }
    public string? DocumentoNomeArquivo { get; set; }
}
