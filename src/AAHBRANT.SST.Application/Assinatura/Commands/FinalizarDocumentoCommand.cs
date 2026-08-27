using AAHBRANT.SST.Application.Assinatura.Queries;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura.Commands;

// Fecha o documento para novas assinaturas e grava, num único evento, o hash de integridade do
// conteúdo (etapa 8), o comprovante em PDF (etapa 9) e o token/QR de validação pública (etapa 10 —
// docs/Motor-Assinatura-Eletronica.md §5), porque a finalização é um evento único no ciclo de vida do
// documento (ver comentário em DocumentoAssinatura.cs: "Preenchidos só na finalização"). A página
// pública que resolve o token (/#/validar/{token}) ainda não existe — fica para a etapa 11.
public record FinalizarDocumentoCommand(Guid DocumentoAssinaturaId) : IRequest<DocumentoAssinaturaDto>;

public class FinalizarDocumentoCommandValidator : AbstractValidator<FinalizarDocumentoCommand>
{
    public FinalizarDocumentoCommandValidator()
    {
        RuleFor(x => x.DocumentoAssinaturaId).NotEmpty();
    }
}

public class FinalizarDocumentoCommandHandler : IRequestHandler<FinalizarDocumentoCommand, DocumentoAssinaturaDto>
{
    private readonly IAppDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly IDocumentoAssinaturaPdfService _pdf;
    private readonly IQrCodeDocumentoService _qrCode;

    public FinalizarDocumentoCommandHandler(IAppDbContext db, IAuditoriaService auditoria, IDocumentoAssinaturaPdfService pdf, IQrCodeDocumentoService qrCode)
    {
        _db = db;
        _auditoria = auditoria;
        _pdf = pdf;
        _qrCode = qrCode;
    }

    public async Task<DocumentoAssinaturaDto> Handle(FinalizarDocumentoCommand request, CancellationToken ct)
    {
        var documento = await _db.DocumentosAssinatura.FirstOrDefaultAsync(d => d.Id == request.DocumentoAssinaturaId, ct);
        if (documento is null)
            throw new KeyNotFoundException("Documento de assinatura não encontrado.");
        if (documento.Status != StatusDocumentoAssinatura.EmAndamento)
            throw new InvalidOperationException("Este documento já foi finalizado ou cancelado.");

        var signatarios = await _db.DocumentoSignatarios
            .Where(s => s.DocumentoAssinaturaId == documento.Id)
            .Join(_db.Trabalhadores, s => s.TrabalhadorId, t => t.Id,
                (s, t) => new DocumentoSignatarioDto(t.Id, t.Nome, s.MetodoAutenticacao, s.AssinadoEm, s.IpAddress))
            .OrderBy(s => s.AssinadoEm)
            .ToListAsync(ct);

        if (signatarios.Count == 0)
            throw new InvalidOperationException("Documento sem nenhuma assinatura não pode ser finalizado.");

        var hash = HashConteudoDocumentoCalculador.Calcular(documento.EntidadeTipo, documento.EntidadeId, signatarios);
        var token = TokenValidacaoPublicaGerador.Gerar();
        var qrCode = _qrCode.Gerar(token);

        documento.Status = StatusDocumentoAssinatura.Finalizado;
        documento.FinalizadoEm = DateTime.UtcNow;
        documento.ConteudoHash = hash;
        documento.TokenValidacaoPublica = token;
        documento.PdfConteudo = _pdf.Gerar(new DocumentoAssinaturaPdfModelo(
            documento.Id, documento.EntidadeTipo, documento.EntidadeId, documento.FinalizadoEm.Value, hash,
            signatarios.Select(s => new DocumentoAssinaturaPdfSignatarioModelo(s.TrabalhadorNome, s.MetodoAutenticacao, s.AssinadoEm)).ToList(),
            qrCode.Png, qrCode.UrlValidacao));

        // usuarioId/trabalhadorId nulos: não há ICurrentUserService no projeto hoje para capturar quem
        // disparou a finalização (ver docs/Motor-Assinatura-Eletronica.md — nota da etapa 8); quando o
        // botão de finalizar existir no frontend, reavaliar se vale adicionar.
        await _auditoria.RegistrarAsync(
            "Documento.Finalizado",
            documento.EntidadeTipo,
            documento.EntidadeId,
            usuarioId: null,
            trabalhadorId: null,
            dadosDepois: new { DocumentoAssinaturaId = documento.Id, ConteudoHash = hash, QuantidadeSignatarios = signatarios.Count },
            ct);

        await _db.SaveChangesAsync(ct);

        return new DocumentoAssinaturaDto(
            documento.Id, documento.EntidadeTipo, documento.EntidadeId, documento.Status, signatarios,
            documento.ConteudoHash, documento.FinalizadoEm, TemPdf: true, TokenValidacaoPublica: token);
    }
}
