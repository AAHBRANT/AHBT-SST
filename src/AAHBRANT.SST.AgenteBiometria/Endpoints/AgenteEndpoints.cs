using AAHBRANT.SST.AgenteBiometria.Leitores;
using AAHBRANT.SST.AgenteBiometria.Opcoes;
using AAHBRANT.SST.AgenteBiometria.Servicos;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.AgenteBiometria.Endpoints;

// O segredo do dispositivo não sai mais para o navegador (11/10): com ele, quem tivesse acesso ao navegador
// conseguia "assinar" por qualquer trabalhador. LeituraAssinada avisa a tela que este agente assina as leituras.
public record DispositivoResponse(Guid DispositivoId, bool LeituraAssinada = true);
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

    public static Ok<DispositivoResponse> ObterDispositivo(IOptions<AgenteOptions> options) =>
        TypedResults.Ok(new DispositivoResponse(options.Value.DispositivoId));

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

    // Texto assinado de cada leitura. Precisa ser idêntico a LeituraBiometricaAssinada.Conteudo no servidor.
    public static string ConteudoAssinado(string desafio, Guid trabalhadorId, double score) =>
        string.Join('|', desafio, trabalhadorId.ToString("D"), score.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));

    public static async Task<Results<Ok<CapturaResponse>, NotFound<ErroResponse>>> Capturar(
        IFingerprintReader leitor, IFingerprintMatcher matcher, TemplateCacheService cache, ChaveAssinaturaAgente chave,
        CapturarRequest? corpo, CancellationToken ct)
    {
        var imagemBruta = await leitor.CapturarAsync(ct);
        var captura = matcher.ExtrairTemplate(imagemBruta);

        var melhor = cache.Templates
            .Select(t => new { t.TrabalhadorId, Score = matcher.Comparar(captura, t.TemplateBruto) })
            .OrderByDescending(r => r.Score)
            .FirstOrDefault();

        if (melhor is null)
        {
            return TypedResults.NotFound(new ErroResponse("Nenhum template cadastrado no cache local. Rode /api/sincronizar primeiro."));
        }

        // Nota arredondada em 2 casas antes de assinar: o servidor confere exatamente o texto assinado, e assim
        // o número não muda no caminho (JSON do agente -> navegador -> servidor).
        var score = Math.Round(melhor.Score, 2);
        var desafio = corpo?.Desafio;
        var assinatura = string.IsNullOrWhiteSpace(desafio) ? null : chave.Assinar(ConteudoAssinado(desafio, melhor.TrabalhadorId, score));
        var assinaturas = corpo?.Desafios?.Select(d => chave.Assinar(ConteudoAssinado(d, melhor.TrabalhadorId, score))).ToList();
        return TypedResults.Ok(new CapturaResponse(melhor.TrabalhadorId, score, ImagemDigitalPng.Converter(imagemBruta), desafio, assinatura, assinaturas));
    }
}
