using System.Diagnostics;
using System.Runtime.CompilerServices;
using AAHBRANT.SST.AgenteCameras.Opcoes;
using AAHBRANT.SST.AgenteCameras.Seguranca;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.AgenteCameras.Quadros;

// Lê o RTSP rodando ffmpeg.exe como processo filho: por padrão 1 quadro por segundo, no máximo 1280 px de largura,
// JPEG q4 pela saída padrão (perfil da análise de EPI; o "ao vivo" usa outro perfil e outro processo). A URL (com a senha) vai na linha de comando do ffmpeg; nunca é logada, e a saída de erro do
// ffmpeg passa pelo mascarador antes de qualquer log.
public sealed class FfmpegFonteQuadros : IFonteQuadros, IFonteAoVivo
{
    private const int LinhasDeErroGuardadas = 8;

    private readonly string _caminhoFfmpeg;
    private readonly Mascarador _mascarador;
    private readonly ILogger<FfmpegFonteQuadros> _log;
    private readonly int _tempoLimiteRwSegundos;

    public FfmpegFonteQuadros(string caminhoFfmpeg, Mascarador mascarador, ILogger<FfmpegFonteQuadros> log, int tempoLimiteRwSegundos = 10)
    {
        _caminhoFfmpeg = caminhoFfmpeg;
        _mascarador = mascarador;
        _log = log;
        _tempoLimiteRwSegundos = tempoLimiteRwSegundos;
    }

    internal static IReadOnlyList<string> MontarArgumentos(string url, int tempoLimiteRwSegundos) =>
        MontarArgumentos(url, tempoLimiteRwSegundos, PerfilCaptura.Analise);

    // parteImagem 1 ou 2: o recorte (metade de cima ou de baixo) vem ANTES de fps e scale. Parte 0: a linha de sempre.
    internal static IReadOnlyList<string> MontarArgumentos(string url, int tempoLimiteRwSegundos, PerfilCaptura perfil, int parteImagem = ParteImagemCamera.Inteira)
    {
        var filtro = $"fps={perfil.Fps},scale='min({perfil.LarguraMaxima},iw)':-2";
        if (ParteImagemCamera.FiltroCrop(parteImagem) is { } crop) filtro = crop + "," + filtro;
        return Montar(url, tempoLimiteRwSegundos, perfil, filtro);
    }

    private static IReadOnlyList<string> Montar(string url, int tempoLimiteRwSegundos, PerfilCaptura perfil, string filtro) => new[]
    {
        "-hide_banner", "-nostdin", "-loglevel", "warning",
        "-rtsp_transport", "tcp",
        // Tempo limite de leitura do RTSP, em microssegundos. É "-timeout": o "-rw_timeout" não existe para
        // entrada RTSP e o ffmpeg recusa abrir ("Option rw_timeout not found") — visto com o ffmpeg 9.0.2 e a
        // câmera real em 12/11/2026.
        "-timeout", (tempoLimiteRwSegundos * 1_000_000L).ToString(),
        "-i", url,
        "-an",
        "-vf", filtro,
        "-f", "image2pipe", "-c:v", "mjpeg", "-q:v", perfil.QualidadeJpeg.ToString(),
        "pipe:1",
    };

    public IAsyncEnumerable<byte[]> LerQuadrosAsync(CameraConfig camera, CancellationToken ct) =>
        LerQuadrosAsync(camera, PerfilCaptura.Analise, ct);

    public async IAsyncEnumerable<byte[]> LerQuadrosAsync(CameraConfig camera, PerfilCaptura perfil, [EnumeratorCancellation] CancellationToken ct)
    {
        if (!File.Exists(_caminhoFfmpeg))
            throw new FfmpegIndisponivelException(
                $"ffmpeg não encontrado em \"{_caminhoFfmpeg}\". Coloque o ffmpeg.exe (build LGPL) nesse caminho ou ajuste AgenteCameras:CaminhoFfmpeg. Nova tentativa em 1 minuto.");

        var inicio = new ProcessStartInfo(_caminhoFfmpeg)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in MontarArgumentos(UrlRtsp.Montar(camera), _tempoLimiteRwSegundos, perfil, camera.ParteImagem))
            inicio.ArgumentList.Add(arg);

        using var processo = new Process { StartInfo = inicio };
        try
        {
            processo.Start();
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new FfmpegIndisponivelException($"Não foi possível iniciar o ffmpeg ({_mascarador.Mascarar(e.Message)}). Nova tentativa em 1 minuto.");
        }

        if (OperatingSystem.IsWindows() && !JobObjectFilhos.Associar(processo))
            _log.LogWarning("[{Camera}] Não foi possível vincular o ffmpeg ao serviço; se o serviço cair, o ffmpeg pode ficar aberto.", camera.Nome);

        var ultimasLinhas = new Queue<string>();
        var leitorErro = Task.Run(async () =>
        {
            try
            {
                string? linha;
                while ((linha = await processo.StandardError.ReadLineAsync().ConfigureAwait(false)) is not null)
                {
                    var limpa = _mascarador.Mascarar(linha);
                    lock (ultimasLinhas)
                    {
                        ultimasLinhas.Enqueue(limpa);
                        while (ultimasLinhas.Count > LinhasDeErroGuardadas) ultimasLinhas.Dequeue();
                    }
                }
            }
            catch (Exception) { /* processo morto: fim da leitura */ }
        }, CancellationToken.None);

        using var aoCancelar = ct.Register(() => Encerrar(processo));
        var separador = new SeparadorJpeg();
        var bloco = new byte[64 * 1024];
        try
        {
            var saida = processo.StandardOutput.BaseStream;
            while (true)
            {
                int lidos;
                try
                {
                    lidos = await saida.ReadAsync(bloco, ct).ConfigureAwait(false);
                }
                catch (Exception e) when (e is IOException or ObjectDisposedException && !ct.IsCancellationRequested)
                {
                    break;
                }
                if (lidos == 0) break;
                foreach (var quadro in separador.Adicionar(bloco.AsSpan(0, lidos)))
                    yield return quadro;
            }

            ct.ThrowIfCancellationRequested();
            await Task.WhenAny(leitorErro, Task.Delay(500, CancellationToken.None)).ConfigureAwait(false);
            string detalhe;
            lock (ultimasLinhas) detalhe = string.Join(" | ", ultimasLinhas);
            var codigo = processo.WaitForExit(2000) ? processo.ExitCode.ToString() : "?";
            throw new FonteEncerradaException($"O ffmpeg encerrou (código {codigo}). {(detalhe.Length > 0 ? "Saída: " + detalhe : "Sem mensagem de erro.")}");
        }
        finally
        {
            Encerrar(processo);
            await Task.WhenAny(leitorErro, Task.Delay(2000, CancellationToken.None)).ConfigureAwait(false);
        }
    }

    private static void Encerrar(Process processo)
    {
        try
        {
            if (!processo.HasExited) processo.Kill(entireProcessTree: true);
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            // já encerrou
        }
    }
}
