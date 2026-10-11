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

    // Chave pública (ECDSA P-256, SubjectPublicKeyInfo em base64) do par gerado pelo próprio agente no
    // PC da obra. Cada leitura de digital vem assinada com a chave privada, que nunca sai daquele PC:
    // assim o servidor deixa de confiar no trabalhador/nota que o navegador manda (antes bastava ter o
    // segredo do dispositivo para "assinar" por qualquer pessoa da obra com nota 100). Registrada pelo
    // próprio agente no primeiro contato (autenticado pelo segredo); depois disso não muda — para trocar,
    // registra-se o leitor de novo.
    public string? ChavePublicaAssinatura { get; set; }
    public DateTime? ChavePublicaRegistradaEm { get; set; }
}

// Desafio de uso único emitido pelo servidor antes de cada leitura de digital. O agente assina o desafio
// junto com o resultado da leitura; um desafio vencido ou já usado é recusado, então uma leitura
// capturada não pode ser reaproveitada depois.
public class DesafioBiometria : AuditableEntity
{
    public Guid DispositivoId { get; set; }
    public DateTime ExpiraEm { get; set; }
    public DateTime? UsadoEm { get; set; }
}
