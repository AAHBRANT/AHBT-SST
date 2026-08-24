using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Funcoes.Queries;

public record ObterFuncaoPorIdQuery(Guid Id) : IRequest<FuncaoDto?>;

public class ObterFuncaoPorIdQueryHandler : IRequestHandler<ObterFuncaoPorIdQuery, FuncaoDto?>
{
    private readonly IAppDbContext _db;

    public ObterFuncaoPorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<FuncaoDto?> Handle(ObterFuncaoPorIdQuery request, CancellationToken ct)
    {
        return await _db.Funcoes
            .Where(f => f.Id == request.Id)
            .Select(f => new FuncaoDto
            {
                Id = f.Id,
                Nome = f.Nome,
                CboCodigo = f.CboCodigo,
                Descricao = f.Descricao
            })
            .FirstOrDefaultAsync(ct);
    }
}
