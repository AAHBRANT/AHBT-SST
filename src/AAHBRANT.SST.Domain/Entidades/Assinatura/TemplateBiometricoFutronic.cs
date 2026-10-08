using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

public class TemplateBiometricoFutronic : AuditableEntity
{
    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }
    public string TemplateCriptografado { get; set; } = string.Empty;
    public DateTime CapturadoEm { get; set; }

    // Imagem (PNG) da 1ª leitura do cadastro, criptografada com a mesma chave do template. Só existe
    // para cadastros feitos depois da implantação do log de assinaturas; os anteriores ficam nulos
    // e o log mostra apenas data e identificação do template. Dado biométrico sensível (LGPD).
    public string? ImagemCadastroCriptografada { get; set; }
}
