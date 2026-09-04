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
        var p = await _db.PcmsoDetalhes.Include(x => x.ResponsavelUsuario)
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);

        if (p is null) return null;

        return new PcmsoDto
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
        };
    }
}
