using AAHBRANT.SST.Application.Cbo;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura;

// Monta o Certificado de Assinaturas Eletrônicas (05/11, modelo único) a partir das assinaturas
// gravadas de um documento com lista de presença (DDS, APR, PT, CIPA...): uma evidência por assinatura,
// com a biometria cadastrada vigente na data ao lado da capturada e o QR de validação do documento.
public static class MontadorCertificadoAssinaturas
{
    // Signatário pronto para o selo/certificado: nome, "Função · CBO · Mat." e os dados do aceite.
    public sealed record DadosSignatario(string Nome, string? Funcao, string? Matricula, DateTime? TermoAceite, DateTime? Consentimento)
    {
        public string Detalhe => $"{Funcao ?? "—"} · Mat. {Matricula ?? "—"}";
    }

    public static string? FuncaoComCbo(string? nome, string? cbo) => nome is null ? null
        : string.IsNullOrWhiteSpace(cbo) ? nome : $"{nome} · CBO {CodigoCbo.Formatar(cbo)}";

    // Código curto do selo: SHA-256 da evidência (foto/digital) quando houver; senão o id da assinatura.
    public static string CodigoSelo(DocumentoSignatario s) =>
        !string.IsNullOrWhiteSpace(s.FotoEvidenciaHash) ? s.FotoEvidenciaHash! : s.Id.ToString("N").ToUpperInvariant();

    public static async Task<Dictionary<Guid, DadosSignatario>> CarregarSignatariosAsync(
        IAppDbContext db, IEnumerable<Guid> trabalhadorIds, CancellationToken ct)
    {
        var ids = trabalhadorIds.Distinct().ToList();
        var lista = await db.Trabalhadores.AsNoTracking().IgnoreQueryFilters()
            .Where(t => ids.Contains(t.Id))
            .Select(t => new
            {
                t.Id, t.Nome, t.Matricula,
                Funcao = t.Funcao != null ? t.Funcao.Nome : null,
                Cbo = t.Funcao != null ? t.Funcao.CboCodigo : null,
                t.TermoAceiteAssinaturaEletronicaEm, t.ConsentimentoBiometriaEm,
            })
            .ToListAsync(ct);
        return lista.ToDictionary(t => t.Id, t => new DadosSignatario(t.Nome, FuncaoComCbo(t.Funcao, t.Cbo), t.Matricula,
            t.TermoAceiteAssinaturaEletronicaEm, t.ConsentimentoBiometriaEm));
    }

    public static async Task<CertificadoAssinaturasModelo?> MontarAsync(
        IAppDbContext db,
        IImagemBiometricaCriptografia? cripto,
        IQrCodeDocumentoService? qrCode,
        DocumentoAssinatura documento,
        IReadOnlyList<DocumentoSignatario> assinaturas,
        IReadOnlyDictionary<Guid, DadosSignatario> signatarios,
        Func<DocumentoSignatario, string> descricao,
        string nomeDocumento,
        string subtitulo,
        string obraNome,
        byte[]? obraLogo,
        RastreabilidadeDocumentoResultado rastreio,
        PainelCertificado? painelGeral,
        bool compacto,
        CancellationToken ct)
    {
        if (assinaturas.Count == 0) return null;

        var referencias = await ReferenciaBiometricaVigente.CarregarAsync(db, assinaturas.Select(s => s.TrabalhadorId).Distinct().ToList(), cripto, ct);
        byte[]? qr = null;
        if (qrCode is not null && !string.IsNullOrWhiteSpace(documento.TokenValidacaoPublica))
        {
            try { qr = qrCode.Gerar(documento.TokenValidacaoPublica!).Png; }
            catch { qr = null; }
        }

        var itens = assinaturas.OrderBy(s => s.AssinadoEm).Select(s =>
        {
            var refs = referencias.Para(s.TrabalhadorId, s.MetodoAutenticacao, s.AssinadoEm);
            signatarios.TryGetValue(s.TrabalhadorId, out var dados);
            var evidencia = new EvidenciaAssinaturaPdf(
                s.MetodoAutenticacao, s.AssinadoEm, documento.CreatedAtUtc, s.IpAddress, s.UserAgent, s.LocalizacaoStatus,
                s.Latitude, s.Longitude, s.PrecisaoMetros, s.ValidacaoModelo, s.ValidacaoGrupoId, s.ValidacaoRequisicaoId,
                s.DispositivoAgenteId, s.FotoEvidenciaConteudo, s.FotoEvidenciaHash,
                refs.Foto, refs.FotoEm, refs.FotoHash, refs.Digital, refs.DigitalEm, qr);
            return new CertificadoAssinaturaItem(descricao(s), dados?.Nome ?? "—", dados?.Detalhe,
                dados?.TermoAceite, dados?.Consentimento, evidencia);
        }).ToList();

        return new CertificadoAssinaturasModelo(
            nomeDocumento, subtitulo, rastreio.DocumentoId.ToString(), DateTime.UtcNow, $"Sistema SST · {obraNome}",
            rastreio.ConteudoHash, documento.Status != StatusDocumentoAssinatura.Finalizado, itens,
            obraNome, obraLogo, rastreio.UrlValidacaoPublica, rastreio.QrCodePng, compacto, painelGeral);
    }
}
