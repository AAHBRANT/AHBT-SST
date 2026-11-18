namespace AAHBRANT.SST.AgenteCameras.Opcoes;

// Seção "AgenteCameras" da configuração, como escrita nos arquivos (valores ainda podem estar protegidos).
public sealed class AgenteCamerasOptions
{
    public const string Secao = "AgenteCameras";

    public string ApiBaseUrl { get; set; } = "";
    public string AgenteId { get; set; } = "";
    public string Segredo { get; set; } = "";
    public string CaminhoFfmpeg { get; set; } = "";
    public string PastaDados { get; set; } = "";
    public int FilaIdadeMaximaHoras { get; set; } = 24;
    public int FilaTamanhoMaximoMb { get; set; } = 500;
    // Liga a leitura das câmeras e dos testes de conexão cadastrados no SST-APP (padrão). Desligada, o serviço
    // usa só a lista "Cameras" deste arquivo, como antes, e não consulta a configuração no servidor.
    public bool LerConfiguracaoDoServidor { get; set; } = true;
    public List<CameraOptions> Cameras { get; set; } = new();
}

public sealed class CameraOptions
{
    public string CameraId { get; set; } = "";
    public string Nome { get; set; } = "";
    public string Host { get; set; } = "";
    public int PortaRtsp { get; set; } = 554;
    public string Usuario { get; set; } = "admin";
    public string Senha { get; set; } = "";
    public int Canal { get; set; } = 1;
    public int Stream { get; set; } = 1;
    public string? ModeloUrl { get; set; }
    public int IntervaloEnvioSegundos { get; set; } = 3;
    public bool Habilitada { get; set; } = true;
}

// Configuração já validada, com segredos em texto claro (só em memória).
public sealed record ConfiguracaoAgente(
    Uri ApiBaseUrl,
    string AgenteId,
    string Segredo,
    string CaminhoFfmpeg,
    string PastaDados,
    TimeSpan FilaIdadeMaxima,
    long FilaTamanhoMaximoBytes,
    IReadOnlyList<CameraConfig> Cameras,
    bool LerConfiguracaoDoServidor = true);

public sealed record CameraConfig(
    string CameraId,
    string Nome,
    string Host,
    int PortaRtsp,
    string Usuario,
    string Senha,
    int Canal,
    int Stream,
    string? ModeloUrl,
    TimeSpan IntervaloEnvio,
    int ParteImagem = ParteImagemCamera.Inteira);

// "Parte da imagem" da câmera: algumas (A31) entregam duas lentes empilhadas numa imagem só. O recorte é feito no
// ffmpeg, antes de qualquer envio. Faz parte da configuração de leitura: mudar a parte reinicia a câmera.
public static class ParteImagemCamera
{
    public const int Inteira = 0;
    public const int MetadeSuperior = 1;
    public const int MetadeInferior = 2;

    /// <summary>Ausente ou fora de 0..2 vira 0 (imagem inteira).</summary>
    public static int Normalizar(int valor) => valor is MetadeSuperior or MetadeInferior ? valor : Inteira;

    /// <summary>Filtro de recorte do ffmpeg (sem vírgula final), ou null para a imagem inteira.</summary>
    public static string? FiltroCrop(int parte) => Normalizar(parte) switch
    {
        MetadeSuperior => "crop=iw:ih/2:0:0",
        MetadeInferior => "crop=iw:ih/2:0:ih/2",
        _ => null,
    };
}

public sealed record ResultadoConfiguracao(
    ConfiguracaoAgente? Configuracao,
    IReadOnlyList<string> Erros,
    IReadOnlyList<string> Avisos)
{
    public bool Valida => Erros.Count == 0 && Configuracao is not null;
}
