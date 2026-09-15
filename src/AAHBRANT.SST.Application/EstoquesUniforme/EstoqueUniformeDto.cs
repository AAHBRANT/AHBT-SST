using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.EstoquesUniforme;

public record EstoqueUniformePorObraDto(
    Guid CatalogoUniformeId,
    string CatalogoUniformeNome,
    string Tamanho,
    int Saldo);

public record MovimentacaoEstoqueUniformeDto(
    Guid Id,
    TipoMovimentacaoEstoqueUniforme Tipo,
    int Quantidade,
    int SaldoResultante,
    DateTime CreatedAtUtc,
    string? Observacao,
    Guid? EntregaUniformeId);
