using AAHBRANT.SST.Application.Cameras;
using System.Net;
using System.Text.Json;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Integracao.Cameras;
using AAHBRANT.SST.Infrastructure.Integracao.SuporteIa;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Tests.Cameras;

public class ClaudeAnalisadorEpiImagemTests
{
    private sealed class HandlerFalso : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _corpoResposta;
        public string? CorpoEnviado { get; private set; }
        public HttpRequestMessage? Requisicao { get; private set; }

        public HandlerFalso(HttpStatusCode status, string corpoResposta)
        {
            _status = status;
            _corpoResposta = corpoResposta;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requisicao = request;
            CorpoEnviado = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(_status) { Content = new StringContent(_corpoResposta) };
        }
    }

    private sealed class FabricaFalsa : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public FabricaFalsa(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private static string RespostaClaude(string jsonInterno)
        => JsonSerializer.Serialize(new { content = new object[] { new { type = "text", text = jsonInterno } } });

    private static ClaudeAnalisadorEpiImagem Criar(HandlerFalso handler, string? apiKey = "chave-de-teste")
    {
        var cliente = new ClaudeChatJsonCliente(
            new FabricaFalsa(handler),
            Options.Create(new AzureOpenAiOptions()),
            Options.Create(new ClaudeOptions { ApiKey = apiKey }),
            NullLogger<ClaudeChatJsonCliente>.Instance);
        return new ClaudeAnalisadorEpiImagem(cliente);
    }

    [Fact]
    public async Task Analisar_DevolveContagensEEnviaImagemEmBase64()
    {
        var handler = new HandlerFalso(HttpStatusCode.OK,
            RespostaClaude("{\"pessoas\":2,\"semCapacete\":1,\"semColete\":0,\"confianca\":\"alta\"}"));
        var imagem = new byte[] { 0xFF, 0xD8, 0x01, 0x02 };

        var resultado = await Criar(handler).AnalisarAsync(imagem, default);

        Assert.Equal(2, resultado.Pessoas);
        Assert.Equal(1, resultado.SemCapacete);
        Assert.Equal(0, resultado.SemColete);
        Assert.Equal(ConfiancaAnaliseEpi.Alta, resultado.Confianca);
        Assert.Contains(Convert.ToBase64String(imagem), handler.CorpoEnviado);
        Assert.Contains("image/jpeg", handler.CorpoEnviado);
        Assert.EndsWith("/v1/messages", handler.Requisicao!.RequestUri!.AbsolutePath);
    }

    // Uma entrada de pessoasDetalhe no formato novo. Padrão: pessoa inteira, de corpo todo, conforme.
    private static string Pessoa(double x = 0.5, double y = 0.3, bool cabeca = true, bool tronco = true,
        string capacete = "conforme", string colete = "conforme", double y0 = 0.1, double y1 = 0.9,
        double cabecaY0 = 0.1, double cabecaY1 = 0.2)
        => string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{{\"cabecaX\":{x},\"cabecaY\":{y},\"cabecaVisivel\":{(cabeca ? "true" : "false")},\"troncoVisivel\":{(tronco ? "true" : "false")}," +
            $"\"capacete\":\"{capacete}\",\"colete\":\"{colete}\",\"caixa\":{{\"x0\":0.3,\"y0\":{y0},\"x1\":0.7,\"y1\":{y1}}}," +
            $"\"caixaCabeca\":{{\"x0\":0.3,\"y0\":{cabecaY0},\"x1\":0.7,\"y1\":{cabecaY1}}}}}");

    // Totais da IA propositalmente diferentes do detalhe: o que vale é o detalhe.
    private static string Resposta(int pessoasIa, int semCapaceteIa, int semColeteIa, params string[] pessoas)
        => RespostaClaude(
            $"{{\"pessoas\":{pessoasIa},\"semCapacete\":{semCapaceteIa},\"semColete\":{semColeteIa},\"confianca\":\"alta\",\"pessoasDetalhe\":[{string.Join(",", pessoas)}]}}");

    private static Task<ResultadoAnaliseEpi> Analisar(string resposta)
        => Criar(new HandlerFalso(HttpStatusCode.OK, resposta)).AnalisarAsync(new byte[] { 0xFF, 0xD8, 0x01 }, default);

    [Fact]
    public async Task Analisar_ComPosicoesCoerentes_DevolveODetalheDeCadaPessoa()
    {
        var handler = new HandlerFalso(HttpStatusCode.OK, Resposta(2, 1, 0,
            Pessoa(x: 0.25, y: 0.3, capacete: "nao_conforme"),
            Pessoa(x: 1.4, y: -0.2)));

        var resultado = await Criar(handler).AnalisarAsync(new byte[] { 0xFF, 0xD8, 0x01 }, default);

        Assert.NotNull(resultado.Detalhe);
        Assert.Equal(2, resultado.Detalhe!.Count);
        var caixa = new CaixaPessoa(0.3, 0.1, 0.7, 0.9);
        var cabeca = new CaixaPessoa(0.3, 0.1, 0.7, 0.2);
        Assert.Equal(new PessoaNaCena(0.25, 0.3, true, false, Caixa: caixa, CaixaCabeca: cabeca), resultado.Detalhe[0]);
        // Posição fora da imagem é trazida para a borda.
        Assert.Equal(new PessoaNaCena(1, 0, false, false, Caixa: caixa, CaixaCabeca: cabeca), resultado.Detalhe[1]);
        Assert.Contains("pessoasDetalhe", handler.CorpoEnviado);
    }

    [Fact]
    public async Task Analisar_CalculaOsTotaisAPartirDoDetalheEIgnoraOsTotaisDaIa()
    {
        // A IA diz 3 pessoas / 4 sem capacete / 4 sem colete, mas o detalhe só tem 1 de cada.
        var r = await Analisar(Resposta(3, 4, 4,
            Pessoa(capacete: "nao_conforme"),
            Pessoa(colete: "nao_conforme"),
            Pessoa()));

        Assert.Equal(3, r.Pessoas);
        Assert.Equal(1, r.SemCapacete);
        Assert.Equal(1, r.SemColete);
        Assert.Equal(0, r.CapaceteNaoAvaliavel);
        Assert.Equal(0, r.ColeteNaoAvaliavel);
    }

    [Fact]
    public async Task Analisar_CabecaForaDaImagem_CapaceteNaoConformeViraNaoAvaliavel()
    {
        var r = await Analisar(Resposta(1, 1, 0, Pessoa(cabeca: false, capacete: "nao_conforme", colete: "conforme")));

        Assert.Equal(1, r.Pessoas);
        Assert.Equal(0, r.SemCapacete);
        Assert.Equal(1, r.CapaceteNaoAvaliavel);
        Assert.Equal(0, r.ColeteNaoAvaliavel);
        var p = Assert.Single(r.Detalhe!);
        Assert.False(p.CabecaVisivel);
        Assert.False(p.SemCapacete);
        Assert.True(p.CapaceteNaoAvaliavel);
    }

    [Fact]
    public async Task Analisar_TroncoForaDaImagem_ColeteNaoConformeViraNaoAvaliavel()
    {
        var r = await Analisar(Resposta(1, 0, 1, Pessoa(tronco: false, capacete: "conforme", colete: "nao_conforme")));

        Assert.Equal(0, r.SemColete);
        Assert.Equal(1, r.ColeteNaoAvaliavel);
        Assert.Equal(0, r.CapaceteNaoAvaliavel);
    }

    [Fact]
    public async Task Analisar_SoPernas_NadaEJulgado()
    {
        var r = await Analisar(Resposta(1, 1, 1, Pessoa(cabeca: false, tronco: false, capacete: "nao_conforme", colete: "nao_conforme")));

        Assert.Equal(1, r.Pessoas);
        Assert.Equal(0, r.SemCapacete);
        Assert.Equal(0, r.SemColete);
        Assert.Equal(1, r.CapaceteNaoAvaliavel);
        Assert.Equal(1, r.ColeteNaoAvaliavel);
    }

    [Fact]
    public async Task Analisar_IaDizNaoAvaliavelComParteVisivel_ContaComoNaoAvaliavel()
    {
        var r = await Analisar(Resposta(1, 0, 0, Pessoa(capacete: "nao_avaliavel", colete: "conforme")));

        Assert.Equal(0, r.SemCapacete);
        Assert.Equal(1, r.CapaceteNaoAvaliavel);
        Assert.Equal(0, r.ColeteNaoAvaliavel);
    }

    [Fact]
    public async Task Analisar_ValorDesconhecido_ViraNaoAvaliavelENaoAlerta()
    {
        var r = await Analisar(Resposta(1, 0, 0, Pessoa(capacete: "talvez", colete: "")));

        Assert.Equal(0, r.SemCapacete);
        Assert.Equal(0, r.SemColete);
        Assert.Equal(1, r.CapaceteNaoAvaliavel);
        Assert.Equal(1, r.ColeteNaoAvaliavel);
    }

    [Fact]
    public async Task Analisar_CabecaPequenaDemais_CapaceteNaoAvaliavelMasColeteContinuaJulgado()
    {
        // Cabeça de 3% da imagem (abaixo do mínimo), pessoa grande: só o capacete deixa de ser julgado.
        var r = await Analisar(Resposta(1, 1, 1,
            Pessoa(capacete: "nao_conforme", colete: "nao_conforme", cabecaY0: 0.10, cabecaY1: 0.13)));

        Assert.Equal(0, r.SemCapacete);
        Assert.Equal(1, r.CapaceteNaoAvaliavel);
        Assert.Equal(1, r.SemColete);
        Assert.Equal(0, r.ColeteNaoAvaliavel);
    }

    [Fact]
    public async Task Analisar_AlturaDaCabecaNoLimite_ComTolerancia_PeloDoisLados()
    {
        var min = ClaudeAnalisadorEpiImagem.AlturaMinimaCabeca;
        // 0.2 + min - 0.2 pode sair 1e-17 abaixo de min em ponto flutuante: a tolerância mantém a pessoa julgada.
        var noLimite = Pessoa(capacete: "nao_conforme", colete: "conforme", cabecaY0: 0.2, cabecaY1: 0.2 + min);
        var logoAbaixo = Pessoa(capacete: "nao_conforme", colete: "conforme", cabecaY0: 0.2, cabecaY1: 0.2 + min - 0.001);
        var logoAcima = Pessoa(capacete: "nao_conforme", colete: "conforme", cabecaY0: 0.2, cabecaY1: 0.2 + min + 0.001);

        var r = await Analisar(Resposta(3, 3, 0, noLimite, logoAbaixo, logoAcima));

        Assert.Equal(2, r.SemCapacete);
        Assert.Equal(1, r.CapaceteNaoAvaliavel);
        Assert.False(r.Detalhe![0].CapaceteNaoAvaliavel);
        Assert.True(r.Detalhe[1].CapaceteNaoAvaliavel);
        Assert.False(r.Detalhe[2].CapaceteNaoAvaliavel);
    }

    [Fact]
    public async Task Analisar_PessoaDaCinturaParaCima_ComCabecaNoTamanhoMinimo_ContinuaJulgada()
    {
        // Parte visível da pessoa com 14% da altura (cabeça + tronco até a cintura): antes, o corte de 10% sobre a caixa
        // da pessoa derrubava casos próximos disso; agora vale o tamanho da cabeça e esta pessoa é julgada nos dois itens.
        var r = await Analisar(Resposta(1, 1, 1,
            Pessoa(capacete: "nao_conforme", colete: "nao_conforme", y0: 0.30, y1: 0.44, cabecaY0: 0.30, cabecaY1: 0.37)));

        Assert.Equal(1, r.SemCapacete);
        Assert.Equal(1, r.SemColete);
        Assert.Equal(0, r.CapaceteNaoAvaliavel);
        Assert.Equal(0, r.ColeteNaoAvaliavel);
    }

    [Fact]
    public async Task Analisar_PessoaPequenaDemaisParaColete_ColeteNaoAvaliavelMasCapaceteJulgado()
    {
        var min = ClaudeAnalisadorEpiImagem.AlturaMinimaPessoaParaColete;
        // Caixa da pessoa de 7% (abaixo de 8%) com cabeça de 4%: capacete julgado, colete não.
        var pequena = Pessoa(capacete: "nao_conforme", colete: "nao_conforme", y0: 0.40, y1: 0.47, cabecaY0: 0.40, cabecaY1: 0.44);
        // Caixa da pessoa exatamente no mínimo (com tolerância): colete julgado.
        var noLimite = Pessoa(capacete: "conforme", colete: "nao_conforme", y0: 0.40, y1: 0.40 + min, cabecaY0: 0.40, cabecaY1: 0.44);

        var r = await Analisar(Resposta(2, 1, 2, pequena, noLimite));

        Assert.Equal(1, r.SemCapacete);
        Assert.Equal(1, r.SemColete);
        Assert.Equal(1, r.ColeteNaoAvaliavel);
        Assert.True(r.Detalhe![0].ColeteNaoAvaliavel);
        Assert.False(r.Detalhe[1].ColeteNaoAvaliavel);
    }

    [Fact]
    public async Task Analisar_PessoaDeCostasSemColete_ContaSemColete()
    {
        // De costas a cabeça e o tronco aparecem (o colete tem faixas nas costas): é julgada normalmente.
        var r = await Analisar(Resposta(1, 0, 1, Pessoa(cabeca: true, tronco: true, capacete: "conforme", colete: "nao_conforme")));

        Assert.Equal(1, r.SemColete);
        Assert.Equal(0, r.ColeteNaoAvaliavel);
    }

    // Cenário A: IA diz 3 pessoas, mas o detalhe tem 2 entradas e só 1 nao_conforme.
    [Fact]
    public async Task Analisar_CenarioA_MenosEntradasQuePessoas_TotaisDoDetalheESemCruzamentoComRostos()
    {
        var r = await Analisar(Resposta(3, 3, 3, Pessoa(capacete: "nao_conforme"), Pessoa()));

        Assert.Equal(3, r.Pessoas);
        Assert.Equal(1, r.SemCapacete);
        Assert.Equal(0, r.SemColete);
        Assert.Null(r.Detalhe);
    }

    // Cenário B: IA diz 0 pessoas, mas há entradas no detalhe.
    [Fact]
    public async Task Analisar_CenarioB_ZeroPessoasComEntradas_UsaAQuantidadeDeEntradas()
    {
        var r = await Analisar(Resposta(0, 0, 0, Pessoa(capacete: "nao_conforme"), Pessoa(colete: "nao_conforme")));

        Assert.Equal(2, r.Pessoas);
        Assert.Equal(1, r.SemCapacete);
        Assert.Equal(1, r.SemColete);
        Assert.Null(r.Detalhe);
    }

    // Cenário C: IA diz 2 pessoas e semColete=1, mas a única entrada é de só pernas: não pode alertar.
    [Fact]
    public async Task Analisar_CenarioC_TotalDaIaComUmaEntradaSoPernas_NaoAlerta()
    {
        var r = await Analisar(Resposta(2, 0, 1,
            Pessoa(cabeca: false, tronco: false, capacete: "nao_conforme", colete: "nao_conforme")));

        Assert.Equal(2, r.Pessoas);
        Assert.Equal(0, r.SemCapacete);
        Assert.Equal(0, r.SemColete);
        Assert.Equal(1, r.CapaceteNaoAvaliavel);
        Assert.Equal(1, r.ColeteNaoAvaliavel);
    }

    [Fact]
    public async Task Analisar_PessoaInteiraSemCapacete_ContaNormalmente()
    {
        var r = await Analisar(Resposta(1, 1, 0, Pessoa(capacete: "nao_conforme", colete: "conforme")));

        Assert.Equal(1, r.SemCapacete);
        Assert.Equal(0, r.CapaceteNaoAvaliavel);
    }

    [Fact]
    public async Task Analisar_EntradaNoFormatoAntigo_UsaTotaisDaIaSemDetalhe()
    {
        // Formato antigo por pessoa (sem cabecaVisivel/capacete/colete): o detalhe é descartado.
        var r = await Analisar(RespostaClaude(
            "{\"pessoas\":1,\"semCapacete\":1,\"semColete\":0,\"confianca\":\"media\",\"pessoasDetalhe\":[" +
            "{\"cabecaX\":0.25,\"cabecaY\":0.3,\"semCapacete\":true,\"semColete\":false}]}"));

        Assert.Equal(1, r.Pessoas);
        Assert.Equal(1, r.SemCapacete);
        Assert.Null(r.Detalhe);
        Assert.Equal(0, r.CapaceteNaoAvaliavel);
    }

    [Fact]
    public async Task Analisar_CaixaAusenteOuInvalida_NaoAplicaOTamanhoMinimo()
    {
        var semCaixa = "{\"cabecaX\":0.5,\"cabecaY\":0.3,\"cabecaVisivel\":true,\"troncoVisivel\":true,\"capacete\":\"nao_conforme\",\"colete\":\"conforme\"}";
        var caixaZerada = "{\"cabecaX\":0.5,\"cabecaY\":0.3,\"cabecaVisivel\":true,\"troncoVisivel\":true,\"capacete\":\"nao_conforme\",\"colete\":\"conforme\"," +
                          "\"caixa\":{\"x0\":0.3,\"y0\":0.5,\"x1\":0.7,\"y1\":0.5}}";

        var r = await Analisar(Resposta(2, 2, 0, semCaixa, caixaZerada));

        Assert.Equal(2, r.SemCapacete);
        Assert.All(r.Detalhe!, p => Assert.Null(p.Caixa));
    }

    [Fact]
    public void Prompt_NaoPresumeOItemEMandaMarcarNaoAvaliavel()
    {
        Assert.DoesNotContain("considere que ela está usando", ClaudeAnalisadorEpiImagem.Instrucoes);
        Assert.Contains("nao_avaliavel", ClaudeAnalisadorEpiImagem.Instrucoes);
        Assert.Contains("Nunca presuma", ClaudeAnalisadorEpiImagem.Instrucoes);
        Assert.Contains("de costas ou de lado a cabeça e o capacete também são avaliáveis", ClaudeAnalisadorEpiImagem.Instrucoes);
        Assert.Contains("faixas refletivas também nas costas", ClaudeAnalisadorEpiImagem.Instrucoes);
        Assert.Contains("de cabeça para baixo", ClaudeAnalisadorEpiImagem.Instrucoes);
        Assert.DoesNotContain("aparece inteira", ClaudeAnalisadorEpiImagem.Instrucoes);
        Assert.Contains("caixaCabeca", ClaudeAnalisadorEpiImagem.Pedido);
    }

    [Fact]
    public async Task Analisar_PosicoesQueNaoBatemComAContagem_MantemContagensSemDetalhe()
    {
        var handler = new HandlerFalso(HttpStatusCode.OK, RespostaClaude(
            "{\"pessoas\":3,\"semCapacete\":1,\"semColete\":0,\"confianca\":\"media\",\"pessoasDetalhe\":[" +
            "{\"cabecaX\":0.25,\"cabecaY\":0.3,\"semCapacete\":true,\"semColete\":false}]}"));

        var resultado = await Criar(handler).AnalisarAsync(new byte[] { 0xFF, 0xD8, 0x01 }, default);

        Assert.Equal(3, resultado.Pessoas);
        Assert.Equal(1, resultado.SemCapacete);
        Assert.Null(resultado.Detalhe);
    }

    [Fact]
    public async Task Analisar_DevolveOsTokensCobradosPelaApi()
    {
        var corpo = JsonSerializer.Serialize(new
        {
            content = new object[] { new { type = "text", text = "{\"pessoas\":0,\"semCapacete\":0,\"semColete\":0,\"confianca\":\"alta\",\"pessoasDetalhe\":[]}" } },
            usage = new { input_tokens = 1700, cache_read_input_tokens = 100, output_tokens = 45 },
        });

        var resultado = await Criar(new HandlerFalso(HttpStatusCode.OK, corpo)).AnalisarAsync(new byte[] { 0xFF, 0xD8, 0x01 }, default);

        Assert.Equal(new UsoIa(1800, 45), resultado.Uso);
    }

    // A análise roda sem parar: usa o modelo barato das câmeras, não o Claude:Modelo das demais leituras.
    [Fact]
    public async Task Analisar_UsaOModeloDasCamerasENaoOModeloPadrao()
    {
        var handler = new HandlerFalso(HttpStatusCode.OK,
            RespostaClaude("{\"pessoas\":0,\"semCapacete\":0,\"semColete\":0,\"confianca\":\"alta\"}"));

        await Criar(handler).AnalisarAsync(new byte[] { 0xFF, 0xD8, 0x01 }, default);

        Assert.Contains("\"model\":\"claude-haiku-5-5\"", handler.CorpoEnviado);
        Assert.DoesNotContain("claude-sonnet", handler.CorpoEnviado);
    }

    [Fact]
    public async Task Analisar_NumerosIncoerentes_LancaInvalidOperationException()
    {
        var handler = new HandlerFalso(HttpStatusCode.OK,
            RespostaClaude("{\"pessoas\":1,\"semCapacete\":3,\"semColete\":0,\"confianca\":\"media\"}"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Criar(handler).AnalisarAsync(new byte[] { 0xFF, 0xD8 }, default));
    }

    [Fact]
    public async Task Analisar_FalhaDoClaude_LancaInvalidOperationException()
    {
        var handler = new HandlerFalso(HttpStatusCode.BadRequest, "erro");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Criar(handler).AnalisarAsync(new byte[] { 0xFF, 0xD8 }, default));
    }

    [Fact]
    public async Task Analisar_SemChaveConfigurada_LancaInvalidOperationException()
    {
        var handler = new HandlerFalso(HttpStatusCode.OK, RespostaClaude("{}"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Criar(handler, apiKey: null).AnalisarAsync(new byte[] { 0xFF, 0xD8 }, default));
        Assert.Null(handler.Requisicao);
    }
}
