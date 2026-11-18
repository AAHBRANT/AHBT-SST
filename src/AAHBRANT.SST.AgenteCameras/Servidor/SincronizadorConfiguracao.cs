using AAHBRANT.SST.AgenteCameras.AoVivo;
using AAHBRANT.SST.AgenteCameras.Envio;
using AAHBRANT.SST.AgenteCameras.Infra;
using AAHBRANT.SST.AgenteCameras.Opcoes;
using AAHBRANT.SST.AgenteCameras.Seguranca;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.AgenteCameras.Servidor;

// Consulta a configuração no servidor a cada intervalo (a consulta é o sinal de vida do agente), reconcilia as
// câmeras em execução e entrega os pedidos de teste de conexão ao processador.
//
// Câmeras desejadas = câmeras do arquivo local + câmeras do servidor; se a mesma cameraId vier das duas, o servidor
// manda. Servidor fora do ar, 401, 429 ou resposta ruim NÃO mexem em nada: o serviço continua com a última
// configuração conhecida (a da última consulta bem-sucedida ou, depois de um reinício, a do cache local).
// 401 segue a regra do envio de quadros: pausa os envios por 5 min (EstadoApi), sem apagar a fila e sem desligar câmeras.
// Exceção: depois de LimiteRespostas401 respostas 401 seguidas na consulta de configuração (agente revogado de verdade, não
// um tropeço), as câmeras vindas do servidor são paradas e o cache local delas (com as senhas) é apagado; a fila offline e as
// câmeras do arquivo local seguem a regra de sempre. Voltando a autenticar, a configuração do servidor é retomada.
public sealed class SincronizadorConfiguracao
{
    public static readonly TimeSpan PausaSegredoInvalido = EnviadorCamera.PausaSegredoInvalido;
    public const int LimiteRespostas401 = 5;
    private const int AvisarACadaNFalhas = 10;

    private readonly IClienteConfiguracao _cliente;
    private readonly ReconciliadorCameras _reconciliador;
    private readonly ProcessadorTestesConexao? _testes;
    private readonly CacheCameras? _cache;
    private readonly IReadOnlyList<CameraConfig> _locais;
    private readonly EstadoApi _estado;
    private readonly IRelogio _relogio;
    private readonly Mascarador _mascarador;
    private readonly ILogger _log;
    private readonly GerenciadorAoVivo? _aoVivo;

    private IReadOnlyList<SessaoAoVivoServidor> _sessoesAoVivo = Array.Empty<SessaoAoVivoServidor>();
    private IReadOnlyList<CameraConfig> _doServidor = Array.Empty<CameraConfig>();
    private TimeSpan _intervalo = TimeSpan.FromSeconds(ConversorServidor.IntervaloPadraoSegundos);
    private int _falhasSeguidas;
    private int _respostas401Seguidas;
    private bool _cacheCarregado;

    public SincronizadorConfiguracao(IClienteConfiguracao cliente, ReconciliadorCameras reconciliador, ProcessadorTestesConexao? testes,
        CacheCameras? cache, IReadOnlyList<CameraConfig> locais, EstadoApi estado, IRelogio relogio, Mascarador mascarador, ILogger log, GerenciadorAoVivo? aoVivo = null)
    {
        _cliente = cliente;
        _reconciliador = reconciliador;
        _testes = testes;
        _cache = cache;
        _locais = locais;
        _estado = estado;
        _relogio = relogio;
        _mascarador = mascarador;
        _log = log;
        _aoVivo = aoVivo;
    }

    /// <summary>Câmeras que vieram do servidor (ou do cache) e valem hoje.</summary>
    public IReadOnlyList<CameraConfig> DoServidor => _doServidor;

    public int FalhasSeguidas => _falhasSeguidas;

    public int Respostas401Seguidas => _respostas401Seguidas;

    public async Task ExecutarAsync(CancellationToken ct)
    {
        await AplicarCacheAsync().ConfigureAwait(false);
        while (!ct.IsCancellationRequested)
        {
            var espera = await PassoAsync(ct).ConfigureAwait(false);
            try { await _relogio.AtrasarAsync(espera, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Na partida: câmeras locais e, se houver, as do último cache do servidor (antes da primeira consulta).</summary>
    public async Task AplicarCacheAsync()
    {
        if (_cacheCarregado) return;
        _cacheCarregado = true;
        if (_cache is not null)
        {
            var doCache = _cache.Carregar();
            var validas = new List<CameraConfig>();
            foreach (var c in doCache)
            {
                _mascarador.Registrar(c.Senha);
                validas.Add(c);
            }
            _doServidor = validas;
            if (validas.Count > 0)
                _log.LogInformation("{N} câmera(s) do último cadastro conhecido no servidor foram carregadas do cache local (valem até a primeira consulta bem-sucedida).", validas.Count);
        }
        await AplicarAsync().ConfigureAwait(false);
    }

    /// <summary>Uma consulta ao servidor. Devolve quanto esperar até a próxima.</summary>
    public async Task<TimeSpan> PassoAsync(CancellationToken ct)
    {
        ResultadoConsulta r;
        try
        {
            r = await _cliente.ConsultarAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }

        switch (r.Tipo)
        {
            case TipoConsulta.Sucesso:
                if (_falhasSeguidas > 0)
                    _log.LogInformation("Contato com o servidor restabelecido depois de {N} falha(s) seguida(s).", _falhasSeguidas);
                _falhasSeguidas = 0;
                _respostas401Seguidas = 0;
                _intervalo = ConversorServidor.IntervaloConsulta(r.Configuracao!.IntervaloConsultaSegundos);
                await AplicarConfiguracaoAsync(r.Configuracao!).ConfigureAwait(false);
                _testes?.Aceitar(r.Configuracao!.TestesConexao ?? new List<TesteServidor>());
                return _intervalo;

            case TipoConsulta.SegredoInvalido:
                _falhasSeguidas++;
                _estado.PausarAte(_relogio.Agora + PausaSegredoInvalido);
                _log.LogError("HTTP 401: a API recusou o segredo do agente ao consultar a configuração (segredo inválido ou agente revogado). Envios pausados por {Minutos} min e a última configuração conhecida continua valendo; confira AgenteCameras:Segredo e o cadastro do agente.",
                    PausaSegredoInvalido.TotalMinutes);
                if (++_respostas401Seguidas >= LimiteRespostas401 && (_doServidor.Count > 0 || _cache is not null))
                    await DesligarCamerasDoServidorAsync().ConfigureAwait(false);
                return PausaSegredoInvalido;

            case TipoConsulta.LimiteTaxa:
                _falhasSeguidas++;
                var esperar = r.EsperarPor ?? _intervalo;
                _log.LogWarning("Consulta da configuração: HTTP 429 (limite de requisições); nova tentativa em {Segundos:0} s.", esperar.TotalSeconds);
                return esperar;

            default:
                _falhasSeguidas++;
                if (_falhasSeguidas == 1 || _falhasSeguidas % AvisarACadaNFalhas == 0)
                    _log.LogWarning("Não foi possível consultar a configuração no servidor ({Detalhe}); o serviço segue com a última configuração conhecida ({N} câmera(s) do servidor). Falhas seguidas: {Falhas}.",
                        _mascarador.Mascarar(r.Detalhe), _doServidor.Count, _falhasSeguidas);
                return _intervalo;
        }
    }

    private async Task DesligarCamerasDoServidorAsync()
    {
        var havia = _doServidor.Count;
        _doServidor = Array.Empty<CameraConfig>();
        _sessoesAoVivo = Array.Empty<SessaoAoVivoServidor>(); // agente revogado: nada de câmera ao vivo
        _cache?.Apagar();
        if (havia > 0)
        {
            _log.LogError("O servidor recusou o segredo do agente {N} vezes seguidas (agente revogado ou segredo trocado): as {Cameras} câmera(s) do servidor foram paradas e o cache local delas foi apagado. A fila de quadros e as câmeras do arquivo local seguem como antes; quando o servidor voltar a aceitar o segredo, a configuração é retomada sozinha.",
                _respostas401Seguidas, havia);
            await AplicarAsync().ConfigureAwait(false);
        }
    }

    private async Task AplicarConfiguracaoAsync(ConfiguracaoServidor config)
    {
        var lista = new List<CameraConfig>();
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in config.Cameras ?? new List<CameraServidor>())
        {
            var conv = ConversorServidor.Converter(c, _mascarador, out var problema);
            if (conv is null)
            {
                _log.LogWarning("Câmera {CameraId} do servidor ignorada: {Problema}.", c.CameraId, problema);
                continue;
            }
            if (!vistos.Add(conv.CameraId)) continue;
            lista.Add(conv);
        }
        _doServidor = lista;
        _sessoesAoVivo = config.AoVivo ?? new List<SessaoAoVivoServidor>(); // campo ausente (API antiga) = nenhuma
        _cache?.Salvar(lista);
        await AplicarAsync().ConfigureAwait(false);
    }

    private async Task AplicarAsync()
    {
        var desejadas = new Dictionary<string, CameraConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in _locais) desejadas[c.CameraId] = c;
        foreach (var c in _doServidor) desejadas[c.CameraId] = c; // o servidor manda

        var resumo = await _reconciliador.ReconciliarAsync(desejadas.Values).ConfigureAwait(false);
        if (resumo.HouveMudanca)
            _log.LogInformation("Câmeras reconciliadas com o servidor: {I} iniciada(s), {P} parada(s), {R} reiniciada(s), {M} sem mudança.",
                resumo.Iniciadas.Count, resumo.Paradas.Count, resumo.Reiniciadas.Count, resumo.Mantidas.Count);

        await AplicarAoVivoAsync(desejadas).ConfigureAwait(false);
    }

    // As transmissões ao vivo são reconciliadas DEPOIS das câmeras e isoladas: qualquer erro aqui é registrado e não
    // afeta a análise nem a consulta de configuração. Sessões não são guardadas em cache (o prazo é relativo).
    private async Task AplicarAoVivoAsync(IReadOnlyDictionary<string, CameraConfig> cameras)
    {
        if (_aoVivo is null) return;
        try
        {
            var r = await _aoVivo.ReconciliarAsync(_sessoesAoVivo, cameras).ConfigureAwait(false);
            if (r.HouveMudanca)
                _log.LogInformation("Transmissões ao vivo reconciliadas: {I} iniciada(s), {P} parada(s), {R} reiniciada(s), {M} sem mudança.",
                    r.Iniciadas.Count, r.Paradas.Count, r.Reiniciadas.Count, r.Mantidas.Count);
        }
        catch (Exception e) when (e is not OutOfMemoryException and not OperationCanceledException)
        {
            _log.LogError("Não foi possível reconciliar as transmissões ao vivo: {Erro}", _mascarador.Mascarar(e.Message));
        }
    }
}
