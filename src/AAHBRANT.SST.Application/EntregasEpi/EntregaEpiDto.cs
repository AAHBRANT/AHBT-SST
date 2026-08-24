namespace AAHBRANT.SST.Application.EntregasEpi;

public record EntregaEpiDto(
    Guid Id,
    Guid TrabalhadorId,
    Guid CatalogoEpiId,
    DateTime DataEntrega,
    DateTime? DataDevolucao,
    DateTime? DataValidade,
    bool AssinaturaColetada);
