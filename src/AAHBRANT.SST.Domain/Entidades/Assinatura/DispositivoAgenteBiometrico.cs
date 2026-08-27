using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// AAHBRANT.SST.AgenteBiometria (processo Windows local no quiosque, fora deste backend) — cada
// instalação física do agente se registra como UM DispositivoAgenteBiometrico. O SegredoHash
// autentica o agente ao sincronizar templates ou reportar uma identificação (ver
// SegredoDispositivoHasher); o segredo em si só existe em texto puro uma vez, na resposta do
// cadastro (DispositivosAgenteController), e depois só no appsettings local do agente.
public class DispositivoAgenteBiometrico : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public string Nome { get; set; } = string.Empty;
    public string SegredoHash { get; set; } = string.Empty;

    public DateTime? UltimaSincronizacaoEm { get; set; }
}
