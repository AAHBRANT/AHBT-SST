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

// PR-SST-003 — dados específicos do PCMSO, originalmente em cima de um DocumentoGestao
// (Tipo="PCMSO") para nome/versão/validade/status/histórico de revisões. PENDENTE: DocumentoGestao
// foi removido junto com o módulo de Conformidade (2026-08-28) — DocumentoGestaoId ficou sem
// entidade correspondente (nenhuma FK real no banco) até o PCMSO ser reformulado para não depender
// mais dele.
public class PcmsoDetalhe : AuditableEntity
{
    public Guid DocumentoGestaoId { get; set; }

    public string? MedicoResponsavelNome { get; set; }
    public string? MedicoResponsavelCrm { get; set; }
    public string? FuncoesContempladas { get; set; }
    public string? RiscosConsiderados { get; set; }
    public string? ExamesPrevistos { get; set; }
    public string? Periodicidades { get; set; }
    public string? UnidadesObrasAbrangidas { get; set; }
}
