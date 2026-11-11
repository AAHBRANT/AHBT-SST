namespace AAHBRANT.SST.Application.IntegracoesAtendimento;

// O avanço da etapa foi recusado por falta de algo. A API responde 409 com a lista (o que falta), em vez do 400
// genérico das demais regras de negócio.
public sealed class IntegracaoBloqueadaException(string mensagem, IReadOnlyList<string> faltas) : InvalidOperationException(mensagem)
{
    public IReadOnlyList<string> Faltas { get; } = faltas;
}

// Já existe atendimento em andamento na obra (pré-checagem ou índice único numa corrida): a API responde 409 amigável.
public sealed class IntegracaoConflitoException(string mensagem) : InvalidOperationException(mensagem)
{
    public const string Padrao = "Já existe uma integração em andamento nesta obra. Continue a integração aberta antes de iniciar outra.";
}
