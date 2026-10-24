using System.Diagnostics;
using System.Net.Http.Json;
using AAHBRANT.SST.AgenteBiometria.Opcoes;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.AgenteBiometria.Atualizacao;

// Verifica de tempos em tempos se há versão nova do agente. Achando, baixa o Setup, confere assinatura do
// manifesto e SHA-256 do arquivo, e entrega a troca para o próprio Setup (--atualizar), que espera este
// processo sair, instala, sobe a versão nova e volta para a anterior se ela não responder.
public class AtualizacaoAgenteService : BackgroundService
{
    private static readonly TimeSpan EsperaInicial = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan TempoParadoMinimo = TimeSpan.FromMinutes(3);

    private readonly HttpClient _http;
    private readonly AgenteOptions _options;
    private readonly AtividadeAgente _atividade;
    private readonly ILogger<AtualizacaoAgenteService> _logger;

    // Ponto de troca para os testes: em produção encerra o agente (ver Program.Main).
    public Action AoIniciarAtualizacao { get; set; } = () => Application.Exit();

    public AtualizacaoAgenteService(HttpClient http, IOptions<AgenteOptions> options, AtividadeAgente atividade,
        ILogger<AtualizacaoAgenteService> logger)
    {
        _http = http;
        _options = options.Value;
        _atividade = atividade;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.UrlManifestoAtualizacao))
        {
            _logger.LogInformation("Atualização automática desligada: Agente:UrlManifestoAtualizacao vazio.");
            return;
        }
        var chave = VerificadorAtualizacao.ChavePublicaEmbutida();
        if (chave is null)
        {
            _logger.LogWarning("Atualização automática desligada: este build não tem a chave pública de atualização.");
            return;
        }

        var intervalo = TimeSpan.FromHours(Math.Clamp(_options.IntervaloVerificacaoAtualizacaoHoras, 1, 168));
        try
        {
            await Task.Delay(EsperaInicial, ct);
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (await VerificarEAplicarAsync(chave, ct))
                        return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Falha de rede ou de servidor: tenta de novo no próximo ciclo, o agente continua funcionando.
                    _logger.LogWarning(ex, "Falha ao verificar atualização do agente.");
                }
                await Task.Delay(intervalo, ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    // true = atualização entregue ao Setup (o agente vai sair).
    public async Task<bool> VerificarEAplicarAsync(string chavePublicaPem, CancellationToken ct)
    {
        var manifesto = await _http.GetFromJsonAsync<ManifestoAtualizacao>(_options.UrlManifestoAtualizacao, ct);
        if (manifesto is null)
            return false;

        if (!VerificadorAtualizacao.AssinaturaValida(manifesto, chavePublicaPem))
        {
            _logger.LogError("Manifesto de atualização com assinatura inválida. Ignorado.");
            return false;
        }
        if (!VerificadorAtualizacao.EhMaisNova(manifesto.Versao, VerificadorAtualizacao.VersaoAtual()))
            return false;
        if (!Uri.TryCreate(manifesto.UrlSetup, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps)
        {
            _logger.LogError("Manifesto de atualização com URL que não é HTTPS. Ignorado.");
            return false;
        }

        // Espera o quiosque ficar parado: nunca troca de versão no meio de uma leitura.
        while (_atividade.TempoParado < TempoParadoMinimo)
            await Task.Delay(TimeSpan.FromSeconds(30), ct);

        var setup = await _http.GetByteArrayAsync(url, ct);
        if (!VerificadorAtualizacao.Sha256Confere(setup, manifesto.Sha256))
        {
            _logger.LogError("Setup baixado não confere com o SHA-256 do manifesto. Ignorado.");
            return false;
        }

        var pasta = Path.Combine(Path.GetTempPath(), "AAHBRANT-AgenteBiometria-atualizacao");
        Directory.CreateDirectory(pasta);
        var caminho = Path.Combine(pasta, $"Setup-AgenteBiometria-{manifesto.Versao}.exe");
        await File.WriteAllBytesAsync(caminho, setup, ct);

        _logger.LogInformation("Atualizando o agente para a versão {Versao}.", manifesto.Versao);
        Process.Start(new ProcessStartInfo(caminho, $"--atualizar --pid {Environment.ProcessId}") { UseShellExecute = false });
        AoIniciarAtualizacao();
        return true;
    }
}
