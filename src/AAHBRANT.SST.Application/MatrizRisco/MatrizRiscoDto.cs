using AAHBRANT.SST.Domain.Entidades;
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
    public int Versao { get; set; }
    public bool Vigente { get; set; }
    public DateTime VigenteDesdeUtc { get; set; }
    public DateTime? SubstituidaEmUtc { get; set; }
    public string? MotivoRevisao { get; set; }
    public string? RevisadaPorNome { get; set; }
    public List<MatrizRiscoCelulaDto> Celulas { get; set; } = new();
}

public static class MatrizRiscoMapeamento
{
    public static MatrizRiscoConfigDto ParaDto(MatrizRiscoConfig c) => new()
    {
        Id = c.Id,
        Nome = c.Nome,
        NumNiveisProbabilidade = c.NumNiveisProbabilidade,
        NumNiveisSeveridade = c.NumNiveisSeveridade,
        Versao = c.Versao,
        Vigente = c.Vigente,
        VigenteDesdeUtc = c.VigenteDesdeUtc,
        SubstituidaEmUtc = c.SubstituidaEmUtc,
        MotivoRevisao = c.MotivoRevisao,
        RevisadaPorNome = c.RevisadaPorNome,
        Celulas = c.Celulas
            .OrderBy(cel => cel.Probabilidade).ThenBy(cel => cel.Severidade)
            .Select(cel => new MatrizRiscoCelulaDto { Probabilidade = cel.Probabilidade, Severidade = cel.Severidade, NivelRisco = cel.NivelRisco })
            .ToList(),
    };
}

// A matriz que classifica os riscos é sempre a vigente. Todo consumidor lê por aqui: a de maior versão entre as
// marcadas como vigentes (só há uma, mas a regra protege contra qualquer estado intermediário).
public static class MatrizRiscoVigente
{
    public static IQueryable<MatrizRiscoConfig> VigenteMaisRecente(this IQueryable<MatrizRiscoConfig> consulta) =>
        consulta.Where(m => m.Vigente).OrderByDescending(m => m.Versao);
}
