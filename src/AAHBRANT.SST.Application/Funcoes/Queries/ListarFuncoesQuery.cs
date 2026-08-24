using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Funcoes.Queries;

public record ListarFuncoesQuery : IRequest<List<FuncaoDto>>;

public class ListarFuncoesQueryHandler : IRequestHandler<ListarFuncoesQuery, List<FuncaoDto>>
{
    private readonly IAppDbContext _db;

    public ListarFuncoesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<FuncaoDto>> Handle(ListarFuncoesQuery request, CancellationToken ct)
    {
        return await _db.Funcoes
            .OrderBy(f => f.Nome)
            .Select(f => new FuncaoDto
            {
                Id = f.Id,
                Nome = f.Nome,
                CboCodigo = f.CboCodigo,
                Descricao = f.Descricao
            })
            .ToListAsync(ct);
    }
}
