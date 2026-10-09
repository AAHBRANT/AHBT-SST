using System.Text;
using AAHBRANT.SST.Application.Acidentes.RelatoIa;

namespace AAHBRANT.SST.Infrastructure.Integracao.SuporteIa;

/// <summary>
/// Análise preliminar de causas e plano de ação via gpt5mini, recebendo o PGR da atividade e os
/// requisitos legais cadastrados como únicas bases citáveis. A conferência da base declarada é feita
/// depois, no servidor (FundamentacaoAcaoPlano) — o modelo não é a fonte de verdade da referência.
/// </summary>
public class AzureOpenAiAnalistaPlanoOcorrencia : IAnalistaPlanoOcorrencia
{
    private const string Instrucoes = """
        Você é técnico de Segurança do Trabalho da AAHBRANT Engenharia. A partir do registro de uma
        ocorrência em obra, faça uma análise PRELIMINAR de causas e proponha o plano de ação, para o
        técnico validar. Responda só com o JSON pedido.

        Bases que você pode citar, nesta ordem de preferência:
        1) PGR da obra para a atividade (lista abaixo): identifique qual perigo cadastrado se
           materializou e qual controle existente ou adicional falhou ou não foi aplicado. Ação com
           baseOrigem "PGR" deve trazer em basePerigo o nome EXATO de um perigo da lista e em
           baseControle o controle envolvido. A atividade NÃO é um perigo.
        2) Requisitos legais cadastrados (lista abaixo): ação com baseOrigem "RequisitoLegal" deve
           trazer em baseReferencia o identificador EXATO do requisito, como aparece na lista.
           Nunca cite norma ou item que não esteja na lista.
        3) Método: hierarquia de medidas de prevenção da NR-01 (eliminação, substituição, controle de
           engenharia/coletivo, administrativo, EPI por último). baseOrigem "Metodo" e em
           baseReferencia o nível aplicado.
        Se nenhuma base sustentar a ação, use baseOrigem "SemBase" e explique em baseReferencia o que
        precisaria ser cadastrado no PGR.

        Regras:
        - metodologia: a mais adequada ao caso.
        - causas: até 800 caracteres, baseada só no registro; marque hipóteses como hipótese; não
          culpe pessoas — foque em condições, procedimentos, barreiras e gestão.
        - acoes: 3 a 5, no imperativo, concretas e verificáveis (até 200 caracteres), priorizando
          eliminação e proteção coletiva antes de EPI. Corretiva elimina a causa deste caso;
          Preventiva evita repetição em outras frentes; Melhoria ajusta gestão ou procedimento.
        - papelResponsavel: quem executa a ação na obra (Encarregado para correção imediata na frente
          de trabalho; Técnico de Segurança para inspeção, treinamento e DDS; Engenheiro de Segurança
          para procedimento e análise técnica; Gestor de Obra para recursos, compras e estoque;
          Gestor QSMS para padrão corporativo).
        - Não inclua ações de DDS nem de reunião de análise (são tratadas à parte).
        """;

    private static readonly object Schema = new
    {
        type = "object",
        additionalProperties = false,
        required = new[] { "metodologia", "causas", "acoes" },
        properties = new
        {
            metodologia = new { type = "string", @enum = new[] { "CincoPorques", "FatoresContribuintes", "FalhasDeBarreira", "AnaliseCausaRaiz", "ArvoreDeCausas" } },
            causas = new { type = "string" },
            acoes = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "tipo", "descricao", "prioridade", "papelResponsavel", "baseOrigem", "basePerigo", "baseControle", "baseReferencia" },
                    properties = new
                    {
                        tipo = new { type = "string", @enum = new[] { "Corretiva", "Preventiva", "Melhoria" } },
                        descricao = new { type = "string" },
                        prioridade = new { type = "string", @enum = new[] { "Critica", "Alta", "Media", "Baixa" } },
                        papelResponsavel = new { type = "string", @enum = new[] { "TecnicoSeguranca", "EngenheiroSeguranca", "GestorDeObra", "Encarregado", "GestorQsms" } },
                        baseOrigem = new { type = "string", @enum = new[] { "PGR", "RequisitoLegal", "Metodo", "SemBase" } },
                        basePerigo = new { type = new[] { "string", "null" } },
                        baseControle = new { type = new[] { "string", "null" } },
                        baseReferencia = new { type = new[] { "string", "null" } },
                    },
                },
            },
        },
    };

    private readonly AzureOpenAiChatJsonCliente _chat;

    public AzureOpenAiAnalistaPlanoOcorrencia(AzureOpenAiChatJsonCliente chat) => _chat = chat;

    public async Task<AnalisePlanoIa> AnalisarAsync(ContextoAnaliseOcorrencia c, CancellationToken ct)
    {
        var bases = new StringBuilder();
        bases.AppendLine($"PGR da atividade \"{c.Atividade ?? "não informada"}\":");
        if (c.RiscosPgr.Count == 0)
            bases.AppendLine("(nenhum risco cadastrado para esta atividade — não use a origem PGR)");
        foreach (var r in c.RiscosPgr)
            bases.AppendLine($"- Perigo: {r.Perigo} | Nível: {r.Nivel} | Consequência: {r.Consequencia} | Controles existentes: {r.ControlesExistentes} | Controles adicionais: {r.ControlesAdicionais}");

        bases.AppendLine();
        bases.AppendLine("Requisitos legais cadastrados aplicáveis:");
        if (c.RequisitosLegais.Count == 0)
            bases.AppendLine("(nenhum cadastrado — não use a origem RequisitoLegal)");
        foreach (var q in c.RequisitosLegais)
            bases.AppendLine($"- {FundamentacaoAcaoPlano.IdentificadorRequisito(q)} | {q.Titulo} | {q.Descricao}");

        var registro = $"""
            Tipo: {c.Tipo}
            Gravidade: {c.Gravidade}
            Descrição: {c.Descricao}
            Lesão: {c.Lesao ?? "não informada"}
            Consequência: {c.Consequencia ?? "não informada"}
            Atendimento: {c.Atendimento ?? "não informado"}
            """;

        var r2 = await _chat.ObterAsync<RespostaModelo>($"{Instrucoes}\n\n{bases}", registro, "plano_acao_sst", Schema, ct);

        return new AnalisePlanoIa(
            r2.Metodologia,
            r2.Causas,
            (r2.Acoes ?? new List<AcaoModelo>())
                .Select(a => new AcaoSugeridaIa(a.Tipo, a.Descricao, a.Prioridade, a.PapelResponsavel, a.BaseOrigem, a.BasePerigo, a.BaseControle, a.BaseReferencia))
                .ToList());
    }

    private sealed record RespostaModelo(string? Metodologia, string? Causas, List<AcaoModelo>? Acoes);

    private sealed record AcaoModelo(
        string? Tipo, string? Descricao, string? Prioridade, string? PapelResponsavel,
        string? BaseOrigem, string? BasePerigo, string? BaseControle, string? BaseReferencia);
}
