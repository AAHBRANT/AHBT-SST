using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Motor de Assinatura Eletrônica, etapa 13 (docs/Motor-Assinatura-Eletronica.md §3) — uma credencial
// FIDO2/WebAuthn registrada para UM trabalhador, seja no leitor biométrico compartilhado da obra
// (impressão digital) ou no celular próprio (biometria/PIN do aparelho). Guarda só o que o protocolo
// WebAuthn exige para verificar futuras assinaturas — nunca o template biométrico em si, que nunca
// sai do autenticador (mesma garantia já documentada em DocumentoSignatario).
public class CredencialWebAuthn : AuditableEntity
{
    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }

    public TipoAutenticadorWebAuthn Tipo { get; set; }

    // Identificador opaco da credencial (retornado pelo autenticador) e a chave pública correspondente
    // (formato COSE) — é contra essa chave que toda assinatura futura é verificada.
    public byte[] CredentialId { get; set; } = Array.Empty<byte>();
    public byte[] PublicKey { get; set; } = Array.Empty<byte>();

    // UserHandle (Fido2User.Id) é o que permite ao leitor compartilhado da obra resolver "qual
    // trabalhador encostou o dedo" sem que o servidor precise adivinhar antes — WebAuthn devolve o
    // UserHandle junto da assertion quando a credencial é "discoverable" (ver §3 do doc).
    public byte[] UserHandle { get; set; } = Array.Empty<byte>();

    // Contador anti-clonagem do autenticador (cresce a cada uso); SignCount que não avança em relação
    // ao valor salvo indica possível clonagem da credencial — checado em Fido2AutenticacaoStrategy.
    public uint SignCount { get; set; }

    public DateTime? UltimoUsoEm { get; set; }
}
