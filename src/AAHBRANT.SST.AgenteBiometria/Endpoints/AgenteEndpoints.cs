using AAHBRANT.SST.AgenteBiometria.Leitores;
using AAHBRANT.SST.AgenteBiometria.Opcoes;
using AAHBRANT.SST.AgenteBiometria.Servicos;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.AgenteBiometria.Endpoints;

// O segredo do dispositivo não sai mais para o navegador (11/10): com ele, quem tivesse acesso ao navegador
// conseguia "assinar" por qualquer trabalhador. LeituraAssinada avisa a tela que este agente assina as leituras.
// Versao: versão do agente instalado no posto (para conferir quem ainda não atualizou).
// DigitaisCarregadas/ErroSincronizacao: quantas digitais da obra o posto tem para comparar e, se a última
// sincronização falhou, o motivo (ver TemplateCacheService.UltimoErroSincronizacao).
public record DispositivoResponse(Guid DispositivoId, bool LeituraAssinada = true, string? Versao = null,
    int DigitaisCarregadas = 0, string? ErroSincronizacao = null);
public record SincronizarResponse(int Total);
// ImagemPng: imagem da leitura, guardada (criptografada) como referência do cadastro no log de assinaturas.
public record CapturaBrutaResponse(byte[] TemplateBruto, byte[]? ImagemPng = null);
public record CompararTemplatesRequest(byte[] TemplateA, byte[] TemplateB);
public record CompararTemplatesResponse(double Score);
// ImagemPng: imagem da digital lida, guardada como evidência visual da assinatura (Cofre).
// Desafio/Assinatura: presentes quando a tela mandou um desafio do servidor (ver Capturar). Assinaturas: uma
// por item de Desafios, na mesma ordem (entrega de EPI em lote: uma leitura assina vários documentos).
public record CapturaResponse(Guid TrabalhadorId, double Score, byte[]? ImagemPng = null, string? Desafio = null, string? Assinatura = null,
    List<string>? Assinaturas = null);
public record CapturarRequest(string? Desafio, List<string>? Desafios = null);
public record ErroResponse(string Erro);

public static class AgenteEndpoints
{
    public static void Mapear(WebApplication app, string politicaCors)
    {
        app.MapGet("/api/dispositivo", ObterDispositivo).RequireCors(politicaCors);
        app.MapPost("/api/sincronizar", Sincronizar).RequireCors(politicaCors);
        app.MapPost("/api/capturar-bruto", CapturarBruto).RequireCors(politicaCors);
        app.MapPost("/api/comparar-templates", CompararTemplates).RequireCors(politicaCors);
        app.MapPost("/api/capturar", Capturar).RequireCors(politicaCors);
    }

    public static Ok<DispositivoResponse> ObterDispositivo(IOptions<AgenteOptions> options, TemplateCacheService cache) =>
        TypedResults.Ok(new DispositivoResponse(options.Value.DispositivoId,
            Versao: Atualizacao.VerificadorAtualizacao.VersaoAtual().ToString(3),
            DigitaisCarregadas: cache.Templates.Count, ErroSincronizacao: cache.UltimoErroSincronizacao));

    public static async Task<Ok<SincronizarResponse>> Sincronizar(TemplateCacheService cache, CancellationToken ct)
    {
        await cache.SincronizarAsync(ct);
        return TypedResults.Ok(new SincronizarResponse(cache.Templates.Count));
    }

    // novoToque=true na segunda leitura do cadastro: exige tirar o dedo e apoiar de novo.
    public static async Task<Ok<CapturaBrutaResponse>> CapturarBruto(IFingerprintReader leitor, IFingerprintMatcher matcher, bool? novoToque, CancellationToken ct)
    {
        var captura = await leitor.CapturarAsync(ct, novoToque ?? false);
        return TypedResults.Ok(new CapturaBrutaResponse(matcher.ExtrairTemplate(captura), ImagemDigitalPng.Converter(captura)));
    }

    // Cadastro com confirmação: duas leituras do mesmo dedo precisam concordar entre si (mesma escala do
    // limiar de assinatura) antes de virarem cadastro.
    public static Results<Ok<CompararTemplatesResponse>, BadRequest<ErroResponse>> CompararTemplates(CompararTemplatesRequest corpo, IFingerprintMatcher matcher)
    {
        if (corpo.TemplateA is not { Length: > 0 } || corpo.TemplateB is not { Length: > 0 })
        {
            return TypedResults.BadRequest(new ErroResponse("Informe os dois templates para comparar."));
        }

        return TypedResults.Ok(new CompararTemplatesResponse(matcher.Comparar(corpo.TemplateA, corpo.TemplateB)));
    }

    // Um template corrompido ou de outra versão do SourceAFIS não pode derrubar a leitura de todo mundo
    // (auditoria 19/10, A08): vale score 0 e fica no log para recadastrar.
    private static double CompararSemDerrubar(IFingerprintMatcher matcher, byte[] captura, TemplateCacheado candidato)
    {
        try
        {
            return matcher.Comparar(captura, candidato.TemplateBruto);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Template inválido ignorado (trabalhador {candidato.TrabalhadorId}): {ex.GetType().Name}");
            return 0;
        }
    }

    public static string MensagemSemDigitais(string? erroSincronizacao) => erroSincronizacao is null
        ? "Nenhuma digital cadastrada na obra deste posto. Cadastre a digital do trabalhador antes de assinar."
        : $"Este posto não conseguiu carregar as digitais da obra: {erroSincronizacao}";

    // Texto assinado de cada leitura. Precisa ser idêntico a LeituraBiometricaAssinada.Conteudo no servidor.
    public static string ConteudoAssinado(string desafio, Guid trabalhadorId, double score) =>
        string.Join('|', desafio, trabalhadorId.ToString("D"), score.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));

    // Mesmo limiar padrão do servidor (assinatura por digital); aqui só decide quando parar de fotografar —
    // quem aceita ou recusa a assinatura continua sendo o servidor.
    public const double LimiarAssinatura = 50;

    private static (Guid TrabalhadorId, double Score)? MelhorDoCache(IFingerprintMatcher matcher, TemplateCacheService cache, byte[] imagem)
    {
        byte[] captura;
        try
        {
            captura = matcher.ExtrairTemplate(imagem);
        }
        catch (ArgumentException)
        {
            return null;
        }

        return cache.Templates
            .Select(t => (t.TrabalhadorId, Score: CompararSemDerrubar(matcher, captura, t)))
            .OrderByDescending(r => r.Score)
            .Select(r => ((Guid, double)?)r)
            .FirstOrDefault();
    }

    public static async Task<Results<Ok<CapturaResponse>, NotFound<ErroResponse>>> Capturar(
        IFingerprintReader leitor, IFingerprintMatcher matcher, TemplateCacheService cache, ChaveAssinaturaAgente chave,
        CapturarRequest? corpo, CancellationToken ct)
    {
        // Sem digital nenhuma para comparar não adianta pedir o dedo: tenta buscar de novo e, se continuar
        // vazio, explica o motivo em vez de ler e responder "nenhum template".
        if (cache.Templates.Count == 0)
        {
            try
            {
                await cache.SincronizarAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
            }
            if (cache.Templates.Count == 0)
                return TypedResults.NotFound(new ErroResponse(MensagemSemDigitais(cache.UltimoErroSincronizacao)));
        }

        // Cada foto do leitor já é comparada com o cache: a primeira que alcança o limiar do servidor encerra a
        // leitura (dedo seco ou fino que o sensor de presença não percebe ainda assina).
        (Guid TrabalhadorId, double Score)? melhor = null;
        byte[]? lida = null;
        bool Aceitar(byte[] imagem)
        {
            melhor = MelhorDoCache(matcher, cache, imagem);
            lida = imagem;
            return melhor?.Score >= LimiarAssinatura;
        }

        var imagemBruta = await leitor.CapturarAsync(ct, aceitar: cache.Templates.Count > 0 ? Aceitar : null);
        if (!ReferenceEquals(lida, imagemBruta))
        {
            melhor = MelhorDoCache(matcher, cache, imagemBruta);
        }

        if (melhor is null)
        {
            return TypedResults.NotFound(new ErroResponse("Não foi possível ler a digital. Limpe o dedo e o leitor e tente de novo."));
        }

        var (trabalhadorId, melhorScore) = melhor.Value;

        // Nota arredondada em 2 casas antes de assinar: o servidor confere exatamente o texto assinado, e assim
        // o número não muda no caminho (JSON do agente -> navegador -> servidor).
        var score = Math.Round(melhorScore, 2);
        // Leitor simulado (testes, sem hardware) nunca assina: com a exigência de leitura assinada ligada no
        // servidor, uma instalação esquecida em "Simulado" não consegue assinar documento de verdade.
        var desafio = leitor is SimuladoFingerprintReader ? null : corpo?.Desafio;
        var assinatura = string.IsNullOrWhiteSpace(desafio) ? null : chave.Assinar(ConteudoAssinado(desafio, trabalhadorId, score));
        var assinaturas = leitor is SimuladoFingerprintReader
            ? null
            : corpo?.Desafios?.Select(d => chave.Assinar(ConteudoAssinado(d, trabalhadorId, score))).ToList();
        return TypedResults.Ok(new CapturaResponse(trabalhadorId, score, ImagemDigitalPng.Converter(imagemBruta), desafio, assinatura, assinaturas));
    }
}
