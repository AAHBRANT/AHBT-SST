using AAHBRANT.SST.Application.Assinatura.Queries;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura;

public record RastreabilidadeDocumentoResultado(string ConteudoHash, string UrlValidacaoPublica, byte[] QrCodePng, bool TemAssinatura);

// Rastreabilidade (hash+token+QR) desacoplada de finalização — deliberadamente NÃO usa
// FinalizarDocumentoCommand (ver docs/superpowers/specs/2026-09-04-rodape-validacao-documentos-design.md
// §3): esse comando fecha o documento para novas assinaturas, o que travaria um DDS/APR/PT ainda em
// assinatura só por ter sido exportado em PDF uma vez. Aqui, hash/token nunca mexem em Status/FinalizadoEm.
public interface IRegistradorRastreabilidadeService
{
    Task<RastreabilidadeDocumentoResultado> GarantirAsync(string entidadeTipo, Guid entidadeId, CancellationToken ct);
}

public class RegistradorRastreabilidadeService : IRegistradorRastreabilidadeService
{
    private readonly IAppDbContext _db;
    private readonly IQrCodeDocumentoService _qrCode;

    public RegistradorRastreabilidadeService(IAppDbContext db, IQrCodeDocumentoService qrCode)
    {
        _db = db;
        _qrCode = qrCode;
    }

    public async Task<RastreabilidadeDocumentoResultado> GarantirAsync(string entidadeTipo, Guid entidadeId, CancellationToken ct)
    {
        var documento = await _db.DocumentosAssinatura
            .Include(d => d.Signatarios)
            .FirstOrDefaultAsync(d => d.EntidadeTipo == entidadeTipo && d.EntidadeId == entidadeId, ct);

        if (documento is null)
        {
            documento = new DocumentoAssinatura { EntidadeTipo = entidadeTipo, EntidadeId = entidadeId };
            _db.DocumentosAssinatura.Add(documento);
        }

        if (documento.TokenValidacaoPublica is null)
        {
            documento.TokenValidacaoPublica = TokenValidacaoPublicaGerador.Gerar();
            documento.RastreadoEm = DateTime.UtcNow;
        }

        // Congelado a partir da finalização real (FinalizarDocumentoCommand) — aqui só recalcula
        // enquanto o documento ainda está aceitando assinaturas, para o hash sempre refletir os
        // signatários atuais (inclusive zero) em cada novo export do PDF.
        if (documento.Status != StatusDocumentoAssinatura.Finalizado)
        {
            var signatariosParaHash = documento.Signatarios
                .Select(s => new DocumentoSignatarioDto(s.TrabalhadorId, string.Empty, s.MetodoAutenticacao, s.AssinadoEm))
                .ToList();
            documento.ConteudoHash = HashConteudoDocumentoCalculador.Calcular(entidadeTipo, entidadeId, signatariosParaHash);
        }

        await _db.SaveChangesAsync(ct);

        var qr = _qrCode.Gerar(documento.TokenValidacaoPublica);
        return new RastreabilidadeDocumentoResultado(documento.ConteudoHash!, qr.UrlValidacao, qr.Png, documento.Signatarios.Count > 0);
    }
}
