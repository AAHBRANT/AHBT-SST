using AAHBRANT.SST.Application.Cameras;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Integracao.SuporteIa;

namespace AAHBRANT.SST.Infrastructure.Integracao.Cameras;

public class ClaudeAnalisadorEpiImagem : IAnalisadorEpiImagem
{
    // Alturas mínimas (fração da altura da imagem) abaixo das quais o item não é julgado. Valores APROXIMADOS,
    // a calibrar com fotos reais das obras.
    // Cabeça: o guia da Intelbras pede cabeça de pelo menos 40 px em imagem de 2 MP (1080 px de altura),
    // cerca de 3,7% da altura; 0,035 é o corte conservador. Cabeça menor que isso: capacete não avaliável.
    public const double AlturaMinimaCabeca = 0.035;

    // Colete: depende do tamanho do tronco, que a IA não mede; usa-se a caixa da pessoa (só a parte que aparece).
    // Peito e tronco até a cintura somam uns 2,3 a 3 alturas de cabeça; com a cabeça no mínimo (3,5%), isso dá
    // 8% a 10%. Por isso 0,08: abaixo disso nem uma pessoa da cintura para cima teria tronco legível. Pessoa da
    // cintura para cima, mais alta que isso, continua sendo julgada.
    public const double AlturaMinimaPessoaParaColete = 0.08;

    // Óculos: distinguir os olhos pede cabeça bem maior que para ver um capacete. 0,07 da altura da imagem (o dobro do mínimo
    // do capacete) é um valor APROXIMADO, a calibrar com fotos reais; cabeça menor que isso: óculos não avaliável.
    public const double AlturaMinimaCabecaParaOculos = 0.07;

    // Tolerância nas comparações de limite (ponto flutuante).
    private const double Tolerancia = 1e-9;

    private const string Conforme = "conforme";
    private const string NaoConforme = "nao_conforme";
    private const string NaoAvaliavel = "nao_avaliavel";

    // Cores de capacete que a IA pode devolver (o padrão AAHBRANT); "outra" e "nao_avaliavel" não viram cor.
    private static readonly string[] CoresCapacete = { "branco", "amarelo", "azul", "verde", "vermelho", "laranja", "outra", NaoAvaliavel };

    public const string Instrucoes =
        "Você analisa fotos de canteiro de obras para segurança do trabalho. " +
        "Conte apenas pessoas claramente visíveis na imagem. " +
        "A imagem pode estar girada ou de cabeça para baixo (câmera instalada invertida): isso não impede avaliar; " +
        "interprete a pessoa na orientação em que ela aparece e avalie normalmente. " +
        "Para cada pessoa, avalie três itens, e cada item só pode ser avaliado quando a parte do corpo dele aparece na imagem, " +
        "de frente, de costas ou de lado: " +
        "(1) capacete de segurança: avalie se a cabeça aparece o suficiente para ver o que está sobre ela; " +
        "o capacete fica no topo da cabeça: se o topo da cabeça está cortado pela borda da imagem ou encoberto, " +
        "responda nao_avaliavel para o capacete, mesmo que o rosto apareça, porque o capacete pode estar justamente na parte que não aparece; " +
        "de costas ou de lado a cabeça e o capacete também são avaliáveis; boné, touca, capuz ou chapéu não contam como capacete; " +
        "(2) colete refletivo ou de alta visibilidade: avalie se os ombros e o tronco aparecem, de frente, de costas ou de lado; " +
        "o colete tem faixas refletivas também nas costas, então de costas ele é avaliável; " +
        "o colete só é avaliável quando os DOIS ombros e o peito (ou as costas) aparecem: se os ombros estão cortados pela borda da imagem, " +
        "o colete é nao_avaliavel, mesmo que a barriga ou a cintura apareçam; " +
        "(3) óculos de proteção: avalie somente se o rosto aparece, de frente ou de lado, com os olhos visíveis, e está perto o bastante para distinguir os olhos; " +
        "de costas o rosto não aparece (rostoVisivel é false); " +
        "lente escura ou fumê também conta como óculos de proteção; você não distingue certificação nem tipo, então qualquer óculos sobre os olhos " +
        "(de proteção, de grau ou de sol) conta como conforme; responda nao_conforme somente quando você vê os olhos descobertos, sem nada sobre eles; " +
        "de costas, com o rosto longe, pequeno, borrado, encoberto ou cortado pela borda da imagem, responda nao_avaliavel para os óculos. " +
        "Além dos três itens, informe a cor do capacete de cada pessoa, somente quando o capacete está sendo usado (conforme) e você distingue a cor: " +
        "branco, amarelo, azul, verde, vermelho ou laranja; qualquer outra cor (preto, cinza, marrom, etc.) é outra; " +
        "se não há capacete, se ele não aparece ou se a cor não é distinguível (contraluz, pessoa pequena, imagem estourada), responda nao_avaliavel. " +
        "Estar de costas ou de lado só justifica nao_avaliavel quando, por causa disso, você realmente não consegue ver o item. " +
        "Se a parte do corpo não aparece (cortada pela borda da imagem, encoberta por objeto ou por outra pessoa, " +
        "ou longe demais para distinguir), responda nao_avaliavel para aquele item. " +
        "Nunca presuma: se você não vê a parte do corpo, não diga que o item está sendo usado nem que está faltando. " +
        "Responda nao_conforme somente quando você vê a parte do corpo e o item não está nela; " +
        "responda conforme somente quando você vê o item sendo usado. " +
        "Não identifique pessoas nem descreva rostos. Responda somente no formato solicitado.";

    public const string Pedido =
        "Informe: pessoas (total de pessoas visíveis), semCapacete e semColete (conte somente pessoas em que você viu a parte do corpo e o item não está presente; " +
        "itens nao_avaliavel não entram nessas contagens) e confianca (alta, media ou baixa) da sua avaliação. " +
        "Em pessoasDetalhe, inclua uma entrada para cada pessoa contada, com: " +
        "cabecaX e cabecaY (posição aproximada do centro da cabeça; cabecaX de 0 na borda esquerda a 1 na borda direita; cabecaY de 0 no topo a 1 na base da imagem, sempre na imagem como recebida); " +
        "cabecaVisivel (true se a cabeça aparece o suficiente para ver o que está sobre ela, incluindo o topo da cabeça, mesmo de costas ou de lado; false se o topo da cabeça está cortado ou encoberto); " +
        "troncoVisivel (true somente se os dois ombros e o peito ou as costas aparecem, de frente, de costas ou de lado; false se os ombros estão cortados pela borda da imagem ou encobertos, mesmo que a barriga ou a cintura apareçam); " +
        "rostoVisivel (true somente se o rosto aparece de frente ou de lado, com os olhos visíveis; false de costas, com o rosto encoberto ou cortado); " +
        "capacete e colete (conforme, nao_conforme ou nao_avaliavel, segundo as regras das instruções); " +
        "oculos (conforme, nao_conforme ou nao_avaliavel, segundo as regras das instruções); " +
        "corCapacete (branco, amarelo, azul, verde, vermelho, laranja, outra ou nao_avaliavel, segundo as regras das instruções); " +
        "caixa (x0, y0, x1, y1 de 0 a 1: retângulo da pessoa inteira, incluindo só a parte que aparece se ela estiver cortada); " +
        "caixaCabeca (x0, y0, x1, y1 de 0 a 1: retângulo da cabeça, incluindo o que está sobre ela; se a cabeça não aparece, use 0, 0, 0, 0). " +
        "A quantidade de entradas deve ser igual a pessoas.";

    private static readonly object Schema = new
    {
        type = "object",
        properties = new
        {
            pessoas = new { type = "integer" },
            semCapacete = new { type = "integer" },
            semColete = new { type = "integer" },
            confianca = new { type = "string", @enum = new[] { "alta", "media", "baixa" } },
            pessoasDetalhe = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        cabecaX = new { type = "number" },
                        cabecaY = new { type = "number" },
                        cabecaVisivel = new { type = "boolean" },
                        troncoVisivel = new { type = "boolean" },
                        rostoVisivel = new { type = "boolean" },
                        capacete = new { type = "string", @enum = new[] { Conforme, NaoConforme, NaoAvaliavel } },
                        colete = new { type = "string", @enum = new[] { Conforme, NaoConforme, NaoAvaliavel } },
                        oculos = new { type = "string", @enum = new[] { Conforme, NaoConforme, NaoAvaliavel } },
                        corCapacete = new { type = "string", @enum = CoresCapacete },
                        caixa = new
                        {
                            type = "object",
                            properties = new
                            {
                                x0 = new { type = "number" },
                                y0 = new { type = "number" },
                                x1 = new { type = "number" },
                                y1 = new { type = "number" },
                            },
                            required = new[] { "x0", "y0", "x1", "y1" },
                            additionalProperties = false,
                        },
                        caixaCabeca = new
                        {
                            type = "object",
                            properties = new
                            {
                                x0 = new { type = "number" },
                                y0 = new { type = "number" },
                                x1 = new { type = "number" },
                                y1 = new { type = "number" },
                            },
                            required = new[] { "x0", "y0", "x1", "y1" },
                            additionalProperties = false,
                        },
                    },
                    required = new[] { "cabecaX", "cabecaY", "cabecaVisivel", "troncoVisivel", "rostoVisivel", "capacete", "colete", "oculos", "corCapacete", "caixa", "caixaCabeca" },
                    additionalProperties = false,
                },
            },
        },
        required = new[] { "pessoas", "semCapacete", "semColete", "confianca", "pessoasDetalhe" },
        additionalProperties = false,
    };

    private sealed record CaixaResposta(double X0, double Y0, double X1, double Y1);
    private sealed record PessoaResposta(double CabecaX, double CabecaY, bool? CabecaVisivel, bool? TroncoVisivel,
        string? Capacete, string? Colete, CaixaResposta? Caixa, CaixaResposta? CaixaCabeca = null,
        string? Oculos = null, string? CorCapacete = null, bool? RostoVisivel = null);
    private sealed record Resposta(int Pessoas, int SemCapacete, int SemColete, string? Confianca, List<PessoaResposta>? PessoasDetalhe = null);

    private readonly ClaudeChatJsonCliente _cliente;
    public ClaudeAnalisadorEpiImagem(ClaudeChatJsonCliente cliente) => _cliente = cliente;

    public async Task<ResultadoAnaliseEpi> AnalisarAsync(byte[] imagemJpeg, CancellationToken ct)
    {
        var imagens = new[] { $"data:image/jpeg;base64,{Convert.ToBase64String(imagemJpeg)}" };
        var (r, tokensEntrada, tokensSaida) = await _cliente.ObterComUsoAsync<Resposta>(Instrucoes, Pedido, "analise_epi", Schema, ct,
            maxTokens: 2500, timeoutSegundos: 60, imagens: imagens, modelo: _cliente.ModeloCameras);

        var confianca = r.Confianca?.ToLowerInvariant() switch
        {
            "alta" => ConfiancaAnaliseEpi.Alta,
            "media" => ConfiancaAnaliseEpi.Media,
            _ => ConfiancaAnaliseEpi.Baixa,
        };
        var uso = new UsoIa(tokensEntrada, tokensSaida);

        // Detalhe em formato válido: os totais saem SEMPRE dele (os da IA são ignorados). Se a quantidade de entradas
        // não bate com "pessoas", vale o maior dos dois para o total de pessoas e só o cruzamento com rostos fica
        // desligado (Detalhe nulo), porque sem correspondência 1 a 1 não dá para ligar rosto a pessoa.
        var detalhe = DetalheValido(r);
        if (detalhe is not null)
        {
            var pessoas = Math.Max(r.Pessoas, detalhe.Count);
            return new ResultadoAnaliseEpi(pessoas, detalhe.Count(p => p.SemCapacete), detalhe.Count(p => p.SemColete), confianca,
                detalhe.Count == r.Pessoas ? detalhe : null, uso,
                detalhe.Count(p => p.CapaceteNaoAvaliavel), detalhe.Count(p => p.ColeteNaoAvaliavel),
                detalhe.Count(p => p.SemOculos), detalhe.Count(p => p.OculosNaoAvaliavel));
        }

        // Sem detalhe válido: valem os totais da IA, que o pedido define como só itens vistos e não conformes.
        // Óculos e cor só existem por pessoa: sem detalhe não há ocorrência de óculos nem aviso de cor.
        if (r.Pessoas < 0 || r.SemCapacete < 0 || r.SemColete < 0 || r.SemCapacete > r.Pessoas || r.SemColete > r.Pessoas)
            throw new InvalidOperationException("A IA devolveu contagens incoerentes.");
        return new ResultadoAnaliseEpi(r.Pessoas, r.SemCapacete, r.SemColete, confianca, null, uso);
    }

    // O detalhe só vale com todos os campos de cada entrada preenchidos; senão a resposta está fora do formato.
    private static IReadOnlyList<PessoaNaCena>? DetalheValido(Resposta r)
    {
        var detalhe = r.PessoasDetalhe;
        if (detalhe is null
            || detalhe.Any(p => double.IsNaN(p.CabecaX) || double.IsNaN(p.CabecaY)
                                || p.CabecaVisivel is null || p.TroncoVisivel is null || p.Capacete is null || p.Colete is null))
            return null;

        return detalhe.Select(Converter).ToList();
    }

    // Parte do corpo invisível, cabeça pequena demais, pessoa pequena demais (colete) ou valor desconhecido viram
    // "não avaliável": a IA dizer nao_conforme sem ver a parte do corpo, ou sem tamanho para julgar, não conta.
    private static PessoaNaCena Converter(PessoaResposta p)
    {
        var caixa = CaixaValida(p.Caixa);
        var caixaCabeca = CaixaValida(p.CaixaCabeca);
        var cabecaPequena = caixaCabeca is not null && caixaCabeca.Altura + Tolerancia < AlturaMinimaCabeca;
        var pessoaPequenaParaColete = caixa is not null && caixa.Altura + Tolerancia < AlturaMinimaPessoaParaColete;

        var capacete = p.CabecaVisivel != true || cabecaPequena ? NaoAvaliavel : Normalizar(p.Capacete);
        var colete = p.TroncoVisivel != true || pessoaPequenaParaColete ? NaoAvaliavel : Normalizar(p.Colete);
        // Óculos só são julgados com o rosto visível (de frente ou de lado, olhos à vista) e a cabeça grande o bastante para
        // distinguir os olhos; caixaCabeca ausente/inválida ou rostoVisivel ausente = não avaliável.
        var cabecaPequenaParaOculos = caixaCabeca is null || caixaCabeca.Altura + Tolerancia < AlturaMinimaCabecaParaOculos;
        var oculos = p.RostoVisivel != true || cabecaPequenaParaOculos ? NaoAvaliavel : Normalizar(p.Oculos);
        // Cor só com capacete conforme e cabeça com tamanho para distinguir; "outra" e nao_avaliavel não viram cor.
        var cor = capacete == Conforme && !cabecaPequena ? ConverterCor(p.CorCapacete) : null;

        return new PessoaNaCena(Math.Clamp(p.CabecaX, 0, 1), Math.Clamp(p.CabecaY, 0, 1),
            SemCapacete: capacete == NaoConforme, SemColete: colete == NaoConforme,
            CabecaVisivel: p.CabecaVisivel == true, TroncoVisivel: p.TroncoVisivel == true,
            CapaceteNaoAvaliavel: capacete == NaoAvaliavel, ColeteNaoAvaliavel: colete == NaoAvaliavel,
            Caixa: caixa, CaixaCabeca: caixaCabeca,
            SemOculos: oculos == NaoConforme, OculosNaoAvaliavel: oculos == NaoAvaliavel, CorCapaceteVista: cor);
    }

    // As seis cores do padrão; qualquer outra coisa (outra, nao_avaliavel, valor desconhecido ou ausente) fica nula.
    private static CorCapacete? ConverterCor(string? valor) => valor?.Trim().ToLowerInvariant() switch
    {
        "branco" => CorCapacete.Branco,
        "amarelo" => CorCapacete.Amarelo,
        "azul" => CorCapacete.Azul,
        "verde" => CorCapacete.Verde,
        "vermelho" => CorCapacete.Vermelho,
        "laranja" => CorCapacete.Laranja,
        _ => null,
    };

    // Qualquer valor fora de conforme/nao_conforme conta como não avaliável.
    private static string Normalizar(string? valor) => valor?.Trim().ToLowerInvariant() switch
    {
        Conforme => Conforme,
        NaoConforme => NaoConforme,
        _ => NaoAvaliavel,
    };

    // Caixa ausente ou sem sentido (número inválido, altura nula, como o 0,0,0,0 de cabeça fora): o tamanho mínimo não se aplica.
    private static CaixaPessoa? CaixaValida(CaixaResposta? c)
    {
        if (c is null || double.IsNaN(c.X0) || double.IsNaN(c.Y0) || double.IsNaN(c.X1) || double.IsNaN(c.Y1)) return null;
        var y0 = Math.Clamp(Math.Min(c.Y0, c.Y1), 0, 1);
        var y1 = Math.Clamp(Math.Max(c.Y0, c.Y1), 0, 1);
        if (y1 <= y0) return null;
        return new CaixaPessoa(Math.Clamp(Math.Min(c.X0, c.X1), 0, 1), y0, Math.Clamp(Math.Max(c.X0, c.X1), 0, 1), y1);
    }
}
