using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Programa de Controle Médico de Saúde Ocupacional (NR-7) — pedido explícito do usuário em
// 2026-08-24, depois de conversa com o Técnico de SST. Mesmo padrão arquitetural do PGR (§16):
// um documento-programa com vigência e revisões, mais a peça que só o PCMSO tem — a matriz de
// exames obrigatórios por função (o coração da NR-7: os exames do trabalhador são definidos a
// partir do risco ocupacional da função dele, não um calendário genérico).
//
// Decisões não-literais assumidas (a validar com o Médico do Trabalho/Técnico de SST antes de
// produção — mesmo espírito das pendências já registradas em docs/RBAC-Matrix.md §5):
// - "Exame" na matriz (PcmsoItemMatriz.NomeExame) é texto livre (ex.: "Audiometria Tonal",
//   "Hemograma", "Espirometria") — não existe hoje um catálogo fechado de exames complementares
//   no projeto (mesma lacuna já registrada em DocumentoGestao.Tipo/Categoria). O ASO (Aso) já
//   existente não distingue QUAL exame complementar foi feito, só a ocasião (TipoExameAso:
//   admissional/periódico/etc.) — então o "calendário de exames" (ver
//   ObterPcmsoDetalheQuery/CalendarioItemDto) só consegue calcular a próxima data com base no
//   último ASO do trabalhador (independente de qual exame específico foi feito), não por exame
//   individual da matriz. Ampliar isso exigiria uma extensão de schema do módulo ASO — fora desta
//   fatia.
// - Não há vocabulário literal de status para o "programa" em si — reaproveita o mesmo desenho de
//   StatusPgr (EmElaboracao/Vigente/EmRevisao/Encerrado).
public class Pcmso : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public string Nome { get; set; } = string.Empty;
    public string? Objetivo { get; set; }

    // Coordenação médica do PCMSO é exigência da NR-7 — texto livre além do vínculo opcional a um
    // Usuario com perfil MedicoDoTrabalho, porque nem todo médico coordenador está necessariamente
    // cadastrado como usuário do sistema (mesmo padrão de "responsável" em texto livre já usado em
    // DdsItemChecklist/outros módulos quando o responsável pode ser externo).
    public string MedicoCoordenadorNome { get; set; } = string.Empty;
    public string? MedicoCoordenadorCrm { get; set; }
    public Guid? MedicoCoordenadorUsuarioId { get; set; }
    public Usuario? MedicoCoordenadorUsuario { get; set; }

    public DateTime DataElaboracao { get; set; }
    public DateTime? DataVigenciaInicio { get; set; }
    public DateTime? DataVigenciaFim { get; set; }

    public StatusPcmso Status { get; set; } = StatusPcmso.EmElaboracao;

    public ICollection<PcmsoItemMatriz> ItensMatriz { get; set; } = new List<PcmsoItemMatriz>();
    public ICollection<PcmsoRevisao> Revisoes { get; set; } = new List<PcmsoRevisao>();
}

// Matriz de exames obrigatórios por função (NR-7, item 7.4.2): o coração do PCMSO. Vincula
// opcionalmente ao Risco que justifica a exigência (rastreabilidade — "por que este exame é
// obrigatório para esta função"), mas o campo que realmente aciona a exigência é FuncaoId.
public class PcmsoItemMatriz : AuditableEntity
{
    public Guid PcmsoId { get; set; }
    public Pcmso? Pcmso { get; set; }

    public Guid FuncaoId { get; set; }
    public Funcao? Funcao { get; set; }

    public Guid? RiscoId { get; set; }
    public Risco? Risco { get; set; }

    public string NomeExame { get; set; } = string.Empty;
    public int PeriodicidadeEmMeses { get; set; }
    public bool ObrigatorioNoAdmissional { get; set; } = true;
    public bool ObrigatorioNoDemissional { get; set; } = true;
    public string? Observacoes { get; set; }
}

// Revisão do programa — mesmo desenho append-only de PgrRevisao (§16): histórico formal de
// revisões do documento PCMSO, distinto da TrilhaAuditoria genérica.
public class PcmsoRevisao : AuditableEntity
{
    public Guid PcmsoId { get; set; }
    public Pcmso? Pcmso { get; set; }

    public int NumeroRevisao { get; set; }
    public DateTime DataRevisao { get; set; }
    public string Motivo { get; set; } = string.Empty;

    public Guid? ResponsavelUsuarioId { get; set; }
    public Usuario? ResponsavelUsuario { get; set; }
}
