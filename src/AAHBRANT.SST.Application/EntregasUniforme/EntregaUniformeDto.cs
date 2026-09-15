using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.EntregasUniforme;

public record EntregaUniformeDto(
    Guid Id,
    Guid TrabalhadorId,
    Guid CatalogoUniformeId,
    string Tamanho,
    int Quantidade,
    DateTime DataEntrega,
    MotivoEntregaUniforme MotivoTipo,
    string? Observacoes);
