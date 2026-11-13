namespace AAHBRANT.SST.Application.Cameras;

// Limites da câmera IP "quase ao vivo" (seção Cameras:AoVivo do appsettings). Os valores padrão são os do contrato; sem a
// seção configurada (ex.: Production sem a chave) o comportamento é o mesmo.
public class AoVivoCameraOpcoes
{
    public const string Secao = "Cameras:AoVivo";

    // Quadros por segundo pedidos ao agente (limitado a FpsMaximo).
    public int Fps { get; set; } = 2;
    public int FpsMaximo { get; set; } = 5;

    // Largura máxima (px) do quadro que o agente gera.
    public int LarguraMaxima { get; set; } = 640;

    // JPEG cru aceito na rota do agente.
    public int TamanhoMaximoQuadroBytes { get; set; } = 307_200;

    public int DuracaoMaximaSegundos { get; set; } = 600;
    public int InatividadeSegundos { get; set; } = 60;
    public int AgenteSemComecarSegundos { get; set; } = 60;

    // Sessão já iniciada cujo último quadro chegou há mais que isto: o agente parou (PC caiu) e a sessão encerra.
    public int SegundosSemQuadro { get; set; } = 30;
    public int MaximoCamerasPorAgente { get; set; } = 2;

    // "Fechei" na tela não derruba a transmissão na hora (outro espectador pode estar olhando): antecipa a inatividade para que a
    // sessão encerre por inatividade neste prazo se ninguém mais buscar quadro.
    public int EncerrarAoFecharEmSegundos { get; set; } = 10;

    // Cada GET de quadro marca espectador, mas o SQL só é regravado depois deste intervalo.
    public int GravarVisualizacaoACadaSegundos { get; set; } = 5;

    public int FpsEfetivo => Math.Clamp(Fps, 1, Math.Max(1, FpsMaximo));
}
