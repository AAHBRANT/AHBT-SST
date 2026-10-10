using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Pgrs.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Pcmsos.Revisoes;

public record PcmsoRevisaoDto(
    Guid Id, Guid PcmsoDetalheId, int NumeroRevisao, DateTime DataRevisao, string Motivo,
    bool TemDocumento, string? DocumentoNomeArquivo, DateTime CriadoEmUtc, string? CriadoPorNome);

public record ListarPcmsoRevisoesQuery(Guid PcmsoId) : IRequest<List<PcmsoRevisaoDto>>;

public class ListarPcmsoRevisoesQueryHandler : IRequestHandler<ListarPcmsoRevisoesQuery, List<PcmsoRevisaoDto>>
{
    private readonly IAppDbContext _db;

    public ListarPcmsoRevisoesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<PcmsoRevisaoDto>> Handle(ListarPcmsoRevisoesQuery request, CancellationToken ct)
    {
        // PcmsoDetalhes tem filtro por obra; PcmsoRevisoes não. Projeção sem os PDFs.
        if (!await _db.PcmsoDetalhes.AnyAsync(p => p.Id == request.PcmsoId, ct))
            return new List<PcmsoRevisaoDto>();

        return await _db.PcmsoRevisoes
            .Where(r => r.PcmsoDetalheId == request.PcmsoId)
            .OrderByDescending(r => r.NumeroRevisao)
            .Select(r => new PcmsoRevisaoDto(
                r.Id, r.PcmsoDetalheId, r.NumeroRevisao, r.DataRevisao, r.Motivo,
                r.DocumentoConteudo != null, r.DocumentoNomeArquivo, r.CreatedAtUtc,
                _db.Usuarios.IgnoreQueryFilters().Where(u => u.Id == r.CreatedBy).Select(u => u.Nome).FirstOrDefault()))
            .ToListAsync(ct);
    }
}

public record ObterDocumentoRevisaoPcmsoQuery(Guid RevisaoId) : IRequest<DocumentoPgrResultado?>;

public class ObterDocumentoRevisaoPcmsoQueryHandler : IRequestHandler<ObterDocumentoRevisaoPcmsoQuery, DocumentoPgrResultado?>
{
    private readonly IAppDbContext _db;

    public ObterDocumentoRevisaoPcmsoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<DocumentoPgrResultado?> Handle(ObterDocumentoRevisaoPcmsoQuery request, CancellationToken ct)
    {
        var visiveis = _db.PcmsoDetalhes.Select(p => p.Id);
        var revisao = await _db.PcmsoRevisoes
            .Where(r => r.Id == request.RevisaoId && visiveis.Contains(r.PcmsoDetalheId))
            .Select(r => new { r.NumeroRevisao, r.DocumentoConteudo, r.DocumentoContentType, r.DocumentoNomeArquivo })
            .FirstOrDefaultAsync(ct);
        if (revisao?.DocumentoConteudo is null) return null;

        return new DocumentoPgrResultado
        {
            Conteudo = revisao.DocumentoConteudo,
            ContentType = revisao.DocumentoContentType ?? "application/pdf",
            NomeArquivo = revisao.DocumentoNomeArquivo ?? $"pcmso-revisao-{revisao.NumeroRevisao:00}.pdf",
        };
    }
}
