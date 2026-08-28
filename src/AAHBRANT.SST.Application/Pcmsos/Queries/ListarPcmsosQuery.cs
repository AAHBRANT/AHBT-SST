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
        var query =
            from d in _db.DocumentosGestao
            join p in _db.PcmsoDetalhes on d.Id equals p.DocumentoGestaoId
            where d.Tipo == "PCMSO"
            select new { Documento = d, Detalhe = p };

        if (request.ObraId.HasValue)
            query = query.Where(x => x.Documento.ObraId == request.ObraId.Value);

        return await query
            .OrderByDescending(x => x.Documento.DataEmissao)
            .Select(x => new PcmsoDto
            {
                Id = x.Detalhe.Id,
                DocumentoGestaoId = x.Documento.Id,
                Nome = x.Documento.Nome,
                Versao = x.Documento.Versao,
                Validade = x.Documento.Validade,
                DataEmissao = x.Documento.DataEmissao,
                ResponsavelUsuarioId = x.Documento.ResponsavelUsuarioId,
                ResponsavelUsuarioNome = x.Documento.ResponsavelUsuario != null ? x.Documento.ResponsavelUsuario.Nome : null,
                ObraId = x.Documento.ObraId,
                SetorId = x.Documento.SetorId,
                Arquivo = x.Documento.Arquivo,
                Status = x.Documento.Status,
                MedicoResponsavelNome = x.Detalhe.MedicoResponsavelNome,
                MedicoResponsavelCrm = x.Detalhe.MedicoResponsavelCrm,
                FuncoesContempladas = x.Detalhe.FuncoesContempladas,
                RiscosConsiderados = x.Detalhe.RiscosConsiderados,
                ExamesPrevistos = x.Detalhe.ExamesPrevistos,
                Periodicidades = x.Detalhe.Periodicidades,
                UnidadesObrasAbrangidas = x.Detalhe.UnidadesObrasAbrangidas
            })
            .ToListAsync(ct);
    }
}
