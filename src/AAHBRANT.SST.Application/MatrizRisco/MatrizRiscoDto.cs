using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.MatrizRisco;

public class MatrizRiscoCelulaDto
{
    public int Probabilidade { get; set; }
    public int Severidade { get; set; }
    public NivelRisco NivelRisco { get; set; }
}

public class MatrizRiscoConfigDto
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int NumNiveisProbabilidade { get; set; }
    public int NumNiveisSeveridade { get; set; }
    public List<MatrizRiscoCelulaDto> Celulas { get; set; } = new();
}
