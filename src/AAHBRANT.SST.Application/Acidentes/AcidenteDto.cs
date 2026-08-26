using AAHBRANT.SST.Application.AcoesPlano;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Acidentes;

public class AcidenteDto
{
    public Guid Id { get; set; }
    public TipoOcorrencia Tipo { get; set; }

    public Guid ObraId { get; set; }
    public string? ObraNome { get; set; }

    public Guid? TrabalhadorId { get; set; }
    public string? TrabalhadorNome { get; set; }

    public Guid? AtividadeId { get; set; }
    public string? AtividadeNome { get; set; }

    public string Local { get; set; } = string.Empty;
    public DateTime Data { get; set; }
    public TimeSpan? Hora { get; set; }

    public string Descricao { get; set; } = string.Empty;
    public string? Lesao { get; set; }
    public string? Consequencia { get; set; }
    public string? Atendimento { get; set; }

    public bool HouveAfastamento { get; set; }
    public int? DiasAfastamento { get; set; }
    public string? NumeroCat { get; set; }

    public GravidadeAcidente Gravidade { get; set; }
    public int DiasDebitados { get; set; }

    public MetodologiaInvestigacao? MetodologiaInvestigacao { get; set; }
    public string? Causas { get; set; }

    public StatusAcidente Status { get; set; }
    public DateTime? DataConclusaoInvestigacao { get; set; }
}

// Composição por query, não por tabela nova — mesmo princípio já usado em NaoConformidadeDetalheDto.
// As ações do plano vinculadas são lidas de AcaoPlano filtrando por OrigemTipo=nameof(Acidente)/OrigemId=Id.
public class AcidenteDetalheDto
{
    public AcidenteDto Acidente { get; set; } = null!;
    public List<AcaoPlanoDto> AcoesPlano { get; set; } = new();
}
