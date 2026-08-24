using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.GestaoDocumental.Queries;

// Agrega DocumentoGestao + DocumentoRevisao (histórico) via query (mesmo princípio já usado em
// RequisitoLegalDetalheDto/NaoConformidadeDetalheDto).
public record ObterDocumentoGestaoDetalheQuery(Guid Id) : IRequest<DocumentoGestaoDetalheDto>;

public class ObterDocumentoGestaoDetalheQueryHandler
    : IRequestHandler<ObterDocumentoGestaoDetalheQuery, DocumentoGestaoDetalheDto>
{
    private readonly IAppDbContext _db;

    public ObterDocumentoGestaoDetalheQueryHandler(IAppDbContext db) => _db = db;

    public async Task<DocumentoGestaoDetalheDto> Handle(ObterDocumentoGestaoDetalheQuery request, CancellationToken ct)
    {
        var documento = await _db.DocumentosGestao
            .Include(d => d.ResponsavelUsuario)
            .Include(d => d.RequisitoLegal)
            .Include(d => d.Obra)
            .Include(d => d.Setor)
            .Where(d => d.Id == request.Id)
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
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"Documento {request.Id} não encontrado.");

        var historico = await _db.DocumentoRevisoes
            .Where(r => r.DocumentoId == request.Id)
            .Include(r => r.ResponsavelUsuario)
            .OrderByDescending(r => r.NumeroRevisao)
            .Select(r => new DocumentoRevisaoDto
            {
                Id = r.Id,
                NumeroRevisao = r.NumeroRevisao,
                DataRevisao = r.DataRevisao,
                Motivo = r.Motivo,
                ResponsavelUsuarioId = r.ResponsavelUsuarioId,
                ResponsavelUsuarioNome = r.ResponsavelUsuario != null ? r.ResponsavelUsuario.Nome : null,
            })
            .ToListAsync(ct);

        return new DocumentoGestaoDetalheDto
        {
            Documento = documento,
            Historico = historico,
        };
    }
}
