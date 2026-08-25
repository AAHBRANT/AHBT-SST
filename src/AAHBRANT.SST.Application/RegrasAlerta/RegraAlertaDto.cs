using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.RegrasAlerta;

public class RegraAlertaDto
{
    public Guid Id { get; set; }
    public TipoModuloAlerta Modulo { get; set; }
    public int DiasAntecedencia { get; set; }
    public SeveridadeAlerta Severidade { get; set; }
    public Guid? ResponsavelUsuarioId { get; set; }
    public string? ResponsavelUsuarioNome { get; set; }
}
