using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PerfisAcesso.Queries;

// Alimenta a tela "Matriz de Permissões": para um perfil, todas as linhas Permissao x Escopo
// já concedidas (o frontend cruza com ListarPermissoesQuery — catálogo completo — para desenhar
// o grid inteiro, marcando o que já está Permitido).
public record ListarPermissoesPorPerfilQuery(Guid PerfilAcessoId) : IRequest<List<PerfilAcessoPermissaoDto>>;

public class ListarPermissoesPorPerfilQueryHandler
    : IRequestHandler<ListarPermissoesPorPerfilQuery, List<PerfilAcessoPermissaoDto>>
{
    private readonly IAppDbContext _db;

    public ListarPermissoesPorPerfilQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<PerfilAcessoPermissaoDto>> Handle(ListarPermissoesPorPerfilQuery request, CancellationToken ct)
    {
        return await _db.PerfisAcessoPermissoes
            .Where(pp => pp.PerfilAcessoId == request.PerfilAcessoId)
            .Select(pp => new PerfilAcessoPermissaoDto
            {
                Id = pp.Id,
                PermissaoId = pp.PermissaoId,
                PermissaoCodigo = pp.Permissao!.Codigo,
                PermissaoModulo = pp.Permissao!.Modulo,
                PermissaoAcao = pp.Permissao!.Acao,
                Escopo = pp.Escopo,
                Permitido = pp.Permitido
            })
            .ToListAsync(ct);
    }
}
