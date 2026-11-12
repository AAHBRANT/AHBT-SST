using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Câmera IP (ou gravador) da rede local da obra, lida pelo agente de câmeras (AgenteCamera). Guarda o endereço e a
// credencial uma vez só: uma câmera de duas lentes vira duas Camera (Canal 1 e 2) apontando para o mesmo equipamento,
// e quando o DHCP muda o IP corrige-se aqui e vale para as duas. A senha fica cifrada (AES-256-GCM, chave dedicada
// Lgpd:ChaveCriptografiaCredenciaisCameraBase64) e NUNCA volta para o front: só sai, decifrada, na configuração
// entregue ao próprio agente (rota autenticada pelo segredo dele). Ativo = exclusão lógica.
public class EquipamentoCamera : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public Guid AgenteCameraId { get; set; }
    public AgenteCamera? AgenteCamera { get; set; }

    public string Nome { get; set; } = string.Empty;

    // IP ou nome na rede local da obra.
    public string Host { get; set; } = string.Empty;
    public int PortaRtsp { get; set; } = 554;
    public int PortaOnvif { get; set; } = 8899;

    // Guardado em maiúsculas, com dois-pontos (AA:BB:CC:DD:EE:FF): o agente o usa para reencontrar o equipamento quando o IP muda.
    public string? EnderecoMac { get; set; }

    public string? Usuario { get; set; }

    // Texto cifrado (base64 de nonce + cifra + tag). Nulo = senha ainda não definida.
    public string? SenhaCifrada { get; set; }

    public string? Fabricante { get; set; }
    public string? Modelo { get; set; }

    // Quando o agente informou, pela última vez, um novo IP encontrado pelo MAC.
    public DateTime? UltimoIpDescobertoEmUtc { get; set; }
}

// Pedido de "Testar conexão". O servidor não alcança a câmera (ela está na rede local da obra): o front cria o pedido,
// o agente o recebe junto da configuração, executa o teste e devolve o resultado, e o front consulta o resultado.
// A miniatura (opcional) é foto de pessoa possível: fica cifrada, é entregue ao front uma única vez e apagada em seguida
// (ou ao expirar).
public class TesteConexaoCamera : AuditableEntity
{
    public Guid ObraId { get; set; }

    public Guid AgenteCameraId { get; set; }
    public Guid EquipamentoCameraId { get; set; }

    // Preenchido quando o teste foi pedido a partir de uma câmera já cadastrada.
    public Guid? CameraId { get; set; }

    public int Canal { get; set; } = 1;
    public int Stream { get; set; }

    public DateTime SolicitadoEmUtc { get; set; }
    public DateTime? EntregueAoAgenteEmUtc { get; set; }
    public DateTime? ConcluidoEmUtc { get; set; }

    public bool? Sucesso { get; set; }

    // Motivo curto (sem senha nem URL com credencial) quando o teste falha, ou observação quando passa.
    public string? Motivo { get; set; }

    // Código estável do motivo da falha (ver CodigosTesteConexaoCamera): é por ele que a tela escolhe a mensagem.
    public string? Codigo { get; set; }

    // Ex.: "1280x720 H.264", informado pelo agente.
    public string? Detalhe { get; set; }

    public byte[]? Miniatura { get; set; }
}
