using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Relatorios;

public record RelatorioListaDto(
    Guid Id,
    TipoRelatorio Tipo,
    Guid? ObraId,
    string? ObraNome,
    string Titulo,
    string Resumo,
    DateTime GeradoEm,
    bool TemPdf);

// Página "Relatórios": do mais recente para o mais antigo. O escopo por obra vem do filtro global do DbContext
// (quem não tem acesso à obra não vê o relatório).
public record ListarRelatoriosQuery(TipoRelatorio? Tipo = null, Guid? ObraId = null, int Limite = 60) : IRequest<List<RelatorioListaDto>>;

public class ListarRelatoriosQueryHandler : IRequestHandler<ListarRelatoriosQuery, List<RelatorioListaDto>>
{
    private readonly IAppDbContext _db;

    public ListarRelatoriosQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<RelatorioListaDto>> Handle(ListarRelatoriosQuery request, CancellationToken ct)
    {
        var limite = Math.Clamp(request.Limite, 1, 200);

        // Sem as colunas pesadas (Imagem e Pdf): a lista só precisa do resumo.
        var linhas = await _db.RelatoriosGerados
            .Where(r => (request.Tipo == null || r.Tipo == request.Tipo) && (request.ObraId == null || r.ObraId == request.ObraId))
            .OrderByDescending(r => r.GeradoEm)
            .Take(limite)
            .Select(r => new { r.Id, r.Tipo, r.ObraId, r.Titulo, r.Resumo, r.GeradoEm, TemPdf = r.PdfNome != null })
            .ToListAsync(ct);

        var obraIds = linhas.Where(l => l.ObraId != null).Select(l => l.ObraId!.Value).Distinct().ToList();
        var obras = await _db.Obras.Where(o => obraIds.Contains(o.Id)).Select(o => new { o.Id, o.Nome }).ToDictionaryAsync(o => o.Id, o => o.Nome, ct);

        return linhas
            .Select(l => new RelatorioListaDto(l.Id, l.Tipo, l.ObraId, l.ObraId is { } oid && obras.TryGetValue(oid, out var n) ? n : null, l.Titulo, l.Resumo, l.GeradoEm, l.TemPdf))
            .ToList();
    }
}

public record ArquivoRelatorio(byte[] Conteudo, string ContentType, string Nome);

public record ObterImagemRelatorioQuery(Guid Id) : IRequest<ArquivoRelatorio?>;

public class ObterImagemRelatorioQueryHandler : IRequestHandler<ObterImagemRelatorioQuery, ArquivoRelatorio?>
{
    private readonly IAppDbContext _db;

    public ObterImagemRelatorioQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ArquivoRelatorio?> Handle(ObterImagemRelatorioQuery request, CancellationToken ct)
    {
        var r = await _db.RelatoriosGerados.Where(x => x.Id == request.Id).Select(x => new { x.Imagem, x.ChaveUnica }).FirstOrDefaultAsync(ct);
        return r is null ? null : new ArquivoRelatorio(r.Imagem, "image/png", $"relatorio-{request.Id:N}.png");
    }
}

public record ObterPdfRelatorioQuery(Guid Id) : IRequest<ArquivoRelatorio?>;

public class ObterPdfRelatorioQueryHandler : IRequestHandler<ObterPdfRelatorioQuery, ArquivoRelatorio?>
{
    private readonly IAppDbContext _db;

    public ObterPdfRelatorioQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ArquivoRelatorio?> Handle(ObterPdfRelatorioQuery request, CancellationToken ct)
    {
        var r = await _db.RelatoriosGerados.Where(x => x.Id == request.Id).Select(x => new { x.Pdf, x.PdfNome }).FirstOrDefaultAsync(ct);
        return r?.Pdf is null ? null : new ArquivoRelatorio(r.Pdf, "application/pdf", r.PdfNome ?? "relatorio.pdf");
    }
}
