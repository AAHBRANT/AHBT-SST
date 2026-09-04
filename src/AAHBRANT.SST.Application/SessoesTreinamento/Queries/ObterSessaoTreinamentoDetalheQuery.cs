using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SessoesTreinamento.Queries;

public record ObterSessaoTreinamentoDetalheQuery(Guid Id) : IRequest<SessaoTreinamentoDetalheDto?>;

public class ObterSessaoTreinamentoDetalheQueryHandler : IRequestHandler<ObterSessaoTreinamentoDetalheQuery, SessaoTreinamentoDetalheDto?>
{
    private readonly IAppDbContext _db;
    public ObterSessaoTreinamentoDetalheQueryHandler(IAppDbContext db) => _db = db;

    public async Task<SessaoTreinamentoDetalheDto?> Handle(ObterSessaoTreinamentoDetalheQuery request, CancellationToken ct)
    {
        var sessao = await _db.SessoesTreinamento
            .Include(s => s.Obra)
            .Include(s => s.CursoTreinamento)
            .FirstOrDefaultAsync(s => s.Id == request.Id, ct);
        if (sessao is null) return null;

        var participantes = await _db.ParticipantesSessaoTreinamento
            .Where(p => p.SessaoTreinamentoId == sessao.Id && p.Ativo)
            .Include(p => p.Trabalhador)
            .ToListAsync(ct);

        var fotosEvidencia = await _db.FotosEvidenciaSessaoTreinamento
            .Where(f => f.SessaoTreinamentoId == sessao.Id && f.Ativo)
            .OrderBy(f => f.Ordem)
            .ToListAsync(ct);

        sessao.Participantes = participantes;
        sessao.FotosEvidencia = fotosEvidencia;

        return new SessaoTreinamentoDetalheDto
        {
            Sessao = ListarSessoesTreinamentoQueryHandler.MapearParaDto(sessao),
            Participantes = participantes
                .OrderBy(p => p.Trabalhador?.Nome)
                .Select(p => new ParticipanteSessaoTreinamentoDto
                {
                    Id = p.Id,
                    TrabalhadorId = p.TrabalhadorId,
                    TrabalhadorNome = p.Trabalhador?.Nome ?? string.Empty,
                    TrabalhadorMatricula = p.Trabalhador?.Matricula,
                    PresencaConfirmadaEm = p.PresencaConfirmadaEm,
                    ScoreConfianca = p.ScoreConfianca,
                    TreinamentoGeradoId = p.TreinamentoGeradoId,
                }).ToList(),
            FotosEvidencia = fotosEvidencia.Select(f => new FotoEvidenciaSessaoTreinamentoDto { Id = f.Id, Ordem = f.Ordem }).ToList(),
        };
    }
}
