using System.Security.Cryptography;

namespace AAHBRANT.SST.Application.Assinatura;

// Integridade do arquivo emitido, complementar a HashConteudoDocumentoCalculador (que cobre só a
// lista de signatários — alterar o conteúdo do documento não mudava aquele hash, então o QR validava
// igual antes e depois da adulteração).
//
// SHA-256 puro sobre os bytes do PDF: qualquer alteração de um único byte muda o hash. A conferência
// por terceiro não depende do sistema — basta baixar o PDF pela página pública e rodar um SHA-256
// (`certutil -hashfile arquivo.pdf SHA256` no Windows, `sha256sum` no Linux) e comparar.
public static class HashArquivoCalculador
{
    public static string Calcular(byte[] arquivo) =>
        Convert.ToHexString(SHA256.HashData(arquivo));
}
