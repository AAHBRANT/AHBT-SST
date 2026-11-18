using System.Text.RegularExpressions;

namespace AAHBRANT.SST.AgenteCameras.Servidor;

// Traduz o que o ffmpeg disse numa causa em português simples, para o técnico de segurança entender na tela.
// Nenhum texto devolvido aqui contém senha nem URL: o motivo é uma frase fixa e o detalhe passa por Sanitizar.
public static class ClassificadorFalhaConexao
{
    // Códigos estáveis do motivo (contrato com o servidor e a tela: o texto em português pode mudar, o código não).
    // O servidor aceita só estes; qualquer outro vira "erro_desconhecido". A tela mapeia a mensagem SÓ por este código.
    public const string CodSenhaRecusada = "senha_recusada";
    public const string CodAcessoNegado = "acesso_negado";
    public const string CodCameraNaoEncontrada = "camera_nao_encontrada";
    public const string CodCanalOuStreamInvalido = "canal_ou_stream_invalido";
    public const string CodConexaoRecusada = "conexao_recusada";
    public const string CodConexaoEncerrada = "conexao_encerrada";
    public const string CodSemResposta = "sem_resposta";
    public const string CodFfmpegAusente = "ffmpeg_ausente";
    public const string CodSemImagem = "sem_imagem";
    public const string CodImagemIlegivel = "imagem_ilegivel";
    public const string CodErroDesconhecido = "erro_desconhecido";

    public const string SenhaRecusada = "Senha recusada pela câmera";
    public const string AcessoNegado = "A câmera negou o acesso deste usuário";
    public const string VideoNaoEncontrado = "A câmera não tem esse canal ou stream (confira canal e stream)";
    public const string NaoEncontradaNaRede = "Câmera não encontrada na rede";
    public const string ConexaoRecusada = "A câmera recusou a conexão (confira a porta RTSP)";
    public const string ConexaoEncerrada = "A câmera encerrou a conexão (confira usuário, senha, canal e stream)";
    public const string SemResposta = "A câmera não respondeu a tempo";
    public const string FfmpegAusente = "O agente não encontrou o ffmpeg neste computador";
    public const string ImagemInvalida = "A câmera respondeu, mas a imagem recebida não pôde ser lida";
    public const string SemImagem = "A câmera conectou, mas não entregou imagem";
    public const string Generico = "Não foi possível receber imagem da câmera";

    private const RegexOptions Opcoes = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;

    // Códigos negativos são erros do Winsock vistos no ffmpeg para Windows (-10060 tempo, -10061 recusada, etc.).
    private static readonly (Regex Padrao, string Motivo, string Codigo)[] Regras =
    {
        (new Regex(@"\b401\b|unauthorized|authorization failed|authentication failed", Opcoes), SenhaRecusada, CodSenhaRecusada),
        (new Regex(@"\b403\b|forbidden", Opcoes), AcessoNegado, CodAcessoNegado),
        (new Regex(@"no route to host|network is unreachable|failed to resolve|name or service not known|nodename nor servname|getaddrinfo|host is down|host not found|could not resolve|-10065\b|-10051\b|-10064\b|-11001\b", Opcoes), NaoEncontradaNaRede, CodCameraNaoEncontrada),
        (new Regex(@"\b404\b|stream not found|not found", Opcoes), VideoNaoEncontrado, CodCanalOuStreamInvalido),
        (new Regex(@"connection refused|econnrefused|-10061\b", Opcoes), ConexaoRecusada, CodConexaoRecusada),
        (new Regex(@"connection reset|-10054\b|broken pipe", Opcoes), ConexaoEncerrada, CodConexaoEncerrada),
        (new Regex(@"timed out|etimedout|-10060\b|immediate exit requested", Opcoes), SemResposta, CodSemResposta),
    };

    /// <summary>Motivo em português para o que o ffmpeg (ou o sistema) relatou. Texto nulo/desconhecido = motivo genérico.</summary>
    public static string Classificar(string? saida) => ClassificarComCodigo(saida).Motivo;

    /// <summary>Motivo em português e código estável para o que o ffmpeg (ou o sistema) relatou.</summary>
    public static (string Motivo, string Codigo) ClassificarComCodigo(string? saida)
    {
        if (string.IsNullOrWhiteSpace(saida)) return (Generico, CodErroDesconhecido);
        foreach (var (padrao, motivo, codigo) in Regras)
            if (padrao.IsMatch(saida)) return (motivo, codigo);
        return (Generico, CodErroDesconhecido);
    }

    private static readonly Regex Url = new(@"\b[a-z][a-z0-9+.\-]*://\S*", Opcoes);
    private static readonly Regex Espacos = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Deixa o texto seguro para ir ao servidor e à tela: tira qualquer URL (que poderia levar usuário e senha), a
    /// senha literal (inclusive escapada), passa pelo mascarador e corta no tamanho máximo.
    /// </summary>
    public static string Sanitizar(string? texto, string? senha, Seguranca.Mascarador mascarador, int maximo)
    {
        var s = mascarador.Mascarar(texto);
        s = Url.Replace(s, "[endereço]");
        if (!string.IsNullOrEmpty(senha))
            foreach (var literal in new[] { senha, Uri.EscapeDataString(senha) }.Distinct().OrderByDescending(x => x.Length))
                s = s.Replace(literal, Seguranca.Mascarador.Marca, StringComparison.Ordinal);
        s = Espacos.Replace(s, " ").Trim();
        return s.Length <= maximo ? s : s[..(maximo - 1)].TrimEnd() + "…";
    }
}
