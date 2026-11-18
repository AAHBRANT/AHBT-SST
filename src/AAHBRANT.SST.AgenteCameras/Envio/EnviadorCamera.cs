using AAHBRANT.SST.AgenteCameras.Fila;
using AAHBRANT.SST.AgenteCameras.Infra;
using AAHBRANT.SST.AgenteCameras.Opcoes;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.AgenteCameras.Envio;

// Pausa compartilhada entre as câmeras do agente: o segredo é um só, então 401 vale para todas.
public sealed class EstadoApi
{
    private long _pausadoAteTicks;

    public DateTimeOffset PausadoAte
    {
        get => new(Interlocked.Read(ref _pausadoAteTicks), TimeSpan.Zero);
    }

    public void PausarAte(DateTimeOffset ate) => Interlocked.Exchange(ref _pausadoAteTicks, ate.UtcTicks);
}

// Um "enviador" por câmera: um único trabalhador sequencial, portanto nunca há dois envios simultâneos da
// mesma câmera. Prioridade: o quadro ao vivo (alerta em tempo real) vai antes da fila; a fila só é drenada
// entre os quadros ao vivo, no ritmo de um envio por IntervaloEnvio, com o horário original de captura.
// Só existe um "quadro ao vivo pendente" por vez (o mais recente) enquanto um envio está em andamento: o
// anterior, se ainda não saiu, é substituído (cena já mudou; o alerta seria velho). Sem internet, o quadro
// vai para a fila em disco em vez de se perder.
//
// Recuperação depois de uma queda: passada a espera, a SONDAGEM é feita com o quadro ao vivo mais recente (não com o
// item mais antigo da fila). Se ele passa, a API está de pé: a fila drena intercalada com os quadros ao vivo. Só se
// a câmera ficar sem quadros ao vivo por EsperaSondagemSemVivo é que o item mais antigo da fila faz a sondagem.
// Um item da fila que falha com 5xx FalhasParaDescartarItem vezes seguidas enquanto envios ao vivo passam é
// descartado (item "envenenado"); sem isso ele travaria a fila e o modo offline por até 24 h.
public sealed class EnviadorCamera
{
    public static readonly TimeSpan PausaSegredoInvalido = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan PausaCameraNaoPertence = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan EsperaSondagemSemVivo = TimeSpan.FromSeconds(15);
    public const int FalhasParaDescartarItem = 5;
    private static readonly TimeSpan Espera429Padrao = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Espera429Maxima = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan EsperaRedeMaxima = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan Ocioso = TimeSpan.FromSeconds(30);

    private readonly CameraConfig _camera;
    private readonly IClienteQuadros _cliente;
    private readonly FilaQuadros _fila;
    private readonly EstadoApi _estado;
    private readonly IRelogio _relogio;
    private readonly ILogger _log;
    private readonly object _trava = new();
    private readonly SemaphoreSlim _sinal = new(0);

    private (byte[] Jpeg, DateTimeOffset Em)? _pendente;
    private bool _offline;
    private DateTimeOffset _bloqueadoAte = DateTimeOffset.MinValue;
    private DateTimeOffset _filaBloqueadaAte = DateTimeOffset.MinValue;
    private DateTimeOffset _cameraPausadaAte = DateTimeOffset.MinValue;
    private DateTimeOffset _ultimoEnvioInicio = DateTimeOffset.MinValue;
    private int _falhasRede;
    private int _falhas429;

    // Item da fila com falha persistente (5xx) enquanto a API atende os quadros ao vivo.
    private string? _itemFalhoCaminho;
    private int _falhasItem;
    private bool _vivoPassouDesdePrimeiraFalhaDoItem;

    public EnviadorCamera(CameraConfig camera, IClienteQuadros cliente, FilaQuadros fila, EstadoApi estado,
        IRelogio relogio, ILogger log)
    {
        _camera = camera;
        _cliente = cliente;
        _fila = fila;
        _estado = estado;
        _relogio = relogio;
        _log = log;
    }

    public bool Offline => _offline;

    /// <summary>
    /// 404 "camera_nao_encontrada": a câmera não pertence à obra do agente. Fica pausada até este instante e
    /// depois tenta de novo (o cadastro pode ser corrigido no servidor sem reiniciar o agente).
    /// </summary>
    public DateTimeOffset CameraPausadaAte => _cameraPausadaAte;

    /// <summary>Chamado pelo leitor quando a política decidiu enviar este quadro. Não bloqueia.</summary>
    public void OferecerVivo(byte[] jpeg, DateTimeOffset capturadaEm)
    {
        var agora = _relogio.Agora;
        if (_estado.PausadoAte > agora)
        {
            _log.LogDebug("[{Camera}] quadro descartado: envio pausado (segredo recusado pela API).", _camera.Nome);
            return;
        }
        if (_cameraPausadaAte > agora)
        {
            _log.LogDebug("[{Camera}] quadro descartado: câmera pausada (a API não a reconhece neste agente).", _camera.Nome);
            return;
        }

        // Em modo offline com a janela de espera ainda aberta, o quadro vai direto para o disco: preserva a ordem
        // de captura e não martela a API. Passada a espera ele vira a sondagem (mesmo com a fila cheia).
        if (_offline && agora < _bloqueadoAte)
        {
            Guardar(jpeg, capturadaEm);
            _sinal.Release();
            return;
        }

        lock (_trava)
        {
            if (_pendente is not null)
                _log.LogDebug("[{Camera}] quadro anterior ainda aguardava envio; substituído pelo mais recente.", _camera.Nome);
            _pendente = (jpeg, capturadaEm);
        }
        if (_sinal.CurrentCount == 0) _sinal.Release();
    }

    public async Task ExecutarAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TimeSpan? espera;
            try
            {
                espera = await PassoAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                _log.LogError(e, "[{Camera}] erro inesperado no envio; nova tentativa em 5 s.", _camera.Nome);
                espera = TimeSpan.FromSeconds(5);
            }

            if (espera is { } t)
            {
                try { await _sinal.WaitAsync(t, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    /// <summary>
    /// Uma rodada do trabalhador. Devolve null se trabalhou (pode haver mais) ou quanto esperar antes da
    /// próxima rodada (a espera é interrompida quando chega quadro novo).
    /// </summary>
    public async Task<TimeSpan?> PassoAsync(CancellationToken ct)
    {
        var agora = _relogio.Agora;

        if (_estado.PausadoAte > agora) return Limitar(_estado.PausadoAte - agora);
        if (_cameraPausadaAte > agora) return Limitar(_cameraPausadaAte - agora);
        if (_bloqueadoAte > agora) return Limitar(_bloqueadoAte - agora);

        (byte[] Jpeg, DateTimeOffset Em)? vivo;
        lock (_trava)
        {
            vivo = _pendente;
            _pendente = null;
        }

        ItemFila? item = null;
        if (vivo is null)
        {
            // Ritmo da drenagem: no máximo um envio por IntervaloEnvio (respeita o rate limit da API).
            var proximo = _ultimoEnvioInicio + _camera.IntervaloEnvio;
            if (proximo > agora) return Limitar(proximo - agora);

            if (_offline)
            {
                // Aguarda um quadro ao vivo para sondar; só sem ele o item da fila faz a sondagem.
                var liberaFila = _bloqueadoAte + EsperaSondagemSemVivo;
                if (liberaFila > agora) return Limitar(liberaFila - agora);
            }
            else if (_filaBloqueadaAte > agora)
            {
                return Limitar(_filaBloqueadaAte - agora);
            }

            item = _fila.ObterMaisAntigo(_camera.CameraId);
            if (item is null) return Ocioso;
        }

        var jpeg = vivo?.Jpeg ?? item!.Jpeg;
        var capturadaEm = vivo?.Em ?? item!.CapturadaEm;
        _ultimoEnvioInicio = agora;

        var resultado = await _cliente.EnviarAsync(_camera.CameraId, jpeg, capturadaEm, ct).ConfigureAwait(false);
        Tratar(resultado, item, jpeg, capturadaEm);
        return null;
    }

    private void Tratar(ResultadoEnvio r, ItemFila? item, byte[] jpeg, DateTimeOffset capturadaEm)
    {
        var origem = item is null ? "ao vivo" : $"fila ({capturadaEm:dd/MM HH:mm:ss})";
        var agora = _relogio.Agora;
        switch (r.Tipo)
        {
            case TipoResultado.Sucesso:
                _offline = false;
                _falhasRede = 0;
                _falhas429 = 0;
                if (item is not null)
                {
                    _fila.Remover(item);
                    ZerarFalhasDoItem();
                }
                else
                {
                    _vivoPassouDesdePrimeiraFalhaDoItem = true;
                }
                _log.LogInformation("[{Camera}] {Origem}: HTTP {Status} {Resumo}", _camera.Nome, origem, r.StatusHttp, Resumir(r.Resposta));
                break;

            case TipoResultado.Descartado:
                _offline = false;
                if (item is not null)
                {
                    _fila.Remover(item);
                    ZerarFalhasDoItem();
                }
                _log.LogWarning("[{Camera}] {Origem}: HTTP {Status} {Detalhe}; quadro descartado.", _camera.Nome, origem, r.StatusHttp, r.Detalhe);
                break;

            case TipoResultado.CameraNaoPertence:
                // Não desativa para sempre nem apaga a fila: o cadastro pode ser corrigido no servidor. Pausa e
                // tenta de novo em intervalos longos; os quadros em disco expiram sozinhos (LimpezaPeriodica).
                _cameraPausadaAte = agora + PausaCameraNaoPertence;
                _log.LogError("[{Camera}] HTTP 404 (camera_nao_encontrada): a câmera {CameraId} não pertence à obra deste agente. Câmera PAUSADA; nova tentativa em {Minutos:0} min (quadros ao vivo são descartados nesse período; os da fila em disco são mantidos e expiram sozinhos). Confira CameraId na configuração e o cadastro da câmera no SST-APP.",
                    _camera.Nome, _camera.CameraId, PausaCameraNaoPertence.TotalMinutes);
                break;

            case TipoResultado.SegredoInvalido:
                _estado.PausarAte(agora + PausaSegredoInvalido);
                _log.LogError("[{Camera}] HTTP 401: a API recusou o segredo do agente (segredo inválido ou agente revogado). Envios pausados por {Minutos} min; confira AgenteCameras:Segredo e o cadastro do agente.",
                    _camera.Nome, PausaSegredoInvalido.TotalMinutes);
                break; // quadro ao vivo se perde; o da fila continua no disco

            case TipoResultado.LimiteTaxa:
                _falhas429++;
                var espera429 = r.EsperarPor ?? TimeSpan.FromTicks(Math.Min(Espera429Maxima.Ticks, Espera429Padrao.Ticks * (1L << Math.Min(_falhas429 - 1, 4))));
                _bloqueadoAte = agora + espera429;
                if (item is null) Guardar(jpeg, capturadaEm);
                _log.LogWarning("[{Camera}] {Origem}: HTTP 429 (limite de requisições); nova tentativa em {Segundos:0} s.", _camera.Nome, origem, espera429.TotalSeconds);
                break;

            case TipoResultado.SemInternet:
                if (item is not null && !_offline && EhFalhaDoServidor(r.StatusHttp))
                    TratarFalhaDoItemComApiNoAr(r, item, jpeg, capturadaEm, origem, agora);
                else
                    RegistrarIndisponibilidade(r, item, jpeg, capturadaEm, origem, agora);
                break;
        }
    }

    // A API respondeu o quadro ao vivo há pouco e o item da fila falhou com erro de servidor: pode ser o item.
    private void TratarFalhaDoItemComApiNoAr(ResultadoEnvio r, ItemFila item, byte[] jpeg, DateTimeOffset capturadaEm, string origem, DateTimeOffset agora)
    {
        if (_itemFalhoCaminho != item.Caminho)
        {
            _itemFalhoCaminho = item.Caminho;
            _falhasItem = 0;
            _vivoPassouDesdePrimeiraFalhaDoItem = false;
        }
        _falhasItem++;

        if (_falhasItem >= FalhasParaDescartarItem)
        {
            if (_vivoPassouDesdePrimeiraFalhaDoItem)
            {
                _fila.Remover(item);
                _log.LogWarning("[{Camera}] {Origem}: o quadro falhou {N} vezes seguidas (HTTP {Status}) enquanto os envios ao vivo seguem normais; quadro descartado para não travar a fila.",
                    _camera.Nome, origem, _falhasItem, r.StatusHttp);
                ZerarFalhasDoItem();
                return;
            }
            // Sem prova de que a API atende outros quadros, o problema provavelmente é a API: modo offline normal.
            RegistrarIndisponibilidade(r, item, jpeg, capturadaEm, origem, agora);
            return;
        }

        var espera = TimeSpan.FromTicks(Math.Min(EsperaRedeMaxima.Ticks, TimeSpan.FromSeconds(5).Ticks * (1L << Math.Min(_falhasItem - 1, 4))));
        _filaBloqueadaAte = agora + espera;
        _log.LogWarning("[{Camera}] {Origem}: HTTP {Status} ({Detalhe}); falha {N} de {Max} deste quadro; nova tentativa da fila em {Segundos:0} s.",
            _camera.Nome, origem, r.StatusHttp, r.Detalhe, _falhasItem, FalhasParaDescartarItem, espera.TotalSeconds);
    }

    private void RegistrarIndisponibilidade(ResultadoEnvio r, ItemFila? item, byte[] jpeg, DateTimeOffset capturadaEm, string origem, DateTimeOffset agora)
    {
        ZerarFalhasDoItem();
        var jaEstavaOffline = _offline;
        _offline = true;
        _falhasRede++;
        _bloqueadoAte = agora + TimeSpan.FromTicks(Math.Min(EsperaRedeMaxima.Ticks, TimeSpan.FromSeconds(5).Ticks * (1L << Math.Min(_falhasRede - 1, 4))));
        if (item is null) Guardar(jpeg, capturadaEm);
        // O quadro que chegou enquanto este envio estava em andamento também vai para o disco.
        (byte[] Jpeg, DateTimeOffset Em)? sobra;
        lock (_trava)
        {
            sobra = _pendente;
            _pendente = null;
        }
        if (sobra is { } s) Guardar(s.Jpeg, s.Em);
        if (!jaEstavaOffline)
            _log.LogWarning("[{Camera}] {Origem}: sem comunicação com a API ({Detalhe}). Quadros serão guardados em disco e enviados quando voltar.", _camera.Nome, origem, r.Detalhe);
        else
            _log.LogDebug("[{Camera}] API ainda indisponível ({Detalhe}).", _camera.Nome, r.Detalhe);
    }

    private void ZerarFalhasDoItem()
    {
        _itemFalhoCaminho = null;
        _falhasItem = 0;
        _vivoPassouDesdePrimeiraFalhaDoItem = false;
        _filaBloqueadaAte = DateTimeOffset.MinValue;
    }

    private static bool EhFalhaDoServidor(int? status) => status is >= 500 or 409;

    private void Guardar(byte[] jpeg, DateTimeOffset capturadaEm)
    {
        try
        {
            _fila.Adicionar(_camera.CameraId, jpeg, capturadaEm);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _log.LogError("[{Camera}] não foi possível guardar o quadro na fila em disco: {Erro}", _camera.Nome, e.Message);
        }
    }

    private static string Resumir(RespostaQuadro? r) => r is null
        ? "(sem resumo)"
        : $"situacao={r.Situacao} pessoas={r.Pessoas} semCapacete={r.SemCapacete} semColete={r.SemColete} alerta={r.AlertaEnviado} repetida={r.Repetida} identificado={r.TrabalhadorId is not null}";

    private static TimeSpan Limitar(TimeSpan t) =>
        t < TimeSpan.FromMilliseconds(100) ? TimeSpan.FromMilliseconds(100) : t > TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : t;
}
