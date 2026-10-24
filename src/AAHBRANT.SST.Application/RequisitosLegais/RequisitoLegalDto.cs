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
    string? Fonte,
    DateTime? ValidadoEmUtc = null,
    string? ValidadoPorNome = null);

public record RequisitoLegalCriterioDto(
    Guid Id,
    TipoCriterioAplicabilidade Tipo,
    Guid? PerigoId,
    string? PerigoNome,
    Guid? FuncaoId,
    string? FuncaoNome,
    TipoAtivo? TipoEquipamento,
    Guid? ItemQuestionarioAplicabilidadeId,
    string? ItemQuestionarioPergunta,
    // Ligado automaticamente pela carga das NRs (palavra-chave do perigo) — QSMS confere.
    bool SugeridoPelaCarga = false);

public record RequisitoLegalDetalheDto(RequisitoLegalDto Requisito, List<RequisitoLegalCriterioDto> Criterios);
