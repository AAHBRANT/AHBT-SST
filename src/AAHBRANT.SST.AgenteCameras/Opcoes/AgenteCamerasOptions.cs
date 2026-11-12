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
    TimeSpan IntervaloEnvio);

public sealed record ResultadoConfiguracao(
    ConfiguracaoAgente? Configuracao,
    IReadOnlyList<string> Erros,
    IReadOnlyList<string> Avisos)
{
    public bool Valida => Erros.Count == 0 && Configuracao is not null;
}
