using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AAHBRANT.SST.AgenteBiometria.Atualizacao;

namespace AAHBRANT.SST.AgenteBiometria.Instalador;

// Usado só por quem publica (publicar.ps1), nunca no posto: gera o par de chaves e assina o manifesto.
// A chave privada fica fora do git e fora dos postos; a pública vai embutida no agente.
public static class AssinadorManifesto
{
    public static void GerarChave(string arquivoPrivada, string arquivoPublica)
    {
        if (File.Exists(arquivoPrivada))
            throw new InvalidOperationException($"Já existe uma chave privada em {arquivoPrivada}. Não sobrescrevo: os postos instalados só aceitam a chave atual.");
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        File.WriteAllText(arquivoPrivada, ecdsa.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(arquivoPublica, ecdsa.ExportSubjectPublicKeyInfoPem());
    }

    public static ManifestoAtualizacao Assinar(string chavePrivadaPem, string versao, string urlSetup, byte[] setup)
    {
        var sha = Convert.ToHexString(SHA256.HashData(setup)).ToLowerInvariant();
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(chavePrivadaPem);
        var assinatura = ecdsa.SignData(Encoding.UTF8.GetBytes(VerificadorAtualizacao.ConteudoAssinado(versao, urlSetup, sha)), HashAlgorithmName.SHA256);
        return new ManifestoAtualizacao(versao, urlSetup, sha, Convert.ToBase64String(assinatura));
    }

    public static string ParaJson(ManifestoAtualizacao manifesto) =>
        JsonSerializer.Serialize(manifesto, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
}
