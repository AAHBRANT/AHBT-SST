namespace AAHBRANT.SST.AgenteCameras.Quadros;

// Separa JPEGs concatenados (saída "image2pipe" do ffmpeg) pelos marcadores SOI (FFD8) e EOI (FFD9).
// Guarda o resto entre leituras: um quadro cortado no meio, ou um marcador partido entre dois blocos, sai
// completo na leitura seguinte. Dentro dos dados comprimidos o byte FF é sempre seguido de 00 (byte stuffing),
// então FFD9 só aparece no fim do quadro (o mjpeg do ffmpeg não grava miniatura EXIF, que teria SOI/EOI próprios).
public sealed class SeparadorJpeg
{
    public const int TamanhoMaximoQuadro = 16 * 1024 * 1024;

    private byte[] _buffer = new byte[256 * 1024];
    private int _tamanho;

    /// <summary>Quantidade de bytes descartados por lixo antes de um SOI ou por quadro maior que o limite.</summary>
    public long BytesDescartados { get; private set; }

    public IReadOnlyList<byte[]> Adicionar(ReadOnlySpan<byte> dados)
    {
        Garantir(_tamanho + dados.Length);
        dados.CopyTo(_buffer.AsSpan(_tamanho));
        _tamanho += dados.Length;

        var quadros = new List<byte[]>();
        var pos = 0;
        while (true)
        {
            var soi = Procurar(0xD8, pos);
            if (soi < 0)
            {
                // Sem início de quadro: lixo. Preserva só um FF final (pode ser a metade de um SOI).
                var guardar = _tamanho > pos && _buffer[_tamanho - 1] == 0xFF ? 1 : 0;
                BytesDescartados += _tamanho - pos - guardar;
                pos = _tamanho - guardar;
                break;
            }

            BytesDescartados += soi - pos;
            var eoi = Procurar(0xD9, soi + 2);
            if (eoi < 0)
            {
                pos = soi;
                break;
            }

            quadros.Add(_buffer.AsSpan(soi, eoi + 2 - soi).ToArray());
            pos = eoi + 2;
        }

        if (pos > 0)
        {
            Buffer.BlockCopy(_buffer, pos, _buffer, 0, _tamanho - pos);
            _tamanho -= pos;
        }

        if (_tamanho > TamanhoMaximoQuadro)
        {
            // Quadro que não termina nunca (fluxo corrompido): recomeça do zero em vez de crescer sem limite.
            BytesDescartados += _tamanho;
            _tamanho = 0;
        }

        return quadros;
    }

    public void Reiniciar() => _tamanho = 0;

    private int Procurar(byte segundo, int de)
    {
        for (var i = de; i < _tamanho - 1; i++)
            if (_buffer[i] == 0xFF && _buffer[i + 1] == segundo) return i;
        return -1;
    }

    private void Garantir(int capacidade)
    {
        if (capacidade <= _buffer.Length) return;
        var nova = Math.Max(capacidade, _buffer.Length * 2);
        Array.Resize(ref _buffer, nova);
    }
}
