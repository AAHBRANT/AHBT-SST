using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Serviço Windows instalado na obra (AAHBRANT.SST.AgenteCameras) que lê as câmeras IP pela rede local e envia
// os quadros à API, sem usuário logado. Autentica-se pelo segredo (cabeçalho X-Agente-Segredo): no banco fica
// só o hash; o segredo em texto existe uma única vez, na resposta do cadastro. Revogar = Ativo false (o
// registro fica para auditoria). Um agente atende uma única obra.
public class AgenteCamera : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public string Nome { get; set; } = string.Empty;
    public string SegredoHash { get; set; } = string.Empty;

    // Versão informada pelo próprio agente (cabeçalho X-Agente-Versao) a cada contato.
    public string? Versao { get; set; }
    public DateTime? UltimoContatoEmUtc { get; set; }
}
