using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

public enum StatusIntegracaoAtendimento
{
    EmAndamento = 1,
    Concluido = 2,
    Cancelado = 3,
}

// Ordem fixa das etapas (decisão do usuário, 11/11/2026): NR-06, NR-12, NR-18, NR-35 e, por fim, a OS (NR-01).
public enum TipoEtapaIntegracao
{
    Nr06 = 1,
    Nr12 = 2,
    Nr18 = 3,
    Nr35 = 4,
    OrdemServico = 5,
}

public enum StatusEtapaIntegracao
{
    Pendente = 1,
    EmAndamento = 2,
    Concluida = 3,
    Pulada = 4,
}

// "Realizar integração": o técnico conduz os admitidos de uma obra em etapas de ordem fixa. Cada etapa de curso é
// uma SessaoTreinamento (certificado, fotos e assinatura biométrica próprios); a última é a OS de cada pessoa.
// Todas as entidades do atendimento têm ObraId próprio (filtro de escopo por obra) e NÃO têm navegação obrigatória
// para o pai: o nome/função de cada pessoa fica gravado como foto do momento, para o histórico não sumir se o
// trabalhador for desligado (INNER JOIN + soft delete esconde registro).
public class IntegracaoAtendimento : AuditableEntity
{
    public Guid ObraId { get; set; }
    // INT-AAAA-NNNN.
    public string Numero { get; set; } = string.Empty;
    // Nulo só com o Entra ID desligado (desenvolvimento): não há usuário humano logado.
    public Guid? ResponsavelUsuarioId { get; set; }
    public Guid InstrutorTrabalhadorId { get; set; }
    public StatusIntegracaoAtendimento Status { get; set; } = StatusIntegracaoAtendimento.EmAndamento;
    // Ordem (1-5) da etapa em andamento; nulo quando o atendimento termina.
    public int? EtapaAtualOrdem { get; set; }
    public DateTime IniciadaEmUtc { get; set; }
    public DateTime? ConcluidaEmUtc { get; set; }
    // Concorrência otimista: cada mudança de estado soma 1.
    public int Versao { get; set; } = 1;
}

public class IntegracaoEtapa : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Guid IntegracaoAtendimentoId { get; set; }
    public int Ordem { get; set; }
    public TipoEtapaIntegracao Tipo { get; set; }
    public Guid? CursoTreinamentoId { get; set; }
    public Guid? SessaoTreinamentoId { get; set; }
    // Foto do mínimo no momento de iniciar (0 = sem trava).
    public int CargaMinimaHoras { get; set; }
    public StatusEtapaIntegracao Status { get; set; } = StatusEtapaIntegracao.Pendente;
    public DateTime? IniciadaEmUtc { get; set; }
    public DateTime? ConcluidaEmUtc { get; set; }
}

public class ParticipanteIntegracao : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Guid IntegracaoAtendimentoId { get; set; }
    public Guid TrabalhadorId { get; set; }
    public Guid? PlanoIntegracaoId { get; set; }
    public Guid FuncaoId { get; set; }
    public string FuncaoNome { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public Guid? OrdemServicoId { get; set; }
}

// Carga horária lançada de uma etapa de curso (um ou mais períodos, inclusive em dias diferentes).
public class PeriodoEtapaIntegracao : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Guid IntegracaoEtapaId { get; set; }
    public DateTime Data { get; set; }
    public TimeSpan Inicio { get; set; }
    public TimeSpan Fim { get; set; }
    public int Minutos { get; set; }
}

// "Integração realizada": registro isolado no perfil, sem certificado (decisão do usuário, 11/11/2026).
public class RegistroIntegracao : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Guid TrabalhadorId { get; set; }
    public Guid IntegracaoAtendimentoId { get; set; }
    public DateTime RealizadaEm { get; set; }
    public Guid? TecnicoUsuarioId { get; set; }
    public string TecnicoNome { get; set; } = string.Empty;
    public string FuncaoNome { get; set; } = string.Empty;
}

// "Não assinou" (Q9): a pessoa saiu da lista de presença da etapa (sem certificado nem ata) e não trava o avanço. Também
// é o destino automático de quem está desligado/inativo ao abrir a etapa (motivo "Desligado").
public class NaoAssinouEtapaIntegracao : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Guid IntegracaoEtapaId { get; set; }
    public Guid TrabalhadorId { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public DateTime MarcadoEmUtc { get; set; }
    public Guid? MarcadoPorUsuarioId { get; set; }
}

// Token de captura de foto emitido pelo servidor (uso único, 5 min, ligado à turma e ao usuário): a hora da captura é a do
// consumo do token, não a do relógio do aparelho.
public class TokenCapturaFoto : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Guid SessaoTreinamentoId { get; set; }
    public Guid? UsuarioId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiraEmUtc { get; set; }
    public DateTime? UsadoEmUtc { get; set; }
}
