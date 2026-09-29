namespace AAHBRANT.SST.AgenteBiometria.Endpoints;

// Chrome/Edge (e o WebView2 do Teams) tratam uma página HTTPS pública chamando 127.0.0.1 como acesso à
// rede privada: mandam um preflight com "Access-Control-Request-Private-Network: true" e só seguem se a
// resposta trouxer "Access-Control-Allow-Private-Network: true". O CORS do ASP.NET não faz isso sozinho.
// Sem este cabeçalho o app em hml não conseguiria falar com o agente, mesmo com o CORS certo.
//
// Só liberamos para a origem configurada (a mesma do CORS) — outra origem continua sem permissão.
public static class AcessoRedePrivada
{
    public const string CabecalhoPedido = "Access-Control-Request-Private-Network";
    public const string CabecalhoResposta = "Access-Control-Allow-Private-Network";

    public static bool DeveLiberar(HttpRequest requisicao, string origemPermitida) =>
        HttpMethods.IsOptions(requisicao.Method)
        && string.Equals(requisicao.Headers[CabecalhoPedido], "true", StringComparison.OrdinalIgnoreCase)
        && string.Equals(requisicao.Headers.Origin, origemPermitida.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);

    public static IApplicationBuilder UseAcessoRedePrivada(this IApplicationBuilder app, string origemPermitida) =>
        app.Use(async (contexto, proximo) =>
        {
            if (DeveLiberar(contexto.Request, origemPermitida))
            {
                contexto.Response.Headers[CabecalhoResposta] = "true";
            }

            await proximo();
        });
}
