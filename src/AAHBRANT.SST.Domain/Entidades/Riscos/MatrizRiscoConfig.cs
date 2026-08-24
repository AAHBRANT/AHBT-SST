using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Matriz de classificação de risco parametrizável (§36) — Probabilidade × Severidade =
// Nível de risco, sem fórmula numérica fixa no documento; cada célula é configurada
// explicitamente pela organização em vez de calculada por fórmula no código.
public class MatrizRiscoConfig : AuditableEntity
{
    public string Nome { get; set; } = string.Empty;
    public int NumNiveisProbabilidade { get; set; }
    public int NumNiveisSeveridade { get; set; }

    public ICollection<MatrizRiscoCelula> Celulas { get; set; } = new List<MatrizRiscoCelula>();
}

public class MatrizRiscoCelula : AuditableEntity
{
    public Guid MatrizRiscoConfigId { get; set; }
    public MatrizRiscoConfig? MatrizRiscoConfig { get; set; }

    public int Probabilidade { get; set; }
    public int Severidade { get; set; }
    public NivelRisco NivelRisco { get; set; }
}
