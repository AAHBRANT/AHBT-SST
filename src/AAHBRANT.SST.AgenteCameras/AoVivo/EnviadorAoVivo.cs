using AAHBRANT.SST.AgenteCameras.Envio;
using AAHBRANT.SST.AgenteCameras.Infra;
using AAHBRANT.SST.AgenteCameras.Seguranca;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.AgenteCameras.AoVivo;

// Envia os quadros de UMA sessão ao vivo. Guarda SÓ o quadro mais recente: se a rede está lenta, o anterior que ainda
// não saiu é descartado (ao vivo não tem sentido atrasado). Um envio por vez. Sem fila em disco e sem relação com a
// FilaQuadros da análise: nada do que passa aqui é gravado.
public sealed class EnviadorAoVivo
{
    /// <summary>Tamanho máximo aceito pela API para um quadro ao vivo. Maior que isso é descartado (não é reencodado).</summary>
    public const int LimiteBytes = 300 * 1024;
    public static readonly TimeSpan EsperaSegredoInvalido = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan EsperaLimiteTaxaPadrao = TimeSpan.FromSeconds(2);

    private readonly Guid _sessaoId;
    private readonly IClienteAoVivo _cliente;
    private readonly IRelogioMonotonico _espera;
    private readonly Mascarador _mascarador;
    private readonly ILogger _log;
    private readonly Action<ResultadoQuadroAoVivo> _aoResultado;
    private readonly SemaphoreSlim _sinal = new(0, 1);
    private readonly object _trava = new();
    private (byte[] Jpeg, DateTimeOffset Em)? _pendente;
    private int _enviados;
    private int _descartados;
    private bool _falhando;

    public EnviadorAoVivo(Guid sessaoId, IClienteAoVivo cliente, IRelogioMonotonico espera,
        Mascarador mascarador, ILogger log, Action<ResultadoQuadroAoVivo> aoResultado)
    {
        _sessaoId = sessaoId;
        _cliente = cliente;
        _espera = espera;
        _mascarador = mascarador;
        _log = log;
        _aoResultado = aoResultado;
    }

    /// <summary>Quadros aceitos pela API.</summary>
    public int Enviados => Volatile.Read(ref _enviados);

    /// <summary>Quadros jogados fora (substituídos por outro mais novo antes de sair, ou grandes demais).</summary>
    public int Descartados => Volatile.Read(ref _descartados);

    /// <summary>Entrega um quadro novo. Se o anterior ainda não saiu, ele é substituído.</summary>
    public void Oferecer(byte[] jpeg, DateTimeOffset capturadoEm)
    {
        if (jpeg.Length == 0) return;
        if (jpeg.Length > LimiteBytes)
        {
            Interlocked.Increment(ref _descartados);
            return;
        }
        lock (_trava)
        {
            if (_pendente is not null) Interlocked.Increment(ref _descartados);
            _pendente = (jpeg, capturadoEm);
        }
        // Semáforo binário: se já há sinal pendente, o laço pega o quadro novo na mesma volta.
        try { _sinal.Release(); } catch (SemaphoreFullException) { }
    }

    public async Task ExecutarAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await _sinal.WaitAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }

            (byte[] Jpeg, DateTimeOffset Em)? quadro;
            lock (_trava)
            {
                quadro = _pendente;
                _pendente = null;
            }
            if (quadro is null) continue;

            ResultadoQuadroAoVivo r;
            try
            {
                r = await _cliente.EnviarQuadroAsync(_sessaoId, quadro.Value.Jpeg, quadro.Value.Em, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                r = ResultadoQuadroAoVivo.SemContato(_mascarador.Mascarar(e.Message));
            }

            Registrar(r);
            try { _aoResultado(r); }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                _log.LogError("[ao vivo {Sessao}] erro ao tratar a resposta do servidor: {Erro}", _sessaoId, _mascarador.Mascarar(e.Message));
            }

            var pausa = r.Tipo switch
            {
                TipoResultadoAoVivo.LimiteTaxa => r.EsperarPor ?? EsperaLimiteTaxaPadrao,
                TipoResultadoAoVivo.SegredoInvalido => EsperaSegredoInvalido,
                _ => TimeSpan.Zero,
            };
            if (pausa > TimeSpan.Zero)
            {
                try { await _espera.AtrasarAsync(pausa, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    // Loga só as mudanças de estado (primeiro quadro e queda/volta); um log por quadro encheria o arquivo.
    private void Registrar(ResultadoQuadroAoVivo r)
    {
        switch (r.Tipo)
        {
            case TipoResultadoAoVivo.Continuar:
                if (Interlocked.Increment(ref _enviados) == 1)
                    _log.LogInformation("[ao vivo {Sessao}] primeiro quadro entregue à API.", _sessaoId);
                else if (_falhando)
                    _log.LogInformation("[ao vivo {Sessao}] envio de quadros restabelecido.", _sessaoId);
                _falhando = false;
                break;
            case TipoResultadoAoVivo.Encerrada:
                _log.LogInformation("[ao vivo {Sessao}] a API encerrou a sessão ({Detalhe}).", _sessaoId, r.Detalhe);
                break;
            default:
                if (!_falhando)
                    _log.LogWarning("[ao vivo {Sessao}] envio de quadro não concluído: {Detalhe}.", _sessaoId, _mascarador.Mascarar(r.Detalhe));
                _falhando = true;
                break;
        }
    }
}
