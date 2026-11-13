using AAHBRANT.SST.AgenteCameras.Envio;
using AAHBRANT.SST.AgenteCameras.Infra;
using AAHBRANT.SST.AgenteCameras.Opcoes;
using AAHBRANT.SST.AgenteCameras.Quadros;
using AAHBRANT.SST.AgenteCameras.Seguranca;
using AAHBRANT.SST.AgenteCameras.Servidor;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.AgenteCameras.AoVivo;

public enum MotivoParadaAoVivo
{
    Nenhum,
    SessaoEncerrada,      // a API respondeu 410 (ou 404 com sessao_nao_encontrada)
    SumiuDaConfiguracao,  // a sessão deixou de vir em "aoVivo"
    PrazoEsgotado,        // acabaram os segundosRestantes (relógio monotônico)
    ServidorSemResposta,  // o servidor ficou sem responder pelo mesmo prazo
    Falha,                // a câmera não abriu ou a leitura caiu (a API foi avisada pela rota /falha)
    ServicoParando,
}

// Transmissão de UMA sessão ao vivo: um ffmpeg próprio no STREAM 1 (secundário) da mesma câmera (mesmo host, canal,
// usuário e senha), com fps e largura pedidos e JPEG q7. Roda separada da análise: processo, envio e cotas próprios;
// uma falha aqui NUNCA reinicia nem mexe no MonitorCamera. Não tenta religar sozinha: ao falhar avisa a API (rota
// /falha), que encerra a sessão; se a tela pedir de novo, nasce outra sessão.
//
// Para (e mata o ffmpeg filho, pelo cancelamento do token que o FfmpegFonteQuadros observa) quando: a API manda 410; a
// sessão some da configuração; acabam os segundosRestantes; o servidor fica sem responder por esse mesmo prazo; o
// serviço para. Todos os prazos usam o relógio monotônico, nunca a data/hora do PC.
public sealed class TransmissaoAoVivo
{
    public static readonly TimeSpan PassoVigia = TimeSpan.FromSeconds(1);
    public const int FpsMaximo = 5;
    public const int LarguraPadrao = 640;
    public const int LarguraMinima = 160;
    public const int LarguraMaxima = 1280;
    public const int StreamSecundario = 1;
    private const int MaximoMotivo = 300;

    private readonly IFonteAoVivo _fonte;
    private readonly IClienteAoVivo _cliente;
    private readonly IRelogio _relogio;
    private readonly IRelogioMonotonico _mono;
    private readonly Mascarador _mascarador;
    private readonly ILogger _log;
    private readonly CancellationTokenSource _cts = new();
    private static readonly TimeSpan PrazoSemRespostaPadrao = TimeSpan.FromSeconds(60);
    private readonly TimeSpan _prazoSemResposta;
    private long _fimTicks;
    private long _ultimoContatoTicks;
    private int _motivo;
    private volatile bool _terminou;

    public TransmissaoAoVivo(SessaoAoVivoServidor sessao, CameraConfig camera, IFonteAoVivo fonte, IClienteAoVivo cliente,
        IRelogio relogio, IRelogioMonotonico mono, Mascarador mascarador, ILogger log, TimeSpan? prazoSemResposta = null)
    {
        Sessao = sessao;
        // Sempre o stream 1 (secundário, 640x720) do mesmo canal; a análise segue no stream dela.
        Camera = camera with { Stream = StreamSecundario };
        Perfil = PerfilCaptura.AoVivo(
            Math.Clamp(sessao.Fps, 1, FpsMaximo),
            sessao.LarguraMaxima <= 0 ? LarguraPadrao : Math.Clamp(sessao.LarguraMaxima, LarguraMinima, LarguraMaxima));
        _fonte = fonte;
        _cliente = cliente;
        _relogio = relogio;
        _mono = mono;
        _mascarador = mascarador;
        _log = log;
        var prazo = TimeSpan.FromSeconds(Math.Max(1, sessao.SegundosRestantes));
        // Servidor fora do ar: para em 60 s (ou no prazo da sessão, se for menor), para não gastar a internet da obra
        // transmitindo até 10 minutos para ninguém.
        _prazoSemResposta = prazoSemResposta ?? (prazo < PrazoSemRespostaPadrao ? prazo : PrazoSemRespostaPadrao);
        var agora = mono.Decorrido;
        _fimTicks = (agora + prazo).Ticks;
        _ultimoContatoTicks = agora.Ticks;
        Enviador = new EnviadorAoVivo(sessao.SessaoId, cliente, mono, mascarador, log, AoResultado);
    }

    public SessaoAoVivoServidor Sessao { get; }
    public CameraConfig Camera { get; }
    public PerfilCaptura Perfil { get; }
    public EnviadorAoVivo Enviador { get; }
    public bool Terminou => _terminou;
    public MotivoParadaAoVivo Motivo => (MotivoParadaAoVivo)Volatile.Read(ref _motivo);

    /// <summary>Segundos que faltam, no relógio monotônico (mínimo 0).</summary>
    public double SegundosRestantes => Math.Max(0, (TimeSpan.FromTicks(Volatile.Read(ref _fimTicks)) - _mono.Decorrido).TotalSeconds);

    /// <summary>Aplica o prazo que a API mandou na última consulta. Só encurta: nunca estende o que já estava valendo.</summary>
    public void AtualizarPrazo(int segundosRestantes)
    {
        var novo = (_mono.Decorrido + TimeSpan.FromSeconds(Math.Max(0, segundosRestantes))).Ticks;
        long atual;
        do
        {
            atual = Volatile.Read(ref _fimTicks);
            if (novo >= atual) return;
        } while (Interlocked.CompareExchange(ref _fimTicks, novo, atual) != atual);
    }

    /// <summary>Pede a parada (o primeiro motivo é o que vale). Mata o ffmpeg pelo cancelamento.</summary>
    public void Parar(MotivoParadaAoVivo motivo)
    {
        TentarParar(motivo);
    }

    private bool TentarParar(MotivoParadaAoVivo motivo)
    {
        var primeiro = Interlocked.CompareExchange(ref _motivo, (int)motivo, (int)MotivoParadaAoVivo.Nenhum) == (int)MotivoParadaAoVivo.Nenhum;
        try { _cts.Cancel(); } catch (ObjectDisposedException) { }
        return primeiro;
    }

    private void AoResultado(ResultadoQuadroAoVivo r)
    {
        switch (r.Tipo)
        {
            case TipoResultadoAoVivo.Encerrada:
                TentarParar(MotivoParadaAoVivo.SessaoEncerrada);
                break;
            case TipoResultadoAoVivo.Continuar:
            case TipoResultadoAoVivo.LimiteTaxa:
            case TipoResultadoAoVivo.Descartado:
                // O servidor respondeu: zera a contagem de "sem resposta". 401, 5xx, 404 sem código e rede não zeram.
                Interlocked.Exchange(ref _ultimoContatoTicks, _mono.Decorrido.Ticks);
                break;
        }
    }

    public async Task ExecutarAsync(CancellationToken ct)
    {
        using var ligado = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, ct);
        var token = ligado.Token;
        _log.LogInformation("[ao vivo {Sessao}] iniciando a transmissão de {Camera} (stream {Stream}, {Fps} q/s, até {Largura} px, {Segundos:0} s).",
            Sessao.SessaoId, Camera.Nome, Camera.Stream, Perfil.Fps, Perfil.LarguraMaxima, SegundosRestantes);

        var vigia = Task.Run(() => VigiarAsync(token), CancellationToken.None);
        var enviador = Task.Run(() => Enviador.ExecutarAsync(token), CancellationToken.None);
        (string Motivo, string Codigo, string? Detalhe)? falha = null;
        try
        {
            var recebeu = false;
            await foreach (var jpeg in _fonte.LerQuadrosAsync(Camera, Perfil, token).WithCancellation(token).ConfigureAwait(false))
            {
                recebeu = true;
                Enviador.Oferecer(jpeg, _relogio.Agora);
            }
            if (!token.IsCancellationRequested)
                falha = recebeu
                    ? (ClassificadorFalhaConexao.ConexaoEncerrada, ClassificadorFalhaConexao.CodConexaoEncerrada, null)
                    : (ClassificadorFalhaConexao.SemImagem, ClassificadorFalhaConexao.CodSemImagem, null);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // parada pedida (prazo, 410, configuração ou serviço)
        }
        catch (FfmpegIndisponivelException)
        {
            falha = (ClassificadorFalhaConexao.FfmpegAusente, ClassificadorFalhaConexao.CodFfmpegAusente, null);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            var (motivo, codigo) = ClassificadorFalhaConexao.ClassificarComCodigo(e.Message);
            falha = (motivo, codigo, ProcessadorTestesConexao.UltimaLinhaDaSaida(e.Message));
        }
        finally
        {
            // Garante o fim dos três: cancelar o token também derruba o ffmpeg filho (FfmpegFonteQuadros).
            if (ct.IsCancellationRequested) TentarParar(MotivoParadaAoVivo.ServicoParando);
            ligado.Cancel();
            try { await Task.WhenAll(vigia, enviador).ConfigureAwait(false); }
            catch (Exception e) when (e is not OutOfMemoryException) { /* só cancelamento */ }
        }

        if (falha is { } f && TentarParar(MotivoParadaAoVivo.Falha))
            await RelatarFalhaAsync(f.Motivo, f.Codigo, f.Detalhe, ct).ConfigureAwait(false);

        _terminou = true;
        _log.LogInformation("[ao vivo {Sessao}] transmissão encerrada: {Motivo}; {Enviados} quadro(s) enviado(s), {Descartados} descartado(s).",
            Sessao.SessaoId, Motivo, Enviador.Enviados, Enviador.Descartados);
    }

    private async Task RelatarFalhaAsync(string motivo, string codigo, string? detalhe, CancellationToken ct)
    {
        var senha = Camera.Senha;
        var motivoLimpo = ClassificadorFalhaConexao.Sanitizar(motivo, senha, _mascarador, MaximoMotivo);
        var detalheLimpo = string.IsNullOrWhiteSpace(detalhe) ? "" : " | " + ClassificadorFalhaConexao.Sanitizar(detalhe, senha, _mascarador, 200);
        _log.LogWarning("[ao vivo {Sessao}] {Camera}: {Motivo} ({Codigo}){Detalhe}", Sessao.SessaoId, Camera.Nome, motivoLimpo, codigo, detalheLimpo);
        try
        {
            if (!await _cliente.EnviarFalhaAsync(Sessao.SessaoId, codigo, motivoLimpo, ct).ConfigureAwait(false))
                _log.LogWarning("[ao vivo {Sessao}] a API não aceitou o aviso de falha; a sessão expira sozinha.", Sessao.SessaoId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // serviço parando
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.LogWarning("[ao vivo {Sessao}] não foi possível avisar a API da falha: {Erro}", Sessao.SessaoId, _mascarador.Mascarar(e.Message));
        }
    }

    private async Task VigiarAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await _mono.AtrasarAsync(PassoVigia, ct).ConfigureAwait(false);
                var agora = _mono.Decorrido;
                if (agora.Ticks >= Volatile.Read(ref _fimTicks))
                {
                    TentarParar(MotivoParadaAoVivo.PrazoEsgotado);
                    return;
                }
                if (agora - TimeSpan.FromTicks(Volatile.Read(ref _ultimoContatoTicks)) >= _prazoSemResposta)
                {
                    TentarParar(MotivoParadaAoVivo.ServidorSemResposta);
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // transmissão terminou
        }
    }
}
