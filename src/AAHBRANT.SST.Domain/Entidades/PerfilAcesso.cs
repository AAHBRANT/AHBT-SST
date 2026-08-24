using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

public class PerfilAcesso : AuditableEntity
{
    // Nulo = perfil customizado criado pela organização (fora dos 12 perfis base da §44).
    public TipoPerfilAcesso? Tipo { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }

    // Perfis de sistema (os 12 da §44, semeados na inicialização) não podem ser excluídos —
    // apenas suas permissões podem ser reconfiguradas. Perfis customizados podem ser excluídos.
    public bool EhSistema { get; set; }

    public ICollection<PerfilAcessoPermissao> Permissoes { get; set; } = new List<PerfilAcessoPermissao>();
}

// Catálogo granular e reutilizável de permissões (ex.: "apr:aprovar", "aso:ver_clinico"),
// independente de perfil — cada perfil escolhe quais concede via PerfilAcessoPermissao.
public class Permissao : AuditableEntity
{
    public string Codigo { get; set; } = string.Empty; // ex.: "apr:aprovar"
    public string Modulo { get; set; } = string.Empty; // ex.: "Apr", "Aso", "PermissaoTrabalho"
    public string Acao { get; set; } = string.Empty;    // Ver | Criar | Editar | Aprovar | Excluir
    public string Descricao { get; set; } = string.Empty;
}

// Matriz RBAC (docs/RBAC-Matrix.md) — uma linha por combinação Perfil x Permissão, com o escopo
// em que aquele perfil a exerce (ex.: EngenheiroSeguranca tem "apr:aprovar" no escopo Obra).
public class PerfilAcessoPermissao : AuditableEntity
{
    public Guid PerfilAcessoId { get; set; }
    public PerfilAcesso? PerfilAcesso { get; set; }

    public Guid PermissaoId { get; set; }
    public Permissao? Permissao { get; set; }

    public EscopoAcesso Escopo { get; set; } = EscopoAcesso.Obra;
    public bool Permitido { get; set; } = true;
}

public class Usuario : AuditableEntity
{
    public string AzureAdObjectId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;

    // Controle administrativo de acesso (bloquear/desativar) — não é estado de login local,
    // que não existe neste app (ver StatusUsuario).
    public StatusUsuario Status { get; set; } = StatusUsuario.Ativo;
    public DateTime? UltimoLoginUtc { get; set; }

    public Guid? TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }

    public ICollection<UsuarioPerfilObra> PerfisPorObra { get; set; } = new List<UsuarioPerfilObra>();
}

// Resolve o escopo por obra dentro da aplicação (o Entra ID resolve só o perfil via JWT `roles`).
public class UsuarioPerfilObra : AuditableEntity
{
    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public Guid PerfilAcessoId { get; set; }
    public PerfilAcesso? PerfilAcesso { get; set; }

    // Nulo = perfil com escopo Global/Unidade (não restrito a uma obra específica)
    public Guid? ObraId { get; set; }
    public Obra? Obra { get; set; }
}
