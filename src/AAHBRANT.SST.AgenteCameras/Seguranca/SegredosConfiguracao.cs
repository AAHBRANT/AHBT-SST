using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AAHBRANT.SST.AgenteCameras.Opcoes;

namespace AAHBRANT.SST.AgenteCameras.Seguranca;

public sealed class SegredoIlegivelException : Exception
{
    public SegredoIlegivelException(string message, Exception? inner = null) : base(message, inner) { }
}

// Valores sensíveis (Segredo do agente e Senha das câmeras) podem ser escritos em texto na primeira vez;
// na partida o serviço os regrava como "dpapi:<base64>". Valor com o prefixo já está protegido.
public static class SegredosConfiguracao
{
    public const string Prefixo = "dpapi:";

    public static bool EstaProtegido(string? valor) =>
        valor is not null && valor.StartsWith(Prefixo, StringComparison.Ordinal);

    public static string Proteger(string texto, IProtetorDados protetor) =>
        Prefixo + Convert.ToBase64String(protetor.Proteger(Encoding.UTF8.GetBytes(texto)));

    // Texto claro volta igual; valor protegido é aberto. Falha = foi gravado em outra máquina ou está corrompido.
    public static string Resolver(string? valor, IProtetorDados protetor)
    {
        if (string.IsNullOrEmpty(valor)) return "";
        if (!EstaProtegido(valor)) return valor;
        try
        {
            var bytes = Convert.FromBase64String(valor[Prefixo.Length..]);
            return Encoding.UTF8.GetString(protetor.Desproteger(bytes));
        }
        catch (Exception e)
        {
            throw new SegredoIlegivelException(
                "valor protegido ilegível neste computador (gravado em outra máquina ou corrompido); informe o valor em texto novamente", e);
        }
    }

    /// <summary>
    /// Regrava o arquivo JSON trocando Segredo e Senha em texto por valores protegidos. Devolve quantos valores
    /// foram protegidos (0 = nada a fazer, arquivo intocado). Escrita atômica (arquivo temporário + troca).
    /// </summary>
    public static int ProtegerArquivo(string caminho, IProtetorDados protetor)
    {
        if (!File.Exists(caminho)) return 0;
        JsonNode? raiz;
        try
        {
            raiz = JsonNode.Parse(File.ReadAllText(caminho), documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
        }
        catch (JsonException e)
        {
            throw new InvalidOperationException($"JSON inválido em {caminho}: {e.Message}", e);
        }

        if (raiz is not JsonObject obj || obj[AgenteCamerasOptions.Secao] is not JsonObject secao) return 0;

        var alterados = 0;
        alterados += ProtegerCampo(secao, "Segredo", protetor);
        if (secao["Cameras"] is JsonArray cameras)
            foreach (var camera in cameras.OfType<JsonObject>())
                alterados += ProtegerCampo(camera, "Senha", protetor);
        if (alterados == 0) return 0;

        var temporario = caminho + ".tmp";
        File.WriteAllText(temporario, raiz.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        File.Move(temporario, caminho, overwrite: true);
        return alterados;
    }

    private static int ProtegerCampo(JsonObject objeto, string campo, IProtetorDados protetor)
    {
        if (objeto[campo] is not JsonValue valor || !valor.TryGetValue<string>(out var texto)) return 0;
        if (string.IsNullOrEmpty(texto) || EstaProtegido(texto)) return 0;
        objeto[campo] = Proteger(texto, protetor);
        return 1;
    }
}
