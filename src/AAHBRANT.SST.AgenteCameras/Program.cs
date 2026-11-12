using AAHBRANT.SST.AgenteCameras.Infra;
using AAHBRANT.SST.AgenteCameras.Opcoes;
using AAHBRANT.SST.AgenteCameras.Seguranca;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;
using Serilog;

namespace AAHBRANT.SST.AgenteCameras;

public static class Program
{
    public const string NomeServico = "AAHBRANT.SST.AgenteCameras";

    // Arquivo de configuração local (não versionado): segredos e câmeras deste PC. Em %ProgramData% para a conta
    // do serviço poder gravá-lo (a pasta de instalação é só leitura para ela). Variável de ambiente sobrescreve.
    public static string CaminhoConfiguracaoLocal() =>
        Environment.GetEnvironmentVariable("AAHBRANT_AGENTECAMERAS_CONFIG") is { Length: > 0 } c
            ? c
            : Path.Combine(ValidadorConfiguracao.PastaDadosPadrao(), "appsettings.local.json");

    public static async Task<int> Main(string[] args)
    {
        var baseDir = AppContext.BaseDirectory;
        var protetor = new ProtetorDpapi();

        var arquivos = new[]
        {
            Path.Combine(baseDir, "appsettings.json"),
            Path.Combine(baseDir, "appsettings.local.json"),
            CaminhoConfiguracaoLocal(),
        };

        // 1) Segredos escritos em texto são regravados protegidos (DPAPI) antes de a configuração ser lida.
        var informacoes = new List<string>();
        var avisos = new List<string>();
        foreach (var arquivo in arquivos.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var n = SegredosConfiguracao.ProtegerArquivo(arquivo, protetor);
                if (n > 0) informacoes.Add($"{n} segredo(s) em \"{arquivo}\" foram protegidos com DPAPI (escopo da máquina).");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                avisos.Add($"Não foi possível proteger os segredos de \"{arquivo}\": {e.Message}. Eles continuam em texto nesse arquivo.");
            }
        }

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = baseDir });
        builder.Configuration.AddJsonFile(arquivos[1], optional: true, reloadOnChange: false);
        builder.Configuration.AddJsonFile(arquivos[2], optional: true, reloadOnChange: false);
        builder.Configuration.AddEnvironmentVariables(); // variáveis e argumentos continuam valendo por cima dos arquivos
        builder.Configuration.AddCommandLine(args);

        var opcoes = builder.Configuration.GetSection(AgenteCamerasOptions.Secao).Get<AgenteCamerasOptions>() ?? new AgenteCamerasOptions();
        var resultado = ValidadorConfiguracao.Validar(opcoes, protetor);
        var pastaDados = resultado.Configuracao?.PastaDados
            ?? (string.IsNullOrWhiteSpace(opcoes.PastaDados) ? ValidadorConfiguracao.PastaDadosPadrao() : opcoes.PastaDados);

        var mascarador = new Mascarador();
        if (resultado.Configuracao is { } c)
        {
            mascarador.Registrar(c.Segredo);
            foreach (var cam in c.Cameras) mascarador.Registrar(cam.Senha);
        }

        // 2) Logs: arquivo com rotação em PastaDados\logs (sempre) + console (modo console) + Event Log (modo serviço, só Warning+).
        var serilog = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(pastaDados, "logs", "agente-cameras-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 20 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(serilog, dispose: true);
        if (!WindowsServiceHelpers.IsWindowsService())
            builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });
        builder.Logging.SetMinimumLevel(LogLevel.Information);
        builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
        builder.Logging.AddFilter<EventLogLoggerProvider>(level => level >= LogLevel.Warning);

        builder.Services.AddWindowsService(o => o.ServiceName = NomeServico);
        builder.Services.AddSingleton(resultado);
        builder.Services.AddSingleton(new MensagensDePartida(informacoes, avisos));
        builder.Services.AddSingleton(mascarador);
        builder.Services.AddSingleton<IProtetorDados>(protetor);
        builder.Services.AddSingleton<IRelogio, RelogioSistema>();
        builder.Services.AddHostedService<AgenteCamerasWorker>();

        using var host = builder.Build();
        await host.RunAsync().ConfigureAwait(false);
        return Environment.ExitCode;
    }
}
