namespace AAHBRANT.SST.Application.Cameras;

// Códigos estáveis do motivo de falha do "Testar conexão" (contrato entre agente, servidor e tela). O agente envia o
// código; o servidor só guarda os conhecidos (qualquer outro vira erro_desconhecido). A tela escolhe a mensagem só por ele.
public static class CodigosTesteConexaoCamera
{
    public const string SenhaRecusada = "senha_recusada";
    public const string AcessoNegado = "acesso_negado";
    public const string CameraNaoEncontrada = "camera_nao_encontrada";
    public const string CanalOuStreamInvalido = "canal_ou_stream_invalido";
    public const string ConexaoRecusada = "conexao_recusada";
    public const string ConexaoEncerrada = "conexao_encerrada";
    public const string SemResposta = "sem_resposta";
    public const string FfmpegAusente = "ffmpeg_ausente";
    public const string SemImagem = "sem_imagem";
    public const string ImagemIlegivel = "imagem_ilegivel";
    public const string ErroDesconhecido = "erro_desconhecido";

    public static readonly IReadOnlyList<string> Conhecidos = new[]
    {
        SenhaRecusada, AcessoNegado, CameraNaoEncontrada, CanalOuStreamInvalido, ConexaoRecusada, ConexaoEncerrada,
        SemResposta, FfmpegAusente, SemImagem, ImagemIlegivel, ErroDesconhecido,
    };

    // Falha: o código conhecido, ou erro_desconhecido (ausente ou fora da lista). Sucesso não tem código.
    public static string? Normalizar(bool sucesso, string? codigo)
    {
        if (sucesso) return null;
        var limpo = codigo?.Trim().ToLowerInvariant();
        return limpo is not null && Conhecidos.Contains(limpo) ? limpo : ErroDesconhecido;
    }
}
