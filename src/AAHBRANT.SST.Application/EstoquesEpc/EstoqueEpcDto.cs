using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.EstoquesEpc;

public record EstoqueEpcPorObraDto(
    Guid CatalogoEpcId,
    string CatalogoEpcNome,
    string? Fabricante,
    int Saldo);

public record MovimentacaoEstoqueEpcDto(
    Guid Id,
    TipoMovimentacaoEstoqueEpc Tipo,
    int Quantidade,
    int SaldoResultante,
    DateTime CreatedAtUtc,
    string? Observacao,
    Guid? InstalacaoEpcId);
