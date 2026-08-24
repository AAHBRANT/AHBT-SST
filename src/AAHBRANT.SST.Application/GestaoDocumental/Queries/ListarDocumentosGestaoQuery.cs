using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.GestaoDocumental.Queries;

public record ListarDocumentosGestaoQuery(
    string? Nome,
    string? Tipo,
    string? Categoria,
    StatusDocumentoGestao? Status,
    Guid? ObraId,
    Guid? SetorId) : IRequest<List<DocumentoGestaoDto>>;

public class ListarDocumentosGestaoQueryHandler
    : IRequestHandler<ListarDocumentosGestaoQuery, List<DocumentoGestaoDto>>
{
    private readonly IAppDbContext _db;

    public ListarDocumentosGestaoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<DocumentoGestaoDto>> Handle(ListarDocumentosGestaoQuery request, CancellationToken ct)
    {
        var query = _db.DocumentosGestao.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Nome))
            query = query.Where(d => d.Nome.Contains(request.Nome));

        if (!string.IsNullOrWhiteSpace(request.Tipo))
            query = query.Where(d => d.Tipo != null && d.Tipo.Contains(request.Tipo));

        if (!string.IsNullOrWhiteSpace(request.Categoria))
            query = query.Where(d => d.Categoria != null && d.Categoria.Contains(request.Categoria));

        if (request.Status.HasValue)
            query = query.Where(d => d.Status == request.Status.Value);

        if (request.ObraId.HasValue)
            query = query.Where(d => d.ObraId == request.ObraId.Value);

        if (request.SetorId.HasValue)
            query = query.Where(d => d.SetorId == request.SetorId.Value);

        return await query
            .Include(d => d.ResponsavelUsuario)
            .Include(d => d.RequisitoLegal)
            .Include(d => d.Obra)
            .Include(d => d.Setor)
            .OrderBy(d => d.Nome)
            .Select(d => new DocumentoGestaoDto
            {
                Id = d.Id,
                Nome = d.Nome,
                Tipo = d.Tipo,
                Categoria = d.Categoria,
                OrigemDocumento = d.OrigemDocumento,
                ResponsavelUsuarioId = d.ResponsavelUsuarioId,
                ResponsavelUsuarioNome = d.ResponsavelUsuario != null ? d.ResponsavelUsuario.Nome : null,
                Versao = d.Versao,
                Validade = d.Validade,
                DataEmissao = d.DataEmissao,
                DataRevisao = d.DataRevisao,
                RequisitoLegalId = d.RequisitoLegalId,
                RequisitoLegalCodigo = d.RequisitoLegal != null ? d.RequisitoLegal.Codigo : null,
                ObraId = d.ObraId,
                ObraNome = d.Obra != null ? d.Obra.Nome : null,
                SetorId = d.SetorId,
                SetorNome = d.Setor != null ? d.Setor.Nome : null,
                Status = d.Status,
                Arquivo = d.Arquivo,
            })
            .ToListAsync(ct);
    }
}
