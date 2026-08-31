using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Cipa;

public class DimensionamentoCipaDto
{
    public Guid Id { get; set; }
    public Guid ObraId { get; set; }
    public string ObraNome { get; set; } = string.Empty;
    public string Cnae { get; set; } = string.Empty;
    public int GrauRisco { get; set; }
    public int NumeroFuncionarios { get; set; }
    public int NumeroTitulares { get; set; }
    public int NumeroSuplentes { get; set; }
    public DateTime DataCalculo { get; set; }
    public string? Observacoes { get; set; }
}

public class CandidatoCipaDto
{
    public Guid Id { get; set; }
    public Guid ProcessoEleitoralId { get; set; }
    public Guid TrabalhadorId { get; set; }
    public string TrabalhadorNome { get; set; } = string.Empty;
    public string TrabalhadorMatricula { get; set; } = string.Empty;
    public DateTime DataInscricao { get; set; }
    public StatusCandidatoCipa Status { get; set; }
    public string? MotivoIndeferimento { get; set; }
    public int VotosRecebidos { get; set; }
}

public class ProcessoEleitoralCipaDto
{
    public Guid Id { get; set; }
    public Guid ObraId { get; set; }
    public string ObraNome { get; set; } = string.Empty;
    public string? NumeroDocumento { get; set; }
    public DateTime DataConvocacao { get; set; }
    public DateTime DataInicioInscricoes { get; set; }
    public DateTime DataFimInscricoes { get; set; }
    public DateTime DataVotacao { get; set; }
    public DateTime? DataApuracao { get; set; }
    public StatusProcessoEleitoralCipa Status { get; set; }
    public int TotalCandidatos { get; set; }
}

public class ProcessoEleitoralCipaDetalheDto
{
    public ProcessoEleitoralCipaDto Processo { get; set; } = null!;
    public List<CandidatoCipaDto> Candidatos { get; set; } = new();
}

public class TreinamentoCipaDto
{
    public Guid Id { get; set; }
    public Guid MembroCipaId { get; set; }
    public int CargaHoraria { get; set; }
    public string? ConteudoProgramatico { get; set; }
    public DateTime DataRealizacao { get; set; }
    public DateTime? DataValidade { get; set; }
    public string? InstituicaoInstrutor { get; set; }
    public bool TemCertificado { get; set; }
    public bool TemListaPresenca { get; set; }
}

public class MembroCipaDto
{
    public Guid Id { get; set; }
    public Guid ObraId { get; set; }
    public string ObraNome { get; set; } = string.Empty;
    public Guid TrabalhadorId { get; set; }
    public string TrabalhadorNome { get; set; } = string.Empty;
    public string TrabalhadorMatricula { get; set; } = string.Empty;
    public OrigemMembroCipa OrigemMembro { get; set; }
    public CargoMembroCipa Cargo { get; set; }
    public DateTime DataInicioMandato { get; set; }
    public DateTime DataFimMandato { get; set; }
    public bool MandatoAtivo { get; set; }
    public int TotalTreinamentos { get; set; }
}

public class MembroCipaDetalheDto
{
    public MembroCipaDto Membro { get; set; } = null!;
    public List<TreinamentoCipaDto> Treinamentos { get; set; } = new();
}

public class ParticipanteReuniaoCipaDto
{
    public Guid Id { get; set; }
    public Guid TrabalhadorId { get; set; }
    public string TrabalhadorNome { get; set; } = string.Empty;
    public bool Presente { get; set; }
}

public class ReuniaoCipaDto
{
    public Guid Id { get; set; }
    public Guid ObraId { get; set; }
    public string ObraNome { get; set; } = string.Empty;
    public TipoReuniaoCipa Tipo { get; set; }
    public DateTime DataReuniao { get; set; }
    public string? Pauta { get; set; }
    public string? Deliberacoes { get; set; }
    public StatusReuniaoCipa Status { get; set; }
    public int TotalParticipantes { get; set; }
    public int TotalPresentes { get; set; }
}

public class ReuniaoCipaDetalheDto
{
    public ReuniaoCipaDto Reuniao { get; set; } = null!;
    public List<ParticipanteReuniaoCipaDto> Participantes { get; set; } = new();
}

public class InspecaoCipaDto
{
    public Guid Id { get; set; }
    public Guid ObraId { get; set; }
    public string ObraNome { get; set; } = string.Empty;
    public Guid? MembroCipaId { get; set; }
    public string? MembroCipaNome { get; set; }
    public DateTime Data { get; set; }
    public string Local { get; set; } = string.Empty;
    public string RiscoIdentificado { get; set; } = string.Empty;
    public NivelRisco? GrauRisco { get; set; }
    public Guid? NaoConformidadeId { get; set; }
}

public class AtividadeSipatDto
{
    public Guid Id { get; set; }
    public DateTime Data { get; set; }
    public string? Horario { get; set; }
    public string TemaPalestra { get; set; } = string.Empty;
    public string? Palestrante { get; set; }
}

public class EventoSipatDto
{
    public Guid Id { get; set; }
    public Guid ObraId { get; set; }
    public string ObraNome { get; set; } = string.Empty;
    public int AnoReferencia { get; set; }
    public DateTime DataInicio { get; set; }
    public DateTime DataFim { get; set; }
    public string? Tema { get; set; }
    public string? Programacao { get; set; }
    public int TotalAtividades { get; set; }
}

public class EventoSipatDetalheDto
{
    public EventoSipatDto Evento { get; set; } = null!;
    public List<AtividadeSipatDto> Atividades { get; set; } = new();
}
