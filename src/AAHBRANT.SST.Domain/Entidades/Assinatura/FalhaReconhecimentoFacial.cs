using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

public enum MotivoFalhaFacial
{
    // Reconheceu a pessoa, mas abaixo do limiar de aceitação: é o sinal clássico de cadastro fraco.
    ConfiancaBaixa = 1,
    // Nenhum candidato com confiança mínima (rosto desconhecido, foto ruim ou cadastro muito fraco).
    RostoNaoReconhecido = 2,
    // Dois candidatos muito próximos na fila: recusado para não confirmar a pessoa errada.
    RostoAmbiguo = 3,
}

// Cada tentativa de reconhecimento facial que o Azure não aceitou. Guarda só o necessário para achar
// cadastros fracos (quem falhou, quantas vezes, por quê): NUNCA a foto. Retenção de 90 dias, depois
// o registro é apagado. TrabalhadorId só é preenchido quando o Azure aponta uma pessoa com confiança
// suficiente para a atribuição fazer sentido (ConfiancaBaixa); nos demais motivos fica vazio, porque
// o rosto pode ser de outra pessoa.
public class FalhaReconhecimentoFacial : AuditableEntity
{
    public Guid ObraId { get; set; }

    public Guid? TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }

    public MotivoFalhaFacial Motivo { get; set; }

    // Confiança devolvida pelo Azure (0 a 1), quando houve candidato.
    public double? Confianca { get; set; }

    public DateTime OcorridaEm { get; set; }
}
