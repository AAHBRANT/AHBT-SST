using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Atividades.Queries;

public record ObterAtividadePorIdQuery(Guid Id) : IRequest<AtividadeDto?>;

public class ObterAtividadePorIdQueryHandler : IRequestHandler<ObterAtividadePorIdQuery, AtividadeDto?>
{
    private readonly IAppDbContext _db;

    public ObterAtividadePorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<AtividadeDto?> Handle(ObterAtividadePorIdQuery request, CancellationToken ct)
    {
        return await _db.Atividades
            .Where(a => a.Id == request.Id)
            .Select(a => new AtividadeDto
            {
                Id = a.Id,
                ObraId = a.ObraId,
                Nome = a.Nome,
                Descricao = a.Descricao
            })
            .FirstOrDefaultAsync(ct);
    }
}
