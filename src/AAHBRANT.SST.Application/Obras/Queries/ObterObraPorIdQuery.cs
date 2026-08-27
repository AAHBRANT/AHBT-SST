using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Obras.Queries;

public record ObterObraPorIdQuery(Guid Id) : IRequest<ObraDto?>;

public class ObterObraPorIdQueryHandler : IRequestHandler<ObterObraPorIdQuery, ObraDto?>
{
    private readonly IAppDbContext _db;

    public ObterObraPorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ObraDto?> Handle(ObterObraPorIdQuery request, CancellationToken ct)
    {
        return await _db.Obras
            .Where(o => o.Id == request.Id)
            .Select(o => new ObraDto
            {
                Id = o.Id,
                Codigo = o.Codigo,
                Nome = o.Nome,
                Cliente = o.Cliente,
                Status = o.Status,
                DataInicio = o.DataInicio,
                DataPrevisaoTermino = o.DataPrevisaoTermino,
                DataTerminoReal = o.DataTerminoReal,
                Endereco = o.Endereco,
                Cidade = o.Cidade,
                Uf = o.Uf,
                Cnpj = o.Cnpj,
                TemLogo = o.LogoConteudo != null
            })
            .FirstOrDefaultAsync(ct);
    }
}
