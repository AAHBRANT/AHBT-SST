using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Empresas.Queries;

public record ListarEmpresasQuery : IRequest<List<EmpresaDto>>;

public class ListarEmpresasQueryHandler : IRequestHandler<ListarEmpresasQuery, List<EmpresaDto>>
{
    private readonly IAppDbContext _db;
    public ListarEmpresasQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<EmpresaDto>> Handle(ListarEmpresasQuery request, CancellationToken ct)
        => await _db.Empresas
            .OrderBy(e => e.RazaoSocial)
            .Select(e => new EmpresaDto(
                e.Id, e.RazaoSocial, e.NomeFantasia, e.Cnpj, e.TipoServicoPrestado,
                e.ContatoNome, e.ContatoTelefone, e.ContatoEmail, e.Status.ToString()))
            .ToListAsync(ct);
}
