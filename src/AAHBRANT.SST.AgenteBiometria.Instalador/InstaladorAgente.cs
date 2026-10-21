using System.Diagnostics;
using System.IO.Compression;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Win32;

namespace AAHBRANT.SST.AgenteBiometria.Instalador;

// Dados que a pessoa preenche na primeira instalação de um posto.
public record ConfiguracaoPosto(
    Guid DispositivoId, string SegredoDispositivo, string ChaveCriptografiaBiometriaBase64,
    string BackendBaseUrl, string OrigemPermitida, string UrlManifestoAtualizacao);

// Instala ou atualiza o agente na mesma pasta e com o mesmo início automático do antigo instalar.ps1, então
// também serve para postos instalados pelo script. A configuração do posto (appsettings.Production.json) e a
// chave de assinatura das leituras (em %ProgramData%) nunca são tocadas numa atualização.
//
// Troca com volta: a pasta atual vira "<pasta>.anterior", a versão nova é copiada, e se ela não responder
// em http://127.0.0.1:5251 a anterior volta. Um posto nunca fica sem agente por causa de uma atualização ruim.
public class InstaladorAgente
{
    public const string NomeExe = "AAHBRANT.SST.AgenteBiometria";
    public const string ArquivoConfig = "appsettings.Production.json";
    private const string ChaveRun = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string NomeRun = "AAHBRANT Agente Biometria";

    public static string DestinoPadrao =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AAHBRANT", "AgenteBiometria");

    private readonly string _destino;
    private readonly Action<string> _log;

    // Pontos de troca para os testes (não sobem processo nem mexem no registro de verdade).
    public Func<string, bool> IniciarEConferir { get; set; }
    public Action PararAgente { get; set; }
    public Action<string> RegistrarInicioAutomatico { get; set; }

    public InstaladorAgente(string destino, Action<string> log)
    {
        _destino = destino;
        _log = log;
        IniciarEConferir = IniciarEConferirPadrao;
        PararAgente = PararAgentePadrao;
        RegistrarInicioAutomatico = RegistrarInicioAutomaticoPadrao;
    }

    public string Destino => _destino;

    public bool JaInstalado => File.Exists(Path.Combine(_destino, ArquivoConfig));

    // config: obrigatória na primeira instalação; null numa atualização (mantém a do posto).
    public void Instalar(Stream pacoteZip, ConfiguracaoPosto? config, string? urlManifestoPadrao = null)
    {
        var configExistente = Path.Combine(_destino, ArquivoConfig);
        if (config is null && !File.Exists(configExistente))
            throw new InvalidOperationException("Primeira instalação neste PC: preencha a configuração do posto.");

        var temporaria = _destino + ".nova";
        var anterior = _destino + ".anterior";
        ApagarSeExistir(temporaria);
        ZipFile.ExtractToDirectory(pacoteZip, temporaria);
        if (!File.Exists(Path.Combine(temporaria, NomeExe + ".exe")))
            throw new InvalidOperationException("Pacote do agente incompleto (executável ausente).");

        // Configuração: a do posto passa para a versão nova; os campos novos ganham valor padrão.
        var configNova = Path.Combine(temporaria, ArquivoConfig);
        if (config is not null)
            File.WriteAllText(configNova, MontarConfig(config).ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        else
            File.WriteAllText(configNova, Completar(File.ReadAllText(configExistente), urlManifestoPadrao));
        RestringirAoUsuarioAtual(configNova);

        _log("Parando o agente atual...");
        PararAgente();

        ApagarSeExistir(anterior);
        var tinhaAnterior = Directory.Exists(_destino);
        if (tinhaAnterior)
            Mover(_destino, anterior);
        Directory.CreateDirectory(Path.GetDirectoryName(_destino)!);
        Mover(temporaria, _destino);

        var exe = Path.Combine(_destino, NomeExe + ".exe");
        RegistrarInicioAutomatico(exe);
        _log("Iniciando a versão nova...");
        if (IniciarEConferir(exe))
        {
            _log("Agente instalado e respondendo.");
            return;
        }

        if (!tinhaAnterior)
            throw new InvalidOperationException("O agente foi instalado, mas não respondeu. Confira o leitor e rode o diagnóstico.");

        _log("A versão nova não respondeu. Voltando para a anterior...");
        PararAgente();
        var falha = _destino + ".falhou";
        ApagarSeExistir(falha);
        Mover(_destino, falha);
        Mover(anterior, _destino);
        IniciarEConferir(exe);
        throw new InvalidOperationException("A versão nova não respondeu e a anterior foi restaurada.");
    }

    public static JsonObject MontarConfig(ConfiguracaoPosto c) => new()
    {
        ["Agente"] = new JsonObject
        {
            ["Leitor"] = "Futronic",
            ["InverterImagem"] = true,
            ["DispositivoId"] = c.DispositivoId.ToString(),
            ["SegredoDispositivo"] = c.SegredoDispositivo,
            ["ChaveCriptografiaBiometriaBase64"] = c.ChaveCriptografiaBiometriaBase64,
            ["BackendBaseUrl"] = c.BackendBaseUrl,
            ["OrigemPermitida"] = c.OrigemPermitida,
            ["UrlManifestoAtualizacao"] = c.UrlManifestoAtualizacao,
        },
    };

    // Mantém tudo que o posto já tem e só acrescenta o que esta versão passou a usar.
    public static string Completar(string jsonExistente, string? urlManifestoPadrao)
    {
        var raiz = JsonNode.Parse(jsonExistente)?.AsObject() ?? new JsonObject();
        if (raiz["Agente"] is not JsonObject agente)
        {
            agente = new JsonObject();
            raiz["Agente"] = agente;
        }
        if (!agente.ContainsKey("UrlManifestoAtualizacao") && !string.IsNullOrWhiteSpace(urlManifestoPadrao))
            agente["UrlManifestoAtualizacao"] = urlManifestoPadrao;
        return raiz.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    // Segredos em disco: só o usuário atual lê (mesma regra do instalar.ps1).
    private static void RestringirAoUsuarioAtual(string arquivo)
    {
        var regras = new FileSecurity();
        regras.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        regras.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!,
            FileSystemRights.Read | FileSystemRights.Write, AccessControlType.Allow));
        new FileInfo(arquivo).SetAccessControl(regras);
    }

    private static string? LerOrigemPermitida(string arquivoConfig)
    {
        try
        {
            return JsonNode.Parse(File.ReadAllText(arquivoConfig))?["Agente"]?["OrigemPermitida"]?.GetValue<string>();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
    }

    // Logo depois de matar o processo o Windows ainda pode segurar algum arquivo por instantes.
    private static void Mover(string de, string para)
    {
        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                Directory.Move(de, para);
                return;
            }
            catch (IOException) when (tentativa < 10)
            {
                Thread.Sleep(1000);
            }
        }
    }

    private static void ApagarSeExistir(string pasta)
    {
        if (Directory.Exists(pasta))
            Directory.Delete(pasta, recursive: true);
    }

    private static void PararAgentePadrao()
    {
        foreach (var p in Process.GetProcessesByName(NomeExe))
        {
            try { p.Kill(); p.WaitForExit(10_000); } catch (InvalidOperationException) { }
        }
    }

    private static void RegistrarInicioAutomaticoPadrao(string exe)
    {
        using var chave = Registry.CurrentUser.CreateSubKey(ChaveRun);
        chave.SetValue(NomeRun, $"\"{exe}\"");
    }

    private static bool IniciarEConferirPadrao(string exe)
    {
        Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = Path.GetDirectoryName(exe), UseShellExecute = true });
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        // Mesma origem da tela do quiosque: o agente só atende chamadas dela.
        var origem = LerOrigemPermitida(Path.Combine(Path.GetDirectoryName(exe)!, ArquivoConfig));
        if (origem is not null)
            http.DefaultRequestHeaders.Add("Origin", origem);
        for (var i = 0; i < 15; i++)
        {
            Thread.Sleep(2000);
            try
            {
                if (http.GetAsync("http://127.0.0.1:5251/api/dispositivo").GetAwaiter().GetResult().IsSuccessStatusCode)
                    return true;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
            }
        }
        return false;
    }
}
