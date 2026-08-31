using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Módulo CIPA (Comissão Interna de Prevenção de Acidentes/Assédio, NR-5) — requisito do usuário
// (31/08), dentro do pilar Operação. Cobre dimensionamento, processo eleitoral, mandato/membros,
// treinamento dos membros, reuniões (ata + presença + plano de ações), inspeções de risco e SIPAT.
//
// Disclosure: modelagem é proposta própria (não há seção da Base de Conhecimento sobre CIPA) —
// segue a estrutura que o usuário passou por escrito. O Plano de Ações 5W2H das reuniões REAPROVEITA
// a entidade genérica AcoesPlano.AcaoPlano (OrigemTipo="ReuniaoCipa", OrigemId=ReuniaoCipa.Id) em vez
// de criar uma tabela própria — mesmo mecanismo já usado por NaoConformidade/Acidente/Pcmso. A
// integração automática "Inspeção CIPA → inventário de riscos do PGR/GRO" descrita no pedido do
// usuário NÃO foi implementada nesta fase (geraria alterações estruturais no módulo de Riscos fora
// do escopo desta fatia); em vez disso, InspecaoCipa pode gerar manualmente uma Não Conformidade
// vinculada (mesmo command genérico CriarNaoConformidadeCommand, Origem=Inspecao) para o
// acompanhamento entrar no fluxo de NC já existente. Editais/atas são gerados sob demanda em PDF
// (mesmo padrão do resto do sistema — Dds/Apr/Pt/Inspeções), não armazenados como upload.
public class DimensionamentoCipa : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    // NR-5 Quadro I: dimensionamento (nº de titulares/suplentes) depende do cruzamento
    // CNAE/Grau de Risco (NR-4) x faixa de nº de empregados. Este sistema NÃO calcula esse
    // cruzamento automaticamente (tabela extensa por CNAE, valor de conformidade legal — risco de
    // gerar um número desatualizado ou incorreto sem validação humana). NumeroTitulares/
    // NumeroSuplentes são sempre preenchidos manualmente por quem faz o dimensionamento (técnico/
    // engenheiro de segurança), com Cnae/GrauRisco/NumeroFuncionarios guardados como referência do
    // enquadramento usado. Ver disclosure na tela (CipaDimensionamentoTab.tsx).
    public string Cnae { get; set; } = string.Empty;
    public int GrauRisco { get; set; }
    public int NumeroFuncionarios { get; set; }
    public int NumeroTitulares { get; set; }
    public int NumeroSuplentes { get; set; }
    public DateTime DataCalculo { get; set; }
    public string? Observacoes { get; set; }
}

// Ciclo eleitoral completo de uma CIPA (convocação → inscrições → votação → apuração). A apuração
// é registrada manualmente (contagem de votos informada por quem conduziu a eleição) — este sistema
// não implementa urna digital/votação online (peso de segurança/sigilo do voto fora do escopo desta
// fatia); o registro serve como fonte da ata e do histórico, não como mecanismo de votação em si.
public class ProcessoEleitoralCipa : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public string? NumeroDocumento { get; set; }
    public DateTime DataConvocacao { get; set; }
    public DateTime DataInicioInscricoes { get; set; }
    public DateTime DataFimInscricoes { get; set; }
    public DateTime DataVotacao { get; set; }
    public DateTime? DataApuracao { get; set; }
    public StatusProcessoEleitoralCipa Status { get; set; } = StatusProcessoEleitoralCipa.Convocado;

    public ICollection<CandidatoCipa> Candidatos { get; set; } = new List<CandidatoCipa>();
}

public class CandidatoCipa : AuditableEntity
{
    public Guid ProcessoEleitoralId { get; set; }
    public ProcessoEleitoralCipa? ProcessoEleitoral { get; set; }

    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }

    public DateTime DataInscricao { get; set; }
    public StatusCandidatoCipa Status { get; set; } = StatusCandidatoCipa.Inscrito;
    public string? MotivoIndeferimento { get; set; }
    public int VotosRecebidos { get; set; }
}

// Composição do mandato — titulares/suplentes eleitos (via CandidatoCipaId/ProcessoEleitoralId) e
// representantes indicados diretamente pelo empregador (Origem=Empregador, sem candidatura/eleição,
// ProcessoEleitoralId nulo). Encerramento antecipado do mandato usa o soft-delete padrão (Ativo=false)
// em vez de um campo próprio — mesma convenção do resto do sistema.
public class MembroCipa : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }

    // Nome "OrigemMembro" (não "Origem") para não colidir com AuditableEntity.Origem (rastreamento
    // de como o registro entrou no sistema — Manual/Importação/OCR — conceito diferente).
    public OrigemMembroCipa OrigemMembro { get; set; }
    public CargoMembroCipa Cargo { get; set; }
    public DateTime DataInicioMandato { get; set; }
    public DateTime DataFimMandato { get; set; }

    public Guid? ProcessoEleitoralId { get; set; }
    public ProcessoEleitoralCipa? ProcessoEleitoral { get; set; }
    public Guid? CandidatoCipaId { get; set; }
    public CandidatoCipa? CandidatoCipa { get; set; }

    public ICollection<TreinamentoCipa> Treinamentos { get; set; } = new List<TreinamentoCipa>();
}

public class TreinamentoCipa : AuditableEntity
{
    public Guid MembroCipaId { get; set; }
    public MembroCipa? MembroCipa { get; set; }

    public int CargaHoraria { get; set; }
    public string? ConteudoProgramatico { get; set; }
    public DateTime DataRealizacao { get; set; }
    public DateTime? DataValidade { get; set; }
    public string? InstituicaoInstrutor { get; set; }

    public byte[]? CertificadoConteudo { get; set; }
    public string? CertificadoContentType { get; set; }
    public byte[]? ListaPresencaConteudo { get; set; }
    public string? ListaPresencaContentType { get; set; }
}

public class ReuniaoCipa : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public TipoReuniaoCipa Tipo { get; set; }
    public DateTime DataReuniao { get; set; }
    public string? Pauta { get; set; }
    public string? Deliberacoes { get; set; }
    public StatusReuniaoCipa Status { get; set; } = StatusReuniaoCipa.Agendada;

    public ICollection<ParticipanteReuniaoCipa> Participantes { get; set; } = new List<ParticipanteReuniaoCipa>();
}

public class ParticipanteReuniaoCipa : AuditableEntity
{
    public Guid ReuniaoCipaId { get; set; }
    public ReuniaoCipa? ReuniaoCipa { get; set; }

    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }

    public bool Presente { get; set; } = true;
}

// Achado de risco de uma inspeção da CIPA — ver disclosure no topo do arquivo sobre a integração
// com PGR/GRO (não automática nesta fase). NaoConformidadeId é preenchido quando o achado vira uma
// Não Conformidade formal (ação manual, comando genérico já existente).
public class InspecaoCipa : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public Guid? MembroCipaId { get; set; }
    public MembroCipa? MembroCipa { get; set; }

    public DateTime Data { get; set; }
    public string Local { get; set; } = string.Empty;
    public string RiscoIdentificado { get; set; } = string.Empty;
    public NivelRisco? GrauRisco { get; set; }

    public Guid? NaoConformidadeId { get; set; }
    public NaoConformidade? NaoConformidade { get; set; }
}

public class EventoSipat : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public int AnoReferencia { get; set; }
    public DateTime DataInicio { get; set; }
    public DateTime DataFim { get; set; }
    public string? Tema { get; set; }
    public string? Programacao { get; set; }

    public ICollection<AtividadeSipat> Atividades { get; set; } = new List<AtividadeSipat>();
}

public class AtividadeSipat : AuditableEntity
{
    public Guid EventoSipatId { get; set; }
    public EventoSipat? EventoSipat { get; set; }

    public DateTime Data { get; set; }
    public string? Horario { get; set; }
    public string TemaPalestra { get; set; } = string.Empty;
    public string? Palestrante { get; set; }
}
