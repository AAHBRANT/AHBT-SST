using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Registra que um usuário já viu o pop-up de uma versão — garante a exibição "só uma vez por
// usuário e por atualização" (requisito do usuário, 18/09).
public class NovidadeVisualizacao : AuditableEntity
{
    public Guid NovidadeVersaoId { get; set; }
    public NovidadeVersao? NovidadeVersao { get; set; }

    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public DateTime VistoEmUtc { get; set; }
}
