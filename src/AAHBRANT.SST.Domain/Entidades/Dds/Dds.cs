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

    // Registro diário dentro de uma semana (31/08) — nullable só para não quebrar linhas já
    // existentes antes da reformulação; todo DDS criado dali em diante é sempre vinculado a uma
    // DdsSemanal (validado em CriarDdsCommand, não aqui no domínio).
    public Guid? DdsSemanalId { get; set; }
    public DdsSemanal? DdsSemanal { get; set; }

    public DateTime Data { get; set; }

    public Guid ResponsavelUsuarioId { get; set; }
    public Usuario? ResponsavelUsuario { get; set; }

    // Tema livre (opcional, aditivo — não substitui os temas das atividades abaixo). Nome/
    // descrição são uma cópia do CatalogoTemaDds no momento da criação (mesmo princípio de
    // snapshot já usado nos itens de checklist): se o item do catálogo for editado ou excluído
    // depois, este DDS continua mostrando o que foi realmente apresentado naquele dia.
    public Guid? CatalogoTemaDdsId { get; set; }
    public CatalogoTemaDds? CatalogoTemaDds { get; set; }
    public string? TemaLivreNome { get; set; }
    public string? TemaLivreDescricao { get; set; }

    // Documento não lista vocabulário literal de status para este módulo (mesma lacuna já
    // registrada em StatusApr/StatusPgr/StatusPt/StatusInspecao) — proposta própria.
    public StatusDds Status { get; set; } = StatusDds.EmAndamento;

    // Dia sem expediente — feriado, folga, obra parada (pedido do usuário, 03/09): em vez de deixar
    // o dia da semana em branco/pendente sem explicação, o responsável registra o motivo. Este
    // registro nasce direto com Status=Concluido (ver RegistrarDiaSemExpedienteCommand) — não passa
    // pelo fluxo normal de encerramento (que exige 3 fotos de evidência do DDS; não há o que
    // fotografar num dia sem DDS) — e não tem Atividades/ItensChecklist/Participantes/FotosEvidencia.
    public bool SemExpediente { get; set; }
    public string? MotivoSemExpediente { get; set; }

    public ICollection<DdsAtividade> Atividades { get; set; } = new List<DdsAtividade>();
    public ICollection<DdsItemChecklist> ItensChecklist { get; set; } = new List<DdsItemChecklist>();
    public ICollection<DdsParticipante> Participantes { get; set; } = new List<DdsParticipante>();
    public ICollection<DdsFotoEvidencia> FotosEvidencia { get; set; } = new List<DdsFotoEvidencia>();
}

// Atividades do dia selecionadas pelo gestor para este DDS — vínculo N:N materializado (mesmo
// padrão de RiscoTrabalhadorExposto). Cada atividade marcada contribui com seu próprio bloco de
// tema (snapshot do Risco de maior nível, ver campos abaixo) — não é mais só a 1ª/2ª da lista.
public class DdsAtividade : AuditableEntity
{
    public Guid DdsId { get; set; }
    public Dds? Dds { get; set; }

    public Guid AtividadeId { get; set; }
    public Atividade? Atividade { get; set; }

    public int Ordem { get; set; }

    // Snapshot do nome da própria Atividade, copiado na criação do Dds — mesmo princípio dos
    // campos de Perigo/Risco abaixo (cópia, não referência viva): Atividade tem exclusão lógica e
    // pode ser renomeada depois, e este DDS precisa continuar mostrando o nome de quando foi
    // registrado.
    public string? AtividadeNome { get; set; }

    // Snapshot do Risco de maior NivelRisco desta atividade, copiado na criação do Dds — mesmo
    // princípio de DdsItemChecklist (cópia, não referência viva). Tudo nullable: a atividade pode
    // não ter nenhum Risco cadastrado ainda.
    public string? PerigoNome { get; set; }
    public string? PerigoDescricao { get; set; }
    public string? Consequencia { get; set; }
    public string? ControlesExistentes { get; set; }
    public string? ControlesAdicionais { get; set; }
}

// Catálogo pré-cadastrado de temas de DDS (31/08) — tema livre opcional, adicionado por cima dos
// temas automáticos das atividades (ver Dds.TemaLivreNome). Cadastro simples (nome + descrição),
// mesmo espírito de CatalogoEpi: sem versionamento, edição in-place.
public class CatalogoTemaDds : AuditableEntity
{
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
}

// Evidência fotográfica do registro diário (31/08, pedido do usuário: "obrigatoriedade de 3 fotos
// por registro de DDS para liberação do encerramento") — distinta da foto de presença por
// participante (DdsParticipante.FotoConteudo, que comprova QUEM esteve no DDS); esta é a evidência
// do DDS em si (ex.: fotos da equipe reunida, do local, do quadro/registro do tema do dia). Ordem
// (1 a 3) só identifica qual das 3 fotos obrigatórias é essa — sem significado além de UI.
public class DdsFotoEvidencia : AuditableEntity
{
    public Guid DdsId { get; set; }
    public Dds? Dds { get; set; }

    public int Ordem { get; set; }
    public byte[] FotoConteudo { get; set; } = Array.Empty<byte>();
    public string FotoContentType { get; set; } = string.Empty;
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
// usuário — a Fase 1 original deixava isso de fora): originalmente foto da pessoa presente OU do
// documento (lista de presença) assinado por ela. A partir de 2026-08-31 (pedido do usuário) a
// evidência passou a ser exclusivamente a validação biométrica (leitor Futronic FS80H) do
// participante selecionado — FotoTipo passa a ser sempre Biometria e ScoreConfianca guarda o score
// do match retornado por IAutenticacaoBiometriaLocalService.AutenticarPorMatchLocalAsync.
// FotoConteudo/FotoContentType continuam presentes só para preservar o histórico de registros
// anteriores a essa mudança (nunca mais preenchidos em novos registros).
public class DdsParticipante : AuditableEntity
{
    public Guid DdsId { get; set; }
    public Dds? Dds { get; set; }

    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }

    public TipoFotoParticipante FotoTipo { get; set; }
    public byte[] FotoConteudo { get; set; } = Array.Empty<byte>();
    public string FotoContentType { get; set; } = string.Empty;
    public double? ScoreConfianca { get; set; }
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
