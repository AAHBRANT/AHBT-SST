using System.Globalization;
using AAHBRANT.SST.AgenteCameras.Infra;
using AAHBRANT.SST.AgenteCameras.Seguranca;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.AgenteCameras.Fila;

public sealed record ItemFila(string Caminho, string CameraId, byte[] Jpeg, DateTimeOffset CapturadaEm);

public sealed record ResultadoLimpeza(int Expirados, int TemporariosApagados, int ExcedenteTamanho, long TamanhoBytes)
{
    public int Total => Expirados + TemporariosApagados + ExcedenteTamanho;
}

// Fila em disco dos quadros que não puderam ser enviados (sem internet). Um arquivo por quadro em
// PastaDados/fila/<cameraId>/, cifrado com DPAPI (escopo da máquina) junto com o horário real da captura.
// O nome do arquivo começa pelo horário UTC da captura em ticks, então a ordem alfabética é a ordem de captura
// e a expiração se decide sem abrir o arquivo (arquivo sem esse prefixo expira pela data de gravação).
// Limites: idade máxima e tamanho total (todas as câmeras); ao estourar o tamanho, descarta os mais antigos de
// qualquer câmera. Arquivo corrompido é apagado e pulado.
// Limpar() faz a manutenção da pasta inteira (todas as câmeras, inclusive as que saíram da configuração) e é chamada
// periodicamente por LimpezaPeriodica, independente de qualquer câmera estar enviando.
public sealed class FilaQuadros
{
    private static readonly byte[] Magia = { (byte)'S', (byte)'Q', (byte)'1' };
    private const string Extensao = ".quadro";
    private const string ExtensaoTemporaria = ".tmp";

    /// <summary>Um ".tmp" mais novo que isto pode ser uma gravação em andamento: não é apagado (mas conta no tamanho).</summary>
    public static readonly TimeSpan IdadeMinimaTemporarioOrfao = TimeSpan.FromMinutes(5);

    private readonly string _raiz;
    private readonly IProtetorDados _protetor;
    private readonly IRelogio _relogio;
    private readonly ILogger<FilaQuadros> _log;
    private readonly TimeSpan _idadeMaxima;
    private readonly long _tamanhoMaximo;
    private readonly object _trava = new();
    private int _sequencia;

    public FilaQuadros(string pastaDados, IProtetorDados protetor, IRelogio relogio, ILogger<FilaQuadros> log,
        TimeSpan idadeMaxima, long tamanhoMaximoBytes)
    {
        _raiz = Path.Combine(pastaDados, "fila");
        _protetor = protetor;
        _relogio = relogio;
        _log = log;
        _idadeMaxima = idadeMaxima;
        _tamanhoMaximo = tamanhoMaximoBytes;
        Directory.CreateDirectory(_raiz);
    }

    public int Contar(string cameraId)
    {
        lock (_trava)
        {
            var pasta = PastaDe(cameraId);
            return Directory.Exists(pasta) ? Directory.EnumerateFiles(pasta, "*" + Extensao).Count() : 0;
        }
    }

    /// <summary>Tamanho de tudo que a fila ocupa em disco (quadros e temporários, todas as câmeras).</summary>
    public long TamanhoTotalBytes()
    {
        lock (_trava) return Arquivos().Sum(a => a.Tamanho);
    }

    public void Adicionar(string cameraId, byte[] jpeg, DateTimeOffset capturadaEm)
    {
        var conteudo = _protetor.Proteger(Serializar(jpeg, capturadaEm));
        lock (_trava)
        {
            var pasta = PastaDe(cameraId);
            Directory.CreateDirectory(pasta);
            var nome = $"{capturadaEm.UtcTicks:D19}_{Interlocked.Increment(ref _sequencia):D6}{Extensao}";
            var caminho = Path.Combine(pasta, nome);
            var temporario = caminho + ExtensaoTemporaria;
            File.WriteAllBytes(temporario, conteudo);
            File.Move(temporario, caminho, overwrite: true);
            AplicarLimitesSemTrava();
        }
    }

    /// <summary>Quadro mais antigo da câmera. Apaga (e registra) os expirados e os corrompidos que encontrar pelo caminho.</summary>
    public ItemFila? ObterMaisAntigo(string cameraId)
    {
        lock (_trava)
        {
            var pasta = PastaDe(cameraId);
            if (!Directory.Exists(pasta)) return null;
            var limiteIdade = _relogio.Agora - _idadeMaxima;
            var expirados = 0;
            try
            {
                foreach (var caminho in Directory.EnumerateFiles(pasta, "*" + Extensao).OrderBy(c => c, StringComparer.Ordinal))
                {
                    if (new DateTimeOffset(TicksDe(caminho), TimeSpan.Zero) < limiteIdade)
                    {
                        Apagar(caminho);
                        expirados++;
                        continue;
                    }

                    try
                    {
                        var (jpeg, capturadaEm) = Desserializar(_protetor.Desproteger(File.ReadAllBytes(caminho)));
                        return new ItemFila(caminho, cameraId, jpeg, capturadaEm);
                    }
                    catch (Exception e) when (e is not OutOfMemoryException)
                    {
                        _log.LogWarning("Quadro da fila ilegível ({Arquivo}); descartado: {Erro}", Path.GetFileName(caminho), e.GetType().Name);
                        Apagar(caminho);
                    }
                }
                return null;
            }
            finally
            {
                if (expirados > 0)
                    _log.LogWarning("Fila offline: {Quantidade} quadro(s) da câmera {Camera} passaram de {Horas:0.#} h e foram descartados.", expirados, cameraId, _idadeMaxima.TotalHours);
            }
        }
    }

    public void Remover(ItemFila item)
    {
        lock (_trava) Apagar(item.Caminho);
    }

    public int LimparCamera(string cameraId)
    {
        lock (_trava)
        {
            var pasta = PastaDe(cameraId);
            if (!Directory.Exists(pasta)) return 0;
            var n = 0;
            foreach (var arquivo in Directory.EnumerateFiles(pasta, "*" + Extensao))
            {
                Apagar(arquivo);
                n++;
            }
            return n;
        }
    }

    /// <summary>Aplica idade máxima e tamanho total a todas as câmeras. Devolve quantos quadros foram descartados.</summary>
    public int AplicarLimites()
    {
        lock (_trava) return AplicarLimitesSemTrava();
    }

    /// <summary>
    /// Manutenção completa da pasta da fila, em todas as pastas de câmera (configuradas ou não): apaga quadros
    /// vencidos, apaga ".tmp" órfãos (mais velhos que <see cref="IdadeMinimaTemporarioOrfao"/>), aplica o limite de
    /// tamanho (os ".tmp" ainda recentes contam no total) e remove pastas de câmera vazias.
    /// </summary>
    public ResultadoLimpeza Limpar()
    {
        lock (_trava)
        {
            var r = LimparSemTrava();
            RemoverPastasVazias();
            return r;
        }
    }

    private int AplicarLimitesSemTrava() => LimparSemTrava().Total;

    private ResultadoLimpeza LimparSemTrava()
    {
        var descartadosIdade = 0;
        var temporarios = 0;
        var descartadosTamanho = 0;
        var agora = _relogio.Agora;
        var limiteIdade = agora - _idadeMaxima;
        var limiteTemporario = agora - IdadeMinimaTemporarioOrfao;
        var restantes = new List<ArquivoFila>();
        long temporariosMantidos = 0;

        foreach (var a in Arquivos().ToList())
        {
            if (a.Temporario)
            {
                if (new DateTimeOffset(a.Ticks, TimeSpan.Zero) < limiteTemporario && Apagar(a.Caminho)) temporarios++;
                else temporariosMantidos += a.Tamanho;
                continue;
            }
            if (new DateTimeOffset(a.Ticks, TimeSpan.Zero) < limiteIdade && Apagar(a.Caminho)) descartadosIdade++;
            else restantes.Add(a);
        }

        var total = restantes.Sum(a => a.Tamanho) + temporariosMantidos;
        // Mais antigos primeiro, de qualquer câmera (o nome começa pelo horário da captura).
        foreach (var a in restantes.OrderBy(a => a.Nome, StringComparer.Ordinal))
        {
            if (total <= _tamanhoMaximo) break;
            if (!Apagar(a.Caminho)) continue;
            total -= a.Tamanho;
            descartadosTamanho++;
        }

        if (descartadosIdade > 0)
            _log.LogWarning("Fila offline: {Quantidade} quadro(s) passaram de {Horas:0.#} h e foram descartados.", descartadosIdade, _idadeMaxima.TotalHours);
        if (temporarios > 0)
            _log.LogWarning("Fila offline: {Quantidade} arquivo(s) temporário(s) órfão(s) foram apagados.", temporarios);
        if (descartadosTamanho > 0)
            _log.LogWarning("Fila offline passou de {Mb} MB: {Quantidade} quadro(s) mais antigos foram descartados.", _tamanhoMaximo / (1024 * 1024), descartadosTamanho);
        return new ResultadoLimpeza(descartadosIdade, temporarios, descartadosTamanho, total);
    }

    private void RemoverPastasVazias()
    {
        if (!Directory.Exists(_raiz)) return;
        try
        {
            foreach (var pasta in Directory.EnumerateDirectories(_raiz))
            {
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(pasta).Any()) Directory.Delete(pasta);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // fica para a próxima rodada
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _log.LogWarning("Não foi possível listar a pasta da fila: {Erro}", e.Message);
        }
    }

    private sealed record ArquivoFila(string Caminho, string Nome, long Tamanho, long Ticks, bool Temporario);

    // Quadros ".quadro" e temporários ".quadro.tmp" de TODAS as pastas de câmera. Ticks: horário da captura (do nome)
    // ou, se o nome não tiver, a data de gravação do arquivo (assim nenhum arquivo fica imune à expiração).
    private IEnumerable<ArquivoFila> Arquivos()
    {
        if (!Directory.Exists(_raiz)) yield break;
        foreach (var pasta in Directory.EnumerateDirectories(_raiz))
            foreach (var caminho in Directory.EnumerateFiles(pasta))
            {
                var nome = Path.GetFileName(caminho);
                var temporario = nome.EndsWith(Extensao + ExtensaoTemporaria, StringComparison.OrdinalIgnoreCase);
                if (!temporario && !nome.EndsWith(Extensao, StringComparison.OrdinalIgnoreCase)) continue;
                long tamanho;
                long ticks;
                try
                {
                    tamanho = new FileInfo(caminho).Length;
                    // Temporário: só vale a data de gravação.
                    ticks = temporario ? File.GetLastWriteTimeUtc(caminho).Ticks : TicksDe(caminho);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    continue;
                }
                yield return new ArquivoFila(caminho, nome, tamanho, ticks, temporario);
            }
    }

    private bool Apagar(string caminho)
    {
        try
        {
            File.Delete(caminho);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _log.LogWarning("Não foi possível apagar {Arquivo} da fila: {Erro}", Path.GetFileName(caminho), e.Message);
            return false;
        }
    }

    private string PastaDe(string cameraId)
    {
        var seguro = string.Concat(cameraId.Select(c => Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c));
        return Path.Combine(_raiz, seguro);
    }

    private static long TicksDe(string caminho)
    {
        if (TentarLerTicks(caminho) is { } t) return t;
        try { return File.GetLastWriteTimeUtc(caminho).Ticks; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return DateTime.UtcNow.Ticks; }
    }

    private static long? TentarLerTicks(string caminho)
    {
        var nome = Path.GetFileName(caminho);
        var corte = nome.IndexOf('_');
        return corte > 0 && long.TryParse(nome.AsSpan(0, corte), NumberStyles.None, CultureInfo.InvariantCulture, out var t) && t <= DateTime.MaxValue.Ticks ? t : null;
    }

    // Formato (antes de cifrar): "SQ1" | ticks UTC (8) | offset em minutos (2) | tamanho do JPEG (4) | JPEG
    private static byte[] Serializar(byte[] jpeg, DateTimeOffset capturadaEm)
    {
        using var ms = new MemoryStream(jpeg.Length + 32);
        using var w = new BinaryWriter(ms);
        w.Write(Magia);
        w.Write(capturadaEm.UtcTicks);
        w.Write((short)capturadaEm.Offset.TotalMinutes);
        w.Write(jpeg.Length);
        w.Write(jpeg);
        w.Flush();
        return ms.ToArray();
    }

    private static (byte[] Jpeg, DateTimeOffset CapturadaEm) Desserializar(byte[] dados)
    {
        using var r = new BinaryReader(new MemoryStream(dados));
        if (!r.ReadBytes(3).AsSpan().SequenceEqual(Magia)) throw new InvalidDataException("formato desconhecido");
        var ticks = r.ReadInt64();
        var offset = TimeSpan.FromMinutes(r.ReadInt16());
        var tamanho = r.ReadInt32();
        if (tamanho <= 0 || tamanho != dados.Length - r.BaseStream.Position) throw new InvalidDataException("tamanho inconsistente");
        var jpeg = r.ReadBytes(tamanho);
        var utc = new DateTimeOffset(ticks, TimeSpan.Zero);
        return (jpeg, utc.ToOffset(offset));
    }
}
