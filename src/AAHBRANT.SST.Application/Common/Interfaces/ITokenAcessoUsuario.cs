namespace AAHBRANT.SST.Application.Common.Interfaces;

/// <summary>
/// Token de acesso (JWT do Entra ID) da requisição atual, para chamar o Microsoft Graph em nome de quem
/// está logado (On-Behalf-Of). Nulo fora de uma requisição HTTP (Worker, jobs). Nunca registrar em log.
/// </summary>
public interface ITokenAcessoUsuario
{
    string? Obter();
}
