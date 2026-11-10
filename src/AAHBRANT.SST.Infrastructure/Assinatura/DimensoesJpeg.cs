namespace AAHBRANT.SST.Infrastructure.Assinatura;

// Largura e altura de um JPEG lidas direto do cabeçalho (marcador SOF), sem decodificar a imagem.
// Usado para converter a posição do rosto devolvida pelo Azure (pixels) em fração da imagem.
public static class DimensoesJpeg
{
    public static (int Largura, int Altura)? Ler(byte[] jpeg)
    {
        if (jpeg.Length < 4 || jpeg[0] != 0xFF || jpeg[1] != 0xD8) return null;

        var i = 2;
        while (i + 3 < jpeg.Length)
        {
            if (jpeg[i] != 0xFF) return null;
            var marcador = jpeg[i + 1];
            // Bytes de preenchimento (0xFF repetido) antes do marcador.
            if (marcador == 0xFF) { i++; continue; }
            // Marcadores sem segmento de dados.
            if (marcador == 0x01 || (marcador >= 0xD0 && marcador <= 0xD8)) { i += 2; continue; }
            // Fim da imagem ou início dos dados comprimidos: não há mais cabeçalho para ler.
            if (marcador == 0xD9 || marcador == 0xDA) return null;

            var tamanho = (jpeg[i + 2] << 8) | jpeg[i + 3];
            if (tamanho < 2) return null;

            // SOF0 a SOF15, exceto DHT (C4), JPG (C8) e DAC (CC): precisão, altura e largura.
            var ehSof = marcador >= 0xC0 && marcador <= 0xCF && marcador != 0xC4 && marcador != 0xC8 && marcador != 0xCC;
            if (ehSof)
            {
                if (i + 8 >= jpeg.Length) return null;
                var altura = (jpeg[i + 5] << 8) | jpeg[i + 6];
                var largura = (jpeg[i + 7] << 8) | jpeg[i + 8];
                return largura > 0 && altura > 0 ? (largura, altura) : null;
            }

            i += 2 + tamanho;
        }
        return null;
    }
}
