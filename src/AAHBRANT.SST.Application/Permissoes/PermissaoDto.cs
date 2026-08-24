namespace AAHBRANT.SST.Application.Permissoes;

public class PermissaoDto
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Modulo { get; set; } = string.Empty;
    public string Acao { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
}
