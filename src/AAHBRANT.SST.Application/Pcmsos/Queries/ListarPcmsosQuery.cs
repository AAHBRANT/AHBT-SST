using MediatR;

namespace AAHBRANT.SST.Application.Pcmsos.Queries;

public record ListarPcmsosQuery(Guid? ObraId = null) : IRequest<List<PcmsoDto>>;

public class ListarPcmsosQueryHandler : IRequestHandler<ListarPcmsosQuery, List<PcmsoDto>>
{
    // PENDENTE: esta query fazia join de PcmsoDetalhe com DocumentoGestao (removido junto com
    // Gestão Documental/Conformidade em 2026-08-28) — ver nota em PcmsoDetalhe
    // (Domain/Entidades/SaudeOcupacional/SaudeOcupacional.cs). Retorna lista vazia até ser
    // reformulada, em vez de derrubar a tela de Saúde Ocupacional com erro.
    public Task<List<PcmsoDto>> Handle(ListarPcmsosQuery request, CancellationToken ct) =>
        Task.FromResult(new List<PcmsoDto>());
}
