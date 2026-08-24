using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Análise Preliminar de Risco (Base de Conhecimento §17): estrutura literal
// Atividade → etapas → perigos → riscos → controles → responsáveis → aprovação.
// Campos literais citados: identificação da atividade; local; equipe; data; validade;
// etapas; riscos; medidas preventivas; responsáveis; aprovação; assinatura; evidências.
// §46 confirma que "Análise de risco" precede "Liberação da atividade" no fluxo operacional.
public class Apr : AuditableEntity
{
    public Guid AtividadeId { get; set; }
    public Atividade? Atividade { get; set; }

    public string Local { get; set; } = string.Empty;

    public Guid? EquipeId { get; set; }
    public Equipe? Equipe { get; set; }

    public DateTime Data { get; set; }
    public DateTime? Validade { get; set; }

    // "Aprovação" (§17) — o documento não descreve um fluxo de status literal para a APR (mesma
    // lacuna já registrada em StatusPgr/StatusControleRisco) — proposta própria, avisar o usuário
    // se quiser outro vocabulário.
    public StatusApr Status { get; set; } = StatusApr.EmElaboracao;

    public Guid? AprovadoPorUsuarioId { get; set; }
    public Usuario? AprovadoPorUsuario { get; set; }
    public DateTime? DataAprovacao { get; set; }
    public string? MotivoReprovacao { get; set; }

    public ICollection<AprEtapa> Etapas { get; set; } = new List<AprEtapa>();
    public ICollection<AprResponsavel> Responsaveis { get; set; } = new List<AprResponsavel>();
    public ICollection<AprAssinatura> Assinaturas { get; set; } = new List<AprAssinatura>();
}

// "Etapas" (§17) — elemento estrutural próprio da APR, sem equivalente já cadastrado em Risco/PGR.
public class AprEtapa : AuditableEntity
{
    public Guid AprId { get; set; }
    public Apr? Apr { get; set; }

    public int Ordem { get; set; }
    public string Descricao { get; set; } = string.Empty;

    // "Medidas preventivas" (§17) — texto livre por etapa, complementar aos campos
    // ControlesExistentes/ControlesAdicionais já registrados no Risco vinculado (não duplica dado).
    public string? MedidasPreventivas { get; set; }

    public ICollection<AprEtapaRisco> Riscos { get; set; } = new List<AprEtapaRisco>();
}

// "Perigos"/"riscos"/"controles" (§17) por etapa — reaproveita o cadastro já existente de
// Risco/Perigo (módulo Riscos) em vez de duplicar, mesmo princípio já usado no PlanoAcaoItem do PGR.
public class AprEtapaRisco : AuditableEntity
{
    public Guid AprEtapaId { get; set; }
    public AprEtapa? AprEtapa { get; set; }

    public Guid RiscoId { get; set; }
    public Risco? Risco { get; set; }
}

// "Responsáveis" (§17) — trabalhadores designados/cobertos por esta APR. Relação a Trabalhador
// identificável (não apenas contagem), mesmo padrão de RiscoTrabalhadorExposto — necessário para
// o motor de elegibilidade (§45) saber quem está autorizado a executar sob esta análise de risco.
public class AprResponsavel : AuditableEntity
{
    public Guid AprId { get; set; }
    public Apr? Apr { get; set; }

    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }
}

// "Assinatura" (§17) — o documento não descreve infraestrutura de assinatura digital/certificado
// (inexistente no projeto). Modelada como confirmação simples de ciência por pessoa envolvida
// (não uma assinatura criptográfica/ICP-Brasil) — sinalizar ao usuário se precisar de assinatura
// eletrônica com validade jurídica real. Append-only (sem edição/exclusão), mesmo padrão de PgrRevisao.
public class AprAssinatura : AuditableEntity
{
    public Guid AprId { get; set; }
    public Apr? Apr { get; set; }

    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }

    public PapelAssinaturaApr Papel { get; set; }
    public DateTime DataAssinatura { get; set; }
}
