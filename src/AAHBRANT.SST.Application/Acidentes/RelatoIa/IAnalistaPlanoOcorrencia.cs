namespace AAHBRANT.SST.Application.Acidentes.RelatoIa;

/// <summary>
/// Análise preliminar de causas e plano de ação de uma ocorrência, ancorados no PGR da obra e nos
/// requisitos legais cadastrados. É sugestão: o técnico revisa antes de registrar.
/// </summary>
public interface IAnalistaPlanoOcorrencia
{
    Task<AnalisePlanoIa> AnalisarAsync(ContextoAnaliseOcorrencia contexto, CancellationToken ct);
}

public record ContextoAnaliseOcorrencia(
    string Tipo,
    string Gravidade,
    string Descricao,
    string? Lesao,
    string? Consequencia,
    string? Atendimento,
    string? Atividade,
    IReadOnlyList<RiscoPgrResumo> RiscosPgr,
    IReadOnlyList<RequisitoLegalResumo> RequisitosLegais);

public record RiscoPgrResumo(string Perigo, int Nivel, string? Consequencia, string? ControlesExistentes, string? ControlesAdicionais);

public record RequisitoLegalResumo(string Norma, string? Item, string Titulo, string Descricao);

/// <summary>Resposta bruta do modelo — enums e papéis como texto, base ainda não conferida.</summary>
public record AnalisePlanoIa(string? Metodologia, string? Causas, IReadOnlyList<AcaoSugeridaIa> Acoes);

public record AcaoSugeridaIa(
    string? Tipo,
    string? Descricao,
    string? Prioridade,
    string? PapelResponsavel,
    string? BaseOrigem,
    string? BasePerigo,
    string? BaseControle,
    string? BaseReferencia);
