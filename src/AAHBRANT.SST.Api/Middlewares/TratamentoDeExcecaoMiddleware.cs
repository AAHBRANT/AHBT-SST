using System.Net;
using System.Text.Json;
using FluentValidation;

namespace AAHBRANT.SST.Api.Middlewares;

// Gap recorrente confirmado em praticamente todo módulo com regra de bloqueio (PT/APR/Inspeções/NC/
// Acidentes/Administração): sem este middleware, InvalidOperationException/KeyNotFoundException viravam
// 500 com o Developer Exception Page bruto (stack trace) exposto direto no frontend.
public class TratamentoDeExcecaoMiddleware
{
    private readonly RequestDelegate _proximo;
    private readonly ILogger<TratamentoDeExcecaoMiddleware> _logger;

    public TratamentoDeExcecaoMiddleware(RequestDelegate proximo, ILogger<TratamentoDeExcecaoMiddleware> logger)
    {
        _proximo = proximo;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext contexto)
    {
        try
        {
            await _proximo(contexto);
        }
        catch (ValidationException ex)
        {
            await EscreverRespostaAsync(contexto, HttpStatusCode.BadRequest,
                string.Join(" ", ex.Errors.Select(e => e.ErrorMessage)));
        }
        catch (KeyNotFoundException ex)
        {
            await EscreverRespostaAsync(contexto, HttpStatusCode.NotFound, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await EscreverRespostaAsync(contexto, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro não tratado em {Metodo} {Caminho}", contexto.Request.Method, contexto.Request.Path);
            await EscreverRespostaAsync(contexto, HttpStatusCode.InternalServerError,
                "Ocorreu um erro inesperado. Tente novamente ou contate o suporte.");
        }
    }

    private static async Task EscreverRespostaAsync(HttpContext contexto, HttpStatusCode status, string mensagem)
    {
        if (contexto.Response.HasStarted)
        {
            return;
        }

        contexto.Response.Clear();
        contexto.Response.StatusCode = (int)status;
        contexto.Response.ContentType = "application/json";
        await contexto.Response.WriteAsync(JsonSerializer.Serialize(new { erro = mensagem }));
    }
}
