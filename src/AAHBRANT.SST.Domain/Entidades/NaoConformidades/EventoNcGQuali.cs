namespace AAHBRANT.SST.Domain.Entidades;

// Fila de saída (outbox) da integração com o G-QUALI (17/10/2026): cada mudança de NC com obra grava aqui, na
// mesma transação, o evento já montado; o EnvioNcGQualiService envia e tenta de novo sem duplicar (o G-QUALI
// deduplica por eventId e ignora revisão antiga). O G-SST continua dono do tratamento; o G-QUALI só espelha.
public class EventoNcGQuali
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NaoConformidadeId { get; set; }
    public string EventId { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;          // criada | atualizada | encerrada | cancelada
    public long Revisao { get; set; }
    public string Payload { get; set; } = string.Empty;       // JSON do contrato do G-QUALI (sem linkOrigem)
    public DateTime CriadoEmUtc { get; set; }
    public SituacaoEventoNcGQuali Situacao { get; set; } = SituacaoEventoNcGQuali.Pendente;
    public int Tentativas { get; set; }
    public DateTime ProximaTentativaEmUtc { get; set; }
    public DateTime? EnviandoDesdeUtc { get; set; }
    public DateTime? EnviadoEmUtc { get; set; }
    public string? RespostaGQuali { get; set; }               // Processado | Ignorado | Triagem
    public string? UltimoErro { get; set; }
}

public enum SituacaoEventoNcGQuali { Pendente = 1, Enviando = 2, Enviado = 3, Recusado = 4 }
