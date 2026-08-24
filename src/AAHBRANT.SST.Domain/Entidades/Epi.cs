using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

public class CatalogoEpi : AuditableEntity
{
    public string Nome { get; set; } = string.Empty;
    public string? CertificadoAprovacaoNumero { get; set; } // CA do EPI
    public DateTime? CertificadoAprovacaoValidade { get; set; }
    public int VidaUtilEmMeses { get; set; }

    public ICollection<EntregaEpi> Entregas { get; set; } = new List<EntregaEpi>();
}

public class EntregaEpi : AuditableEntity
{
    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }

    public Guid CatalogoEpiId { get; set; }
    public CatalogoEpi? CatalogoEpi { get; set; }

    public DateTime DataEntrega { get; set; }
    public DateTime? DataDevolucao { get; set; }
    public DateTime? DataValidade { get; set; }
    public bool AssinaturaColetada { get; set; }

    public ICollection<Evidencia> Evidencias { get; set; } = new List<Evidencia>();
}
