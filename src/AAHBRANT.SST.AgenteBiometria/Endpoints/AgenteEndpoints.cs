using AAHBRANT.SST.AgenteBiometria.Leitores;
using AAHBRANT.SST.AgenteBiometria.Opcoes;
using AAHBRANT.SST.AgenteBiometria.Servicos;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.AgenteBiometria.Endpoints;

public record DispositivoResponse(Guid DispositivoId, string SegredoDispositivo);
public record SincronizarResponse(int Total);
public record CapturaBrutaResponse(byte[] TemplateBruto);
public record CapturaResponse(Guid TrabalhadorId, double Score);
public record ErroResponse(string Erro);

public static class AgenteEndpoints
{
    public static void Mapear(WebApplication app, string politicaCors)
    {
        app.MapGet("/api/dispositivo", ObterDispositivo).RequireCors(politicaCors);
        app.MapPost("/api/sincronizar", Sincronizar).RequireCors(politicaCors);
        app.MapPost("/api/capturar-bruto", CapturarBruto).RequireCors(politicaCors);
        app.MapPost("/api/capturar", Capturar).RequireCors(politicaCors);
    }

    public static Ok<DispositivoResponse> ObterDispositivo(IOptions<AgenteOptions> options) =>
        TypedResults.Ok(new DispositivoResponse(options.Value.DispositivoId, options.Value.SegredoDispositivo));

    public static async Task<Ok<SincronizarResponse>> Sincronizar(TemplateCacheService cache, CancellationToken ct)
    {
        await cache.SincronizarAsync(ct);
        return TypedResults.Ok(new SincronizarResponse(cache.Templates.Count));
    }

    public static async Task<Ok<CapturaBrutaResponse>> CapturarBruto(IFingerprintReader leitor, CancellationToken ct)
    {
        var captura = await leitor.CapturarAsync(ct);
        return TypedResults.Ok(new CapturaBrutaResponse(captura));
    }

    public static async Task<Results<Ok<CapturaResponse>, NotFound<ErroResponse>>> Capturar(
        IFingerprintReader leitor, IFingerprintMatcher matcher, TemplateCacheService cache, CancellationToken ct)
    {
        var captura = await leitor.CapturarAsync(ct);

        var melhor = cache.Templates
            .Select(t => new { t.TrabalhadorId, Score = matcher.Comparar(captura, t.TemplateBruto) })
            .OrderByDescending(r => r.Score)
            .FirstOrDefault();

        if (melhor is null)
        {
            return TypedResults.NotFound(new ErroResponse("Nenhum template cadastrado no cache local. Rode /api/sincronizar primeiro."));
        }

        return TypedResults.Ok(new CapturaResponse(melhor.TrabalhadorId, melhor.Score));
    }
}
