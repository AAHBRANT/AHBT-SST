using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Empresas.Queries;

public record ObterEmpresaPorIdQuery(Guid Id) : IRequest<EmpresaDto?>;

public class ObterEmpresaPorIdQueryHandler : IRequestHandler<ObterEmpresaPorIdQuery, EmpresaDto?>
{
    private readonly IAppDbContext _db;
    public ObterEmpresaPorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<EmpresaDto?> Handle(ObterEmpresaPorIdQuery request, CancellationToken ct)
        => await _db.Empresas
            .Where(e => e.Id == request.Id)
            .Select(e => new EmpresaDto(
                e.Id, e.RazaoSocial, e.NomeFantasia, e.Cnpj, e.TipoServicoPrestado,
                e.ContatoNome, e.ContatoTelefone, e.ContatoEmail, e.Status.ToString()))
            .FirstOrDefaultAsync(ct);
}
