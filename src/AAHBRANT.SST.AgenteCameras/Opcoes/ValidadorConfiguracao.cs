using System.Text.RegularExpressions;
using AAHBRANT.SST.AgenteCameras.Quadros;
using AAHBRANT.SST.AgenteCameras.Seguranca;

namespace AAHBRANT.SST.AgenteCameras.Opcoes;

// Valida a configuração na partida, com mensagens que dizem o que corrigir e onde.
// Erro global (API, agente, segredo, ffmpeg indefinido...) impede a partida. Erro numa câmera só tira essa câmera
// do ar; as outras seguem. Câmera sem senha não é ativada (decisão do usuário): aviso e segue.
public static class ValidadorConfiguracao
{
    public static string PastaDadosPadrao() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "AAHBRANT", "AgenteCameras");

    public static ResultadoConfiguracao Validar(AgenteCamerasOptions o, IProtetorDados protetor)
    {
        var erros = new List<string>();
        var avisos = new List<string>();

        Uri? api = null;
        if (string.IsNullOrWhiteSpace(o.ApiBaseUrl))
            erros.Add("AgenteCameras:ApiBaseUrl não foi informada (ex.: https://api.exemplo.com.br).");
        else if (!Uri.TryCreate(o.ApiBaseUrl.Trim(), UriKind.Absolute, out api) || (api.Scheme != Uri.UriSchemeHttps && api.Scheme != Uri.UriSchemeHttp))
            erros.Add($"AgenteCameras:ApiBaseUrl inválida (\"{o.ApiBaseUrl}\"): use uma URL http(s) completa.");
        else if (api.Scheme == Uri.UriSchemeHttp && !api.IsLoopback)
            avisos.Add("AgenteCameras:ApiBaseUrl usa http (sem criptografia). Em produção use https: o segredo do agente trafega nessa conexão.");

        var agenteId = "";
        if (string.IsNullOrWhiteSpace(o.AgenteId))
            erros.Add("AgenteCameras:AgenteId não foi informado.");
        else if (!TentarLerGuid(o.AgenteId, out agenteId))
            erros.Add($"AgenteCameras:AgenteId inválido (\"{o.AgenteId}\"): deve ser o GUID do agente exibido ao cadastrá-lo no SST-APP (ex.: 3f2504e0-4f89-11d3-9a0c-0305e82c3301).");

        var segredo = "";
        if (string.IsNullOrEmpty(o.Segredo))
            erros.Add("AgenteCameras:Segredo não foi informado (é o segredo gerado ao cadastrar o agente no SST-APP).");
        else
        {
            try { segredo = SegredosConfiguracao.Resolver(o.Segredo, protetor); }
            catch (SegredoIlegivelException e) { erros.Add($"AgenteCameras:Segredo: {e.Message}."); }
        }

        if (string.IsNullOrWhiteSpace(o.CaminhoFfmpeg))
            erros.Add("AgenteCameras:CaminhoFfmpeg não foi informado (caminho completo do ffmpeg.exe).");
        // Arquivo ausente NÃO é erro de partida: o serviço avisa e tenta de novo a cada minuto (o ffmpeg pode ser colocado depois).

        if (o.FilaIdadeMaximaHoras < 1) erros.Add("AgenteCameras:FilaIdadeMaximaHoras deve ser 1 ou mais.");
        if (o.FilaTamanhoMaximoMb < 1) erros.Add("AgenteCameras:FilaTamanhoMaximoMb deve ser 1 ou mais.");

        var pasta = string.IsNullOrWhiteSpace(o.PastaDados) ? PastaDadosPadrao() : o.PastaDados.Trim();

        var cameras = new List<CameraConfig>();
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < o.Cameras.Count; i++)
        {
            var c = o.Cameras[i];
            var rotulo = $"Cameras[{i}]" + (string.IsNullOrWhiteSpace(c.Nome) ? "" : $" \"{c.Nome}\"");

            if (!c.Habilitada)
            {
                avisos.Add($"{rotulo}: desabilitada (Habilitada=false); ignorada.");
                continue;
            }

            var erroCamera = new List<string>();
            var cameraId = "";
            if (string.IsNullOrWhiteSpace(c.CameraId)) erroCamera.Add("CameraId não informado");
            else if (!TentarLerGuid(c.CameraId, out cameraId)) erroCamera.Add($"CameraId \"{c.CameraId}\" não é um GUID válido (copie o identificador da câmera no SST-APP)");
            else if (!vistos.Add(cameraId)) erroCamera.Add($"CameraId \"{c.CameraId}\" repetido");
            if (string.IsNullOrWhiteSpace(c.Host)) erroCamera.Add("Host não informado");
            if (c.PortaRtsp is < 1 or > 65535) erroCamera.Add("PortaRtsp deve estar entre 1 e 65535");
            if (c.Canal is not (1 or 2)) erroCamera.Add("Canal deve ser 1 ou 2");
            if (c.Stream is not (0 or 1)) erroCamera.Add("Stream deve ser 0 ou 1");
            if (c.IntervaloEnvioSegundos < 1) erroCamera.Add("IntervaloEnvioSegundos deve ser 1 ou mais");
            if (string.IsNullOrWhiteSpace(c.Usuario) && string.IsNullOrWhiteSpace(c.ModeloUrl)) erroCamera.Add("Usuario não informado");
            if (!string.IsNullOrWhiteSpace(c.ModeloUrl))
            {
                var invalidos = UrlRtsp.MarcadoresInvalidos(c.ModeloUrl);
                if (invalidos.Count > 0)
                    erroCamera.Add($"ModeloUrl usa marcadores desconhecidos ({string.Join(", ", invalidos)}); válidos: {string.Join(", ", UrlRtsp.MarcadoresValidos)}");
            }

            var senha = "";
            try { senha = SegredosConfiguracao.Resolver(c.Senha, protetor); }
            catch (SegredoIlegivelException e) { erroCamera.Add($"Senha: {e.Message}"); }

            if (erroCamera.Count > 0)
            {
                avisos.Add($"{rotulo}: NÃO ativada. {string.Join("; ", erroCamera)}.");
                continue;
            }

            if (string.IsNullOrEmpty(senha))
            {
                avisos.Add($"{rotulo}: sem senha definida; câmera NÃO ativada. Informe Cameras[{i}].Senha no arquivo de configuração e reinicie o serviço.");
                continue;
            }

            cameras.Add(new CameraConfig(
                cameraId,
                string.IsNullOrWhiteSpace(c.Nome) ? cameraId : c.Nome.Trim(),
                c.Host.Trim(), c.PortaRtsp, (c.Usuario ?? "").Trim(), senha, c.Canal, c.Stream,
                string.IsNullOrWhiteSpace(c.ModeloUrl) ? null : c.ModeloUrl.Trim(),
                TimeSpan.FromSeconds(c.IntervaloEnvioSegundos)));
        }

        if (erros.Count > 0) return new ResultadoConfiguracao(null, erros, avisos);

        if (cameras.Count == 0 && !o.LerConfiguracaoDoServidor)
            avisos.Add("Nenhuma câmera ativa na configuração: o serviço fica em espera. Ajuste AgenteCameras:Cameras e reinicie.");

        var config = new ConfiguracaoAgente(
            api!, agenteId, segredo, o.CaminhoFfmpeg.Trim(), pasta,
            TimeSpan.FromHours(o.FilaIdadeMaximaHoras), o.FilaTamanhoMaximoMb * 1024L * 1024L, cameras, o.LerConfiguracaoDoServidor);
        return new ResultadoConfiguracao(config, erros, avisos);
    }

    // GUID no formato canônico (minúsculas, com hifens), que é o que entra na URL. Guid vazio não é identificador válido.
    private static bool TentarLerGuid(string valor, out string canonico)
    {
        if (Guid.TryParse(valor.Trim(), out var g) && g != Guid.Empty)
        {
            canonico = g.ToString("D");
            return true;
        }
        canonico = "";
        return false;
    }
}
