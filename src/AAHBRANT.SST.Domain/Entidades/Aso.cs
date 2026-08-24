using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

public class Aso : AuditableEntity
{
    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }

    public TipoExameAso Tipo { get; set; }
    public DateTime DataExame { get; set; }
    public DateTime DataValidade { get; set; }

    // Conteúdo clínico — visível apenas ao perfil Médico do Trabalho (docs/RBAC-Matrix.md); demais perfis veem só ResultadoStatus.
    public ResultadoAso ResultadoStatus { get; set; } = ResultadoAso.Pendente;
    public string? MedicoNome { get; set; }
    public string? MedicoCrm { get; set; }
    public string? ObservacoesClinicas { get; set; }

    public ICollection<AsoRestricao> Restricoes { get; set; } = new List<AsoRestricao>();
    public ICollection<Evidencia> Evidencias { get; set; } = new List<Evidencia>();
}

public class AsoRestricao : AuditableEntity
{
    public Guid AsoId { get; set; }
    public Aso? Aso { get; set; }

    public string Descricao { get; set; } = string.Empty;
}
