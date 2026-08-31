using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Queries;

public record ObterDdsSemanalDetalheQuery(Guid Id) : IRequest<DdsSemanalDetalheDto?>;

public class ObterDdsSemanalDetalheQueryHandler : IRequestHandler<ObterDdsSemanalDetalheQuery, DdsSemanalDetalheDto?>
{
    private readonly IAppDbContext _db;

    public ObterDdsSemanalDetalheQueryHandler(IAppDbContext db) => _db = db;

    public async Task<DdsSemanalDetalheDto?> Handle(ObterDdsSemanalDetalheQuery request, CancellationToken ct)
    {
        var semanal = await _db.DdsSemanais
            .Include(s => s.Obra)
            .Include(s => s.ResponsavelUsuario)
            .Include(s => s.ResponsavelObraSstUsuario)
            .Include(s => s.RegistrosDiarios)
            .FirstOrDefaultAsync(s => s.Id == request.Id, ct);
        if (semanal is null) return null;

        var diasIds = semanal.RegistrosDiarios.Where(d => d.Ativo).Select(d => d.Id).ToList();
        var fotosPorDia = await _db.DdsFotosEvidencia
            .Where(f => diasIds.Contains(f.DdsId) && f.Ativo)
            .GroupBy(f => f.DdsId)
            .Select(g => new { DdsId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(g => g.DdsId, g => g.Total, ct);
        var participantesPorDia = await _db.DdsParticipantes
            .Where(p => diasIds.Contains(p.DdsId) && p.Ativo)
            .GroupBy(p => p.DdsId)
            .Select(g => new { DdsId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(g => g.DdsId, g => g.Total, ct);

        var dias = new List<DdsSemanalDiaDto>();
        for (var i = 0; i < 5; i++)
        {
            var data = semanal.DataInicioSemana.AddDays(i);
            var registroDoDia = semanal.RegistrosDiarios.FirstOrDefault(d => d.Ativo && d.Data.Date == data.Date);
            dias.Add(new DdsSemanalDiaDto
            {
                DiaSemana = data.DayOfWeek,
                Data = data,
                DdsId = registroDoDia?.Id,
                TopicoPrincipal = registroDoDia?.TopicoPrincipal,
                Status = registroDoDia?.Status,
                TotalFotosEvidencia = registroDoDia is null ? 0 : fotosPorDia.GetValueOrDefault(registroDoDia.Id),
                TotalParticipantes = registroDoDia is null ? 0 : participantesPorDia.GetValueOrDefault(registroDoDia.Id),
            });
        }

        return new DdsSemanalDetalheDto
        {
            Semanal = ListarDdsSemanaisQueryHandler.MapearParaDto(semanal),
            Dias = dias,
        };
    }
}
