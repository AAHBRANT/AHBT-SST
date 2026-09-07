using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.EntregasEpc;

public record EntregaEpcDto(
    Guid Id,
    Guid TrabalhadorId,
    Guid CatalogoEpcId,
    int Quantidade,
    DateTime DataEntrega,
    MotivoEntregaEpc MotivoTipo,
    string? Observacoes);
