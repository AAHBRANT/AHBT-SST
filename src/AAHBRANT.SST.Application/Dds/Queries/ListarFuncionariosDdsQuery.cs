using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Queries;

public record ListarFuncionariosDdsQuery(Guid DdsId) : IRequest<List<DdsFuncionarioDto>>;

public class ListarFuncionariosDdsQueryHandler(IAppDbContext db)
    : IRequestHandler<ListarFuncionariosDdsQuery, List<DdsFuncionarioDto>>
{
    public async Task<List<DdsFuncionarioDto>> Handle(ListarFuncionariosDdsQuery request, CancellationToken ct)
    {
        var dds = await db.Dds.AsNoTracking().FirstOrDefaultAsync(d => d.Id == request.DdsId, ct)
            ?? throw new KeyNotFoundException("DDS não encontrado.");

        return await db.Trabalhadores.AsNoTracking()
            .Where(t => t.ObraId == dds.ObraId && t.Ativo && t.Situacao == SituacaoTrabalhador.Ativo
                && (!t.DataDemissao.HasValue || t.DataDemissao.Value.Date > dds.Data.Date))
            .OrderBy(t => t.Nome)
            .Select(t => new DdsFuncionarioDto(t.Id, t.Nome, t.Matricula))
            .ToListAsync(ct);
    }
}
