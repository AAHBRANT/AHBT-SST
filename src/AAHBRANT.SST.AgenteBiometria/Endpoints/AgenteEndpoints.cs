using AAHBRANT.SST.AgenteBiometria.Leitores;
using AAHBRANT.SST.AgenteBiometria.Opcoes;
using AAHBRANT.SST.AgenteBiometria.Servicos;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.AgenteBiometria.Endpoints;

public record DispositivoResponse(Guid DispositivoId, string SegredoDispositivo);
public record SincronizarResponse(int Total);
public record CapturaBrutaResponse(byte[] TemplateBruto);
public record CompararTemplatesRequest(byte[] TemplateA, byte[] TemplateB);
public record CompararTemplatesResponse(double Score);
// ImagemPng: imagem da digital lida, guardada como evidência visual da assinatura (Cofre).
public record CapturaResponse(Guid TrabalhadorId, double Score, byte[]? ImagemPng = null);
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
        TypedResults.Ok(new DispositivoResponse(options.Value.DispositivoId, options.Value.SegredoDispositivo));

    public static async Task<Ok<SincronizarResponse>> Sincronizar(TemplateCacheService cache, CancellationToken ct)
    {
        await cache.SincronizarAsync(ct);
        return TypedResults.Ok(new SincronizarResponse(cache.Templates.Count));
    }

    // novoToque=true na segunda leitura do cadastro: exige tirar o dedo e apoiar de novo.
    public static async Task<Ok<CapturaBrutaResponse>> CapturarBruto(IFingerprintReader leitor, IFingerprintMatcher matcher, bool? novoToque, CancellationToken ct)
    {
        var captura = await leitor.CapturarAsync(ct, novoToque ?? false);
        return TypedResults.Ok(new CapturaBrutaResponse(matcher.ExtrairTemplate(captura)));
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

    public static async Task<Results<Ok<CapturaResponse>, NotFound<ErroResponse>>> Capturar(
        IFingerprintReader leitor, IFingerprintMatcher matcher, TemplateCacheService cache, CancellationToken ct)
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

        return TypedResults.Ok(new CapturaResponse(melhor.TrabalhadorId, melhor.Score, ImagemDigitalPng.Converter(imagemBruta)));
    }
}
