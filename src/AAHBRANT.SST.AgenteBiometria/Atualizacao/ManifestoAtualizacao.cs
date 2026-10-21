using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace AAHBRANT.SST.AgenteBiometria.Atualizacao;

// Manifesto publicado junto com cada Setup do agente. Assinatura = ECDSA P-256 (SHA-256) sobre
// ConteudoAssinado, feita com a chave privada que só quem publica tem (ver scripts/agente-biometria/publicar.ps1).
// O agente só baixa e roda um Setup cujo manifesto bate com a chave pública embutida nele: quem conseguir
// trocar o arquivo no servidor de atualização não consegue fazer o posto rodar outro executável.
public record ManifestoAtualizacao(string Versao, string UrlSetup, string Sha256, string Assinatura);

public static class VerificadorAtualizacao
{
    public const string RecursoChavePublica = "AAHBRANT.SST.AgenteBiometria.Atualizacao.chave-publica-atualizacao.pem";

    public static string ConteudoAssinado(string versao, string urlSetup, string sha256) =>
        string.Join('|', versao, urlSetup, sha256.ToLowerInvariant());

    // Chave pública gravada no executável na hora do build. Null = build sem chave (atualização desligada).
    public static string? ChavePublicaEmbutida()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(RecursoChavePublica);
        if (stream is null) return null;
        using var leitor = new StreamReader(stream);
        var pem = leitor.ReadToEnd();
        return string.IsNullOrWhiteSpace(pem) ? null : pem;
    }

    public static bool AssinaturaValida(ManifestoAtualizacao manifesto, string chavePublicaPem)
    {
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(chavePublicaPem);
            var dados = Encoding.UTF8.GetBytes(ConteudoAssinado(manifesto.Versao, manifesto.UrlSetup, manifesto.Sha256));
            return ecdsa.VerifyData(dados, Convert.FromBase64String(manifesto.Assinatura), HashAlgorithmName.SHA256);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException)
        {
            return false;
        }
    }

    public static bool EhMaisNova(string versaoManifesto, Version atual) =>
        Version.TryParse(versaoManifesto, out var nova) && Normalizar(nova) > Normalizar(atual);

    public static bool Sha256Confere(byte[] arquivo, string sha256Esperado) =>
        string.Equals(Convert.ToHexString(SHA256.HashData(arquivo)), sha256Esperado, StringComparison.OrdinalIgnoreCase);

    // Version("1.2") tem Build = -1: normaliza para comparar 1.2 com 1.2.0.0 sem achar que é diferente.
    private static Version Normalizar(Version v) =>
        new(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));

    public static Version VersaoAtual() =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);
}
