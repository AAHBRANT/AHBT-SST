using System.Text.RegularExpressions;
using AAHBRANT.SST.AgenteCameras.Opcoes;

namespace AAHBRANT.SST.AgenteCameras.Quadros;

// Monta a URL RTSP da câmera. O modelo padrão é o da câmera testada (firmware tipo Xiongmai/iCSee).
// Usuário e senha entram escapados para URL (senha com "&", "@" ou "#" não quebra o endereço).
public static class UrlRtsp
{
    public const string ModeloPadrao =
        "rtsp://{host}:{porta}/user={usuario}&password={senha}&channel={canal}&stream={stream}.sdp?real_stream";

    public static readonly IReadOnlyList<string> MarcadoresValidos = new[] { "{host}", "{porta}", "{usuario}", "{senha}", "{canal}", "{stream}" };

    private static readonly Regex Marcador = new(@"\{[^{}]*\}", RegexOptions.Compiled);

    public static IReadOnlyList<string> MarcadoresInvalidos(string modelo) =>
        Marcador.Matches(modelo).Select(m => m.Value).Where(v => !MarcadoresValidos.Contains(v, StringComparer.OrdinalIgnoreCase)).Distinct().ToList();

    public static string Montar(CameraConfig c)
    {
        var modelo = c.ModeloUrl ?? ModeloPadrao;
        var valores = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{host}"] = c.Host,
            ["{porta}"] = c.PortaRtsp.ToString(),
            ["{usuario}"] = Uri.EscapeDataString(c.Usuario),
            ["{senha}"] = Uri.EscapeDataString(c.Senha),
            ["{canal}"] = c.Canal.ToString(),
            ["{stream}"] = c.Stream.ToString(),
        };
        return Marcador.Replace(modelo, m => valores.TryGetValue(m.Value, out var v) ? v : m.Value);
    }
}
