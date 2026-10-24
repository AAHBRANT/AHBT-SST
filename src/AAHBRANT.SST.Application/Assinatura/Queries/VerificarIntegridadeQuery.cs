using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura.Queries;

// PdfIntegro: null quando o documento não tem PDF/hash gravado (finalizado antes do HashPdf existir).
public record VerificacaoIntegridadeDto(bool Integro, string HashArmazenado, string HashRecalculado,
    bool ConteudoIntegro = true, bool? PdfIntegro = null);

// Recalcula o hash do conteúdo (mesmo algoritmo de FinalizarDocumentoCommand, via
// HashConteudoDocumentoCalculador) e compara com o que foi gravado na finalização. Divergência = alguém
// alterou signatário/data/método por fora do fluxo normal (ex.: update direto no banco) depois de
// finalizado — o documento em si (EntidadeTipo/EntidadeId) nunca muda após criado.
public record VerificarIntegridadeQuery(Guid DocumentoAssinaturaId) : IRequest<VerificacaoIntegridadeDto>;

public class VerificarIntegridadeQueryValidator : AbstractValidator<VerificarIntegridadeQuery>
{
    public VerificarIntegridadeQueryValidator()
    {
        RuleFor(x => x.DocumentoAssinaturaId).NotEmpty();
    }
}

public class VerificarIntegridadeQueryHandler : IRequestHandler<VerificarIntegridadeQuery, VerificacaoIntegridadeDto>
{
    private readonly IAppDbContext _db;

    public VerificarIntegridadeQueryHandler(IAppDbContext db) => _db = db;

    public async Task<VerificacaoIntegridadeDto> Handle(VerificarIntegridadeQuery request, CancellationToken ct)
    {
        var documento = await _db.DocumentosAssinatura.FirstOrDefaultAsync(d => d.Id == request.DocumentoAssinaturaId, ct);
        if (documento is null || !await EscopoDocumentoAssinatura.PodeVerAsync(_db, documento.EntidadeTipo, documento.EntidadeId, ct))
            throw new KeyNotFoundException("Documento de assinatura não encontrado.");
        if (documento.Status != StatusDocumentoAssinatura.Finalizado || documento.ConteudoHash is null)
            throw new InvalidOperationException("Este documento ainda não foi finalizado — nada para verificar.");

        var signatarios = await _db.DocumentoSignatarios
            .Where(s => s.DocumentoAssinaturaId == documento.Id)
            // IgnoreQueryFilters: signatário desligado ou transferido de obra continua no documento. Com o filtro
            // ele sumia do recálculo e o documento aparecia como "adulterado" sem ter sido (11/10).
            .Join(_db.Trabalhadores.IgnoreQueryFilters(), s => s.TrabalhadorId, t => t.Id,
                (s, t) => new DocumentoSignatarioDto(t.Id, t.Nome, s.MetodoAutenticacao, s.AssinadoEm, s.IpAddress))
            .ToListAsync(ct);

        var hashRecalculado = HashConteudoDocumentoCalculador.Calcular(documento.EntidadeTipo, documento.EntidadeId, signatarios);

        var conteudoIntegro = hashRecalculado == documento.ConteudoHash;
        // Os bytes do PDF também entram (auditoria 19/10, A07): antes, trocar o arquivo guardado mantendo
        // a lista de signatários passava como "íntegro".
        bool? pdfIntegro = documento.HashPdf is not null && documento.PdfConteudo is not null
            ? string.Equals(HashArquivoCalculador.Calcular(documento.PdfConteudo), documento.HashPdf, StringComparison.OrdinalIgnoreCase)
            : null;

        return new VerificacaoIntegridadeDto(conteudoIntegro && pdfIntegro != false, documento.ConteudoHash, hashRecalculado,
            conteudoIntegro, pdfIntegro);
    }
}
