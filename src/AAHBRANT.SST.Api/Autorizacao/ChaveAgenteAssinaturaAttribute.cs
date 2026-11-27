using System.Security.Cryptography;
using System.Text;
using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AAHBRANT.SST.Api.Autorizacao;

// Rotas internas do agente engenheiro da assinatura (Claude Console) e da vigia do Worker. Sem login do
// Entra ID: quem chama é uma máquina, com a chave AgenteAssinatura:Chave no cabeçalho X-Chave-Agente
// (no Claude Console ela fica no cofre e é trocada na saída — o agente nunca vê o valor).
//
// Sem chave configurada, a rota responde 404: desligada por padrão. Chave errada: 401. Chave certa:
// libera o filtro global por obra (o relatório cobre todas as obras), como faz a integração do G-RH.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ChaveAgenteAssinaturaAttribute : Attribute, IAsyncAuthorizationFilter
{
    public const string Cabecalho = "X-Chave-Agente";
    public const string Configuracao = "AgenteAssinatura:Chave";

    // Curta demais é fácil de adivinhar: a rota fica desligada até alguém configurar uma chave de verdade.
    public const int TamanhoMinimo = 32;

    public Task OnAuthorizationAsync(AuthorizationFilterContext contexto)
    {
        var servicos = contexto.HttpContext.RequestServices;
        var esperada = servicos.GetRequiredService<IConfiguration>()[Configuracao];
        if (string.IsNullOrWhiteSpace(esperada) || esperada.Length < TamanhoMinimo)
        {
            contexto.Result = new NotFoundResult();
            return Task.CompletedTask;
        }

        var recebida = contexto.HttpContext.Request.Headers[Cabecalho].ToString();
        if (!ChavesIguais(esperada, recebida))
        {
            contexto.Result = new UnauthorizedResult();
            return Task.CompletedTask;
        }

        servicos.GetRequiredService<ICurrentUserService>().DefinirEscopo(temAcessoGlobal: true, Array.Empty<Guid>());
        return Task.CompletedTask;
    }

    // Compara os hashes, em tempo constante: o tamanho e o conteúdo da chave não vazam pelo tempo de resposta.
    internal static bool ChavesIguais(string esperada, string recebida)
        => !string.IsNullOrEmpty(recebida)
           && CryptographicOperations.FixedTimeEquals(
               SHA256.HashData(Encoding.UTF8.GetBytes(esperada)),
               SHA256.HashData(Encoding.UTF8.GetBytes(recebida)));
}
