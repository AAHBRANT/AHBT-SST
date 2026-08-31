using MediatR;

namespace AAHBRANT.SST.Application.Pcmsos.Queries;

public record ObterPcmsoPorIdQuery(Guid Id) : IRequest<PcmsoDto?>;

public class ObterPcmsoPorIdQueryHandler : IRequestHandler<ObterPcmsoPorIdQuery, PcmsoDto?>
{
    // PENDENTE: esta query fazia join de PcmsoDetalhe com DocumentoGestao (removido junto com
    // Gestão Documental/Conformidade em 2026-08-28) — ver nota em PcmsoDetalhe
    // (Domain/Entidades/SaudeOcupacional/SaudeOcupacional.cs). Retorna null (404) até ser reformulada.
    public Task<PcmsoDto?> Handle(ObterPcmsoPorIdQuery request, CancellationToken ct) =>
        Task.FromResult<PcmsoDto?>(null);
}
