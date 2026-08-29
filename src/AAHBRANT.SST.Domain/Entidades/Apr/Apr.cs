using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Análise Preliminar de Risco — reformulada em 2026-08-29 para reproduzir literalmente o formulário
// "APR – ANÁLISE PRELIMINAR DE RISCO | REV.02" (planilha fornecida pelo usuário), substituindo o
// desenho anterior (que reaproveitava a entidade genérica Risco do módulo Riscos, sem risco
// residual e sem os campos de texto do formulário). Sem dado real cadastrado no seeder para migrar.
public class Apr : AuditableEntity
{
    // Campo "Nº APR:" do cabeçalho — texto livre preenchido manualmente, sem numeração automática
    // (o documento não define uma regra de numeração).
    public string? NumeroApr { get; set; }

    public Guid AtividadeId { get; set; }
    public Atividade? Atividade { get; set; }

    public string Local { get; set; } = string.Empty;

    // "MÁQUINAS / EQUIP.:" e "PGR / PROCEDIMENTO REF.:" do cabeçalho — texto livre; PgrReferencia
    // não vira FK para o Pgr do módulo Riscos porque o campo do documento é uma citação/referência
    // (ex.: número do documento), não necessariamente o mesmo Pgr cadastrado no sistema.
    public string? MaquinasEquipamentos { get; set; }
    public string? PgrReferencia { get; set; }

    public Guid? EquipeId { get; set; }
    public Equipe? Equipe { get; set; }

    public DateTime Data { get; set; }
    public DateTime? Validade { get; set; }

    // "Aprovação" — o documento não descreve um fluxo de status literal para a APR (mesma lacuna já
    // registrada em StatusPgr/StatusControleRisco) — proposta própria, avisar o usuário se quiser
    // outro vocabulário.
    public StatusApr Status { get; set; } = StatusApr.EmElaboracao;

    public Guid? AprovadoPorUsuarioId { get; set; }
    public Usuario? AprovadoPorUsuario { get; set; }
    public DateTime? DataAprovacao { get; set; }
    public string? MotivoReprovacao { get; set; }

    public ICollection<AprEtapa> Etapas { get; set; } = new List<AprEtapa>();
    // "ENVOLVIDOS NA ATIVIDADE / EQUIPE EXPOSTA" (Nome/Função) do documento — o "Ass./Visto" de cada
    // um é uma AprAssinatura com Papel=Envolvido para o mesmo TrabalhadorId.
    public ICollection<AprResponsavel> Responsaveis { get; set; } = new List<AprResponsavel>();
    public ICollection<AprAssinatura> Assinaturas { get; set; } = new List<AprAssinatura>();
}

// "ETAPA DA ATIVIDADE" — no documento, a mesma etapa se repete em várias linhas da tabela (uma por
// perigo/evento perigoso identificado nela); aqui isso é literalmente Etapa (Ordem/Descrição) → N
// AprEtapaRisco. MedidasPreventivas (texto por etapa) foi removido: no documento a coluna
// "MEDIDAS DE PREVENÇÃO/CONTROLE" é por linha de risco, não por etapa — ver AprEtapaRisco.
public class AprEtapa : AuditableEntity
{
    public Guid AprId { get; set; }
    public Apr? Apr { get; set; }

    public int Ordem { get; set; }
    public string Descricao { get; set; } = string.Empty;

    public ICollection<AprEtapaRisco> Riscos { get; set; } = new List<AprEtapaRisco>();
}

// Uma linha da tabela principal do formulário — todos os campos são literais das colunas da
// planilha. Diferente do desenho anterior (FK para o Risco genérico do módulo Riscos), aqui os
// dados são inteiramente próprios da APR: "Trabalhadores Expostos" e "Responsável" são texto livre
// (o documento os preenche como descrição de papel/função — ex. "Encarregado / Operador" — não como
// vínculo a um Trabalhador/Usuario específico do cadastro).
public class AprEtapaRisco : AuditableEntity
{
    public Guid AprEtapaId { get; set; }
    public AprEtapa? AprEtapa { get; set; }

    public string PerigoEventoPerigoso { get; set; } = string.Empty;
    public string? FonteCircunstancia { get; set; }
    public string? PossiveisLesoes { get; set; }
    public string? TrabalhadoresExpostos { get; set; }

    // "P" / "S" / "RISCO INICIAL" — RiscoInicial calculado por AprNivelRiscoCalculator no momento do
    // salvamento (mesmo princípio já usado em Risco.NivelRisco), não uma coluna computada no banco.
    public int ProbabilidadeInicial { get; set; }
    public int SeveridadeInicial { get; set; }
    public NivelRiscoApr NivelRiscoInicial { get; set; }

    public string? MedidasPrevencao { get; set; }
    public string? Responsavel { get; set; }

    // "P RES." / "S RES." / "RISCO RESIDUAL" — mesmo cálculo, aplicado depois das medidas de
    // prevenção/controle.
    public int ProbabilidadeResidual { get; set; }
    public int SeveridadeResidual { get; set; }
    public NivelRiscoApr NivelRiscoResidual { get; set; }
}

// "Responsáveis"/"Envolvidos" — trabalhadores designados/cobertos por esta APR. Relação a
// Trabalhador identificável (não apenas contagem), mesmo padrão de RiscoTrabalhadorExposto —
// necessário para o motor de elegibilidade (§45) saber quem está autorizado a executar sob esta
// análise de risco.
public class AprResponsavel : AuditableEntity
{
    public Guid AprId { get; set; }
    public Apr? Apr { get; set; }

    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }
}

// "Ass./Visto" (linha de envolvido) e os dois blocos formais "Elaboração"/"Supervisão" do rodapé —
// o documento não descreve infraestrutura de assinatura digital/certificado (inexistente no
// projeto). Modelada como confirmação simples de ciência por pessoa (não uma assinatura
// criptográfica/ICP-Brasil) — sinalizar ao usuário se precisar de assinatura eletrônica com
// validade jurídica real (o Motor de Assinatura Eletrônica do sistema, hoje usado por Dds, poderia
// ser estendido para cá numa fase futura). Append-only (sem edição/exclusão), mesmo padrão de
// PgrRevisao.
public class AprAssinatura : AuditableEntity
{
    public Guid AprId { get; set; }
    public Apr? Apr { get; set; }

    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }

    public PapelAssinaturaApr Papel { get; set; }
    public DateTime DataAssinatura { get; set; }
}
