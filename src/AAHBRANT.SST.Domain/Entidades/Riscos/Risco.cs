using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Registro de avaliação de risco (Base de Conhecimento §14): Atividade → Perigo → Evento
// perigoso → Consequência → Risco → Avaliação → Controle. Evidências (fotos, laudos etc.)
// reaproveitam a tabela polimórfica Evidencia (EntidadeTipo = nameof(Risco)).
public class Risco : AuditableEntity
{
    public Guid AtividadeId { get; set; }
    public Atividade? Atividade { get; set; }

    public Guid PerigoId { get; set; }
    public Perigo? Perigo { get; set; }

    public string? Ambiente { get; set; }
    public string? Exposicao { get; set; }
    public string? Consequencia { get; set; }

    // Índices na escala configurada pela MatrizRiscoConfig ativa (ex.: 1-5).
    public int Probabilidade { get; set; }
    public int Severidade { get; set; }

    // Calculado a partir de MatrizRiscoCelula (Probabilidade × Severidade) no momento do salvamento — §36.
    public NivelRisco NivelRisco { get; set; }

    public string? ControlesExistentes { get; set; }
    public string? ControlesAdicionais { get; set; }

    public Guid? ResponsavelUsuarioId { get; set; }
    public Usuario? ResponsavelUsuario { get; set; }

    public DateTime? Prazo { get; set; }
    public StatusControleRisco Status { get; set; } = StatusControleRisco.Pendente;

    public ICollection<RiscoTrabalhadorExposto> TrabalhadoresExpostos { get; set; } = new List<RiscoTrabalhadorExposto>();
}

// "Trabalhadores expostos" do §14 — modelado como relação identificável (não só contagem),
// pois o motor de elegibilidade (§45) precisa saber exatamente quem está exposto a qual risco.
public class RiscoTrabalhadorExposto : AuditableEntity
{
    public Guid RiscoId { get; set; }
    public Risco? Risco { get; set; }

    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }
}
