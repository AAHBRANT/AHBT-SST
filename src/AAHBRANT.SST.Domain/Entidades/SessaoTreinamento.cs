using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Sessão/Turma de Treinamento (pedido do usuário, 04/09) — reformulação do módulo Treinamentos:
// até aqui Treinamento era só "1 trabalhador + 1 curso", sem noção de turma (ver Treinamento.cs).
// Mesmo princípio de modelagem já usado em Dds/DdsSemanal: a Sessão é o evento em si (turma
// reunida numa aula), com N participantes pré-selecionados na criação; ao encerrar (ver
// EncerrarSessaoTreinamentoCommand), gera 1 Treinamento (e certificado) por participante que
// confirmou presença por biometria — quem não confirmou fica registrado como ausente, sem
// certificado (evita inventar uma regra de "falta justificada" que não foi pedida).
public class SessaoTreinamento : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public Guid CursoTreinamentoId { get; set; }
    public CursoTreinamento? CursoTreinamento { get; set; }

    public DateTime DataRealizacao { get; set; }
    public int CargaHorariaRealizada { get; set; }
    public string? InstituicaoInstrutor { get; set; }

    // Nº do certificado/registro é externo e manual (mesma decisão já tomada para Treinamento —
    // não é gerado automaticamente para não inventar dado sensível a compliance). Preenchido uma
    // vez para a turma inteira e copiado para cada Treinamento gerado no encerramento; pode ser
    // ajustado depois por participante via AtualizarTreinamentoCommand se precisar de números
    // individuais distintos.
    public string? NumeroCertificado { get; set; }

    public StatusSessaoTreinamento Status { get; set; } = StatusSessaoTreinamento.EmAndamento;
    public DateTime? DataEncerramento { get; set; }

    public ICollection<ParticipanteSessaoTreinamento> Participantes { get; set; } = new List<ParticipanteSessaoTreinamento>();
    public ICollection<FotoEvidenciaSessaoTreinamento> FotosEvidencia { get; set; } = new List<FotoEvidenciaSessaoTreinamento>();
}

// Trabalhador inscrito na turma — pré-selecionado na criação da Sessão (ao contrário do
// DdsParticipante, que só existe quando a biometria já validou presença). PresencaConfirmadaEm
// fica nulo até a biometria confirmar durante a aula; TreinamentoGeradoId só é preenchido no
// encerramento, e só para quem confirmou presença.
public class ParticipanteSessaoTreinamento : AuditableEntity
{
    public Guid SessaoTreinamentoId { get; set; }
    public SessaoTreinamento? SessaoTreinamento { get; set; }

    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }

    public DateTime? PresencaConfirmadaEm { get; set; }
    public double? ScoreConfianca { get; set; }

    public Guid? TreinamentoGeradoId { get; set; }
    public Treinamento? TreinamentoGerado { get; set; }
}

// Evidência fotográfica da turma (pedido do usuário: mínimo 3 fotos, obrigatórias para liberar o
// encerramento) — mesmo princípio de DdsFotoEvidencia.cs.
public class FotoEvidenciaSessaoTreinamento : AuditableEntity
{
    public Guid SessaoTreinamentoId { get; set; }
    public SessaoTreinamento? SessaoTreinamento { get; set; }

    public int Ordem { get; set; }
    public byte[] FotoConteudo { get; set; } = Array.Empty<byte>();
    public string FotoContentType { get; set; } = string.Empty;
}
