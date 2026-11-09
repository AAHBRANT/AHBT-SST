using AAHBRANT.SST.Application.Common.Interfaces;

namespace AAHBRANT.SST.Api.Autorizacao;

// Lê o Bearer da requisição atual para o fluxo On-Behalf-Of (reunião criada em nome de quem está logado).
public class TokenAcessoUsuarioHttp(IHttpContextAccessor http) : ITokenAcessoUsuario
{
    public string? Obter()
    {
        var cabecalho = http.HttpContext?.Request.Headers.Authorization.ToString();
        return cabecalho is not null && cabecalho.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? cabecalho["Bearer ".Length..].Trim()
            : null;
    }
}
