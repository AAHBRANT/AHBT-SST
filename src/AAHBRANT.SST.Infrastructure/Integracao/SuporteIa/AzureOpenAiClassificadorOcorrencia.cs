using System.Globalization;
using AAHBRANT.SST.Application.Acidentes.RelatoIa;

namespace AAHBRANT.SST.Infrastructure.Integracao.SuporteIa;

/// <summary>
/// Preenchimento do registro de ocorrência (NBR 14280) a partir do relato, via gpt5mini com saída
/// estruturada. Recebe só o relato e os nomes das atividades da obra — nunca a lista de funcionários.
/// </summary>
public class AzureOpenAiClassificadorOcorrencia : IClassificadorRelatoOcorrencia
{
    private const string InstrucoesBase = """
        Você é técnico de Segurança do Trabalho da AAHBRANT Engenharia e transforma o relato de uma
        ocorrência em obra no formulário de registro, seguindo a NBR 14280. O relato pode ter sido
        falado e transcrito, com hesitações e linguagem informal. Responda só com o JSON pedido.

        Regras:
        - Nunca invente fatos. Campo que o relato não informa fica null.
        - perguntas: até 4 perguntas curtas e diretas ao técnico sobre informações que o relato NÃO
          trouxe e que o registro precisa (local, data, hora, lesão, atendimento, afastamento,
          funcionários envolvidos, atividade). Uma pergunta por informação. Nunca pergunte o que o
          relato ou as informações complementares já respondem. 'campo' indica o que a resposta
          preenche; use "outro" para circunstâncias úteis à análise (ex.: condição do local).
        - Informações complementares (perguntas já respondidas pelo técnico) valem como parte do relato.
        - tipo: Acidente (houve lesão ou doença no trabalhador), Incidente (dano material ou ao
          processo, sem lesão), QuaseAcidente (poderia ter causado lesão, ninguém se feriu),
          CondicaoInsegura (situação de risco no ambiente, sem evento), AtoInseguro (comportamento
          de risco observado, sem evento), DoencaOcupacional.
        - gravidade: SemAfastamento quando não há lesão ou não houve afastamento; ComAfastamento só
          quando o relato disser que houve afastamento ou atestado; IncapacidadePermanenteParcial,
          IncapacidadePermanenteTotal e Obito só quando o relato afirmar isso explicitamente.
        - houveAfastamento e diasAfastamento: só com base em afastamento ou atestado citado.
        - data no formato AAAA-MM-DD e hora no formato HH:mm, resolvendo expressões como "hoje",
          "ontem", "segunda passada" a partir da data atual informada. Sem hora citada, hora = null.
        - local: o ponto da obra onde ocorreu (bloco, pavimento, setor, frente de serviço).
        - atividade: exatamente um nome da lista de atividades da obra, ou null se nenhuma servir.
        - nomesCitados: nomes de pessoas envolvidas como foram ditos no relato (sem cargo).
        - descricao: o relato reescrito em linguagem técnica e impessoal, em ordem cronológica,
          sem perder nenhum detalhe e sem acrescentar conclusões.
        - lesao, consequencia, atendimento: frases curtas, só com o que o relato informa.
        - Nunca informe número de CAT nem dias debitados.
        """;

    private static readonly object Schema = new
    {
        type = "object",
        additionalProperties = false,
        required = new[]
        {
            "tipo", "gravidade", "local", "data", "hora", "descricao", "lesao", "consequencia",
            "atendimento", "houveAfastamento", "diasAfastamento", "atividade", "nomesCitados", "perguntas",
        },
        properties = new
        {
            tipo = new { type = "string", @enum = new[] { "Acidente", "Incidente", "QuaseAcidente", "CondicaoInsegura", "AtoInseguro", "DoencaOcupacional" } },
            gravidade = new { type = "string", @enum = new[] { "SemAfastamento", "ComAfastamento", "IncapacidadePermanenteParcial", "IncapacidadePermanenteTotal", "Obito" } },
            local = new { type = new[] { "string", "null" } },
            data = new { type = new[] { "string", "null" } },
            hora = new { type = new[] { "string", "null" } },
            descricao = new { type = "string" },
            lesao = new { type = new[] { "string", "null" } },
            consequencia = new { type = new[] { "string", "null" } },
            atendimento = new { type = new[] { "string", "null" } },
            houveAfastamento = new { type = new[] { "boolean", "null" } },
            diasAfastamento = new { type = new[] { "integer", "null" } },
            atividade = new { type = new[] { "string", "null" } },
            nomesCitados = new { type = "array", items = new { type = "string" } },
            perguntas = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "campo", "pergunta" },
                    properties = new
                    {
                        campo = new { type = "string", @enum = new[] { "local", "data", "hora", "lesao", "consequencia", "atendimento", "afastamento", "funcionarios", "atividade", "outro" } },
                        pergunta = new { type = "string" },
                    },
                },
            },
        },
    };

    private static readonly CultureInfo PtBr = new("pt-BR");

    private readonly AzureOpenAiChatJsonCliente _chat;

    public AzureOpenAiClassificadorOcorrencia(AzureOpenAiChatJsonCliente chat) => _chat = chat;

    public async Task<OcorrenciaSugeridaIa> ClassificarAsync(
        string relato,
        IReadOnlyList<RespostaPerguntaRelato> complementos,
        DateTime agoraLocal,
        IReadOnlyList<string> atividadesDaObra,
        CancellationToken ct)
    {
        var atividades = atividadesDaObra.Count == 0
            ? "(a obra não tem atividades cadastradas — use null)"
            : string.Join("; ", atividadesDaObra);

        var instrucoes = $"""
            {InstrucoesBase}
            Data e hora atuais (Brasília): {agoraLocal.ToString("dddd, dd/MM/yyyy HH:mm", PtBr)}.
            Atividades da obra: {atividades}.
            """;

        var mensagem = complementos.Count == 0
            ? relato
            : $"{relato}\n\nInformações complementares do técnico:\n" +
              string.Join("\n", complementos.Select(c => $"- {c.Pergunta.Trim()} Resposta: {c.Resposta.Trim()}"));

        var r = await _chat.ObterAsync<RespostaModelo>(instrucoes, mensagem, "ocorrencia_sst", Schema, ct);

        return new OcorrenciaSugeridaIa(
            r.Tipo, r.Gravidade, r.Local, r.Data, r.Hora, r.Descricao, r.Lesao, r.Consequencia,
            r.Atendimento, r.HouveAfastamento, r.DiasAfastamento, r.Atividade,
            r.NomesCitados ?? new List<string>(),
            (r.Perguntas ?? new List<PerguntaModelo>())
                .Select(q => new PerguntaRelatoOcorrencia(q.Campo ?? "outro", q.Pergunta ?? string.Empty))
                .ToList());
    }

    private sealed record RespostaModelo(
        string? Tipo, string? Gravidade, string? Local, string? Data, string? Hora, string? Descricao,
        string? Lesao, string? Consequencia, string? Atendimento, bool? HouveAfastamento, int? DiasAfastamento,
        string? Atividade, List<string>? NomesCitados, List<PerguntaModelo>? Perguntas);

    private sealed record PerguntaModelo(string? Campo, string? Pergunta);
}
