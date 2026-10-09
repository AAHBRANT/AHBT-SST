using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Queries;

// Temas agendados pendentes para o DDS de uma obra até o dia (ex.: tema gerado por uma ocorrência na
// véspera, ou de um dia anterior que ficou sem DDS). O primeiro é o tema fixo do registro do dia
// (CriarDdsCommand aplica o mesmo); os demais seguem para os próximos DDS.
public record ListarTemasDdsAgendadosQuery(Guid ObraId, DateTime Data) : IRequest<List<TemaDdsAgendadoDto>>;

public record TemaDdsAgendadoDto(Guid Id, Guid CatalogoTemaDdsId, string Nome, string? Roteiro, string? DescricaoOrigem);

public class ListarTemasDdsAgendadosQueryHandler : IRequestHandler<ListarTemasDdsAgendadosQuery, List<TemaDdsAgendadoDto>>
{
    private readonly IAppDbContext _db;

    public ListarTemasDdsAgendadosQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<TemaDdsAgendadoDto>> Handle(ListarTemasDdsAgendadosQuery request, CancellationToken ct)
    {
        var dia = request.Data.Date;
        return await _db.TemasDdsAgendados
            .Where(t => t.ObraId == request.ObraId && t.Data <= dia && t.DdsId == null)
            .OrderBy(t => t.Data)
            .ThenBy(t => t.CreatedAtUtc)
            .Select(t => new TemaDdsAgendadoDto(
                t.Id, t.CatalogoTemaDdsId, t.CatalogoTemaDds!.Nome, t.CatalogoTemaDds.Descricao, t.DescricaoOrigem))
            .ToListAsync(ct);
    }
}
