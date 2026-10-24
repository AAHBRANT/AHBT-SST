namespace AAHBRANT.SST.AgenteBiometria.Endpoints;

// Traduz falhas do leitor em respostas HTTP com corpo { "erro": "..." } — o front (extrairMensagemErro)
// já sabe ler esse formato e mostra a mensagem ao operador, em vez de "500 Internal Server Error".
public static class AgenteErros
{
    public static (int Status, string Mensagem)? Traduzir(Exception ex) => ex switch
    {
        TimeoutException => (StatusCodes.Status408RequestTimeout, ex.Message),
        InvalidOperationException => (StatusCodes.Status503ServiceUnavailable, ex.Message),
        DllNotFoundException => (StatusCodes.Status503ServiceUnavailable,
            "SDK do leitor Futronic (ftrScanAPI.dll) não encontrado ao lado do agente."),
        BadImageFormatException => (StatusCodes.Status503ServiceUnavailable,
            "O agente precisa rodar em 32 bits (x86) para usar o leitor Futronic."),
        ArgumentException a => (StatusCodes.Status422UnprocessableEntity, a.Message.Split(" (Parameter")[0]),
        _ => null,
    };

    public static IApplicationBuilder UseTraducaoDeErros(this IApplicationBuilder app) =>
        app.Use(async (contexto, proximo) =>
        {
            try
            {
                await proximo();
            }
            catch (Exception ex) when (Traduzir(ex) is not null && !contexto.Response.HasStarted)
            {
                var (status, mensagem) = Traduzir(ex)!.Value;
                contexto.Response.StatusCode = status;
                await contexto.Response.WriteAsJsonAsync(new Endpoints.ErroResponse(mensagem));
            }
        });
}
