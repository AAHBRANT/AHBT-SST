using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura;

// Biometria cadastrada vigente na data de cada assinatura (05/11), a mesma regra da Ficha de EPI
// (ExportarFichaEpiTrabalhadorQuery): facial → foto de cadastro mais recente até a assinatura; digital →
// imagem do template mais recente até a assinatura (descriptografada). Inclui cadastros arquivados.
// Usada no Certificado de Assinaturas de qualquer documento.
public sealed class ReferenciaBiometricaVigente
{
    private readonly Dictionary<Guid, List<(DateTime Em, byte[] Conteudo, string Hash)>> _fotos;
    private readonly Dictionary<Guid, List<(DateTime Em, string? Cifrada)>> _digitais;
    private readonly IImagemBiometricaCriptografia? _cripto;

    private ReferenciaBiometricaVigente(
        Dictionary<Guid, List<(DateTime, byte[], string)>> fotos,
        Dictionary<Guid, List<(DateTime, string?)>> digitais,
        IImagemBiometricaCriptografia? cripto)
    {
        _fotos = fotos;
        _digitais = digitais;
        _cripto = cripto;
    }

    public static async Task<ReferenciaBiometricaVigente> CarregarAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> trabalhadorIds, IImagemBiometricaCriptografia? cripto, CancellationToken ct)
    {
        var fotos = (await db.FotosCadastroFacial.AsNoTracking().IgnoreQueryFilters()
                .Where(f => trabalhadorIds.Contains(f.TrabalhadorId))
                .Select(f => new { f.TrabalhadorId, f.CapturadaEm, f.Conteudo, f.HashSha256 })
                .ToListAsync(ct))
            .GroupBy(f => f.TrabalhadorId)
            .ToDictionary(g => g.Key, g => g.OrderBy(f => f.CapturadaEm).Select(f => (f.CapturadaEm, f.Conteudo, f.HashSha256)).ToList());
        var digitais = (await db.TemplatesBiometricoFutronic.AsNoTracking().IgnoreQueryFilters()
                .Where(t => trabalhadorIds.Contains(t.TrabalhadorId))
                .Select(t => new { t.TrabalhadorId, t.CapturadoEm, t.ImagemCadastroCriptografada })
                .ToListAsync(ct))
            .GroupBy(t => t.TrabalhadorId)
            .ToDictionary(g => g.Key, g => g.OrderBy(t => t.CapturadoEm).Select(t => (t.CapturadoEm, t.ImagemCadastroCriptografada)).ToList());
        return new ReferenciaBiometricaVigente(fotos, digitais, cripto);
    }

    public (byte[]? Foto, DateTime? FotoEm, string? FotoHash, byte[]? Digital, DateTime? DigitalEm) Para(
        Guid trabalhadorId, MetodoAutenticacaoAssinatura metodo, DateTime assinadoEmUtc)
    {
        if (metodo == MetodoAutenticacaoAssinatura.ReconhecimentoFacial
            && _fotos.TryGetValue(trabalhadorId, out var fotos)
            && fotos.LastOrDefault(f => f.Em <= assinadoEmUtc) is { Conteudo: not null } foto)
            return (foto.Conteudo, foto.Em, foto.Hash, null, null);

        if (metodo == MetodoAutenticacaoAssinatura.Biometria
            && _digitais.TryGetValue(trabalhadorId, out var digitais)
            && digitais.Where(d => d.Em <= assinadoEmUtc).Select(d => ((DateTime, string?)?)d).LastOrDefault() is { } digital)
        {
            byte[]? imagem = null;
            if (!string.IsNullOrEmpty(digital.Item2) && _cripto is not null)
            {
                try { imagem = _cripto.Descriptografar(digital.Item2); }
                catch { imagem = null; }
            }
            return (null, null, null, imagem, digital.Item1);
        }

        return (null, null, null, null, null);
    }
}
