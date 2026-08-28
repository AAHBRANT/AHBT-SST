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
        var query =
            from d in _db.DocumentosGestao
            join p in _db.PcmsoDetalhes on d.Id equals p.DocumentoGestaoId
            where d.Id == request.Id
            select new PcmsoDto
            {
                Id = p.Id,
                DocumentoGestaoId = d.Id,
                Nome = d.Nome,
                Versao = d.Versao,
                Validade = d.Validade,
                DataEmissao = d.DataEmissao,
                ResponsavelUsuarioId = d.ResponsavelUsuarioId,
                ResponsavelUsuarioNome = d.ResponsavelUsuario != null ? d.ResponsavelUsuario.Nome : null,
                ObraId = d.ObraId,
                SetorId = d.SetorId,
                Arquivo = d.Arquivo,
                Status = d.Status,
                MedicoResponsavelNome = p.MedicoResponsavelNome,
                MedicoResponsavelCrm = p.MedicoResponsavelCrm,
                FuncoesContempladas = p.FuncoesContempladas,
                RiscosConsiderados = p.RiscosConsiderados,
                ExamesPrevistos = p.ExamesPrevistos,
                Periodicidades = p.Periodicidades,
                UnidadesObrasAbrangidas = p.UnidadesObrasAbrangidas
            };

        return await query.FirstOrDefaultAsync(ct);
    }
}
