using System.Diagnostics;
using System.Reflection;

namespace AAHBRANT.SST.AgenteBiometria.Instalador;

// Modos:
//   (sem argumentos)                 instalar ou atualizar com tela (duplo clique no posto)
//   --atualizar --pid <n>            atualização automática, sem tela (chamado pelo próprio agente)
//   --gerar-chave <privada> <pública>  só para quem publica
//   --assinar <privada> <versão> <urlSetup> <setup.exe> <manifesto.json>  só para quem publica
public static class Program
{
    // Valores sugeridos na tela da primeira instalação (o técnico pode trocar).
    private const string BackendPadrao = "https://sst-api-hml.azurewebsites.net";
    private const string OrigemPadrao = "https://sst-web-hml.azurewebsites.net";

    // Endereço do manifesto que vem preenchido. publicar.ps1 grava aqui via -UrlManifesto.
    private static string ManifestoPadrao =>
        Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "UrlManifestoAtualizacao")?.Value ?? string.Empty;

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length >= 3 && args[0] == "--gerar-chave")
            {
                AssinadorManifesto.GerarChave(args[1], args[2]);
                return 0;
            }
            if (args.Length >= 6 && args[0] == "--assinar")
            {
                var manifesto = AssinadorManifesto.Assinar(File.ReadAllText(args[1]), args[2], args[3], File.ReadAllBytes(args[4]));
                File.WriteAllText(args[5], AssinadorManifesto.ParaJson(manifesto));
                return 0;
            }
            if (args.Length >= 1 && args[0] == "--atualizar")
                return AtualizarSemTela(args);

            return InstalarComTela();
        }
        catch (Exception ex)
        {
            Registrar($"ERRO: {ex}");
            if (args.Length == 0)
                MessageBox.Show(ex.Message, "Agente de Biometria AAHBRANT", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static int InstalarComTela()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        var instalador = new InstaladorAgente(InstaladorAgente.DestinoPadrao, Registrar);

        var ehAtualizacao = instalador.JaInstalado;
        ConfiguracaoPosto? config = null;
        if (!ehAtualizacao)
        {
            using var form = new ConfiguracaoForm(BackendPadrao, OrigemPadrao, ManifestoPadrao);
            if (form.ShowDialog() != DialogResult.OK)
                return 2;
            config = form.Resultado;
        }

        using (var pacote = AbrirPacote())
            instalador.Instalar(pacote, config, ManifestoPadrao);

        MessageBox.Show(
            ehAtualizacao
                ? $"Agente atualizado para a versão {VersaoDoSetup()}."
                : $"Agente {VersaoDoSetup()} instalado. Ele inicia junto com o Windows e se atualiza sozinho.",
            "Agente de Biometria AAHBRANT", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return 0;
    }

    private static int AtualizarSemTela(string[] args)
    {
        // Espera o agente que pediu a atualização sair (ele encerra logo depois de iniciar este Setup).
        var indicePid = Array.IndexOf(args, "--pid");
        if (indicePid >= 0 && indicePid + 1 < args.Length && int.TryParse(args[indicePid + 1], out var pid))
        {
            try
            {
                using var processo = Process.GetProcessById(pid);
                if (!processo.WaitForExit(60_000))
                    processo.Kill();
            }
            catch (ArgumentException)
            {
                // já saiu
            }
        }

        var instalador = new InstaladorAgente(InstaladorAgente.DestinoPadrao, Registrar);
        using var pacote = AbrirPacote();
        instalador.Instalar(pacote, config: null, ManifestoPadrao);
        Registrar($"Atualizado para {VersaoDoSetup()}.");
        return 0;
    }

    private static Stream AbrirPacote() =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream("pacote-agente.zip")
        ?? throw new InvalidOperationException("Este Setup foi gerado sem o pacote do agente. Gere com scripts/agente-biometria/publicar.ps1.");

    private static string VersaoDoSetup() =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";

    // Log fica ao lado da instalação (fora dela, para sobreviver à troca de pasta).
    private static void Registrar(string mensagem)
    {
        try
        {
            var pasta = Path.GetDirectoryName(InstaladorAgente.DestinoPadrao)!;
            Directory.CreateDirectory(pasta);
            File.AppendAllText(Path.Combine(pasta, "instalador-agente.log"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {mensagem}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }
    }
}
