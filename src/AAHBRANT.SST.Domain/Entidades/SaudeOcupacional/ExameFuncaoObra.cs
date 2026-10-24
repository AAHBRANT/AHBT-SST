using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// "Quadro de exames" do PCMSO (NR-07): exame previsto para uma função numa obra, em quais ASOs ele é
// pedido e de quanto em quanto tempo no periódico. Por obra porque cada obra tem o seu PCMSO; a
// Funcao é global. Exame é texto (nome + código da Tabela 27 do eSocial, ex.: "AUDIOMETRIA TONAL" /
// "0281") e não TipoExameComplementar: o enum só tem 6 categorias e o PCMSO cita exames específicos
// (ECG, EEG, glicemia, ácido hipúrico…).
public class ExameFuncaoObra : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public Guid FuncaoId { get; set; }
    public Funcao? Funcao { get; set; }

    // PCMSO de origem, quando cadastrado no sistema.
    public Guid? PcmsoDetalheId { get; set; }
    public PcmsoDetalhe? PcmsoDetalhe { get; set; }

    public string Exame { get; set; } = string.Empty;
    public string? CodigoExame { get; set; }

    public bool Admissional { get; set; }
    public bool Periodico { get; set; }
    public bool RetornoTrabalho { get; set; }
    public bool MudancaRisco { get; set; }
    public bool Demissional { get; set; }

    // Intervalo do periódico (ANUAL = 12, BIENAL = 24, SEMESTRAL = 6). Nulo quando o PCMSO não diz.
    public int? PeriodicidadeMeses { get; set; }
    public string? Observacao { get; set; }
}
