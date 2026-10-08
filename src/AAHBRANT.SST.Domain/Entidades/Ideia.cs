using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Banco de Ideias e Evolução do Produto — especificação do usuário de 08/10/2026 (§5, campos principais).
// O Telegram é só o canal de captura (§18): esta tabela é a fonte oficial. A mensagem original
// (MensagemOriginal) é o registro de origem e nunca é alterada depois de gravada (§3).
public class Ideia : AuditableEntity
{
    // "IDEIA-0037" — sequencial único, sem reinício anual (ver RegistrarIdeiaCommand).
    public int Numero { get; set; }
    public string Codigo { get; set; } = string.Empty;

    public CanalIdeia Canal { get; set; } = CanalIdeia.Web;
    public string MensagemOriginal { get; set; } = string.Empty;

    public long? TelegramChatId { get; set; }
    public long? TelegramMensagemId { get; set; }
    public long? TelegramUsuarioId { get; set; }
    public string? TelegramUsuarioNome { get; set; }

    public Guid? RegistradoPorUsuarioId { get; set; }
    public Usuario? RegistradoPorUsuario { get; set; }
    public string? RegistradoPorNome { get; set; }

    // Estruturação feita no registro (§3) — editável pela equipe na análise.
    public string Titulo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string? ProblemaOportunidade { get; set; }
    public string? Objetivo { get; set; }
    public string? SolucaoSugerida { get; set; }
    public string? Modulo { get; set; }
    public string? Submodulo { get; set; }
    public string? Categoria { get; set; }
    public string? BeneficioEsperado { get; set; }
    public string? PossiveisImpactos { get; set; }
    public string? IntegracoesNecessarias { get; set; }
    public bool? NecessidadeIa { get; set; }
    public string? Dependencias { get; set; }
    public string? InformacoesFaltantes { get; set; }

    // "Heuristica" ou "IA" — deixa claro de onde veio a estruturação inicial.
    public string EstruturadoPor { get; set; } = "Heuristica";

    // Análise (§7).
    public NivelIdeia? Impacto { get; set; }
    public NivelIdeia? Urgencia { get; set; }
    public NivelIdeia? Complexidade { get; set; }
    public NivelIdeia? Esforco { get; set; }
    public NivelIdeia? ValorNegocio { get; set; }
    public string? EsforcoEstimado { get; set; }
    public ViabilidadeIdeia ViabilidadeTecnica { get; set; } = ViabilidadeIdeia.NaoAvaliada;
    public ViabilidadeIdeia ViabilidadeOperacional { get; set; } = ViabilidadeIdeia.NaoAvaliada;

    public Guid? ResponsavelAnaliseUsuarioId { get; set; }
    public Usuario? ResponsavelAnaliseUsuario { get; set; }
    public string? ResponsavelAnaliseNome { get; set; }

    // Priorização (§9) — a pontuação é só apoio à decisão; a prioridade é definida pelo gestor.
    public PrioridadeIdeia? Prioridade { get; set; }
    public int? Pontuacao { get; set; }

    public StatusIdeia Status { get; set; } = StatusIdeia.NovaIdeia;
    public DecisaoIdeia? Decisao { get; set; }
    public string? Justificativa { get; set; }
    public string? DecididoPorNome { get; set; }

    public Guid? ResponsavelDesenvolvimentoUsuarioId { get; set; }
    public Usuario? ResponsavelDesenvolvimentoUsuario { get; set; }
    public string? ResponsavelDesenvolvimentoNome { get; set; }

    public DateTime? DataAprovacaoUtc { get; set; }
    public DateTime? DataInicioUtc { get; set; }
    public DateTime? DataConclusaoUtc { get; set; }
    public DateTime? DataImplantacaoUtc { get; set; }
    public string? Observacoes { get; set; }

    // Duplicidade (§16): IdeiaPrincipal = ideia à qual esta foi vinculada; IdeiaSemelhante = sugestão
    // da IA ainda não resolvida pelo usuário.
    public Guid? IdeiaPrincipalId { get; set; }
    public Ideia? IdeiaPrincipal { get; set; }
    public Guid? IdeiaSemelhanteId { get; set; }
    public Ideia? IdeiaSemelhante { get; set; }

    // Pergunta objetiva deixada no Telegram (§4/§16).
    public TipoPerguntaIdeia PerguntaPendente { get; set; } = TipoPerguntaIdeia.Nenhuma;
    public long? PerguntaMensagemId { get; set; }

    public ICollection<IdeiaComentario> Comentarios { get; set; } = new List<IdeiaComentario>();
    public ICollection<IdeiaHistorico> Historico { get; set; } = new List<IdeiaHistorico>();
    public ICollection<IdeiaAnexo> Anexos { get; set; } = new List<IdeiaAnexo>();
    public ICollection<IdeiaRequisito> Requisitos { get; set; } = new List<IdeiaRequisito>();
}

// Espaço de discussão da ideia (§12).
public class IdeiaComentario : AuditableEntity
{
    public Guid IdeiaId { get; set; }
    public Ideia? Ideia { get; set; }

    public Guid? AutorUsuarioId { get; set; }
    public string AutorNome { get; set; } = string.Empty;
    public string Texto { get; set; } = string.Empty;
}

// Histórico append-only (§13): só INSERT, nunca UPDATE/DELETE.
public class IdeiaHistorico : AuditableEntity
{
    public Guid IdeiaId { get; set; }
    public Ideia? Ideia { get; set; }

    public TipoHistoricoIdeia Tipo { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public Guid? AutorUsuarioId { get; set; }
    public string AutorNome { get; set; } = string.Empty;
    public DateTime OcorridoEmUtc { get; set; }
}

// Documentos, imagens, prints e fluxogramas (§12). Conteúdo no próprio banco: o projeto ainda não
// tem armazenamento de blobs para este tipo de arquivo, e o limite (ver AnexarArquivoIdeiaCommand) é pequeno.
public class IdeiaAnexo : AuditableEntity
{
    public Guid IdeiaId { get; set; }
    public Ideia? Ideia { get; set; }

    public string NomeArquivo { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long Tamanho { get; set; }
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();
    public string? EnviadoPorNome { get; set; }
}

// Requisito funcional gerado a partir de uma ideia aprovada (§10).
public class IdeiaRequisito : AuditableEntity
{
    public Guid IdeiaId { get; set; }
    public Ideia? Ideia { get; set; }

    public string Titulo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    // Um critério de aceite por linha.
    public string CriteriosAceite { get; set; } = string.Empty;

    public StatusRequisitoIdeia Status { get; set; } = StatusRequisitoIdeia.Rascunho;
    public DateTime? AprovadoEmUtc { get; set; }
    public string? AprovadoPorNome { get; set; }

    public ICollection<DemandaDesenvolvimento> Demandas { get; set; } = new List<DemandaDesenvolvimento>();
}

// IDEIA → REQUISITO → DEMANDA → FUNCIONALIDADE (§11). FuncionalidadeEntregue registra o que foi
// efetivamente entregue, fechando a rastreabilidade.
public class DemandaDesenvolvimento : AuditableEntity
{
    public string Codigo { get; set; } = string.Empty;

    public Guid IdeiaId { get; set; }
    public Ideia? Ideia { get; set; }
    public Guid RequisitoId { get; set; }
    public IdeiaRequisito? Requisito { get; set; }

    public string Titulo { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public StatusDemandaDesenvolvimento Status { get; set; } = StatusDemandaDesenvolvimento.Aberta;

    public Guid? ResponsavelUsuarioId { get; set; }
    public string? ResponsavelNome { get; set; }
    public string? FuncionalidadeEntregue { get; set; }
    public DateTime? ConcluidaEmUtc { get; set; }
}
