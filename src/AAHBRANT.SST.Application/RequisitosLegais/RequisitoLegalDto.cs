using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.RequisitosLegais;

public record RequisitoLegalDto(
    Guid Id,
    string Norma,
    string? Artigo,
    string Titulo,
    string Descricao,
    CategoriaRequisitoLegal Categoria,
    StatusRequisitoLegal Status,
    string? Fonte);

public record RequisitoLegalCriterioDto(
    Guid Id,
    TipoCriterioAplicabilidade Tipo,
    Guid? PerigoId,
    string? PerigoNome,
    Guid? FuncaoId,
    string? FuncaoNome,
    TipoAtivo? TipoEquipamento,
    Guid? ItemQuestionarioAplicabilidadeId,
    string? ItemQuestionarioPergunta);

public record RequisitoLegalDetalheDto(RequisitoLegalDto Requisito, List<RequisitoLegalCriterioDto> Criterios);
