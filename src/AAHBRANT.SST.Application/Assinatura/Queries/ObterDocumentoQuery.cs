using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura.Queries;

public record DocumentoSignatarioDto(Guid TrabalhadorId, string TrabalhadorNome, MetodoAutenticacaoAssinatura MetodoAutenticacao, DateTime AssinadoEm);

public record DocumentoAssinaturaDto(
    Guid Id,
    string EntidadeTipo,
    Guid EntidadeId,
    StatusDocumentoAssinatura Status,
    List<DocumentoSignatarioDto> Signatarios,
    string? ConteudoHash = null,
    DateTime? FinalizadoEm = null,
    bool TemPdf = false,
    string? TokenValidacaoPublica = null);

// Busca o documento pela entidade de origem (ex.: EntidadeTipo="Dds", EntidadeId=ddsId) em vez de por
// Id do documento — é assim que a tela de quiosque descobre se já existe um documento aberto para o
// DDS que está sendo conduzido, sem precisar guardar o DocumentoAssinaturaId em nenhum lugar do
// módulo Dds (mantendo o motor desacoplado, conforme §3 do doc).
public record ObterDocumentoQuery(string EntidadeTipo, Guid EntidadeId) : IRequest<DocumentoAssinaturaDto?>;

public class ObterDocumentoQueryValidator : AbstractValidator<ObterDocumentoQuery>
{
    public ObterDocumentoQueryValidator()
    {
        RuleFor(x => x.EntidadeTipo).NotEmpty();
        RuleFor(x => x.EntidadeId).NotEmpty();
    }
}

public class ObterDocumentoQueryHandler : IRequestHandler<ObterDocumentoQuery, DocumentoAssinaturaDto?>
{
    private readonly IAppDbContext _db;

    public ObterDocumentoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<DocumentoAssinaturaDto?> Handle(ObterDocumentoQuery request, CancellationToken ct)
    {
        var documento = await _db.DocumentosAssinatura
            .Where(d => d.EntidadeTipo == request.EntidadeTipo && d.EntidadeId == request.EntidadeId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
        if (documento is null)
            return null;

        var signatarios = await _db.DocumentoSignatarios
            .Where(s => s.DocumentoAssinaturaId == documento.Id)
            .Join(_db.Trabalhadores, s => s.TrabalhadorId, t => t.Id,
                (s, t) => new DocumentoSignatarioDto(t.Id, t.Nome, s.MetodoAutenticacao, s.AssinadoEm))
            .OrderBy(s => s.AssinadoEm)
            .ToListAsync(ct);

        return new DocumentoAssinaturaDto(
            documento.Id, documento.EntidadeTipo, documento.EntidadeId, documento.Status, signatarios,
            documento.ConteudoHash, documento.FinalizadoEm, documento.PdfConteudo != null, documento.TokenValidacaoPublica);
    }
}
