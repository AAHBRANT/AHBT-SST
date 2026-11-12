using AAHBRANT.SST.AgenteCameras.Opcoes;
using AAHBRANT.SST.AgenteCameras.Seguranca;

namespace AAHBRANT.SST.AgenteCameras.Servidor;

// Contrato de GET /api/agentes-camera/{agenteId}/configuracao (campos que o agente usa; o resto é ignorado).
public sealed record CameraServidor(
    Guid CameraId,
    string? Nome,
    Guid EquipamentoId,
    string? Host,
    int PortaRtsp,
    string? Usuario,
    string? Senha,
    int Canal,
    int Stream,
    int IntervaloEnvioSegundos)
{
    // O ToString gerado para records imprimiria a senha: este a omite.
    public override string ToString() => $"{nameof(CameraServidor)} {{ CameraId = {CameraId}, Host = {Host} }}";
}

public sealed record TesteServidor(
    Guid TesteId,
    Guid EquipamentoId,
    Guid? CameraId,
    string? Host,
    int PortaRtsp,
    string? Usuario,
    string? Senha,
    int Canal,
    int Stream)
{
    public override string ToString() => $"{nameof(TesteServidor)} {{ TesteId = {TesteId}, Host = {Host} }}";
}

public sealed record ConfiguracaoServidor(
    int IntervaloConsultaSegundos,
    List<CameraServidor>? Cameras,
    List<TesteServidor>? TestesConexao);

// Corpo de POST .../testes-conexao/{testeId}/resultado. Codigo (só na falha) é o motivo estável que a tela usa para escolher
// a mensagem (ver ClassificadorFalhaConexao.Cod*); Motivo e Detalhe são texto para o técnico de suporte.
public sealed record ResultadoTesteConexao(bool Sucesso, string? Motivo, string? Detalhe, string? MiniaturaBase64, string? Codigo = null);

public static class ConversorServidor
{
    public const int IntervaloPadraoSegundos = 60;
    public const int IntervaloMinimoSegundos = 15;
    public const int IntervaloMaximoSegundos = 300;

    public static TimeSpan IntervaloConsulta(int segundosDoServidor)
    {
        if (segundosDoServidor <= 0) return TimeSpan.FromSeconds(IntervaloPadraoSegundos);
        return TimeSpan.FromSeconds(Math.Clamp(segundosDoServidor, IntervaloMinimoSegundos, IntervaloMaximoSegundos));
    }

    /// <summary>
    /// Converte a câmera do servidor na configuração de leitura. Devolve null (com o motivo) para entrada que a leitura
    /// não suporta; a validação é a mesma da configuração local. A senha é registrada no mascarador.
    /// </summary>
    public static CameraConfig? Converter(CameraServidor c, Mascarador mascarador, out string? problema)
    {
        problema = null;
        if (c.CameraId == Guid.Empty) problema = "identificador ausente";
        else if (string.IsNullOrWhiteSpace(c.Host)) problema = "host não informado";
        else if (c.PortaRtsp is < 1 or > 65535) problema = "porta RTSP fora de 1 a 65535";
        else if (c.Canal is not (1 or 2)) problema = "canal deve ser 1 ou 2";
        else if (c.Stream is not (0 or 1)) problema = "stream deve ser 0 ou 1";
        else if (string.IsNullOrEmpty(c.Senha)) problema = "equipamento sem senha";
        if (problema is not null) return null;

        mascarador.Registrar(c.Senha);
        var id = c.CameraId.ToString("D");
        return new CameraConfig(
            id,
            string.IsNullOrWhiteSpace(c.Nome) ? id : c.Nome.Trim(),
            c.Host!.Trim(), c.PortaRtsp, (c.Usuario ?? "").Trim(), c.Senha!, c.Canal, c.Stream, null,
            TimeSpan.FromSeconds(Math.Clamp(c.IntervaloEnvioSegundos, 1, 600)));
    }
}
