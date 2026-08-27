using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.EntregasEpi;

public record EntregaEpiDto(
    Guid Id,
    Guid TrabalhadorId,
    Guid CatalogoEpiId,
    DateTime DataEntrega,
    DateTime? DataDevolucao,
    DateTime? DataValidade,
    int Quantidade,
    int? QuantidadeDevolucao,
    string? VistoConsorcioResponsavel,
    string? Motivo,
    string? Observacoes,
    MotivoEntregaEpi? MotivoTipo,
    string? NumeroListaPresencaNr6,
    DateTime? DataTreinamentoNr6);
