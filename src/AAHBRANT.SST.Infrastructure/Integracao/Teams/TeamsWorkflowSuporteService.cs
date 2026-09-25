using System.Net.Http.Json;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.Teams;

public class TeamsWorkflowSuporteOptions
{
    // URL do gatilho "Quando uma solicitação de webhook do Teams for recebida" do Workflow.
    public string? SuporteWebhookUrl { get; set; }

    // Opcional: link do botão "Abrir Central de Suporte IA" no cartão.
    public string? LinkCentralSuporte { get; set; }
}

// Mesmo contrato do TelegramSuporteService: sem configuração, só registra aviso; falha de envio é
// logada e não derruba a criação do chamado. O corpo segue o formato que o gatilho de webhook do
// Workflow espera (message + attachment Adaptive Card), que o passo "Postar cartão no chat" repassa.
public class TeamsWorkflowSuporteService : ITeamsWorkflowSuporteService
{
    private const int TamanhoMaximoTexto = 1500;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<TeamsWorkflowSuporteOptions> _options;
    private readonly ILogger<TeamsWorkflowSuporteService> _logger;

    public TeamsWorkflowSuporteService(
        IHttpClientFactory httpClientFactory,
        IOptions<TeamsWorkflowSuporteOptions> options,
        ILogger<TeamsWorkflowSuporteService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task EnviarDemandaAsync(SuporteIaSolicitacao solicitacao, CancellationToken ct = default)
    {
        var url = _options.Value.SuporteWebhookUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            _logger.LogWarning("Workflow do Teams para o Suporte IA não configurado. Configure TeamsWorkflow:SuporteWebhookUrl.");
            return;
        }

        var client = _httpClientFactory.CreateClient();
        var response = await client.PostAsJsonAsync(url, MontarCorpo(solicitacao, _options.Value.LinkCentralSuporte), ct);

        if (!response.IsSuccessStatusCode)
        {
            var erro = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Falha ao enviar chamado do Suporte IA ao Workflow do Teams: {StatusCode} {Erro}", response.StatusCode, erro);
        }
    }

    internal static object MontarCorpo(SuporteIaSolicitacao s, string? linkCentral)
    {
        var precisaAlterar = s.RequerAlteracaoCodigo;
        var corpo = new List<object>
        {
            new
            {
                type = "TextBlock",
                text = precisaAlterar ? "Suporte IA — demanda técnica" : "Suporte IA — novo chamado",
                weight = "Bolder",
                size = "Medium",
                color = precisaAlterar ? "Attention" : "Default",
                wrap = true,
            },
            new { type = "TextBlock", text = s.Titulo, weight = "Bolder", wrap = true },
            new
            {
                type = "FactSet",
                facts = new[]
                {
                    new { title = "Tipo", value = RotuloTipo(s.Tipo) },
                    new { title = "Severidade", value = RotuloSeveridade(s.SeveridadeInformada) },
                    new { title = "Triagem", value = precisaAlterar ? "Exige alteração no sistema" : "Respondido pela IA" },
                    new { title = "Módulo", value = s.Modulo ?? "Não informado" },
                    new { title = "Solicitante", value = $"{s.SolicitanteNome ?? "Não informado"} {s.SolicitanteEmail}".Trim() },
                },
            },
            Secao("Descrição", s.Descricao),
            Secao("Resposta ao usuário", s.RespostaAoUsuario),
        };

        if (precisaAlterar)
        {
            corpo.Add(Secao("Demanda reduzida", s.DemandaReduzida));
            corpo.Add(Secao("Solução proposta", s.SolucaoProposta));
        }

        var acoes = string.IsNullOrWhiteSpace(linkCentral)
            ? Array.Empty<object>()
            : new object[] { new { type = "Action.OpenUrl", title = "Abrir Central de Suporte IA", url = linkCentral } };

        return new
        {
            type = "message",
            attachments = new[]
            {
                new
                {
                    contentType = "application/vnd.microsoft.card.adaptive",
                    contentUrl = (string?)null,
                    content = new Dictionary<string, object>
                    {
                        ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
                        ["type"] = "AdaptiveCard",
                        ["version"] = "1.4",
                        ["body"] = corpo,
                        ["actions"] = acoes,
                    },
                },
            },
        };
    }

    private static object Secao(string titulo, string? texto) => new
    {
        type = "TextBlock",
        text = $"**{titulo}:** {Cortar(string.IsNullOrWhiteSpace(texto) ? "não aplicável" : texto)}",
        wrap = true,
        spacing = "Medium",
    };

    private static string Cortar(string texto) =>
        texto.Length > TamanhoMaximoTexto ? texto[..TamanhoMaximoTexto] + "…" : texto;

    private static string RotuloTipo(TipoSolicitacaoSuporteIa tipo) => tipo switch
    {
        TipoSolicitacaoSuporteIa.Erro => "Erro",
        TipoSolicitacaoSuporteIa.Duvida => "Dúvida",
        TipoSolicitacaoSuporteIa.Melhoria => "Melhoria",
        _ => tipo.ToString(),
    };

    private static string RotuloSeveridade(SeveridadeSolicitacaoSuporteIa severidade) => severidade switch
    {
        SeveridadeSolicitacaoSuporteIa.Baixa => "Baixa",
        SeveridadeSolicitacaoSuporteIa.Media => "Média",
        SeveridadeSolicitacaoSuporteIa.Alta => "Alta",
        SeveridadeSolicitacaoSuporteIa.Critica => "Crítica",
        _ => severidade.ToString(),
    };
}
