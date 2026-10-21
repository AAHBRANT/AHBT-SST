using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;

namespace AAHBRANT.SST.Infrastructure.Imagens;

// Porta única para abrir imagem com ImageSharp (auditoria 19/10, A13). As versões em uso têm avisos de
// segurança nos decodificadores (TIFF, BMP, imagens com dimensões absurdas que estouram a memória).
// Aqui:
// - só JPEG, PNG e WebP são reconhecidos: os outros decodificadores nem ficam registrados;
// - o cabeçalho é lido primeiro (Identify, sem decodificar os pixels) e a imagem é recusada se passar
//   do limite de tamanho, antes de alocar memória;
// - só o primeiro quadro é decodificado (PNG/WebP animado).
// O app já reduz as fotos para no máximo 1600 px antes de enviar, então o limite tem folga grande.
public static class ImagemSegura
{
    public const int LadoMaximoPx = 8000;
    public const long PixelsMaximos = 40_000_000;

    private static readonly Configuration Configuracao = new(
        new JpegConfigurationModule(), new PngConfigurationModule(), new WebpConfigurationModule());

    private static readonly DecoderOptions Opcoes = new() { Configuration = Configuracao, MaxFrames = 1 };

    public static Image Carregar(byte[] bytes)
    {
        Validar(bytes);
        return Image.Load(Opcoes, bytes);
    }

    // Lança InvalidOperationException com mensagem para o usuário quando a imagem não é aceita.
    public static void Validar(byte[] bytes)
    {
        ImageInfo info;
        try
        {
            info = Image.Identify(Opcoes, bytes);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
        {
            throw new InvalidOperationException("Formato de imagem não suportado. Envie uma foto JPEG ou PNG.", ex);
        }

        if (info.Width <= 0 || info.Height <= 0
            || info.Width > LadoMaximoPx || info.Height > LadoMaximoPx
            || (long)info.Width * info.Height > PixelsMaximos)
            throw new InvalidOperationException(
                $"Imagem grande demais ({info.Width}x{info.Height}). O limite é {LadoMaximoPx} pixels por lado.");
    }

    // Para checagens "é uma imagem que dá para usar?" que não devem lançar.
    public static bool EhValida(byte[] bytes)
    {
        try
        {
            Validar(bytes);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
