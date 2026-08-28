using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Pcmso;

public class PcmsoDto
{
    public Guid Id { get; set; }
    public Guid ObraId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Objetivo { get; set; }
    public string MedicoCoordenadorNome { get; set; } = string.Empty;
    public string? MedicoCoordenadorCrm { get; set; }
    public Guid? MedicoCoordenadorUsuarioId { get; set; }
    public DateTime DataElaboracao { get; set; }
    public DateTime? DataVigenciaInicio { get; set; }
    public DateTime? DataVigenciaFim { get; set; }
    public StatusPcmso Status { get; set; }
}

public class PcmsoItemMatrizDto
{
    public Guid Id { get; set; }
    public Guid PcmsoId { get; set; }
    public Guid FuncaoId { get; set; }
    public string FuncaoNome { get; set; } = string.Empty;
    public Guid? RiscoId { get; set; }
    public string NomeExame { get; set; } = string.Empty;
    public int PeriodicidadeEmMeses { get; set; }
    public bool ObrigatorioNoAdmissional { get; set; }
    public bool ObrigatorioNoDemissional { get; set; }
    public string? Observacoes { get; set; }
}

public class PcmsoRevisaoDto
{
    public Guid Id { get; set; }
    public Guid PcmsoId { get; set; }
    public int NumeroRevisao { get; set; }
    public DateTime DataRevisao { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public Guid? ResponsavelUsuarioId { get; set; }
}

// Calendário de exames (computado, não armazenado — ver disclosure em Domain/Entidades/Pcmso/
// Pcmso.cs): para cada trabalhador ativo da obra cuja função tem item na matriz, quando é o
// próximo exame previsto, com base no último ASO do trabalhador (qualquer tipo) + a periodicidade
// definida na matriz para aquela função.
public class ItemCalendarioExameDto
{
    public Guid TrabalhadorId { get; set; }
    public string TrabalhadorNome { get; set; } = string.Empty;
    public Guid FuncaoId { get; set; }
    public string FuncaoNome { get; set; } = string.Empty;
    public string NomeExame { get; set; } = string.Empty;
    public DateTime? UltimoExameData { get; set; }
    public DateTime ProximaDataPrevista { get; set; }
    public bool Vencido { get; set; }
}

// Relatório Analítico de Saúde (NR-7) — visão agregada por função, sem identificar trabalhador
// individualmente (anonimizado por desenho, não só por omissão de campo).
public class LinhaRelatorioAnaliticoDto
{
    public Guid FuncaoId { get; set; }
    public string FuncaoNome { get; set; } = string.Empty;
    public int TotalAsos { get; set; }
    public int Aptos { get; set; }
    public int AptosComRestricao { get; set; }
    public int Inaptos { get; set; }
    public int Pendentes { get; set; }
}

public class PcmsoDetalheDto
{
    public PcmsoDto Pcmso { get; set; } = null!;
    public List<PcmsoItemMatrizDto> ItensMatriz { get; set; } = new();
    public List<PcmsoRevisaoDto> Revisoes { get; set; } = new();
    public List<ItemCalendarioExameDto> Calendario { get; set; } = new();
    public List<LinhaRelatorioAnaliticoDto> RelatorioAnalitico { get; set; } = new();
}
