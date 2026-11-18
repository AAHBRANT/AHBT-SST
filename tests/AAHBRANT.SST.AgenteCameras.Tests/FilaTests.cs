using AAHBRANT.SST.AgenteCameras.Fila;
using AAHBRANT.SST.AgenteCameras.Seguranca;

namespace AAHBRANT.SST.AgenteCameras.Tests;

public class FilaQuadrosTests : IDisposable
{
    private readonly PastaTemporaria _pasta = new();
    private readonly RelogioFalso _relogio = new();
    private readonly LogColetor<FilaQuadros> _log = new();

    public void Dispose() => _pasta.Dispose();

    private FilaQuadros Criar(TimeSpan? idade = null, long tamanho = 500L * 1024 * 1024, IProtetorDados? protetor = null) =>
        new(_pasta.Caminho, protetor ?? new ProtetorFalso(), _relogio, _log, idade ?? TimeSpan.FromHours(24), tamanho);

    private static byte[] Jpeg(byte marca, int tamanho = 100) =>
        new byte[] { 0xFF, 0xD8 }.Concat(Enumerable.Repeat(marca, tamanho)).Concat(new byte[] { 0xFF, 0xD9 }).ToArray();

    [Fact]
    public void SaiDoMaisAntigoParaOMaisNovoComOHorarioOriginal()
    {
        var fila = Criar();
        var t = _relogio.Agora;
        // Inserção fora de ordem: a ordem de saída é a da captura.
        fila.Adicionar("c1", Jpeg(3), t.AddMinutes(-1));
        fila.Adicionar("c1", Jpeg(1), t.AddMinutes(-30));
        fila.Adicionar("c1", Jpeg(2), t.AddMinutes(-10));

        var ordem = new List<byte>();
        var horarios = new List<DateTimeOffset>();
        while (fila.ObterMaisAntigo("c1") is { } item)
        {
            ordem.Add(item.Jpeg[2]);
            horarios.Add(item.CapturadaEm);
            fila.Remover(item);
        }
        Assert.Equal(new byte[] { 1, 2, 3 }, ordem);
        Assert.Equal(t.AddMinutes(-30), horarios[0]);
        Assert.Equal(0, fila.Contar("c1"));
    }

    [Fact]
    public void PreservaOInstanteEODeslocamentoDeFusoDaCaptura()
    {
        var fila = Criar();
        var em = _relogio.Agora.ToOffset(TimeSpan.FromHours(-3)).AddMinutes(-5);
        fila.Adicionar("c1", Jpeg(1), em);
        var item = fila.ObterMaisAntigo("c1")!;
        Assert.Equal(em, item.CapturadaEm);
        Assert.Equal(TimeSpan.FromHours(-3), item.CapturadaEm.Offset);
    }

    [Fact]
    public void CamerasTemFilasSeparadas()
    {
        var fila = Criar();
        fila.Adicionar("c1", Jpeg(1), _relogio.Agora);
        fila.Adicionar("c2", Jpeg(2), _relogio.Agora);
        Assert.Equal(1, fila.Contar("c1"));
        Assert.Equal(1, fila.Contar("c2"));
        Assert.Equal(2, fila.ObterMaisAntigo("c2")!.Jpeg[2]);
    }

    [Fact]
    public void QuadroExpiradoEDescartadoERegistrado()
    {
        var fila = Criar(idade: TimeSpan.FromHours(24));
        fila.Adicionar("c1", Jpeg(1), _relogio.Agora.AddHours(-25));
        fila.Adicionar("c1", Jpeg(2), _relogio.Agora.AddHours(-23));
        _log.Tudo(); // limpa nada; só garante acesso
        var item = fila.ObterMaisAntigo("c1")!;
        Assert.Equal(2, item.Jpeg[2]);
        Assert.Equal(1, fila.Contar("c1"));
    }

    [Fact]
    public void ExpiraComOPassarDoTempo()
    {
        var fila = Criar(idade: TimeSpan.FromHours(24));
        fila.Adicionar("c1", Jpeg(1), _relogio.Agora);
        Assert.NotNull(fila.ObterMaisAntigo("c1"));
        _relogio.Avancar(TimeSpan.FromHours(24) + TimeSpan.FromMinutes(1));
        Assert.Null(fila.ObterMaisAntigo("c1"));
        Assert.Equal(0, fila.Contar("c1"));
        Assert.Contains("descartados", _log.Tudo());
    }

    [Fact]
    public void AoEstourarOTamanhoDescartaOsMaisAntigosDeQualquerCameraERegistraQuantos()
    {
        // cada arquivo cifrado tem ~ 5 KB; limite de 12 KB => cabem 2
        var fila = Criar(tamanho: 12 * 1024);
        var t = _relogio.Agora;
        fila.Adicionar("c1", Jpeg(1, 5000), t.AddMinutes(-4)); // mais antigo
        fila.Adicionar("c2", Jpeg(2, 5000), t.AddMinutes(-3));
        fila.Adicionar("c1", Jpeg(3, 5000), t.AddMinutes(-2)); // estoura: sai o mais antigo (c1 / marca 1)

        Assert.True(fila.TamanhoTotalBytes() <= 12 * 1024);
        Assert.Equal(1, fila.Contar("c1"));
        Assert.Equal(1, fila.Contar("c2"));
        Assert.Equal(3, fila.ObterMaisAntigo("c1")!.Jpeg[2]);
        Assert.Contains("1 quadro(s) mais antigos foram descartados", _log.Tudo());
    }

    [Fact]
    public void ArquivoCorrompidoNaoTravaAFila()
    {
        var fila = Criar();
        var t = _relogio.Agora;
        fila.Adicionar("c1", Jpeg(9), t.AddMinutes(-1));
        // arquivo ilegível com nome que o coloca ANTES do bom
        var pasta = Path.Combine(_pasta.Caminho, "fila", "c1");
        var ruim = Path.Combine(pasta, $"{t.AddMinutes(-5).UtcTicks:D19}_999999.quadro");
        File.WriteAllBytes(ruim, new byte[] { 1, 2, 3, 4, 5 });
        var ruim2 = Path.Combine(pasta, $"{t.AddMinutes(-4).UtcTicks:D19}_999998.quadro");
        File.WriteAllBytes(ruim2, Array.Empty<byte>());

        var item = fila.ObterMaisAntigo("c1");
        Assert.NotNull(item);
        Assert.Equal(9, item!.Jpeg[2]);
        Assert.False(File.Exists(ruim));
        Assert.False(File.Exists(ruim2));
        Assert.Contains("ilegível", _log.Tudo());
    }

    [Fact]
    public void ArquivoCifradoPorOutraMaquinaTambemEPulado()
    {
        var pasta = Path.Combine(_pasta.Caminho, "fila", "c1");
        Directory.CreateDirectory(pasta);
        var t = _relogio.Agora;
        // cifra com um protetor, lê com outro que não reconhece
        var fila = Criar(protetor: new ProtetorFalso());
        fila.Adicionar("c1", Jpeg(1), t.AddMinutes(-2));
        var outra = new FilaQuadros(_pasta.Caminho, new ProtetorQueFalha(), _relogio, _log, TimeSpan.FromHours(24), 1L << 30);
        Assert.Null(outra.ObterMaisAntigo("c1"));
        Assert.Equal(0, outra.Contar("c1"));
    }

    [Fact]
    public void ConteudoNoDiscoNaoContemOJpegEmTextoClaro()
    {
        var fila = Criar();
        var jpeg = Jpeg(0x41, 400);
        fila.Adicionar("c1", jpeg, _relogio.Agora);
        var arquivo = Directory.GetFiles(Path.Combine(_pasta.Caminho, "fila", "c1"), "*.quadro").Single();
        var bytes = File.ReadAllBytes(arquivo);
        var padrao = Enumerable.Repeat((byte)0x41, 50).ToArray();
        Assert.DoesNotContain(true, Enumerable.Range(0, bytes.Length - 50).Select(i => bytes.AsSpan(i, 50).SequenceEqual(padrao)));
    }

    [Fact]
    public void FuncionaComDpapiReal()
    {
        var fila = Criar(protetor: new ProtetorDpapi());
        var jpeg = Jpeg(5, 300);
        fila.Adicionar("c1", jpeg, _relogio.Agora);
        var item = fila.ObterMaisAntigo("c1")!;
        Assert.Equal(jpeg, item.Jpeg);
    }

    [Fact]
    public void LimparCameraApagaSoAquelaCamera()
    {
        var fila = Criar();
        fila.Adicionar("c1", Jpeg(1), _relogio.Agora);
        fila.Adicionar("c1", Jpeg(2), _relogio.Agora);
        fila.Adicionar("c2", Jpeg(3), _relogio.Agora);
        Assert.Equal(2, fila.LimparCamera("c1"));
        Assert.Equal(0, fila.Contar("c1"));
        Assert.Equal(1, fila.Contar("c2"));
    }

    private sealed class ProtetorQueFalha : IProtetorDados
    {
        public byte[] Proteger(byte[] dados) => dados;
        public byte[] Desproteger(byte[] dados) => throw new System.Security.Cryptography.CryptographicException("outra máquina");
    }
}
