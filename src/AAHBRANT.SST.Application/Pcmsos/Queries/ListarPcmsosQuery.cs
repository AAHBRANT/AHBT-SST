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
        var query = _db.PcmsoDetalhes.Include(p => p.ResponsavelUsuario).AsQueryable();

        if (request.ObraId.HasValue)
            query = query.Where(p => p.ObraId == request.ObraId.Value);

        var pcmsos = await query.OrderByDescending(p => p.CreatedAtUtc).ToListAsync(ct);

        return pcmsos.Select(p => new PcmsoDto
        {
            Id = p.Id,
            NumeroDocumento = p.NumeroDocumento,
            Nome = p.Nome,
            Versao = p.Versao,
            Validade = p.Validade,
            DataEmissao = p.DataEmissao,
            ResponsavelUsuarioId = p.ResponsavelUsuarioId,
            ResponsavelUsuarioNome = p.ResponsavelUsuario?.Nome,
            ObraId = p.ObraId,
            SetorId = p.SetorId,
            Arquivo = p.Arquivo,
            Status = p.Status,
            MedicoResponsavelNome = p.MedicoResponsavelNome,
            MedicoResponsavelCrm = p.MedicoResponsavelCrm,
            FuncoesContempladas = p.FuncoesContempladas,
            RiscosConsiderados = p.RiscosConsiderados,
            ExamesPrevistos = p.ExamesPrevistos,
            Periodicidades = p.Periodicidades,
            UnidadesObrasAbrangidas = p.UnidadesObrasAbrangidas
        }).ToList();
    }
}
