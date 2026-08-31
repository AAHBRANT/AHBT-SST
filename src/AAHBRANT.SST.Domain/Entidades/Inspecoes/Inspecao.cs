using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Inspeção (§23/§46): execução de um ChecklistModelo vigente. "Cada inspeção deverá gerar
// evidência e, quando necessário, uma não conformidade" (§23) — evidência é por item
// (InspecaoItemResposta, via Evidencia genérica); geração de NC a partir de um item não conforme
// está implementada — ver disclosure em InspecaoItemResposta.
public class Inspecao : AuditableEntity
{
    public TipoInspecao TipoInspecao { get; set; }

    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    // Nem toda inspeção está ligada a uma atividade específica (ex.: inspeção de canteiro é
    // ampla, não pontual) — nullable por decisão própria, não citação literal. Quando preenchida,
    // dá a rastreabilidade que §46 sugere no fluxo "Liberação da atividade → Inspeção".
    public Guid? AtividadeId { get; set; }
    public Atividade? Atividade { get; set; }

    public Guid ChecklistModeloId { get; set; }
    public ChecklistModelo? ChecklistModelo { get; set; }

    public DateTime Data { get; set; }

    public Guid ResponsavelUsuarioId { get; set; }
    public Usuario? ResponsavelUsuario { get; set; }

    // Documento não lista vocabulário literal para o "status" da execução (mesma lacuna já
    // registrada em StatusApr/StatusPgr/StatusPt/StatusControleRisco) — proposta própria.
    public StatusInspecao Status { get; set; } = StatusInspecao.EmAndamento;

    public ICollection<InspecaoItemResposta> Respostas { get; set; } = new List<InspecaoItemResposta>();
}

// Resposta por item do checklist (§24). Fotografia/evidência reaproveita a entidade genérica
// Evidencia (EntidadeTipo="InspecaoItemResposta"), mesmo padrão de ASO/Treinamento/EPI/APR/PT —
// não cria campo de anexo próprio.
//
// Geração de Não Conformidade a partir de StatusItem=NaoConforme: implementado em 2026-08-29
// conforme o Procedimento de Inspeção Técnica de Campo (§6.2) — CriarNaoConformidadeDeItemCommand
// oferece "gerar NC a partir deste item" para itens com StatusItem=NaoConforme, gravando o vínculo
// em NaoConformidade.InspecaoItemRespostaId (não o inverso: um item pode, em tese, já existir sem
// NC gerada — a FK fica do lado da NC, que é sempre opcional/posterior ao item).
public class InspecaoItemResposta : AuditableEntity
{
    public Guid InspecaoId { get; set; }
    public Inspecao? Inspecao { get; set; }

    public Guid ChecklistModeloItemId { get; set; }
    public ChecklistModeloItem? ChecklistModeloItem { get; set; }

    // Nullable por decisão própria: ao criar a Inspecao, uma linha é gerada para cada item do
    // checklist já como "ainda não respondido" (StatusItem = null) — §24 lista os 3 estados
    // possíveis de uma resposta dada (conforme/não conforme/não aplicável), mas não um 4º estado
    // de pendência; nulo modela isso sem inventar um valor de enum extra.
    public StatusItemChecklist? StatusItem { get; set; }
    public string? Observacao { get; set; }

    // Campos adicionados para o formato "Patrulha de Segurança do Trabalho" (planilha do usuário,
    // 31/08) — decisão do usuário: reaproveitar o checklist existente em vez de criar um modelo de
    // achados livres, liberando a descrição do item para edição na própria execução. Quando
    // preenchida, DescricaoPersonalizada sobrescreve ChecklistModeloItem.Descricao só nesta
    // resposta (o item do template, compartilhado entre execuções, não é alterado). Local e
    // PlanoDeAcao não existiam no modelo (só apareciam ao gerar uma Não Conformidade a partir do
    // item, ver CriarNaoConformidadeDeItemCommand) — aqui ficam registrados desde a execução.
    public string? DescricaoPersonalizada { get; set; }
    public string? Local { get; set; }
    public string? PlanoDeAcao { get; set; }

    public Guid? ResponsavelUsuarioId { get; set; }
    public Usuario? ResponsavelUsuario { get; set; }
    public DateTime? Prazo { get; set; }

    // Campo próprio em vez da Evidencia genérica citada acima: decisão tomada em 2026-08-25
    // (confirmada com o usuário) para reaproveitar o mesmo padrão já em produção em
    // Dds.FotoConteudo, já que a entidade Evidencia (BlobUrl) nunca chegou a ser implementada
    // (sem controller/command/blob storage por trás). Representa a evidência ANTES (a irregularidade
    // encontrada); FotoDepois* (abaixo, 31/08) é a evidência DEPOIS, registrada quando o achado é
    // resolvido — mesmo par "Evidência Anterior/Evidência posterior" da planilha de patrulha.
    public byte[] FotoConteudo { get; set; } = Array.Empty<byte>();
    public string FotoContentType { get; set; } = string.Empty;

    public byte[]? FotoDepoisConteudo { get; set; }
    public string? FotoDepoisContentType { get; set; }
}
