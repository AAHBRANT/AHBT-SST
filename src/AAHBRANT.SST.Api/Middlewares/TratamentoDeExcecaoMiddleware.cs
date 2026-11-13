using AAHBRANT.SST.Application.Common;
using System.Net;
using System.Text.Json;
using FluentValidation;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

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
        catch (AcessoNegadoException ex)
        {
            await EscreverRespostaAsync(contexto, HttpStatusCode.Forbidden, ex.Message);
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
        catch (ServicoIndisponivelException ex)
        {
            // Temporário (Azure Face fora ou com cota estourada): 503 para a fila offline tentar de novo. A
            // pessoa vê só a mensagem; a causa técnica fica no log.
            _logger.LogWarning(ex, "Serviço externo indisponível em {Metodo} {Caminho}", contexto.Request.Method, contexto.Request.Path);
            await EscreverRespostaAsync(contexto, HttpStatusCode.ServiceUnavailable, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await EscreverRespostaAsync(contexto, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Sincronização offline: o registro que o app de campo editou sem conexão foi alterado
            // por outra pessoa no servidor nesse meio tempo. Política acordada: o servidor sempre
            // vence (evita corrupção silenciosa), mas o cliente precisa do dado atual para avisar o
            // usuário do que foi descartado (ver SyncStatusBadge/conflitos no frontend).
            await EscreverConflitoAsync(contexto, ex);
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

    // Colunas que o 409 nunca devolve (o dadosAtuais traz a linha inteira): credencial cifrada da câmera, hash do segredo do
    // agente e a miniatura (foto de pessoa possível) do teste de conexão. Só as das câmeras IP; o resto do sistema não muda.
    public static readonly IReadOnlyDictionary<Type, string[]> ColunasSensiveisDoConflito = new Dictionary<Type, string[]>
    {
        [typeof(EquipamentoCamera)] = new[] { nameof(EquipamentoCamera.SenhaCifrada) },
        [typeof(TesteConexaoCamera)] = new[] { nameof(TesteConexaoCamera.Miniatura) },
        [typeof(AgenteCamera)] = new[] { nameof(AgenteCamera.SegredoHash) },
        [typeof(SessaoAoVivoCamera)] = new[] { nameof(SessaoAoVivoCamera.UltimoQuadro) },
    };

    // Colunas da linha que vão no 409, sem as sensíveis do tipo (se houver). Público para ser testado sem banco relacional.
    public static IEnumerable<KeyValuePair<string, object?>> ColunasDoConflito(Type tipoDaEntidade, IEnumerable<KeyValuePair<string, object?>> colunas)
    {
        ColunasSensiveisDoConflito.TryGetValue(tipoDaEntidade, out var omitir);
        return omitir is null ? colunas : colunas.Where(c => !omitir.Contains(c.Key));
    }

    private static async Task EscreverConflitoAsync(HttpContext contexto, DbUpdateConcurrencyException ex)
    {
        if (contexto.Response.HasStarted)
        {
            return;
        }

        var dadosAtuais = new Dictionary<string, object?>();
        foreach (var entry in ex.Entries)
        {
            var valoresAtuais = await entry.GetDatabaseValuesAsync();
            if (valoresAtuais is null)
            {
                dadosAtuais["_excluido"] = true;
                continue;
            }

            var colunas = valoresAtuais.Properties.Select(p => new KeyValuePair<string, object?>(p.Name, valoresAtuais[p]));
            foreach (var (nome, valor) in ColunasDoConflito(entry.Metadata.ClrType, colunas))
            {
                dadosAtuais[nome] = valor;
            }
        }

        contexto.Response.Clear();
        contexto.Response.StatusCode = (int)HttpStatusCode.Conflict;
        contexto.Response.ContentType = "application/json";
        await contexto.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            conflito = true,
            mensagem = "Este registro foi alterado por outra pessoa enquanto você estava offline. A versão do servidor foi mantida.",
            dadosAtuais,
        }));
    }
}
