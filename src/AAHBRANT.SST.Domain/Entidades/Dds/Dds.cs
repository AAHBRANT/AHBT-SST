using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// DDS — Diálogo Diário de Segurança. Módulo pedido explicitamente pelo usuário em 2026-08-20
// (fora dos 22 itens do MVP da §47) — modelagem é proposta própria, sem seção literal da Base de
// Conhecimento para se basear. Fase 1 (2026-08-24): o "roteiro" é gerado cruzando as Atividades do
// dia selecionadas pelo gestor com os Riscos já cadastrados (Atividade → Risco → Perigo) — ver
// disclosure em DdsItemChecklist sobre a fonte do checklist.
public class Dds : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public DateTime Data { get; set; }

    public Guid ResponsavelUsuarioId { get; set; }
    public Usuario? ResponsavelUsuario { get; set; }

    // Snapshot gerado na criação a partir do Perigo de maior NivelRisco entre os Riscos das
    // atividades selecionadas — se o Risco for editado depois, o DDS já gerado não muda.
    public string TopicoPrincipal { get; set; } = string.Empty;

    // Documento não lista vocabulário literal de status para este módulo (mesma lacuna já
    // registrada em StatusApr/StatusPgr/StatusPt/StatusInspecao) — proposta própria.
    public StatusDds Status { get; set; } = StatusDds.EmAndamento;

    public ICollection<DdsAtividade> Atividades { get; set; } = new List<DdsAtividade>();
    public ICollection<DdsItemChecklist> ItensChecklist { get; set; } = new List<DdsItemChecklist>();
    public ICollection<DdsParticipante> Participantes { get; set; } = new List<DdsParticipante>();
}

// Atividades do dia selecionadas pelo gestor para este DDS — vínculo N:N materializado (mesmo
// padrão de RiscoTrabalhadorExposto).
public class DdsAtividade : AuditableEntity
{
    public Guid DdsId { get; set; }
    public Dds? Dds { get; set; }

    public Guid AtividadeId { get; set; }
    public Atividade? Atividade { get; set; }
}

// Um item de checklist por linha de controle dos Riscos vinculados às atividades selecionadas —
// gerado automaticamente na criação do DDS (snapshot, não é uma referência viva ao Risco).
//
// Disclosure: a proposta do usuário (2026-08-24) previa um "checklist de EPI/EPC" como categoria
// própria. O schema atual não tem um vínculo estruturado entre CatalogoEpi e Perigo/Risco/
// Atividade (CatalogoEpi só se liga a Trabalhador via EntregaEpi) — então este checklist não
// fabrica essa categoria. Ele reaproveita, linha a linha, os campos de texto livre
// Risco.ControlesExistentes/ControlesAdicionais que o usuário já registra no módulo de Riscos
// (que na prática incluem uso de EPI quando o usuário assim descreve o controle). Um vínculo
// EPI↔Perigo estruturado, se desejado, é uma extensão de schema do módulo Riscos — fora desta fatia.
public class DdsItemChecklist : AuditableEntity
{
    public Guid DdsId { get; set; }
    public Dds? Dds { get; set; }

    public Guid? RiscoId { get; set; }
    public Risco? Risco { get; set; }

    public string Descricao { get; set; } = string.Empty;
    public bool Verificado { get; set; }
}

// Registro de presença. Evidência obrigatória (2026-08-24, trazida para o escopo a pedido do
// usuário — a Fase 1 original deixava isso de fora): foto da pessoa presente OU do documento
// (lista de presença) assinado por ela, à escolha de quem conduz o DDS. Guardada como binário no
// próprio banco (sem storage externo), pedido explícito do usuário.
public class DdsParticipante : AuditableEntity
{
    public Guid DdsId { get; set; }
    public Dds? Dds { get; set; }

    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }

    public TipoFotoParticipante FotoTipo { get; set; }
    public byte[] FotoConteudo { get; set; } = Array.Empty<byte>();
    public string FotoContentType { get; set; } = string.Empty;
}

// Um envio de Telegram por trabalhador — dobra como log de envio e como registro de confirmação de
// ciência (botão inline "Confirmo ciência" no chat). Id (Guid gerado no client, ver AuditableEntity)
// é usado como callback_data do botão para correlacionar o clique a este envio.
public class DdsTelegramEnvio : AuditableEntity
{
    public Guid DdsId { get; set; }
    public Dds? Dds { get; set; }

    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }

    public long ChatId { get; set; }
    public DateTime EnviadoEm { get; set; }
    public int? MessageId { get; set; }
    public DateTime? ConfirmadoEm { get; set; }
}
