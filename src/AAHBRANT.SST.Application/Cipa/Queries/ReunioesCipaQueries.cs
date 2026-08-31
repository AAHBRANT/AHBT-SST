using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Queries;

public record ListarReunioesCipaQuery(Guid? ObraId = null) : IRequest<List<ReuniaoCipaDto>>;

public class ListarReunioesCipaQueryHandler : IRequestHandler<ListarReunioesCipaQuery, List<ReuniaoCipaDto>>
{
    private readonly IAppDbContext _db;
    public ListarReunioesCipaQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<ReuniaoCipaDto>> Handle(ListarReunioesCipaQuery request, CancellationToken ct)
    {
        var query = _db.ReunioesCipa.Include(r => r.Obra).Include(r => r.Participantes).AsQueryable();
        if (request.ObraId.HasValue) query = query.Where(r => r.ObraId == request.ObraId.Value);

        var lista = await query.OrderByDescending(r => r.DataReuniao).ToListAsync(ct);
        return lista.Select(MapearParaDto).ToList();
    }

    internal static ReuniaoCipaDto MapearParaDto(Domain.Entidades.ReuniaoCipa r) => new()
    {
        Id = r.Id,
        ObraId = r.ObraId,
        ObraNome = r.Obra?.Nome ?? string.Empty,
        Tipo = r.Tipo,
        DataReuniao = r.DataReuniao,
        Pauta = r.Pauta,
        Deliberacoes = r.Deliberacoes,
        Status = r.Status,
        TotalParticipantes = r.Participantes.Count(p => p.Ativo),
        TotalPresentes = r.Participantes.Count(p => p.Ativo && p.Presente),
    };
}

public record ObterReuniaoCipaDetalheQuery(Guid Id) : IRequest<ReuniaoCipaDetalheDto?>;

public class ObterReuniaoCipaDetalheQueryHandler : IRequestHandler<ObterReuniaoCipaDetalheQuery, ReuniaoCipaDetalheDto?>
{
    private readonly IAppDbContext _db;
    public ObterReuniaoCipaDetalheQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ReuniaoCipaDetalheDto?> Handle(ObterReuniaoCipaDetalheQuery request, CancellationToken ct)
    {
        var reuniao = await _db.ReunioesCipa
            .Include(r => r.Obra)
            .Include(r => r.Participantes.Where(p => p.Ativo)).ThenInclude(p => p.Trabalhador)
            .FirstOrDefaultAsync(r => r.Id == request.Id, ct);
        if (reuniao is null) return null;

        return new ReuniaoCipaDetalheDto
        {
            Reuniao = ListarReunioesCipaQueryHandler.MapearParaDto(reuniao),
            Participantes = reuniao.Participantes.Where(p => p.Ativo).Select(p => new ParticipanteReuniaoCipaDto
            {
                Id = p.Id,
                TrabalhadorId = p.TrabalhadorId,
                TrabalhadorNome = p.Trabalhador?.Nome ?? string.Empty,
                Presente = p.Presente,
            }).OrderBy(p => p.TrabalhadorNome).ToList(),
        };
    }
}
