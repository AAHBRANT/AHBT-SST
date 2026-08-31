using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Queries;

public record ListarEventosSipatQuery(Guid? ObraId = null) : IRequest<List<EventoSipatDto>>;

public class ListarEventosSipatQueryHandler : IRequestHandler<ListarEventosSipatQuery, List<EventoSipatDto>>
{
    private readonly IAppDbContext _db;
    public ListarEventosSipatQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<EventoSipatDto>> Handle(ListarEventosSipatQuery request, CancellationToken ct)
    {
        var query = _db.EventosSipat.Include(e => e.Obra).Include(e => e.Atividades).AsQueryable();
        if (request.ObraId.HasValue) query = query.Where(e => e.ObraId == request.ObraId.Value);

        var lista = await query.OrderByDescending(e => e.DataInicio).ToListAsync(ct);
        return lista.Select(MapearParaDto).ToList();
    }

    internal static EventoSipatDto MapearParaDto(Domain.Entidades.EventoSipat e) => new()
    {
        Id = e.Id,
        ObraId = e.ObraId,
        ObraNome = e.Obra?.Nome ?? string.Empty,
        AnoReferencia = e.AnoReferencia,
        DataInicio = e.DataInicio,
        DataFim = e.DataFim,
        Tema = e.Tema,
        Programacao = e.Programacao,
        TotalAtividades = e.Atividades.Count(a => a.Ativo),
    };
}

public record ObterEventoSipatDetalheQuery(Guid Id) : IRequest<EventoSipatDetalheDto?>;

public class ObterEventoSipatDetalheQueryHandler : IRequestHandler<ObterEventoSipatDetalheQuery, EventoSipatDetalheDto?>
{
    private readonly IAppDbContext _db;
    public ObterEventoSipatDetalheQueryHandler(IAppDbContext db) => _db = db;

    public async Task<EventoSipatDetalheDto?> Handle(ObterEventoSipatDetalheQuery request, CancellationToken ct)
    {
        var evento = await _db.EventosSipat
            .Include(e => e.Obra)
            .Include(e => e.Atividades.Where(a => a.Ativo))
            .FirstOrDefaultAsync(e => e.Id == request.Id, ct);
        if (evento is null) return null;

        return new EventoSipatDetalheDto
        {
            Evento = ListarEventosSipatQueryHandler.MapearParaDto(evento),
            Atividades = evento.Atividades.Where(a => a.Ativo).OrderBy(a => a.Data).Select(a => new AtividadeSipatDto
            {
                Id = a.Id,
                Data = a.Data,
                Horario = a.Horario,
                TemaPalestra = a.TemaPalestra,
                Palestrante = a.Palestrante,
            }).ToList(),
        };
    }
}
