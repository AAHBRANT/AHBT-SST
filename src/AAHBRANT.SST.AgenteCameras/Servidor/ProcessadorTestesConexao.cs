using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using AAHBRANT.SST.AgenteCameras.Infra;
using AAHBRANT.SST.AgenteCameras.Opcoes;
using AAHBRANT.SST.AgenteCameras.Quadros;
using AAHBRANT.SST.AgenteCameras.Seguranca;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.AgenteCameras.Servidor;

// Executa os pedidos de "Testar conexão" da tela: abre a câmera pelo mesmo caminho da leitura normal (ffmpeg com a
// URL RTSP e o -timeout de sempre), captura UM quadro, gera a miniatura e devolve o resultado ao servidor.
//
// - Cada teste roda em segundo plano, num processo ffmpeg próprio: não bloqueia a consulta de configuração (sinal de
//   vida) e não mexe na leitura normal da mesma câmera (que segue com a conexão dela).
// - Cada testeId é executado UMA vez (o servidor continua entregando o pedido até receber a resposta).
// - Tempo: captura até TempoCaptura (25 s) + envio com até 3 tentativas de 20 s; o servidor expira o pedido em 2 min.
// - Nada que sai daqui (motivo, detalhe, log) contém a senha nem URL com credencial.
public sealed class ProcessadorTestesConexao
{
    public static readonly TimeSpan TempoCapturaPadrao = TimeSpan.FromSeconds(25);
    public static readonly TimeSpan PrazoTotalPadrao = TimeSpan.FromSeconds(100);
    public const int MaximoSimultaneosPadrao = 2;
    public const int TentativasEnvio = 3;
    public const int MaximoMotivo = 300;   // o servidor trunca em 300
    public const int MaximoDetalhe = 100;  // e em 100
    private static readonly TimeSpan MemoriaDosPedidos = TimeSpan.FromHours(1);

    private readonly IFonteQuadros _fonte;
    private readonly IClienteConfiguracao _cliente;
    private readonly Mascarador _mascarador;
    private readonly IRelogio _relogio;
    private readonly ILogger _log;
    private readonly CancellationToken _parada;
    private readonly TimeSpan _tempoCaptura;
    private readonly TimeSpan _prazoTotal;
    private readonly TimeSpan _pausaReenvio;
    private readonly SemaphoreSlim _vagas;
    private readonly object _trava = new();
    private readonly Dictionary<Guid, DateTimeOffset> _vistos = new();
    private readonly List<Task> _tarefas = new();

    public ProcessadorTestesConexao(IFonteQuadros fonte, IClienteConfiguracao cliente, Mascarador mascarador, IRelogio relogio,
        ILogger log, CancellationToken parada, TimeSpan? tempoCaptura = null, TimeSpan? prazoTotal = null,
        int maximoSimultaneos = MaximoSimultaneosPadrao, TimeSpan? pausaReenvio = null)
    {
        _fonte = fonte;
        _cliente = cliente;
        _mascarador = mascarador;
        _relogio = relogio;
        _log = log;
        _parada = parada;
        _tempoCaptura = tempoCaptura ?? TempoCapturaPadrao;
        _prazoTotal = prazoTotal ?? PrazoTotalPadrao;
        _pausaReenvio = pausaReenvio ?? TimeSpan.FromSeconds(3);
        _vagas = new SemaphoreSlim(maximoSimultaneos, maximoSimultaneos);
    }

    /// <summary>Aceita os pedidos que ainda não foram vistos e os executa em segundo plano. Devolve quantos começaram.</summary>
    public int Aceitar(IEnumerable<TesteServidor> testes)
    {
        var novos = new List<TesteServidor>();
        var agora = _relogio.Agora;
        lock (_trava)
        {
            foreach (var velho in _vistos.Where(p => agora - p.Value > MemoriaDosPedidos).Select(p => p.Key).ToList())
                _vistos.Remove(velho);
            _tarefas.RemoveAll(t => t.IsCompleted);
            foreach (var t in testes)
                if (t.TesteId != Guid.Empty && _vistos.TryAdd(t.TesteId, agora))
                    novos.Add(t);
        }

        foreach (var t in novos)
        {
            _mascarador.Registrar(t.Senha);
            var tarefa = Task.Run(() => ExecutarAsync(t, agora), CancellationToken.None);
            lock (_trava) _tarefas.Add(tarefa);
        }
        return novos.Count;
    }

    /// <summary>Espera os testes em andamento (usado ao encerrar o serviço e nos testes automatizados).</summary>
    public async Task AguardarTodosAsync()
    {
        Task[] tarefas;
        lock (_trava) tarefas = _tarefas.ToArray();
        await Task.WhenAll(tarefas).ConfigureAwait(false);
    }

    private async Task ExecutarAsync(TesteServidor teste, DateTimeOffset aceitoEm)
    {
        try
        {
            await _vagas.WaitAsync(_parada).ConfigureAwait(false);
            try
            {
                _log.LogInformation("Teste de conexão {TesteId}: abrindo a câmera em {Host}.", teste.TesteId, teste.Host);
                var resultado = await TestarAsync(teste).ConfigureAwait(false);
                _log.LogInformation("Teste de conexão {TesteId}: {Situacao}{Motivo}.", teste.TesteId,
                    resultado.Sucesso ? "imagem recebida" : "falhou", resultado.Sucesso ? "" : " - " + resultado.Motivo);
                await EnviarAsync(teste.TesteId, resultado, aceitoEm).ConfigureAwait(false);
            }
            finally
            {
                _vagas.Release();
            }
        }
        catch (OperationCanceledException) when (_parada.IsCancellationRequested)
        {
            // serviço parando: o pedido expira no servidor
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.LogError("Teste de conexão {TesteId} terminou com erro inesperado: {Erro}", teste.TesteId, _mascarador.Mascarar(e.Message));
        }
    }

    internal async Task<ResultadoTesteConexao> TestarAsync(TesteServidor teste)
    {
        var senha = teste.Senha ?? "";
        ResultadoTesteConexao Falha(string motivo, string codigo, string? detalhe) =>
            new(false,
                ClassificadorFalhaConexao.Sanitizar(motivo, senha, _mascarador, MaximoMotivo),
                string.IsNullOrWhiteSpace(detalhe) ? null : ClassificadorFalhaConexao.Sanitizar(detalhe, senha, _mascarador, MaximoDetalhe),
                null,
                codigo);

        if (string.IsNullOrWhiteSpace(teste.Host))
            return Falha("Endereço da câmera não informado no cadastro do equipamento", ClassificadorFalhaConexao.CodErroDesconhecido, null);

        var id = (teste.CameraId ?? teste.EquipamentoId).ToString("D");
        var camera = new CameraConfig(id, "teste de conexão", teste.Host.Trim(), teste.PortaRtsp is >= 1 and <= 65535 ? teste.PortaRtsp : 554,
            (teste.Usuario ?? "").Trim(), senha, teste.Canal is 1 or 2 ? teste.Canal : 1, teste.Stream is 0 or 1 ? teste.Stream : 0,
            null, TimeSpan.FromSeconds(1), ParteImagemCamera.Normalizar(teste.ParteImagem));

        byte[]? quadro = null;
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(_parada);
        limite.CancelAfter(_tempoCaptura);
        try
        {
            // Para no primeiro quadro: sair do foreach encerra o ffmpeg do teste.
            await foreach (var jpeg in _fonte.LerQuadrosAsync(camera, limite.Token).WithCancellation(limite.Token).ConfigureAwait(false))
            {
                quadro = jpeg;
                break;
            }
        }
        catch (OperationCanceledException) when (_parada.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Falha(ClassificadorFalhaConexao.SemResposta, ClassificadorFalhaConexao.CodSemResposta, $"Sem imagem em {_tempoCaptura.TotalSeconds:0} s.");
        }
        catch (FfmpegIndisponivelException)
        {
            return Falha(ClassificadorFalhaConexao.FfmpegAusente, ClassificadorFalhaConexao.CodFfmpegAusente, "Confira AgenteCameras:CaminhoFfmpeg no computador do agente.");
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            var texto = e.Message;
            var (motivo, codigo) = ClassificadorFalhaConexao.ClassificarComCodigo(texto);
            return Falha(motivo, codigo, UltimaLinhaDaSaida(texto));
        }

        if (quadro is null)
            return Falha(ClassificadorFalhaConexao.SemImagem, ClassificadorFalhaConexao.CodSemImagem, "A câmera encerrou a conexão sem enviar imagem.");

        var miniatura = Miniatura.Gerar(quadro);
        if (miniatura is null)
            return Falha(ClassificadorFalhaConexao.ImagemInvalida, ClassificadorFalhaConexao.CodImagemIlegivel, null);

        return new ResultadoTesteConexao(true, null,
            ClassificadorFalhaConexao.Sanitizar($"Imagem recebida ({miniatura.Value.Largura}x{miniatura.Value.Altura}).", senha, _mascarador, MaximoDetalhe),
            Convert.ToBase64String(miniatura.Value.Jpeg));
    }

    // O FfmpegFonteQuadros monta "O ffmpeg encerrou (código N). Saída: linha1 | linha2": o detalhe é a última linha.
    internal static string? UltimaLinhaDaSaida(string mensagem)
    {
        var i = mensagem.IndexOf("Saída:", StringComparison.Ordinal);
        var saida = i >= 0 ? mensagem[(i + "Saída:".Length)..] : mensagem;
        var partes = saida.Split(" | ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return partes.Length == 0 ? null : partes[^1];
    }

    private async Task EnviarAsync(Guid testeId, ResultadoTesteConexao resultado, DateTimeOffset aceitoEm)
    {
        for (var tentativa = 1; tentativa <= TentativasEnvio; tentativa++)
        {
            var tipo = await _cliente.EnviarResultadoTesteAsync(testeId, resultado, _parada).ConfigureAwait(false);
            switch (tipo)
            {
                case TipoEnvioTeste.Enviado:
                    return;
                case TipoEnvioTeste.TesteNaoEncontrado:
                    _log.LogInformation("Teste de conexão {TesteId}: o servidor não o reconhece (expirou); resultado ignorado.", testeId);
                    return;
                case TipoEnvioTeste.SegredoInvalido:
                    _log.LogError("Teste de conexão {TesteId}: HTTP 401 ao enviar o resultado (segredo inválido ou agente revogado).", testeId);
                    return;
                case TipoEnvioTeste.Descartado:
                    _log.LogWarning("Teste de conexão {TesteId}: o servidor recusou o resultado; não será reenviado.", testeId);
                    return;
            }

            if (tentativa == TentativasEnvio || _relogio.Agora - aceitoEm + _pausaReenvio >= _prazoTotal)
            {
                _log.LogWarning("Teste de conexão {TesteId}: não foi possível enviar o resultado ao servidor; o pedido expira sozinho.", testeId);
                return;
            }
            await _relogio.AtrasarAsync(_pausaReenvio, _parada).ConfigureAwait(false);
        }
    }
}

// Miniatura da tela de teste: JPEG de até 150 KB (o servidor recusa mais que isso), largura máxima de 480 px.
[SupportedOSPlatform("windows")]
public static class Miniatura
{
    public const int LimiteBytes = 150 * 1024;
    public const int LarguraMaxima = 480;

    public static (byte[] Jpeg, int Largura, int Altura)? Gerar(byte[] jpeg, int limite = LimiteBytes)
    {
        try
        {
            using var ms = new MemoryStream(jpeg);
            using var origem = Image.FromStream(ms, false, true);
            var larguraOriginal = origem.Width;
            var alturaOriginal = origem.Height;
            if (larguraOriginal < 1 || alturaOriginal < 1) return null;

            var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
            var largura = Math.Min(LarguraMaxima, larguraOriginal);
            var qualidade = 75L;
            for (var tentativa = 0; tentativa < 6; tentativa++)
            {
                var altura = Math.Max(1, (int)Math.Round(alturaOriginal * (largura / (double)larguraOriginal)));
                using var bmp = new Bitmap(largura, altura, PixelFormat.Format24bppRgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(origem, 0, 0, largura, altura);
                }
                using var saida = new MemoryStream();
                using var parametros = new EncoderParameters(1);
                parametros.Param[0] = new EncoderParameter(Encoder.Quality, qualidade);
                bmp.Save(saida, codec, parametros);
                if (saida.Length <= limite) return (saida.ToArray(), larguraOriginal, alturaOriginal);
                largura = Math.Max(16, (int)(largura * 0.75));
                qualidade = Math.Max(40, qualidade - 10);
            }
        }
        catch (Exception e) when (e is ArgumentException or OutOfMemoryException or System.Runtime.InteropServices.ExternalException)
        {
            // JPEG ilegível
        }
        return null;
    }
}
