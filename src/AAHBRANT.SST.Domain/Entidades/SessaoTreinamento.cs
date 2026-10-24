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

    // Dados do certificado gravados na turma (19/10): vêm preenchidos com o padrão da NR do curso,
    // só o Técnico de Segurança altera, e são copiados para cada Treinamento no encerramento — o
    // certificado mostra o que foi ministrado naquele dia, mesmo que o curso mude depois.
    public string? ConteudoProgramatico { get; set; }
    public string? Local { get; set; }
    public string? InstrutorRegistroProfissional { get; set; }
    // 0 = usa a validade do curso (turmas criadas antes desta coluna).
    public int ValidadeEmMeses { get; set; }

    // Nº da turma (CERT-ANO-NNNN). Desde 19/10 cada participante recebe o próprio número de
    // certificado no encerramento (AAH-NRxx-ANO-NNNN); este fica só como identificação da turma.
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

    // Como a presença foi confirmada (02/10) — sai na legenda de assinatura da Ata. Nulo nas
    // presenças anteriores à coluna, quando o método não era gravado: a Ata mostra só data e hora.
    public MetodoAutenticacaoAssinatura? MetodoPresenca { get; set; }

    public Guid? TreinamentoGeradoId { get; set; }
    public Treinamento? TreinamentoGerado { get; set; }
}

// Evidência fotográfica da turma (pedido do usuário: mínimo 3 fotos, obrigatórias para liberar o
// encerramento) — mesmo princípio de DdsFotoEvidencia.cs.
public class FotoEvidenciaSessaoTreinamento : AuditableEntity
{
    public string? FotoMetadadosJson { get; set; }
    public Guid SessaoTreinamentoId { get; set; }
    public SessaoTreinamento? SessaoTreinamento { get; set; }

    public int Ordem { get; set; }
    public byte[] FotoConteudo { get; set; } = Array.Empty<byte>();
    public string FotoContentType { get; set; } = string.Empty;
}
