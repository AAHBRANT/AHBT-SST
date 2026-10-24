using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Foto usada no cadastro facial do trabalhador (a "régua" contra a qual toda assinatura por
// reconhecimento facial é comparada). Fica guardada no perfil para auditoria e para conferir depois
// se a referência era boa. Só é gravada quando a foto passou na validação de qualidade e foi aceita
// pelo Azure. Dado biométrico sensível (LGPD): acesso restrito à permissão de assinatura.
public class FotoCadastroFacial : AuditableEntity
{
    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }

    public byte[] Conteudo { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "image/jpeg";

    // SHA-256 (hex) dos bytes gravados — permite provar depois que a foto não foi alterada.
    public string HashSha256 { get; set; } = string.Empty;

    public DateTime CapturadaEm { get; set; }
}
