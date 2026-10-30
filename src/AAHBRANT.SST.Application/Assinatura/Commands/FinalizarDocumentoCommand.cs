using AAHBRANT.SST.Application.Assinatura.Queries;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.GeracaoPgr;
using AAHBRANT.SST.Application.Inspecoes.Queries;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura.Commands;

// Fecha o documento para novas assinaturas e grava, num único evento, o hash de integridade do
// conteúdo (etapa 8), o comprovante em PDF (etapa 9) e o token/QR de validação pública (etapa 10 —
// docs/Motor-Assinatura-Eletronica.md §5), porque a finalização é um evento único no ciclo de vida do
// documento (ver comentário em DocumentoAssinatura.cs: "Preenchidos só na finalização").
// Disparado pelo botão "Finalizar documento" da tela de assinatura e automaticamente nos fins naturais
// de cada fluxo (ver FinalizacaoAutomaticaDocumento).
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
    private readonly IMediator _mediator;
    private readonly IDocumentoAssinaturaPdfService _pdf;
    private readonly IQrCodeDocumentoService _qrCode;

    private readonly IClienteIpProvider? _cliente;

    public FinalizarDocumentoCommandHandler(IAppDbContext db, IAuditoriaService auditoria, IMediator mediator, IDocumentoAssinaturaPdfService pdf, IQrCodeDocumentoService qrCode,
        IClienteIpProvider? cliente = null)
    {
        _cliente = cliente;
        _db = db;
        _auditoria = auditoria;
        _mediator = mediator;
        _pdf = pdf;
        _qrCode = qrCode;
    }

    public async Task<DocumentoAssinaturaDto> Handle(FinalizarDocumentoCommand request, CancellationToken ct)
    {
        var documento = await _db.DocumentosAssinatura.FirstOrDefaultAsync(d => d.Id == request.DocumentoAssinaturaId, ct);
        if (documento is null
            || !await EscopoDocumentoAssinatura.PodeVerAsync(_db, documento.EntidadeTipo, documento.EntidadeId, ct))
            throw new KeyNotFoundException("Documento de assinatura não encontrado.");
        if (documento.Status != StatusDocumentoAssinatura.EmAndamento)
            throw new InvalidOperationException("Este documento já foi finalizado ou cancelado.");

        // IgnoreQueryFilters nos trabalhadores: quem assinou e depois foi desligado ou transferido de obra
        // continua sendo signatário — sem isso ele sumia do comprovante e do hash.
        // Ordena antes de projetar: OrderBy sobre o construtor do DTO não vira SQL ("The LINQ expression ...
        // could not be translated") e fazia toda finalização falhar — em hml nenhum documento tinha sido
        // finalizado até 30/10 (inspeção do Gilton ficou "Aguardando assinatura" no PDF).
        var signatarios = await _db.DocumentoSignatarios
            .Where(s => s.DocumentoAssinaturaId == documento.Id)
            .OrderBy(s => s.AssinadoEm)
            .Join(_db.Trabalhadores.IgnoreQueryFilters(), s => s.TrabalhadorId, t => t.Id,
                (s, t) => new { t.Id, t.Nome, s.MetodoAutenticacao, s.AssinadoEm, s.IpAddress })
            .ToListAsync(ct);
        var signatariosDto = signatarios
            .OrderBy(s => s.AssinadoEm)
            .Select(s => new DocumentoSignatarioDto(s.Id, s.Nome, s.MetodoAutenticacao, s.AssinadoEm, s.IpAddress))
            .ToList();

        if (signatarios.Count == 0)
            throw new InvalidOperationException("Documento sem nenhuma assinatura não pode ser finalizado.");

        var hash = HashConteudoDocumentoCalculador.Calcular(documento.EntidadeTipo, documento.EntidadeId, signatariosDto);
        // Mantém o token se o documento já tinha sido exportado (rodapé com QR): trocar invalidaria o QR já
        // impresso nos PDFs anteriores.
        var token = documento.TokenValidacaoPublica ?? TokenValidacaoPublicaGerador.Gerar();
        var qrCode = _qrCode.Gerar(token);

        documento.Status = StatusDocumentoAssinatura.Finalizado;
        documento.FinalizadoEm = DateTime.UtcNow;
        documento.ConteudoHash = hash;
        documento.TokenValidacaoPublica = token;
        // O comprovante é sempre gerado agora, com todas as assinaturas: antes, um PDF exportado no meio do
        // caminho (sem as últimas assinaturas) ficava como "prova congelada". O SHA-256 do arquivo também é
        // gravado, para quem baixar o PDF pela página pública conferir que é o mesmo.
        if (documento.EntidadeTipo == "Inspecao")
        {
            await _db.SaveChangesAsync(ct);
            documento.PdfConteudo = await _mediator.Send(new ExportarInspecaoPdfQuery(documento.EntidadeId), ct);
        }
        else if (documento.EntidadeTipo == AssinaturaGeracaoPgr.EntidadeTipo)
        {
            // PGR: o arquivo guardado é o próprio PGR, com os carimbos das assinaturas, o QR e o código de verificação.
            await _db.SaveChangesAsync(ct);
            documento.PdfConteudo = (await _mediator.Send(new GerarPdfGeracaoPgrEapQuery(documento.EntidadeId), ct)).Conteudo;
        }
        else
        {
            documento.PdfConteudo = _pdf.Gerar(new DocumentoAssinaturaPdfModelo(
                documento.Id, documento.EntidadeTipo, documento.EntidadeId, documento.FinalizadoEm.Value, hash,
                signatariosDto.Select(s => new DocumentoAssinaturaPdfSignatarioModelo(s.TrabalhadorNome, s.MetodoAutenticacao, s.AssinadoEm)).ToList(),
                qrCode.Png, qrCode.UrlValidacao));
        }
        documento.HashPdf = HashArquivoCalculador.Calcular(documento.PdfConteudo);
        documento.ArquivoAtualizadoEm = documento.FinalizadoEm;

        // Quem finalizou: o usuário logado (botão "Finalizar documento" ou quem encerrou o DDS/a turma).
        var oid = _cliente?.ObterAzureAdObjectId();
        var usuarioId = oid is null ? null
            : await _db.Usuarios.Where(u => u.AzureAdObjectId == oid).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);
        await _auditoria.RegistrarAsync(
            "Documento.Finalizado",
            documento.EntidadeTipo,
            documento.EntidadeId,
            usuarioId: usuarioId,
            trabalhadorId: null,
            dadosDepois: new { DocumentoAssinaturaId = documento.Id, ConteudoHash = hash, QuantidadeSignatarios = signatarios.Count },
            ct);

        await _db.SaveChangesAsync(ct);

        return new DocumentoAssinaturaDto(
            documento.Id, documento.EntidadeTipo, documento.EntidadeId, documento.Status, signatariosDto,
            documento.ConteudoHash, documento.FinalizadoEm, TemPdf: true, TokenValidacaoPublica: token);
    }
}
