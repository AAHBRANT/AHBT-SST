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

    // Chave de sincronização com o G-RH (fonte do documento "ASO" na tabela `documentos` deles) —
    // nulo pra ASOs cadastrados manualmente no SST. Ver SincronizarAsoGrhCommand: campos clínicos
    // (ResultadoStatus/Restricoes/MedicoNome) só são sobrescritos quando o G-RH manda valor
    // preenchido, nunca apagam o que o médico do trabalho já lançou no SST.
    public string? GrhAsoId { get; set; }
    public DateTime? DataUltimaSincronizacao { get; set; }

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
