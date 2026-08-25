using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Ativos.Queries;

public record ObterAtivoSstPorIdQuery(Guid Id) : IRequest<AtivoSstDto?>;

public class ObterAtivoSstPorIdQueryHandler : IRequestHandler<ObterAtivoSstPorIdQuery, AtivoSstDto?>
{
    private readonly IAppDbContext _db;

    public ObterAtivoSstPorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<AtivoSstDto?> Handle(ObterAtivoSstPorIdQuery request, CancellationToken ct)
    {
        return await _db.AtivosSst
            .Include(a => a.Obra)
            .Where(a => a.Id == request.Id)
            .Select(a => new AtivoSstDto
            {
                Id = a.Id,
                ObraId = a.ObraId,
                ObraNome = a.Obra != null ? a.Obra.Nome : string.Empty,
                TipoAtivo = a.TipoAtivo,
                Identificacao = a.Identificacao,
                Descricao = a.Descricao,
                Localizacao = a.Localizacao,
                DataValidade = a.DataValidade,
                Observacoes = a.Observacoes
            })
            .FirstOrDefaultAsync(ct);
    }
}
