using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Queries;

public record ObterDdsDetalheQuery(Guid Id) : IRequest<DdsDetalheDto?>;

public class ObterDdsDetalheQueryHandler : IRequestHandler<ObterDdsDetalheQuery, DdsDetalheDto?>
{
    private readonly IAppDbContext _db;

    public ObterDdsDetalheQueryHandler(IAppDbContext db) => _db = db;

    public async Task<DdsDetalheDto?> Handle(ObterDdsDetalheQuery request, CancellationToken ct)
    {
        var dds = await _db.Dds
            .Include(d => d.Obra)
            .Include(d => d.ResponsavelUsuario)
            .Include(d => d.Atividades).ThenInclude(a => a.Atividade)
            .FirstOrDefaultAsync(d => d.Id == request.Id, ct);
        if (dds is null) return null;

        var itens = await _db.DdsItensChecklist
            .Where(i => i.DdsId == dds.Id && i.Ativo)
            .ToListAsync(ct);

        var participantes = await _db.DdsParticipantes
            .Where(p => p.DdsId == dds.Id && p.Ativo)
            .Include(p => p.Trabalhador)
            .ToListAsync(ct);

        var fotosEvidencia = await _db.DdsFotosEvidencia
            .Where(f => f.DdsId == dds.Id && f.Ativo)
            .OrderBy(f => f.Ordem)
            .ToListAsync(ct);

        // Envio mais recente por trabalhador (reenvios substituem o status anterior na tela).
        var enviosPorTrabalhador = await _db.DdsTelegramEnvios
            .Where(e => e.DdsId == dds.Id && e.Ativo)
            .GroupBy(e => e.TrabalhadorId)
            .Select(g => g.OrderByDescending(e => e.EnviadoEm).First())
            .ToDictionaryAsync(e => e.TrabalhadorId, ct);

        dds.ItensChecklist = itens;
        dds.Participantes = participantes;
        dds.FotosEvidencia = fotosEvidencia;

        return new DdsDetalheDto
        {
            Dds = ListarDdsQueryHandler.MapearParaDto(dds),
            ItensChecklist = itens.Select(i => new DdsItemChecklistDto
            {
                Id = i.Id,
                DdsId = i.DdsId,
                RiscoId = i.RiscoId,
                Descricao = i.Descricao,
                Verificado = i.Verificado,
            }).ToList(),
            Participantes = participantes.Select(p =>
            {
                enviosPorTrabalhador.TryGetValue(p.TrabalhadorId, out var envio);
                return new DdsParticipanteDto
                {
                    Id = p.Id,
                    TrabalhadorId = p.TrabalhadorId,
                    TrabalhadorNome = p.Trabalhador?.Nome ?? string.Empty,
                    FotoTipo = p.FotoTipo,
                    ScoreConfianca = p.ScoreConfianca,
                    TelegramEnviadoEm = envio?.EnviadoEm,
                    TelegramConfirmadoEm = envio?.ConfirmadoEm,
                };
            }).ToList(),
            FotosEvidencia = fotosEvidencia.Select(f => new DdsFotoEvidenciaDto { Id = f.Id, Ordem = f.Ordem }).ToList(),
        };
    }
}
