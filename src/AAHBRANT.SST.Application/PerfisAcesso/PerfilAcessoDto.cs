using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.PerfisAcesso;

public class PerfilAcessoDto
{
    public Guid Id { get; set; }
    public TipoPerfilAcesso? Tipo { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public bool EhSistema { get; set; }
    public int QuantidadePermissoes { get; set; }
}

public class PerfilAcessoPermissaoDto
{
    public Guid Id { get; set; }
    public Guid PermissaoId { get; set; }
    public string PermissaoCodigo { get; set; } = string.Empty;
    public string PermissaoModulo { get; set; } = string.Empty;
    public string PermissaoAcao { get; set; } = string.Empty;
    public EscopoAcesso Escopo { get; set; }
    public bool Permitido { get; set; }
}
