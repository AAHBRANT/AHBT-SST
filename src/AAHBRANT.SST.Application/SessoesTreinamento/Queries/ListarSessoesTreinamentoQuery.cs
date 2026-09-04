using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SessoesTreinamento.Queries;

public record ListarSessoesTreinamentoQuery(Guid? ObraId = null) : IRequest<List<SessaoTreinamentoDto>>;

public class ListarSessoesTreinamentoQueryHandler : IRequestHandler<ListarSessoesTreinamentoQuery, List<SessaoTreinamentoDto>>
{
    private readonly IAppDbContext _db;
    public ListarSessoesTreinamentoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<SessaoTreinamentoDto>> Handle(ListarSessoesTreinamentoQuery request, CancellationToken ct)
    {
        var query = _db.SessoesTreinamento
            .Include(s => s.Obra)
            .Include(s => s.CursoTreinamento)
            .Include(s => s.Participantes)
            .Include(s => s.FotosEvidencia)
            .AsQueryable();

        if (request.ObraId.HasValue)
            query = query.Where(s => s.ObraId == request.ObraId.Value);

        var lista = await query.OrderByDescending(s => s.CreatedAtUtc).ToListAsync(ct);
        return lista.Select(MapearParaDto).ToList();
    }

    internal static SessaoTreinamentoDto MapearParaDto(Domain.Entidades.SessaoTreinamento sessao) => new()
    {
        Id = sessao.Id,
        ObraId = sessao.ObraId,
        ObraNome = sessao.Obra?.Nome ?? string.Empty,
        CursoTreinamentoId = sessao.CursoTreinamentoId,
        CursoTreinamentoNome = sessao.CursoTreinamento?.Nome ?? string.Empty,
        DataRealizacao = sessao.DataRealizacao,
        CargaHorariaRealizada = sessao.CargaHorariaRealizada,
        InstituicaoInstrutor = sessao.InstituicaoInstrutor,
        NumeroCertificado = sessao.NumeroCertificado,
        Status = sessao.Status,
        DataEncerramento = sessao.DataEncerramento,
        TotalParticipantes = sessao.Participantes.Count(p => p.Ativo),
        TotalPresencasConfirmadas = sessao.Participantes.Count(p => p.Ativo && p.PresencaConfirmadaEm is not null),
        TotalFotosEvidencia = sessao.FotosEvidencia.Count(f => f.Ativo),
    };
}
