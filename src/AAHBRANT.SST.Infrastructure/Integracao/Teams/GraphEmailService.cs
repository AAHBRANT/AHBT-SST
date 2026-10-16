using System.Net.Http.Headers;
using System.Net.Http.Json;
using AAHBRANT.SST.Application.Common.Interfaces;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.Teams;

// Envio de e-mail via Microsoft Graph (POST /users/{remetente}/sendMail) com o mesmo App
// Registration do Activity Feed. Permissão de aplicativo Mail.Send concedida em 16/10/2026, restrita
// por ApplicationAccessPolicy (grupo SST-Envio-Graph) à caixa compartilhada sst@aahbrant.com — o app
// não consegue enviar como nenhuma outra pessoa.
public class GraphEmailService : IEmailService
{
    private const string RecursoLogo = "AAHBRANT.SST.Infrastructure.Documentos.Assets.logo-aahbrant.png";
    private static readonly Lazy<string> LogoBase64 = new(CarregarLogo);

    private readonly GraphOptions _opcoes;
    private readonly IHttpClientFactory _httpClientFactory;

    public GraphEmailService(IOptions<GraphOptions> opcoes, IHttpClientFactory httpClientFactory)
    {
        _opcoes = opcoes.Value;
        _httpClientFactory = httpClientFactory;
    }

    public async Task EnviarAsync(string para, string assunto, string corpoHtml, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opcoes.ClientSecret) || string.IsNullOrWhiteSpace(_opcoes.RemetenteEmail))
            throw new InvalidOperationException("Graph:ClientSecret ou Graph:RemetenteEmail não configurado — e-mail não enviado.");

        var credential = new ClientSecretCredential(_opcoes.TenantId, _opcoes.ClientId, _opcoes.ClientSecret);
        var token = await credential.GetTokenAsync(
            new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" }), ct);

        var corpo = new
        {
            message = new
            {
                subject = assunto,
                body = new { contentType = "HTML", content = corpoHtml.Replace("{{LINK_SST}}", _opcoes.TopicWebUrl) },
                toRecipients = new[] { new { emailAddress = new { address = para } } },
                attachments = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["@odata.type"] = "#microsoft.graph.fileAttachment",
                        ["name"] = "logo-aahbrant.png",
                        ["contentType"] = "image/png",
                        ["contentId"] = IEmailService.ContentIdLogo,
                        ["isInline"] = true,
                        ["contentBytes"] = LogoBase64.Value,
                    },
                },
            },
            saveToSentItems = true,
        };

        var httpClient = _httpClientFactory.CreateClient();
        using var requisicao = new HttpRequestMessage(
            HttpMethod.Post, $"https://graph.microsoft.com/v1.0/users/{_opcoes.RemetenteEmail}/sendMail")
        {
            Content = JsonContent.Create(corpo),
        };
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        var resposta = await httpClient.SendAsync(requisicao, ct);
        if (!resposta.IsSuccessStatusCode)
        {
            var corpoResposta = await resposta.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Falha ao enviar e-mail para {para}: {(int)resposta.StatusCode} {resposta.StatusCode} — {corpoResposta}");
        }
    }

    private static string CarregarLogo()
    {
        using var stream = typeof(GraphEmailService).Assembly.GetManifestResourceStream(RecursoLogo)
            ?? throw new InvalidOperationException($"Recurso {RecursoLogo} não encontrado.");
        using var memoria = new MemoryStream();
        stream.CopyTo(memoria);
        return Convert.ToBase64String(memoria.ToArray());
    }
}
