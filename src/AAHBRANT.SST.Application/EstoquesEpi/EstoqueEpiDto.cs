using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.EstoquesEpi;

public record EstoqueEpiPorObraDto(
    Guid CatalogoEpiId,
    string CatalogoEpiNome,
    string? Fabricante,
    int Saldo);

public record MovimentacaoEstoqueEpiDto(
    Guid Id,
    TipoMovimentacaoEstoqueEpi Tipo,
    int Quantidade,
    int SaldoResultante,
    DateTime CreatedAtUtc,
    string? Observacao,
    Guid? EntregaEpiId);
