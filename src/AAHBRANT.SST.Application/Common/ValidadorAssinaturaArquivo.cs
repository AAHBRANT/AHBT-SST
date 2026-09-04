namespace AAHBRANT.SST.Application.Common;

// O Content-Type de um upload é só um rótulo que o próprio navegador manda junto do arquivo no
// multipart — falsificável por quem monta a requisição na mão (não é uma checagem de UI). Esta
// classe confere os primeiros bytes do arquivo (assinatura/"magic number") contra o tipo declarado,
// usada como regra adicional nos validators de todo endpoint de upload do sistema.
public static class ValidadorAssinaturaArquivo
{
    public static bool AssinaturaConfere(byte[] conteudo, string contentTypeDeclarado) => contentTypeDeclarado switch
    {
        "image/jpeg" => TemPrefixo(conteudo, 0xFF, 0xD8, 0xFF),
        "image/png" => TemPrefixo(conteudo, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A),
        "application/pdf" => TemPrefixo(conteudo, 0x25, 0x50, 0x44, 0x46), // "%PDF"
        _ => false,
    };

    private static bool TemPrefixo(byte[] conteudo, params byte[] assinatura)
        => conteudo.Length >= assinatura.Length && assinatura.AsSpan().SequenceEqual(conteudo.AsSpan(0, assinatura.Length));
}
