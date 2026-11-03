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
    public Func<bool> AgenteEstaRodando { get; set; }

    public InstaladorAgente(string destino, Action<string> log)
    {
        _destino = destino;
        _log = log;
        IniciarEConferir = IniciarEConferirPadrao;
        PararAgente = PararAgentePadrao;
        RegistrarInicioAutomatico = RegistrarInicioAutomaticoPadrao;
        AgenteEstaRodando = AgenteEstaRodandoPadrao;
    }

    // Rede de segurança da atualização automática (03/11): o agente fecha a si mesmo para o Setup trocar a
    // versão. Se a troca falhar no meio (arquivo preso pelo antivírus, disco, pacote ruim), ninguém reabria o
    // agente e o posto ficava sem leitor até o próximo login. Aqui devolve a pasta anterior, se a atual sumiu,
    // e abre o agente se ele não estiver rodando. Não faz nada quando o agente já está de pé.
    public void GarantirAgenteRodando()
    {
        var exe = Path.Combine(_destino, NomeExe + ".exe");
        var anterior = _destino + ".anterior";
        if (!File.Exists(exe) && Directory.Exists(anterior))
        {
            _log("A pasta do agente ficou incompleta depois da falha. Restaurando a versão anterior...");
            var falha = _destino + ".falhou";
            ApagarSeExistir(falha);
            if (Directory.Exists(_destino))
                Mover(_destino, falha);
            Mover(anterior, _destino);
        }

        if (!File.Exists(exe))
        {
            _log("Não há agente instalado para reabrir.");
            return;
        }
        if (AgenteEstaRodando())
            return;

        _log("O agente não está aberto. Abrindo de novo...");
        RegistrarInicioAutomatico(exe);
        if (!IniciarEConferir(exe))
            _log("O agente foi aberto, mas não respondeu. Rode o diagnóstico.");
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

    private static readonly (string Chave, string Antigo, string Novo)[] EnderecosCorrigidos =
    [
        ("BackendBaseUrl", "https://sst-api-hml.azurewebsites.net", "https://sst-api-hml.kindground-7a44c4f0.brazilsouth.azurecontainerapps.io"),
        ("OrigemPermitida", "https://sst-web-hml.azurewebsites.net", "https://sst-web-hml.kindground-7a44c4f0.brazilsouth.azurecontainerapps.io"),
    ];

    // Mantém tudo que o posto já tem e só acrescenta o que esta versão passou a usar.
    public static string Completar(string jsonExistente, string? urlManifestoPadrao)
    {
        var raiz = JsonNode.Parse(jsonExistente)?.AsObject() ?? new JsonObject();
        if (raiz["Agente"] is not JsonObject agente)
        {
            agente = new JsonObject();
            raiz["Agente"] = agente;
        }
        // Postos das versões até 1.1.0 vieram com endereços *.azurewebsites.net, que não existem.
        foreach (var (chave, antigo, novo) in EnderecosCorrigidos)
            if (agente[chave]?.GetValue<string>()?.TrimEnd('/') == antigo)
                agente[chave] = novo;
        // Vazia ou de um Setup gerado só para instalar à mão (domínio .invalid): o Setup oficial pode corrigir.
        if (!string.IsNullOrWhiteSpace(urlManifestoPadrao) && UrlPodeSerSubstituida(agente["UrlManifestoAtualizacao"]?.GetValue<string>()))
            agente["UrlManifestoAtualizacao"] = urlManifestoPadrao;
        return raiz.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static bool UrlPodeSerSubstituida(string? url) =>
        string.IsNullOrWhiteSpace(url)
        || (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Host.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase));

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

    private static bool AgenteEstaRodandoPadrao() => Process.GetProcessesByName(NomeExe).Length > 0;

    private static void RegistrarInicioAutomaticoPadrao(string exe)
    {
        using var chave = Registry.CurrentUser.CreateSubKey(ChaveRun);
        chave.SetValue(NomeRun, $"\"{exe}\"");
        RegistrarVigia(exe);
    }

    // Vigia (03/11): a entrada Run só abre o agente no login. Se ele travar ou alguém fechar no meio do dia,
    // o posto ficava sem leitor. A tarefa abre o agente a cada 5 min; se ele já estiver aberto, a segunda
    // instância sai na hora (mutex em Program.Main). Roda no usuário do posto, sem administrador.
    public const string NomeTarefaVigia = "AAHBRANT Agente Biometria (vigia)";

    private static void RegistrarVigia(string exe)
    {
        // Pela API COM do Agendador, e não por schtasks.exe: em PCs da empresa o antivírus nega ("Acesso negado")
        // que um programa sem assinatura abra o schtasks.exe, e a vigia não era criada.
        try
        {
            dynamic servico = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!)!;
            servico.Connect();
            dynamic tarefa = servico.NewTask(0);
            tarefa.RegistrationInfo.Description = "Reabre o Agente de Biometria se ele for fechado.";
            tarefa.Settings.DisallowStartIfOnBatteries = false;
            tarefa.Settings.StopIfGoingOnBatteries = false;
            tarefa.Settings.ExecutionTimeLimit = "PT0S";
            tarefa.Settings.MultipleInstances = 2; // ignora nova execução enquanto a anterior roda
            dynamic gatilho = tarefa.Triggers.Create(9); // ao fazer logon deste usuário
            gatilho.UserId = $@"{Environment.UserDomainName}\{Environment.UserName}";
            gatilho.Repetition.Interval = "PT5M";
            // E já a partir de agora, sem esperar o próximo login.
            dynamic agora = tarefa.Triggers.Create(1);
            agora.StartBoundary = DateTime.Now.AddMinutes(1).ToString("yyyy-MM-ddTHH:mm:ss");
            agora.Repetition.Interval = "PT5M";
            dynamic acao = tarefa.Actions.Create(0);
            acao.Path = exe;
            acao.WorkingDirectory = Path.GetDirectoryName(exe);
            tarefa.Principal.LogonType = 3; // sessão interativa: o agente aparece na bandeja
            servico.GetFolder(@"\").RegisterTaskDefinition(NomeTarefaVigia, tarefa, 6, null, null, 3, null);
        }
        catch (Exception) { /* sem a vigia o agente ainda abre no login pela entrada Run */ }
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
