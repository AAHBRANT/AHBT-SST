using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Api.Middlewares;

// Suporte à sincronização offline: quando o app de campo reenvia uma mutação (POST/PUT) porque
// não recebeu a resposta original (conexão caiu antes de a fila local confirmar o envio), este
// middleware garante que a mutação não é aplicada duas vezes — devolve a mesma resposta já dada
// da primeira vez. Só age quando o cliente manda o header "Idempotency-Key"; requests sem esse
// header (a maioria dos módulos, ainda não migrados para o fluxo offline) seguem sem nenhuma
// mudança de comportamento.
public class IdempotenciaMiddleware
{
    private const string CabecalhoChave = "Idempotency-Key";

    private readonly RequestDelegate _proximo;

    public IdempotenciaMiddleware(RequestDelegate proximo)
    {
        _proximo = proximo;
    }

    public async Task InvokeAsync(HttpContext contexto, IAppDbContext db)
    {
        if (!HttpMethods.IsPost(contexto.Request.Method) && !HttpMethods.IsPut(contexto.Request.Method))
        {
            await _proximo(contexto);
            return;
        }

        if (!contexto.Request.Headers.TryGetValue(CabecalhoChave, out var valores) ||
            string.IsNullOrWhiteSpace(valores.ToString()))
        {
            await _proximo(contexto);
            return;
        }

        var chave = valores.ToString();

        var existente = await db.IdempotenciaRegistros.FirstOrDefaultAsync(x => x.Chave == chave);
        if (existente is not null)
        {
            contexto.Response.StatusCode = existente.StatusCodeResposta;
            contexto.Response.ContentType = "application/json";
            await contexto.Response.WriteAsync(existente.CorpoResposta);
            return;
        }

        var corpoOriginalResponse = contexto.Response.Body;
        using var buffer = new MemoryStream();
        contexto.Response.Body = buffer;

        try
        {
            await _proximo(contexto);
        }
        finally
        {
            contexto.Response.Body = corpoOriginalResponse;
        }

        buffer.Seek(0, SeekOrigin.Begin);
        var corpoResposta = await new StreamReader(buffer).ReadToEndAsync();

        // Só registra respostas de sucesso: um erro de validação (400) deve poder ser corrigido e
        // reenviado pelo cliente com a MESMA chave sem ficar preso ao resultado da tentativa falha.
        if (contexto.Response.StatusCode is >= 200 and < 300)
        {
            db.IdempotenciaRegistros.Add(new IdempotenciaRegistro
            {
                Chave = chave,
                Rota = contexto.Request.Path,
                StatusCodeResposta = contexto.Response.StatusCode,
                CorpoResposta = corpoResposta,
            });
            await db.SaveChangesAsync(CancellationToken.None);
        }

        buffer.Seek(0, SeekOrigin.Begin);
        await buffer.CopyToAsync(corpoOriginalResponse);
    }
}
