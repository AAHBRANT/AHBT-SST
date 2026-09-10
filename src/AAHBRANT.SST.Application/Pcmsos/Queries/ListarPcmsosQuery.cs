using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Pcmsos.Queries;

public record ListarPcmsosQuery(Guid? ObraId = null) : IRequest<List<PcmsoDto>>;

public class ListarPcmsosQueryHandler : IRequestHandler<ListarPcmsosQuery, List<PcmsoDto>>
{
    private readonly IAppDbContext _db;

    public ListarPcmsosQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<PcmsoDto>> Handle(ListarPcmsosQuery request, CancellationToken ct)
    {
        var query = _db.PcmsoDetalhes.AsQueryable();

        if (request.ObraId.HasValue)
            query = query.Where(p => p.ObraId == request.ObraId.Value);

        return await query.OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new PcmsoDto
            {
                Id = p.Id,
                NumeroDocumento = p.NumeroDocumento,
                Nome = p.Nome,
                Versao = p.Versao,
                Validade = p.Validade,
                DataEmissao = p.DataEmissao,
                ResponsavelUsuarioId = p.ResponsavelUsuarioId,
                ResponsavelUsuarioNome = p.ResponsavelUsuario != null ? p.ResponsavelUsuario.Nome : null,
                ObraId = p.ObraId,
                SetorId = p.SetorId,
                Status = p.Status,
                MedicoResponsavelNome = p.MedicoResponsavelNome,
                MedicoResponsavelCrm = p.MedicoResponsavelCrm,
                FuncoesContempladas = p.FuncoesContempladas,
                RiscosConsiderados = p.RiscosConsiderados,
                ExamesPrevistos = p.ExamesPrevistos,
                Periodicidades = p.Periodicidades,
                UnidadesObrasAbrangidas = p.UnidadesObrasAbrangidas
            })
            .ToListAsync(ct);
    }
}
