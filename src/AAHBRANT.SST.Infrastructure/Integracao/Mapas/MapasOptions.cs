namespace AAHBRANT.SST.Infrastructure.Integracao.Mapas;

// Endereços dos serviços de mapa do plano de emergência (seção "Mapas" do appsettings). Tudo é opcional: sem
// configurar, valem os serviços públicos do OpenStreetMap. Serve para trocar por instâncias próprias ou por um
// espelho quando o serviço público estiver lento ou bloqueado na rede.
public class MapasOptions
{
    public string? Nominatim { get; set; }

    // Lista de espelhos do Overpass, tentados em ordem. Matriz (não lista) para o valor configurado substituir o padrão.
    public string[]? Overpass { get; set; }

    public string? Osrm { get; set; }
}
