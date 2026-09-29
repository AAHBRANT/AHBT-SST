using AAHBRANT.SST.AgenteBiometria.Endpoints;
using AAHBRANT.SST.AgenteBiometria.Leitores;
using AAHBRANT.SST.AgenteBiometria.Opcoes;
using AAHBRANT.SST.AgenteBiometria.Servicos;
using AAHBRANT.SST.AgenteBiometria.Tray;

namespace AAHBRANT.SST.AgenteBiometria;

public static class Program
{
    private const string PoliticaCorsKiosk = "KioskOrigin";

    [STAThread]
    public static void Main(string[] args)
    {
        var app = CriarApp(args);
        _ = app.RunAsync();

        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.Run(new TrayApplicationContext(app));
    }

    // Separado de Main() para ser testável sem precisar do loop de mensagens WinForms — Main() em si
    // não tem cobertura de teste automatizado por chamar Application.Run (bloqueante).
    public static WebApplication CriarApp(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.Configure<AgenteOptions>(builder.Configuration.GetSection("Agente"));
        builder.Services.AddHttpClient<BackendClient>();
        builder.Services.AddSingleton<TemplateCacheService>();
        builder.Services.AddHostedService<SincronizacaoTemplatesService>();

        var agenteOptions = builder.Configuration.GetSection("Agente").Get<AgenteOptions>() ?? new AgenteOptions();

        // Leitor real só quando configurado ("Leitor": "Futronic") — exige ftrScanAPI.dll ao lado do
        // executável e processo x86. O match usa SourceAFIS (código aberto) até haver o SDK ANSI da Futronic.
        if (string.Equals(agenteOptions.Leitor, "Futronic", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddSingleton<IFingerprintReader>(_ => new FutronicFingerprintReader(
                TimeSpan.FromSeconds(Math.Clamp(agenteOptions.TempoLimiteDedoSegundos, 5, 120)), agenteOptions.DetectarDedoFalso));
        }
        else
        {
            builder.Services.AddSingleton<IFingerprintReader>(new SimuladoFingerprintReader(new byte[] { 1, 2, 3, 4 }));
        }

        if (string.Equals(agenteOptions.Leitor, "Futronic", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddSingleton<IFingerprintMatcher>(new SourceAfisFingerprintMatcher(agenteOptions.InverterImagem));
        }
        else
        {
            builder.Services.AddSingleton<IFingerprintMatcher, SimuladoFingerprintMatcher>();
        }

        builder.Services.AddCors(options =>
        {
            options.AddPolicy(PoliticaCorsKiosk, policy =>
                policy.WithOrigins(agenteOptions.OrigemPermitida).AllowAnyHeader().AllowAnyMethod());
        });

        // Kestrel só escuta em loopback — junto com o CORS travado na origem exata do quiosque, isso
        // substitui a ideia (descartada por complexidade desnecessária) de um token de sessão emitido
        // pelo backend só para este canal local — ver spec §4.4 e Architecture deste plano.
        builder.WebHost.ConfigureKestrel(serverOptions =>
            serverOptions.Listen(System.Net.IPAddress.Loopback, 5251));

        var app = builder.Build();
        app.UseAcessoRedePrivada(agenteOptions.OrigemPermitida);
        app.UseCors(PoliticaCorsKiosk);
        app.UseTraducaoDeErros();
        AgenteEndpoints.Mapear(app, PoliticaCorsKiosk);

        return app;
    }
}
