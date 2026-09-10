using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Pcmsos.Queries;

public record ObterPcmsoPorIdQuery(Guid Id) : IRequest<PcmsoDto?>;

public class ObterPcmsoPorIdQueryHandler : IRequestHandler<ObterPcmsoPorIdQuery, PcmsoDto?>
{
    private readonly IAppDbContext _db;

    public ObterPcmsoPorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<PcmsoDto?> Handle(ObterPcmsoPorIdQuery request, CancellationToken ct)
    {
        return await _db.PcmsoDetalhes
            .Where(p => p.Id == request.Id)
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
            .FirstOrDefaultAsync(ct);
    }
}
