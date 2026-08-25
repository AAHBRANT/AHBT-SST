using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Usuarios;

public class UsuarioDto
{
    public Guid Id { get; set; }
    public string? AzureAdObjectId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public StatusUsuario Status { get; set; }
    public DateTime? UltimoLoginUtc { get; set; }
    public Guid? TrabalhadorId { get; set; }
    public List<UsuarioPerfilObraDto> PerfisPorObra { get; set; } = new();
}

public class UsuarioPerfilObraDto
{
    public Guid Id { get; set; }
    public Guid PerfilAcessoId { get; set; }
    public string PerfilAcessoNome { get; set; } = string.Empty;
    public Guid? ObraId { get; set; }
    public string? ObraNome { get; set; }
}
