using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Sessão de "câmera IP quase ao vivo": a tela pede, o agente da obra envia JPEGs em sequência e a API guarda só o ÚLTIMO
// quadro nesta linha (sobrescrevendo). Vários espectadores da mesma câmera dividem uma sessão. Todo o estado fica no SQL
// (a API roda em várias réplicas): nada de sessão ou quadro em memória.
//
// Estados (derivados, sem coluna própria): Aguardando = IniciadaEmUtc nulo (o agente ainda não enviou o primeiro quadro);
// AoVivo = IniciadaEmUtc preenchido e EncerradaEmUtc nulo; Encerrada = EncerradaEmUtc preenchido.
// Ao encerrar, UltimoQuadro vira nulo (imagem de pessoa possível não fica guardada); a linha, sem imagem, fica como
// trilha de acesso (quem abriu qual câmera e quando).
// Sem navegação para Camera/Obra/AgenteCamera de propósito: a rota anônima do agente consulta esta tabela sem JOIN.
public class SessaoAoVivoCamera : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Guid CameraId { get; set; }
    public Guid AgenteCameraId { get; set; }

    // Usuário que abriu a sessão (nulo com a autenticação desligada, como em dev).
    public Guid? SolicitadaPorUsuarioId { get; set; }

    public DateTime SolicitadaEmUtc { get; set; }

    // Última vez que alguma tela buscou quadro (gravada no máximo a cada 5 s). Parte de SolicitadaEmUtc.
    public DateTime UltimaVisualizacaoEmUtc { get; set; }

    // Fim máximo da sessão (10 min por padrão), fixado na criação.
    public DateTime LimiteEmUtc { get; set; }

    // Quando chegou o primeiro quadro (ou a primeira falha) do agente.
    public DateTime? IniciadaEmUtc { get; set; }

    public DateTime? EncerradaEmUtc { get; set; }

    // inatividade | tempo_maximo | encerrada | agente_sem_resposta | falha | camera_desabilitada | agente_revogado
    public string? MotivoEncerramento { get; set; }

    // Código estável (CodigosTesteConexaoCamera) quando o agente encerrou por falha.
    public string? CodigoFalha { get; set; }

    public int Fps { get; set; }

    // JPEG do último quadro. Nulo antes do primeiro e depois de encerrar.
    public byte[]? UltimoQuadro { get; set; }
    public long UltimoQuadroSequencia { get; set; }
    public DateTime? UltimoQuadroCapturadoEmUtc { get; set; }

    // Quando o servidor recebeu o último quadro (relógio da API, não o do PC da obra). Base da regra "agente parou de enviar".
    public DateTime? UltimoQuadroRecebidoEmUtc { get; set; }

    public long QuadrosRecebidos { get; set; }
    public long BytesRecebidos { get; set; }
}
