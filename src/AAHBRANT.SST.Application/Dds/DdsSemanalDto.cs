using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Dds;

public class DdsSemanalDto
{
    public Guid Id { get; set; }
    public Guid ObraId { get; set; }
    public string ObraNome { get; set; } = string.Empty;
    public TipoDdsSemanal Tipo { get; set; }
    public string? EmpresaTerceirizada { get; set; }
    public string? NumeroDocumento { get; set; }
    public string? LocalFrenteServico { get; set; }
    public Guid ResponsavelUsuarioId { get; set; }
    public string ResponsavelUsuarioNome { get; set; } = string.Empty;
    public DateTime DataInicioSemana { get; set; }
    public DateTime DataFimSemana { get; set; }
    public StatusDdsSemanal Status { get; set; }
    public string? ResponsavelObraSstNome { get; set; }
    public string? ResponsavelEmpresaTerceirizadaNome { get; set; }
    public string? ResponsavelEmpresaTerceirizadaFuncao { get; set; }
    public DateTime? EncerradaEm { get; set; }
    public int TotalDiasRegistrados { get; set; }
    public int TotalDiasConcluidos { get; set; }
}

// Um slot por dia útil (sempre 5, segunda a sexta) — DdsId nulo significa que o dia ainda não tem
// registro criado (tela mostra "Criar registro do dia" nesse caso).
public class DdsSemanalDiaDto
{
    public DayOfWeek DiaSemana { get; set; }
    public DateTime Data { get; set; }
    public Guid? DdsId { get; set; }
    public string? TopicoPrincipal { get; set; }
    public StatusDds? Status { get; set; }
    public int TotalFotosEvidencia { get; set; }
    public int TotalParticipantes { get; set; }
}

public class DdsSemanalDetalheDto
{
    public DdsSemanalDto Semanal { get; set; } = null!;
    public List<DdsSemanalDiaDto> Dias { get; set; } = new();
}
