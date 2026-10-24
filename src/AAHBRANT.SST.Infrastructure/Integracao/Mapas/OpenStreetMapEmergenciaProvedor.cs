using System.Globalization;
using System.Text.Json;
using AAHBRANT.SST.Application.GeracaoPgr;

namespace AAHBRANT.SST.Infrastructure.Integracao.Mapas;

// Provedor de mapas do plano de emergência (24/10/2026) sobre os serviços públicos do OpenStreetMap:
// Nominatim (endereço → coordenadas), Overpass (hospitais, bombeiros e delegacias ao redor) e OSRM (rota de
// carro). Sem chave de API. Os serviços públicos têm limite de uso: aqui só roda quando o técnico pede a
// sugestão, com poucas consultas por vez. Telefones nem sempre existem no mapa; quando faltam, ficam nulos.
public class OpenStreetMapEmergenciaProvedor : IProvedorMapaEmergencia
{
    private static readonly string[] EndpointsOverpassPadrao =
    {
        "https://overpass-api.de/api/interpreter",
        "https://overpass.openstreetmap.fr/api/interpreter",
        "https://overpass.kumi.systems/api/interpreter",
    };

    // Cada tentativa a um espelho do Overpass tem tempo próprio: os serviços públicos às vezes travam.
    private static readonly TimeSpan TempoPorTentativa = TimeSpan.FromSeconds(15);

    private readonly HttpClient _http;
    private readonly string _nominatim;
    private readonly string[] _overpass;
    private readonly string _osrm;

    public OpenStreetMapEmergenciaProvedor(HttpClient http, Microsoft.Extensions.Options.IOptions<MapasOptions> opcoes)
    {
        _http = http;
        var o = opcoes.Value;
        _nominatim = (o.Nominatim ?? "https://nominatim.openstreetmap.org").TrimEnd('/');
        _overpass = o.Overpass is { Length: > 0 } ? o.Overpass : EndpointsOverpassPadrao;
        _osrm = (o.Osrm ?? "https://router.project-osrm.org").TrimEnd('/');
    }

    public string Fonte => "OpenStreetMap";

    public async Task<LocalGeocodificado?> LocalizarAsync(string consulta, CancellationToken ct)
    {
        var url = $"{_nominatim}/search?format=jsonv2&limit=1&countrycodes=br&accept-language=pt-BR&q="
                  + Uri.EscapeDataString(consulta);
        using var resposta = await _http.GetAsync(url, ct);
        if (!resposta.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(ct));
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0) return null;
        var item = doc.RootElement[0];
        if (!TryNumero(item, "lat", out var lat) || !TryNumero(item, "lon", out var lon)) return null;
        var nome = item.TryGetProperty("display_name", out var dn) ? dn.GetString() ?? consulta : consulta;
        return new LocalGeocodificado(new PontoGeografico(lat, lon), nome, false);
    }

    public async Task<List<LocalEmergenciaMapa>> BuscarProximosAsync(PontoGeografico centro, int raioMetros, CancellationToken ct)
    {
        var lat = centro.Latitude.ToString("F6", CultureInfo.InvariantCulture);
        var lon = centro.Longitude.ToString("F6", CultureInfo.InvariantCulture);
        var consulta = $"[out:json][timeout:25];(" +
                       $"nwr[\"amenity\"=\"hospital\"](around:{raioMetros},{lat},{lon});" +
                       $"nwr[\"amenity\"=\"fire_station\"](around:{raioMetros},{lat},{lon});" +
                       $"nwr[\"amenity\"=\"police\"](around:{raioMetros},{lat},{lon});" +
                       ");out center tags 120;";

        string? json = null;
        foreach (var endpoint in _overpass)
        {
            try
            {
                using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
                limite.CancelAfter(TempoPorTentativa);
                using var conteudo = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("data", consulta) });
                using var resposta = await _http.PostAsync(endpoint, conteudo, limite.Token);
                if (!resposta.IsSuccessStatusCode) continue;
                json = await resposta.Content.ReadAsStringAsync(limite.Token);
                break;
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // espelho fora do ar ou lento: tenta o seguinte
            }
        }
        // Último recurso: a busca do próprio Nominatim (menos completa, mas quase sempre no ar).
        if (json is null) return await BuscarViaNominatimAsync(centro, raioMetros, ct);

        using var doc = JsonDocument.Parse(json);
        var locais = new List<LocalEmergenciaMapa>();
        if (!doc.RootElement.TryGetProperty("elements", out var elementos)) return locais;
        foreach (var el in elementos.EnumerateArray())
        {
            if (!el.TryGetProperty("tags", out var tags)) continue;
            var nome = Tag(tags, "name");
            if (string.IsNullOrWhiteSpace(nome)) continue; // sem nome não dá para o técnico reconhecer o local
            var amenity = Tag(tags, "amenity");
            var tipo = amenity switch
            {
                "hospital" => TipoLocalEmergencia.Hospital,
                "fire_station" => TipoLocalEmergencia.Bombeiros,
                "police" => TipoLocalEmergencia.Policia,
                _ => (TipoLocalEmergencia?)null,
            };
            if (tipo is null) continue;

            double plat, plon;
            if (el.TryGetProperty("lat", out _) && TryNumero(el, "lat", out plat) && TryNumero(el, "lon", out plon)) { }
            else if (el.TryGetProperty("center", out var centroEl) && TryNumero(centroEl, "lat", out plat) && TryNumero(centroEl, "lon", out plon)) { }
            else continue;

            var tipoOsm = el.TryGetProperty("type", out var t) ? t.GetString() : "node";
            var id = $"{tipoOsm}/{(el.TryGetProperty("id", out var idEl) ? idEl.GetInt64() : 0)}";
            var urgencia = Tag(tags, "emergency") == "yes" || (Tag(tags, "healthcare:speciality") ?? string.Empty).Contains("emergency");
            locais.Add(new LocalEmergenciaMapa(
                id, tipo.Value, nome.Trim(), Endereco(tags), Telefone(tags), urgencia,
                new PontoGeografico(plat, plon), DistanciaRetaKm(centro, new PontoGeografico(plat, plon))));
        }
        return locais;
    }

    public async Task<RotaMapa?> RotaAsync(PontoGeografico origem, PontoGeografico destino, CancellationToken ct)
    {
        var coords = FormattableString.Invariant($"{origem.Longitude:F6},{origem.Latitude:F6};{destino.Longitude:F6},{destino.Latitude:F6}");
        var url = $"{_osrm}/route/v1/driving/{coords}?overview=false&steps=true&alternatives=false";
        try
        {
            using var resposta = await _http.GetAsync(url, ct);
            if (!resposta.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("routes", out var rotas) || rotas.GetArrayLength() == 0) return null;
            var rota = rotas[0];
            var distanciaM = rota.GetProperty("distance").GetDouble();
            var segundos = rota.GetProperty("duration").GetDouble();
            var passos = new List<string>();
            foreach (var leg in rota.GetProperty("legs").EnumerateArray())
                foreach (var step in leg.GetProperty("steps").EnumerateArray())
                {
                    var texto = PassoEmPortugues(step);
                    if (texto is not null) passos.Add(texto);
                }
            return new RotaMapa(distanciaM / 1000.0, Math.Max(1, (int)Math.Round(segundos / 60.0)), ReduzirPassos(passos));
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }

    // Busca por tipo dentro de uma caixa ao redor da obra. A política do Nominatim pede no máximo uma consulta
    // por segundo, por isso o intervalo entre os três tipos.
    private async Task<List<LocalEmergenciaMapa>> BuscarViaNominatimAsync(PontoGeografico centro, int raioMetros, CancellationToken ct)
    {
        var dLat = raioMetros / 111_000.0;
        var dLon = dLat / Math.Max(0.2, Math.Cos(centro.Latitude * Math.PI / 180.0));
        var caixa = FormattableString.Invariant($"{centro.Longitude - dLon:F5},{centro.Latitude + dLat:F5},{centro.Longitude + dLon:F5},{centro.Latitude - dLat:F5}");
        var locais = new List<LocalEmergenciaMapa>();
        var tipos = new (string Amenity, TipoLocalEmergencia Tipo)[]
        {
            ("hospital", TipoLocalEmergencia.Hospital),
            ("fire_station", TipoLocalEmergencia.Bombeiros),
            ("police", TipoLocalEmergencia.Policia),
        };
        var primeiro = true;
        foreach (var (amenity, tipo) in tipos)
        {
            if (!primeiro) await Task.Delay(TimeSpan.FromMilliseconds(1200), ct);
            primeiro = false;
            var url = $"{_nominatim}/search?format=jsonv2&limit=15&bounded=1&extratags=1&addressdetails=1&accept-language=pt-BR&amenity={amenity}&viewbox={caixa}";
            try
            {
                using var resposta = await _http.GetAsync(url, ct);
                if (!resposta.IsSuccessStatusCode) continue;
                using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(ct));
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (!TryNumero(item, "lat", out var lat) || !TryNumero(item, "lon", out var lon)) continue;
                    var nome = item.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (string.IsNullOrWhiteSpace(nome)) continue;
                    var tipoOsm = item.TryGetProperty("osm_type", out var ot) ? ot.GetString() : "node";
                    var idOsm = item.TryGetProperty("osm_id", out var oi) ? oi.GetInt64() : 0;
                    JsonElement extras = default, endereco = default;
                    var temExtras = item.TryGetProperty("extratags", out extras) && extras.ValueKind == JsonValueKind.Object;
                    var temEndereco = item.TryGetProperty("address", out endereco) && endereco.ValueKind == JsonValueKind.Object;
                    var partes = new List<string>();
                    if (temEndereco)
                    {
                        var rua = Tag(endereco, "road");
                        var numero = Tag(endereco, "house_number");
                        if (!string.IsNullOrWhiteSpace(rua)) partes.Add(string.IsNullOrWhiteSpace(numero) ? rua : $"{rua}, {numero}");
                        var bairro = Tag(endereco, "suburb") ?? Tag(endereco, "neighbourhood");
                        if (!string.IsNullOrWhiteSpace(bairro)) partes.Add(bairro);
                        var cidade = Tag(endereco, "city") ?? Tag(endereco, "town") ?? Tag(endereco, "municipality");
                        if (!string.IsNullOrWhiteSpace(cidade)) partes.Add(cidade);
                    }
                    var ponto = new PontoGeografico(lat, lon);
                    locais.Add(new LocalEmergenciaMapa(
                        $"{tipoOsm}/{idOsm}", tipo, nome.Trim(), partes.Count == 0 ? null : string.Join(" — ", partes),
                        temExtras ? Telefone(extras) : null,
                        temExtras && Tag(extras, "emergency") == "yes",
                        ponto, DistanciaRetaKm(centro, ponto)));
                }
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // sem este tipo agora; os outros seguem
            }
        }
        if (locais.Count == 0)
            throw new InvalidOperationException("O serviço de mapas (OpenStreetMap) não respondeu agora. Tente novamente em instantes.");
        return locais;
    }

    // ---- Tradução dos passos do OSRM ----

    private static string? PassoEmPortugues(JsonElement step)
    {
        var manobra = step.GetProperty("maneuver");
        var tipo = manobra.TryGetProperty("type", out var t) ? t.GetString() : null;
        var modificador = manobra.TryGetProperty("modifier", out var m) ? m.GetString() : null;
        var via = NomeVia(step);
        var distancia = Distancia(step.TryGetProperty("distance", out var d) ? d.GetDouble() : 0);
        var naVia = via is null ? string.Empty : $" na {via}";
        var peloVia = via is null ? string.Empty : $" pela {via}";

        return tipo switch
        {
            "depart" => $"Saia da obra e siga{peloVia} por {distancia}.",
            "arrive" => modificador switch
            {
                "left" => "Chegue ao destino, que fica à esquerda.",
                "right" => "Chegue ao destino, que fica à direita.",
                _ => "Chegue ao destino.",
            },
            "roundabout" or "rotary" or "roundabout turn" =>
                $"Na rotatória, pegue a saída {(manobra.TryGetProperty("exit", out var e) ? e.GetInt32() : 1)}º{naVia}, e siga por {distancia}.",
            "turn" or "end of road" or "fork" or "off ramp" or "on ramp" =>
                modificador == "straight"
                    ? $"{Direcao(modificador, tipo)}{naVia} por {distancia}."
                    : $"{Direcao(modificador, tipo)}{naVia} e siga por {distancia}.",
            "merge" => $"Entre{naVia} e siga por {distancia}.",
            "new name" or "continue" => via is null ? null : $"Continue{peloVia} por {distancia}.",
            _ => via is null ? null : $"Siga{peloVia} por {distancia}.",
        };
    }

    private static string Direcao(string? modificador, string? tipo)
    {
        var verbo = tipo is "fork" ? "Mantenha-se" : "Vire";
        return modificador switch
        {
            "left" => tipo is "fork" ? "Mantenha-se à esquerda" : "Vire à esquerda",
            "right" => tipo is "fork" ? "Mantenha-se à direita" : "Vire à direita",
            "slight left" => "Mantenha-se à esquerda",
            "slight right" => "Mantenha-se à direita",
            "sharp left" => "Faça uma curva fechada à esquerda",
            "sharp right" => "Faça uma curva fechada à direita",
            "uturn" => "Faça o retorno",
            "straight" => "Siga em frente",
            _ => verbo,
        };
    }

    private static string? NomeVia(JsonElement step)
    {
        var nome = step.TryGetProperty("name", out var n) ? n.GetString() : null;
        var referencia = step.TryGetProperty("ref", out var r) ? r.GetString() : null;
        if (!string.IsNullOrWhiteSpace(nome) && !string.IsNullOrWhiteSpace(referencia)) return $"{nome} ({referencia})";
        return !string.IsNullOrWhiteSpace(nome) ? nome : string.IsNullOrWhiteSpace(referencia) ? null : referencia;
    }

    private static string Distancia(double metros) =>
        metros < 1000
            ? $"{Math.Max(10, (int)(Math.Round(metros / 10.0) * 10))} m"
            : (metros / 1000.0).ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',') + " km";

    // Rotas longas viram instruções enormes: mantém o começo e o fim, que são o que o motorista confere.
    private static IReadOnlyList<string> ReduzirPassos(List<string> passos)
    {
        const int maximo = 12;
        if (passos.Count <= maximo) return passos;
        var resultado = passos.Take(maximo - 3).ToList();
        resultado.Add("… siga a rota pelas vias principais …");
        resultado.AddRange(passos.TakeLast(2));
        return resultado;
    }

    // ---- Leitura de tags e números ----

    private static string? Tag(JsonElement tags, string chave) =>
        tags.TryGetProperty(chave, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string? Telefone(JsonElement tags)
    {
        var bruto = Tag(tags, "phone") ?? Tag(tags, "contact:phone");
        return string.IsNullOrWhiteSpace(bruto) ? null : bruto.Split(';')[0].Trim();
    }

    private static string? Endereco(JsonElement tags)
    {
        var rua = Tag(tags, "addr:street");
        var numero = Tag(tags, "addr:housenumber");
        var bairro = Tag(tags, "addr:suburb");
        var cidade = Tag(tags, "addr:city");
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(rua)) partes.Add(string.IsNullOrWhiteSpace(numero) ? rua : $"{rua}, {numero}");
        if (!string.IsNullOrWhiteSpace(bairro)) partes.Add(bairro);
        if (!string.IsNullOrWhiteSpace(cidade)) partes.Add(cidade);
        return partes.Count == 0 ? Tag(tags, "addr:full") : string.Join(" — ", partes);
    }

    private static bool TryNumero(JsonElement el, string nome, out double valor)
    {
        valor = 0;
        if (!el.TryGetProperty(nome, out var p)) return false;
        return p.ValueKind == JsonValueKind.Number
            ? p.TryGetDouble(out valor)
            : p.ValueKind == JsonValueKind.String && double.TryParse(p.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out valor);
    }

    private static double DistanciaRetaKm(PontoGeografico a, PontoGeografico b)
    {
        const double raioTerraKm = 6371.0;
        double Rad(double g) => g * Math.PI / 180.0;
        var dLat = Rad(b.Latitude - a.Latitude);
        var dLon = Rad(b.Longitude - a.Longitude);
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(Rad(a.Latitude)) * Math.Cos(Rad(b.Latitude)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * raioTerraKm * Math.Asin(Math.Sqrt(h));
    }
}
