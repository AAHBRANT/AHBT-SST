using AAHBRANT.SST.AgenteCameras.Infra;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.AgenteCameras.Fila;

// Manutenção da pasta da fila independente das câmeras: roda na partida e a cada 10 min, mesmo sem nenhuma câmera
// ativa, com câmera pausada (401/404) ou desabilitada. Garante que imagens não fiquem em disco além do limite de
// idade (24 h por padrão), que ".tmp" órfãos sumam e que o tamanho total respeite o limite.
public sealed class LimpezaPeriodica
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(10);

    private readonly FilaQuadros _fila;
    private readonly IRelogio _relogio;
    private readonly ILogger _log;

    public LimpezaPeriodica(FilaQuadros fila, IRelogio relogio, ILogger log)
    {
        _fila = fila;
        _relogio = relogio;
        _log = log;
    }

    public int Execucoes { get; private set; }

    public async Task ExecutarAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            ExecutarUmaVez();
            try { await _relogio.AtrasarAsync(Intervalo, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    public ResultadoLimpeza? ExecutarUmaVez()
    {
        Execucoes++;
        try
        {
            var r = _fila.Limpar();
            _log.LogDebug("Limpeza da fila: {Expirados} vencido(s), {Tmp} temporário(s), {Excedente} por tamanho; {Mb:0.#} MB em disco.",
                r.Expirados, r.TemporariosApagados, r.ExcedenteTamanho, r.TamanhoBytes / (1024.0 * 1024.0));
            return r;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // Nunca derruba o laço: tenta de novo na próxima rodada.
            _log.LogError("Falha na limpeza da fila em disco ({Tipo}): {Erro}", e.GetType().Name, e.Message);
            return null;
        }
    }
}
