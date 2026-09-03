using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Permissão de Trabalho — reformulada em 2026-08-29 para reproduzir literalmente o formulário
// "PT – PERMISSÃO DE TRABALHO REV.01" (planilha do usuário), substituindo o desenho anterior
// (catálogos genéricos Perigo/Controle/Requisito de texto livre) por checklists fixos e literais
// do documento — mesmo princípio já aplicado à reformulação da Apr. Sem dado real cadastrado no
// seeder para migrar.
public class PermissaoTrabalho : AuditableEntity
{
    // "Nº PT:" do cabeçalho — gerado automaticamente pelo sistema na criação (formato
    // "PT-{ano}-{sequencial}", ver GeradorNumeroDocumento; pedido do usuário, 03/09), não editável
    // depois. O documento original não definia uma regra de numeração — decisão própria.
    public string? NumeroPt { get; set; }

    public Guid AtividadeId { get; set; }
    public Atividade? Atividade { get; set; }

    // "Descrição objetiva da atividade:" — texto próprio da PT, distinto do nome cadastrado em
    // Atividade (que é reutilizado por Risco/Apr/Pgr e tende a ser mais genérico).
    public string DescricaoAtividade { get; set; } = string.Empty;

    public string Local { get; set; } = string.Empty;

    // "Empresa executante:" — texto livre (a PT pode ser emitida para uma contratada específica,
    // distinta da Obra/Contrato principal).
    public string? EmpresaExecutante { get; set; }

    public Guid? EquipeId { get; set; }
    public Equipe? Equipe { get; set; }

    public DateTime Data { get; set; }

    // "Validade: ____:____ às ____:____" do cabeçalho — janela de horário dentro do dia em que a
    // atividade está liberada (mesmo campo já existente antes da reformulação).
    public TimeSpan? HorarioInicio { get; set; }
    public TimeSpan? HorarioFim { get; set; }
    public DateTime? Validade { get; set; }

    // "Responsável pela execução:" / "Responsável pela área / liberação:" do cabeçalho —
    // identificação de quem ocupa cada papel; a assinatura de fato (com hora) acontece na
    // liberação (§7), ver campos DataAssinatura* abaixo.
    public Guid? ResponsavelExecucaoUsuarioId { get; set; }
    public Usuario? ResponsavelExecucaoUsuario { get; set; }
    public Guid? ResponsavelAreaUsuarioId { get; set; }
    public Usuario? ResponsavelAreaUsuario { get; set; }

    public StatusPt Status { get; set; } = StatusPt.EmElaboracao;

    // §7 "Liberação da atividade" — três assinaturas nomeadas do documento. AutorizadoPorUsuarioId/
    // DataAutorizacao correspondem a "Emitente / Responsável pela Área" (mesmo campo já existente
    // antes da reformulação, papel que já modelava "quem autoriza"). ResponsavelSstUsuarioId é
    // "quando requerido" pelo documento — pode ficar nulo.
    public Guid? AutorizadoPorUsuarioId { get; set; }
    public Usuario? AutorizadoPorUsuario { get; set; }
    public DateTime? DataAutorizacao { get; set; }
    public DateTime? DataAssinaturaExecucao { get; set; }
    public Guid? ResponsavelSstUsuarioId { get; set; }
    public Usuario? ResponsavelSstUsuario { get; set; }
    public DateTime? DataAssinaturaSst { get; set; }

    // §8 "Suspensão" — SuspenderPermissaoTrabalhoCommand.
    public Guid? SuspensaPorUsuarioId { get; set; }
    public Usuario? SuspensaPorUsuario { get; set; }
    public DateTime? DataSuspensao { get; set; }
    public string? MotivoSuspensao { get; set; }

    // §8 "Revalidação" — RevalidarPermissaoTrabalhoCommand atualiza Validade/HorarioFim (a "nova
    // validade" do documento) e registra quem revalidou e quando; não é um campo de "nova validade"
    // separado porque a Validade em vigor é sempre a mais recente.
    public Guid? RevalidadaPorUsuarioId { get; set; }
    public Usuario? RevalidadaPorUsuario { get; set; }
    public DateTime? DataRevalidacao { get; set; }

    // §8 "Encerramento" (mesmos campos já existentes antes da reformulação).
    public Guid? EncerradaPorUsuarioId { get; set; }
    public Usuario? EncerradaPorUsuario { get; set; }
    public DateTime? DataEncerramento { get; set; }
    public string? ObservacoesEncerramento { get; set; }

    // "Outros EPIs"/"Outros EPCs/recursos" do §5 — texto livre complementar às listas fixas
    // (PermissaoTrabalhoEpi/PermissaoTrabalhoEpc abaixo).
    public string? OutrosEpis { get; set; }
    public string? OutrosEpcs { get; set; }

    public ICollection<PermissaoTrabalhoPreRequisito> PreRequisitos { get; set; } = new List<PermissaoTrabalhoPreRequisito>();
    public ICollection<PermissaoTrabalhoTipoTrabalho> TiposTrabalho { get; set; } = new List<PermissaoTrabalhoTipoTrabalho>();
    public ICollection<PermissaoTrabalhoVerificacao> Verificacoes { get; set; } = new List<PermissaoTrabalhoVerificacao>();
    public ICollection<PermissaoTrabalhoEpi> Epis { get; set; } = new List<PermissaoTrabalhoEpi>();
    public ICollection<PermissaoTrabalhoEpc> Epcs { get; set; } = new List<PermissaoTrabalhoEpc>();
    public ICollection<PermissaoTrabalhoRiscoCritico> RiscosCriticos { get; set; } = new List<PermissaoTrabalhoRiscoCritico>();

    // §9 "Ciência da equipe executante" — trabalhadores designados/executantes. A "Assinatura /
    // Ciência" de cada um usa o Motor de Assinatura Eletrônica já existente (DocumentoAssinatura
    // com EntidadeTipo=nameof(PermissaoTrabalho)), não uma tabela própria — mesmo padrão já usado
    // para Dds, sem alteração nesta reformulação.
    public ICollection<PermissaoTrabalhoResponsavel> Responsaveis { get; set; } = new List<PermissaoTrabalhoResponsavel>();
}

// §2 do formulário — nasce com os 6 itens fixos (ver CriarPermissaoTrabalhoCommand); só permite
// marcar Atendido, não criar/excluir linha (ao contrário do antigo PermissaoTrabalhoRequisito de
// texto livre que substitui).
public class PermissaoTrabalhoPreRequisito : AuditableEntity
{
    public Guid PermissaoTrabalhoId { get; set; }
    public PermissaoTrabalho? PermissaoTrabalho { get; set; }

    public ItemPreRequisitoPt Item { get; set; }
    public bool Atendido { get; set; }
}

// §3 do formulário — só os tipos marcados pelo usuário viram linha (mesmo princípio de
// MatrizEpiFuncao: ausência = não marcado), ao contrário de PreRequisito/Verificacao (que nascem
// com todos os itens fixos já presentes).
public class PermissaoTrabalhoTipoTrabalho : AuditableEntity
{
    public Guid PermissaoTrabalhoId { get; set; }
    public PermissaoTrabalho? PermissaoTrabalho { get; set; }

    public TipoTrabalhoEspecialPt Tipo { get; set; }
    public string? DescricaoOutro { get; set; }
}

// §4 do formulário — nasce com os 15 itens fixos (ver CriarPermissaoTrabalhoCommand); Resposta
// nulo = ainda não verificado. Qualquer NaoConforme bloqueia a liberação (ver
// AutorizarPermissaoTrabalhoCommand e o aviso literal do documento em §7).
public class PermissaoTrabalhoVerificacao : AuditableEntity
{
    public Guid PermissaoTrabalhoId { get; set; }
    public PermissaoTrabalho? PermissaoTrabalho { get; set; }

    public ItemVerificacaoPt Item { get; set; }
    public RespostaVerificacaoPt? Resposta { get; set; }
}

// §5 do formulário, "EPIs aplicáveis" — só os itens marcados viram linha; Complemento é o texto
// livre embutido em algumas opções (ex.: "Luvas: ____", "Respirador: ____").
public class PermissaoTrabalhoEpi : AuditableEntity
{
    public Guid PermissaoTrabalhoId { get; set; }
    public PermissaoTrabalho? PermissaoTrabalho { get; set; }

    public ItemEpiPt Item { get; set; }
    public string? Complemento { get; set; }
}

// §5 do formulário, "EPCs / recursos aplicáveis".
public class PermissaoTrabalhoEpc : AuditableEntity
{
    public Guid PermissaoTrabalhoId { get; set; }
    public PermissaoTrabalho? PermissaoTrabalho { get; set; }

    public ItemEpcPt Item { get; set; }
}

// §6 "Riscos críticos e controles complementares" — tabela livre (linhas em branco no formulário
// original); substitui o antigo PermissaoTrabalhoControle (texto solto sem estrutura), que não
// tinha campo de responsável/evidência.
public class PermissaoTrabalhoRiscoCritico : AuditableEntity
{
    public Guid PermissaoTrabalhoId { get; set; }
    public PermissaoTrabalho? PermissaoTrabalho { get; set; }

    public string RiscoCondicao { get; set; } = string.Empty;
    public string? ControleComplementar { get; set; }
    public string? ResponsavelEvidencia { get; set; }
}

// "Responsáveis"/§9 "Ciência da equipe executante" — mesma entidade/finalidade de antes da
// reformulação (trabalhadores designados/executantes), sem alteração de campos.
public class PermissaoTrabalhoResponsavel : AuditableEntity
{
    public Guid PermissaoTrabalhoId { get; set; }
    public PermissaoTrabalho? PermissaoTrabalho { get; set; }

    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }
}
