using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Matriz de classificação de risco parametrizável (§36) — Probabilidade × Severidade =
// Nível de risco, sem fórmula numérica fixa no documento; cada célula é configurada
// explicitamente pela organização em vez de calculada por fórmula no código.
//
// Versionada (25/10/2026): a matriz vigente é a única usada para classificar riscos e fica só para consulta;
// qualquer mudança é uma nova versão (revisão) que substitui a anterior. As versões antigas ficam guardadas
// como histórico, sem edição nem exclusão, e os riscos já avaliados mantêm o nível que receberam na época.
public class MatrizRiscoConfig : AuditableEntity
{
    public string Nome { get; set; } = string.Empty;
    public int NumNiveisProbabilidade { get; set; }
    public int NumNiveisSeveridade { get; set; }

    // Número da versão (1, 2, 3...). Vigente = true em uma única versão por vez.
    public int Versao { get; set; } = 1;
    public bool Vigente { get; set; } = true;
    public DateTime VigenteDesdeUtc { get; set; } = DateTime.UtcNow;
    // Preenchida quando uma versão nova toma o lugar desta.
    public DateTime? SubstituidaEmUtc { get; set; }
    // Por que a versão foi criada. Obrigatório a partir da versão 2.
    public string? MotivoRevisao { get; set; }
    public Guid? RevisadaPorUsuarioId { get; set; }
    public string? RevisadaPorNome { get; set; }

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
