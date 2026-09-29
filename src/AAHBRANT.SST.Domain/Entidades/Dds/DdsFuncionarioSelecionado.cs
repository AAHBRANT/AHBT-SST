using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Lista preparada pelo responsável. A presença comprovada continua em DdsParticipante.
public class DdsFuncionarioSelecionado : AuditableEntity
{
    public Guid DdsId { get; set; }
    public Dds? Dds { get; set; }
    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }
}
