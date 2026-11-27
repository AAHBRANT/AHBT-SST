using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Ações irreversíveis que o agente engenheiro da assinatura pode PROPOR, mas só o responsável aprova
// (botão no Telegram). Lista fechada: o agente não executa nada fora daqui.
public enum TipoAcaoAssinatura
{
    // Excluir do grupo de rostos da obra as pessoas do Azure sem funcionário ligado e retreinar o grupo.
    ExcluirPessoasFaciaisSoltas = 1,
}

public enum StatusPropostaAcaoAssinatura
{
    Pendente = 1,
    // Aprovada e em execução (estado curto: protege contra dois cliques executarem duas vezes).
    Aprovada = 2,
    Executada = 3,
    Recusada = 4,
    Expirada = 5,
    Falhou = 6,
}

// Pedido do agente engenheiro (27/11). Os alvos são calculados pelo SERVIDOR a partir do diagnóstico no
// momento da proposta — o agente só diz o tipo e a obra — e são conferidos de novo na hora de executar:
// só sai quem continua solto. Guarda só ids técnicos do Azure, nunca nome ou foto.
public class PropostaAcaoAssinatura : AuditableEntity
{
    public TipoAcaoAssinatura Tipo { get; set; }
    public Guid ObraId { get; set; }

    // Ids técnicos (personId do Azure) separados por vírgula.
    public string Alvos { get; set; } = string.Empty;

    // O que o responsável lê antes de aprovar (sem nome de funcionário).
    public string Descricao { get; set; } = string.Empty;

    // Por que o agente propôs.
    public string? Motivo { get; set; }

    public StatusPropostaAcaoAssinatura Status { get; set; } = StatusPropostaAcaoAssinatura.Pendente;
    public DateTime CriadaEm { get; set; }
    public DateTime ExpiraEm { get; set; }
    public DateTime? DecididaEm { get; set; }
    public string? Resultado { get; set; }

    // Mensagem do Telegram com os botões, para trocar pelo resultado depois da decisão.
    public long? TelegramMensagemId { get; set; }

    public IReadOnlyList<string> ListaAlvos() =>
        Alvos.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
