using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// PR-SST-003 — exame complementar do PCMSO (audiometria, acuidade visual etc.). Vínculo com Aso é
// opcional: nem todo exame complementar é solicitado junto de um ASO específico.
public class ExameComplementar : AuditableEntity
{
    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }

    public Guid? AsoId { get; set; }
    public Aso? Aso { get; set; }

    public TipoExameComplementar Tipo { get; set; }
    public DateTime DataRealizacao { get; set; }
    public DateTime DataValidade { get; set; }
    public string Resultado { get; set; } = string.Empty;
    public string? Observacoes { get; set; }
    public string? ResponsavelTecnico { get; set; }
}

// PR-SST-003 — aptidão para atividade crítica (ex.: Altura, Espaço Confinado). "Atividade crítica"
// é texto livre: o documento fonte só dá exemplos, não um vocabulário fechado (mesma convenção já
// usada em DocumentoGestao.Tipo/Categoria). Reaproveita ResultadoAso para o status de aptidão.
public class AptidaoAtividadeEspecifica : AuditableEntity
{
    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }

    public string AtividadeCritica { get; set; } = string.Empty;
    public ResultadoAso Aptidao { get; set; } = ResultadoAso.Pendente;
    public DateTime DataAvaliacao { get; set; }
    public DateTime? DataValidade { get; set; }
    public string? MedicoResponsavel { get; set; }
    public string? Observacoes { get; set; }
}

// PR-SST-003 — dados do PCMSO. Reformulado em 2026-09-03: originalmente reaproveitava um
// DocumentoGestao genérico (Tipo="PCMSO") para nome/versão/validade/status/arquivo; DocumentoGestao
// foi removido junto com o módulo de Conformidade (2026-08-28) e deixou os 5 handlers de
// Application/Pcmsos/* (Criar/Atualizar/Excluir/Listar/ObterPorId) inertes (lançando
// NotSupportedException ou retornando vazio). Estes campos "genéricos" agora vivem direto aqui —
// mesmo padrão já usado por Pgr (Domain/Entidades/Pgr/Pgr.cs), que nunca dependeu de DocumentoGestao.
public class PcmsoDetalhe : AuditableEntity
{
    public string Nome { get; set; } = string.Empty;
    public string? Versao { get; set; }
    public DateTime? Validade { get; set; }
    public DateTime DataEmissao { get; set; }

    public Guid? ResponsavelUsuarioId { get; set; }
    public Usuario? ResponsavelUsuario { get; set; }

    // Nulável de propósito (não Guid obrigatório como em Pgr.ObraId): a tela de criação permite
    // "Obra: Nenhuma" (PcmsoTab.tsx) — decisão de UX já existente, mantida aqui.
    public Guid? ObraId { get; set; }
    public Obra? Obra { get; set; }

    public Guid? SetorId { get; set; }
    public Setor? Setor { get; set; }

    public string? Arquivo { get; set; }
    public StatusPcmsoDocumento Status { get; set; } = StatusPcmsoDocumento.Rascunho;

    public string? MedicoResponsavelNome { get; set; }
    public string? MedicoResponsavelCrm { get; set; }
    public string? FuncoesContempladas { get; set; }
    public string? RiscosConsiderados { get; set; }
    public string? ExamesPrevistos { get; set; }
    public string? Periodicidades { get; set; }
    public string? UnidadesObrasAbrangidas { get; set; }
}
