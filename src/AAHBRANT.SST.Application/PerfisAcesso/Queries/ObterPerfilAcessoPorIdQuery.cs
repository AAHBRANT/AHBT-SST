using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PerfisAcesso.Queries;

public record ObterPerfilAcessoPorIdQuery(Guid Id) : IRequest<PerfilAcessoDto?>;

public class ObterPerfilAcessoPorIdQueryHandler : IRequestHandler<ObterPerfilAcessoPorIdQuery, PerfilAcessoDto?>
{
    private readonly IAppDbContext _db;

    public ObterPerfilAcessoPorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<PerfilAcessoDto?> Handle(ObterPerfilAcessoPorIdQuery request, CancellationToken ct)
    {
        return await _db.PerfisAcesso
            .Where(p => p.Id == request.Id)
            .Select(p => new PerfilAcessoDto
            {
                Id = p.Id,
                Tipo = p.Tipo,
                Nome = p.Nome,
                Descricao = p.Descricao,
                EhSistema = p.EhSistema,
                QuantidadePermissoes = p.Permissoes.Count(pp => pp.Permitido)
            })
            .FirstOrDefaultAsync(ct);
    }
}
