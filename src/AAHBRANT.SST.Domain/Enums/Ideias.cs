namespace AAHBRANT.SST.Domain.Enums;

// Banco de Ideias e Evolução do Produto (especificação do usuário, 08/10/2026, §6, §7, §9).
// Nenhum destes enums vem da Base de Conhecimento de SST — são decisões de produto trazidas
// literalmente da especificação do Banco de Ideias, citada em cada tipo.

// Especificação §6 — fluxo de status (+ Adiada e Descartada, que podem ocorrer em vários pontos).
public enum StatusIdeia
{
    NovaIdeia = 0,
    EmAnalise = 1,
    AguardandoDecisao = 2,
    Aprovada = 3,
    Priorizada = 4,
    EmDesenvolvimento = 5,
    EmTesteValidacao = 6,
    Implantada = 7,
    Adiada = 8,
    Descartada = 9,
}

// Especificação §7 — Esforço / Impacto / Urgência usam a mesma escala de três níveis.
// Valor de negócio (§9) reaproveita a escala.
public enum NivelIdeia
{
    Baixo = 1,
    Medio = 2,
    Alto = 3,
}

// Especificação §5 — viabilidade técnica e operacional (§7: "é tecnicamente possível?").
public enum ViabilidadeIdeia
{
    NaoAvaliada = 0,
    Viavel = 1,
    ViavelComRestricoes = 2,
    Inviavel = 3,
}

// Especificação §9 — P1 alta, P2 média, P3 baixa.
public enum PrioridadeIdeia
{
    P1 = 1,
    P2 = 2,
    P3 = 3,
}

// Especificação §8 — a decisão final é sempre humana (gestor); a IA só apoia.
public enum DecisaoIdeia
{
    Aprovada = 1,
    Reprovada = 2,
    Adiada = 3,
}

// Por onde a ideia entrou no banco (§2 — Telegram é o canal de captura; a tela do app também registra).
public enum CanalIdeia
{
    Telegram = 1,
    Web = 2,
}

// Pergunta objetiva que o bot deixou em aberto no Telegram (§4 e §16). A resposta do usuário
// (responder à mensagem do bot) é tratada de acordo com o tipo.
public enum TipoPerguntaIdeia
{
    Nenhuma = 0,
    Modulo = 1,
    Duplicidade = 2,
}

// Especificação §13 — histórico que nunca é apagado.
public enum TipoHistoricoIdeia
{
    Registro = 1,
    Classificacao = 2,
    MudancaStatus = 3,
    Analise = 4,
    Decisao = 5,
    Vinculo = 6,
    Requisito = 7,
    Demanda = 8,
    Anexo = 9,
}

// Especificação §10 — requisito funcional nasce de uma ideia aprovada.
public enum StatusRequisitoIdeia
{
    Rascunho = 0,
    Aprovado = 1,
}

// Especificação §11 — demanda de desenvolvimento criada a partir do requisito aprovado.
public enum StatusDemandaDesenvolvimento
{
    Aberta = 0,
    EmDesenvolvimento = 1,
    EmTeste = 2,
    Concluida = 3,
    Cancelada = 4,
}
