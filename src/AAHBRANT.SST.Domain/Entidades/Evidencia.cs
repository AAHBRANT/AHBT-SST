using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Genérica e reutilizável (EntidadeTipo/EntidadeId polimórfico) — usada por Aso, Treinamento, EntregaEpi
// e por qualquer módulo futuro, em vez de cada um reinventar seu próprio campo de anexo.
public class Evidencia : AuditableEntity
{
    public string EntidadeTipo { get; set; } = string.Empty;
    public Guid EntidadeId { get; set; }

    public string BlobUrl { get; set; } = string.Empty;
    public string NomeArquivo { get; set; } = string.Empty;
    public string? ContentType { get; set; }

    // Integridade probatória (Análise de Oportunidades, Nível 2)
    public string HashSha256 { get; set; } = string.Empty;

    public Guid AutorUsuarioId { get; set; }
    public Usuario? AutorUsuario { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

// Trilha append-only, separada do histórico de dados (Temporal Tables cobre o histórico linha-a-linha).
// Sem UPDATE/DELETE a nível de permissão de banco — só INSERT.
public class TrilhaAuditoria : AuditableEntity
{
    public DateTime Timestamp { get; set; }

    // Opcional (não Restrict-obrigatório): a trilha deve sobreviver à desativação do usuário
    // (soft-delete via Ativo), então a relação não pode ser required — senão o filtro global
    // de Usuario esconderia o autor de registros antigos.
    public Guid? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    // Adicionado para o Motor de Assinatura Eletrônica (docs/Motor-Assinatura-Eletronica.md §5,
    // etapa "auditoria"): trabalhador de obra que assina por crachá/PIN ou biometria normalmente não
    // tem conta Usuario, então UsuarioId sozinho não capturava "quem" para esses registros. Mesmo
    // padrão nullable/opcional de UsuarioId, pelo mesmo motivo — não esconder autor de registros
    // antigos quando o trabalhador for desativado.
    public Guid? TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }

    public string Acao { get; set; } = string.Empty; // ex.: "Aso.VisualizarClinico", "PT.Bloqueada"
    public string EntidadeTipo { get; set; } = string.Empty;
    public Guid EntidadeId { get; set; }

    public string? DadosAntesJson { get; set; }
    public string? DadosDepoisJson { get; set; }

    public string HashRegistroAnterior { get; set; } = string.Empty;
    public string HashRegistroAtual { get; set; } = string.Empty;
}
