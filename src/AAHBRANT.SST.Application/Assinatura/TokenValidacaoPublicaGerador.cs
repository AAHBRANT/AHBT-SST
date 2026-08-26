using System.Security.Cryptography;

namespace AAHBRANT.SST.Application.Assinatura;

public static class TokenValidacaoPublicaGerador
{
    // 32 chars hex (16 bytes aleatórios) — cabe em TokenValidacaoPublica (nvarchar(64), único). Aleatório
    // via CSPRNG (não é segredo derivado de chave, então é seguro gerar direto no Application, mesmo
    // raciocínio de HashConteudoDocumentoCalculador). Hex sem hífen fica mais curto e mais limpo numa URL
    // pública (/sst/validar/{token}) do que o formato com hífens de Guid.NewGuid().
    public static string Gerar() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
}
