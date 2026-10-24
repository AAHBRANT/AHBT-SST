using System.Reflection;
using System.Text;
using System.Text.Json;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Mapa corporal da lesão no relatório da ocorrência. Lê o mesmo mapaCorporal.json da tela
// (TeamsApp/src/components/mapaCorporal, embutido pelo .csproj — fonte única do desenho, gerada por
// scripts/mapa-corporal/gerar_mapa_corporal.py) e monta um SVG por vista com as regiões marcadas.
public static class MapaCorporalSvg
{
    public const string Recurso = "AAHBRANT.SST.Infrastructure.Documentos.Assets.mapaCorporal.json";

    // Mesmas cores do componente MapaCorporal.tsx; a marcação usa o vermelho de alerta do sistema.
    private const string CorMarcada = "#C4262E";

    public record VistaDesenhada(string Id, string Titulo, string Svg, bool TemMarcacao);

    private record RegiaoJson(string Codigo, string Rotulo, string D);
    private record VistaJson(string Id, string Titulo, List<string> Detalhes, List<RegiaoJson> Regioes);
    private record MapaJson(string ViewBox, string Silhueta, List<VistaJson> Vistas);

    private static readonly Lazy<MapaJson> Mapa = new(Carregar);

    public static IReadOnlyList<VistaDesenhada> Desenhar(IEnumerable<string> selecionadas)
    {
        var marcadas = selecionadas.ToHashSet(StringComparer.Ordinal);
        var mapa = Mapa.Value;
        return mapa.Vistas
            .Select(v => new VistaDesenhada(v.Id, v.Titulo, Svg(mapa, v, marcadas), v.Regioes.Any(r => marcadas.Contains(r.Codigo))))
            .ToList();
    }

    public static bool Existe(string codigo) => Mapa.Value.Vistas.Any(v => v.Regioes.Any(r => r.Codigo == codigo));

    public static string Rotulo(string codigo) =>
        Mapa.Value.Vistas.SelectMany(v => v.Regioes).FirstOrDefault(r => r.Codigo == codigo)?.Rotulo ?? codigo;

    /// <summary>Ordem do desenho (cabeça → pés, frente antes das costas); códigos desconhecidos no fim.</summary>
    public static IReadOnlyList<string> Ordenar(IEnumerable<string> codigos)
    {
        var ordem = Mapa.Value.Vistas.SelectMany(v => v.Regioes).Select((r, i) => (r.Codigo, i)).ToDictionary(x => x.Codigo, x => x.i);
        return codigos.Distinct().OrderBy(c => ordem.GetValueOrDefault(c, int.MaxValue)).ToList();
    }

    private static string Svg(MapaJson mapa, VistaJson vista, HashSet<string> marcadas)
    {
        var svg = new StringBuilder();
        svg.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"{mapa.ViewBox}\">");
        svg.Append("<defs>");
        svg.Append($"<clipPath id=\"silhueta\"><path d=\"{mapa.Silhueta}\"/></clipPath>");
        svg.Append("<linearGradient id=\"volume\" x1=\"40\" y1=\"0\" x2=\"160\" y2=\"0\" gradientUnits=\"userSpaceOnUse\">");
        svg.Append("<stop offset=\"0\" stop-color=\"#B9C3CF\"/><stop offset=\"0.22\" stop-color=\"#DFE4EA\"/>");
        svg.Append("<stop offset=\"0.5\" stop-color=\"#F1F3F6\"/><stop offset=\"0.78\" stop-color=\"#DFE4EA\"/>");
        svg.Append("<stop offset=\"1\" stop-color=\"#B9C3CF\"/></linearGradient>");
        svg.Append("</defs>");
        svg.Append($"<path d=\"{mapa.Silhueta}\" fill=\"url(#volume)\"/>");
        svg.Append("<g clip-path=\"url(#silhueta)\">");
        foreach (var regiao in vista.Regioes.Where(r => marcadas.Contains(r.Codigo)))
            svg.Append($"<path d=\"{regiao.D}\" fill=\"{CorMarcada}\" fill-opacity=\"0.85\"/>");
        svg.Append("</g>");
        svg.Append("<g fill=\"none\" stroke=\"#8796AB\" stroke-width=\"0.55\" stroke-linecap=\"round\" stroke-opacity=\"0.8\">");
        foreach (var detalhe in vista.Detalhes)
            svg.Append($"<path d=\"{detalhe}\"/>");
        svg.Append("</g>");
        svg.Append($"<path d=\"{mapa.Silhueta}\" fill=\"none\" stroke=\"#5B6B80\" stroke-width=\"0.8\" stroke-linejoin=\"round\"/>");
        svg.Append("</svg>");
        return svg.ToString();
    }

    private static MapaJson Carregar()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(Recurso)
            ?? throw new InvalidOperationException($"Recurso {Recurso} não encontrado (mapaCorporal.json do TeamsApp).");
        return JsonSerializer.Deserialize<MapaJson>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("mapaCorporal.json inválido.");
    }
}
