using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AAHBRANT.SST.Application.Assinatura;

// Leitura de digital assinada pelo agente do leitor (ver DispositivoAgenteBiometrico.ChavePublicaAssinatura).
// Desafio: o id do DesafioBiometria emitido pelo servidor. Assinatura: ECDSA P-256/SHA-256 (formato
// IEEE P1363, base64) sobre Conteudo(desafio, trabalhadorId, score).
public record LeituraBiometricaAssinada(string Desafio, string Assinatura)
{
    // Texto exato que o agente assina. Precisa ser idêntico ao de AgenteEndpoints.ConteudoAssinado (projeto
    // do agente): qualquer diferença (separador, casas decimais, formato do Guid) faz toda leitura falhar.
    public static string Conteudo(string desafio, Guid trabalhadorId, double score) =>
        string.Join('|', desafio, trabalhadorId.ToString("D"), score.ToString("F2", CultureInfo.InvariantCulture));

    public bool Verificar(string chavePublicaBase64, Guid trabalhadorId, double score)
    {
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(chavePublicaBase64), out _);
            return ecdsa.VerifyData(
                Encoding.UTF8.GetBytes(Conteudo(Desafio, trabalhadorId, score)),
                Convert.FromBase64String(Assinatura),
                HashAlgorithmName.SHA256);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
