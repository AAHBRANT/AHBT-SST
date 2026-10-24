using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.SuporteIa;

public class HeuristicaSuporteIaTriagemService : ISuporteIaTriagemService
{
    private static readonly string[] TermosTecnicos =
    [
        "erro", "bug", "falha", "travou", "não salva", "nao salva", "não abre", "nao abre",
        "não carrega", "nao carrega", "tela branca", "exception", "500", "404", "corrigir",
        "alterar", "melhoria", "criar campo", "novo campo", "não aparece", "nao aparece",
        "permissão", "permissao", "deploy", "codigo", "código", "banco"
    ];

    public Task<TriagemSuporteIaResultado> TriarAsync(SuporteIaEntradaTriagem entrada, CancellationToken ct)
    {
        var texto = $"{entrada.Tipo} {entrada.Titulo} {entrada.Descricao} {entrada.Modulo}".ToLowerInvariant();
        var tipoDemandaTecnica = entrada.Tipo.Equals("Erro", StringComparison.OrdinalIgnoreCase)
            || entrada.Tipo.Equals("Melhoria", StringComparison.OrdinalIgnoreCase);
        var requerCodigo = tipoDemandaTecnica || TermosTecnicos.Any(texto.Contains);

        if (!requerCodigo)
        {
            var resposta = "Recebi sua dúvida e, pela triagem inicial, ela parece ser de orientação de uso. Verifique se a obra correta está selecionada, confirme os filtros da tela e tente repetir a operação. Se o comportamento persistir, reenvie como erro informando o passo exato, tela e mensagem exibida.";
            return Task.FromResult(new TriagemSuporteIaResultado(
                ResultadoTriagemSuporteIa.RespostaAoUsuario,
                false,
                resposta,
                $"Dúvida de uso em {entrada.Modulo ?? "módulo não informado"}: {entrada.Titulo}",
                "Responder ao usuário com orientação operacional; sem alteração técnica identificada na triagem inicial.",
                "Triagem local: solicitação classificada sem indício suficiente de bug ou melhoria de código."));
        }

        var demanda = $"{entrada.Tipo} em {entrada.Modulo ?? "módulo não informado"}: {entrada.Titulo}. Contexto: {entrada.Descricao}";
        var solucao = entrada.Tipo.Equals("Erro", StringComparison.OrdinalIgnoreCase)
            ? "Reproduzir o fluxo informado, localizar endpoint/tela relacionada, corrigir a falha preservando regra de negócio e adicionar teste de regressão quando houver handler ou validação afetada."
            : "Avaliar impacto funcional, implementar a melhoria no menor escopo possível, expor em tela/API somente o necessário e validar build/testes antes do deploy.";

        var respostaUsuario = "Obrigado, registrei sua solicitação e ela foi encaminhada para análise técnica. A IA reduziu a demanda e avisou o responsável; você não precisa abrir outro chamado para o mesmo ponto.";
        return Task.FromResult(new TriagemSuporteIaResultado(
            ResultadoTriagemSuporteIa.DemandaTecnica,
            true,
            respostaUsuario,
            demanda,
            solucao,
            "Triagem local: termos e tipo da solicitação indicam necessidade provável de ajuste técnico."));
    }
}
