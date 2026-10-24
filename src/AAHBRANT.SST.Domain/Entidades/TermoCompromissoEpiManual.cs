using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Registro de que o funcionário JÁ assinou o Termo de Recebimento e Compromisso de Uso em papel
// (03/10, decisão do usuário): o sistema nunca fabrica assinatura eletrônica de quem assinou à mão.
// Este registro é uma declaração de quem o lançou — por isso guarda quem registrou e quando, a data
// real da assinatura em papel e, opcionalmente, a foto/PDF do papel. Um ativo por trabalhador; só o
// Administrador remove. Se o funcionário assinar digitalmente, o registro manual fica bloqueado (e
// vice-versa) — ver RegistrarTermoManualCommand e RegistradorAssinaturaService.
//
// O arquivo vive na própria linha (mesmo padrão de bytes-no-banco de ArquivoCertificadoTreinamento),
// então toda consulta que não precisa dele deve PROJETAR campos em vez de materializar a entidade.
public class TermoCompromissoEpiManual : AuditableEntity
{
    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }

    // Dia em que o funcionário assinou o papel (não pode ser futuro).
    public DateTime DataAssinaturaPapel { get; set; }
    public string? Observacao { get; set; }

    public Guid RegistradoPorUsuarioId { get; set; }
    public Usuario? RegistradoPorUsuario { get; set; }

    public string? ArquivoNome { get; set; }
    public string? ArquivoContentType { get; set; }
    public byte[]? ArquivoConteudo { get; set; }
    public long? ArquivoTamanhoBytes { get; set; }
}
