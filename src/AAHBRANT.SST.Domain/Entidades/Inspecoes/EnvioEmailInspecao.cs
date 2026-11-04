using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Envio do relatório da inspeção por e-mail (03/11/2026): e-mails salvos para reaproveitar no próximo
// envio. Um contato é OU do usuário ("Meus contatos", UsuarioId preenchido) OU padrão da obra (ObraId
// preenchido, aparece já marcado para todos os técnicos daquela obra) — nunca os dois.
public class ContatoEmailRelatorio : AuditableEntity
{
    public string? Nome { get; set; }
    public string Email { get; set; } = string.Empty;

    public Guid? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public Guid? ObraId { get; set; }
    public Obra? Obra { get; set; }
}

// Histórico: quem enviou, para quem e quando — também é o registro (LGPD) de quem recebeu o relatório,
// que pode levar nomes e fotos de trabalhadores para fora da empresa.
public class EnvioEmailInspecao : AuditableEntity
{
    public Guid InspecaoId { get; set; }
    public Inspecao? Inspecao { get; set; }

    public DateTime EnviadoEmUtc { get; set; }
    public string EnviadoPorNome { get; set; } = string.Empty;

    // E-mails separados por "; " — só leitura no histórico, sem consulta por destinatário.
    public string Destinatarios { get; set; } = string.Empty;
    public string? Falhas { get; set; }
    public string? Mensagem { get; set; }
}
