using AAHBRANT.SST.Application.AcoesPlano;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.MatrizLegal;

public class RequisitoLegalDto
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Norma { get; set; } = string.Empty;
    public string? Item { get; set; }
    public string Tema { get; set; } = string.Empty;
    public string Requisito { get; set; } = string.Empty;
    public bool Aplicabilidade { get; set; }
    public string? Justificativa { get; set; }
    public string? Evidencia { get; set; }
    public Guid? ResponsavelUsuarioId { get; set; }
    public string? ResponsavelUsuarioNome { get; set; }
    public string? Periodicidade { get; set; }
    public DateTime? Prazo { get; set; }
    public StatusRequisitoLegal Status { get; set; }
    public DateTime? UltimaRevisao { get; set; }
    public DateTime? ProximaRevisao { get; set; }
    public Guid? ObraId { get; set; }
    public string? ObraNome { get; set; }
}

// Composição por query, não por tabela nova — mesmo princípio já usado em NaoConformidadeDetalheDto.
// Ações corretivas de itens "Não conforme" são lidas de AcaoPlano filtrando por
// OrigemTipo=nameof(RequisitoLegal) e OrigemId=Id.
public class RequisitoLegalDetalheDto
{
    public RequisitoLegalDto RequisitoLegal { get; set; } = null!;
    public List<AcaoPlanoDto> AcoesPlano { get; set; } = new();
}
