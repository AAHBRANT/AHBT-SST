using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PerfisAcesso.Queries;

public record ListarPerfisAcessoQuery : IRequest<List<PerfilAcessoDto>>;

public class ListarPerfisAcessoQueryHandler : IRequestHandler<ListarPerfisAcessoQuery, List<PerfilAcessoDto>>
{
    private readonly IAppDbContext _db;

    public ListarPerfisAcessoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<PerfilAcessoDto>> Handle(ListarPerfisAcessoQuery request, CancellationToken ct)
    {
        return await _db.PerfisAcesso
            .OrderBy(p => p.Nome)
            .Select(p => new PerfilAcessoDto
            {
                Id = p.Id,
                Tipo = p.Tipo,
                Nome = p.Nome,
                Descricao = p.Descricao,
                EhSistema = p.EhSistema,
                QuantidadePermissoes = p.Permissoes.Count(pp => pp.Permitido)
            })
            .ToListAsync(ct);
    }
}
