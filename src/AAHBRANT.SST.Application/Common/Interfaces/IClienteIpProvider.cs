namespace AAHBRANT.SST.Application.Common.Interfaces;

// IP do cliente da requisição HTTP em andamento, para o audit trail do Cofre de Assinaturas.
// Nunca vem do corpo da requisição (a evidência não pode ser controlada pelo cliente): a
// implementação (na Api) lê X-Forwarded-For / RemoteIpAddress. Existe para que NENHUM caminho de
// assinatura grave o IP em branco só porque o controller esqueceu de repassá-lo.
public interface IClienteIpProvider
{
    string? ObterIp();

    // Cabeçalho User-Agent da requisição (navegador/sistema do aparelho), para o log de assinaturas.
    string? ObterUserAgent();
}
