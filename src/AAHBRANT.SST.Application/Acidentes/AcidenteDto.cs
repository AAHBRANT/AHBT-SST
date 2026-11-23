using AAHBRANT.SST.Application.AcoesPlano;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Acidentes;

public record AcidenteEnvolvidoDto(Guid TrabalhadorId, string Nome, PapelEnvolvido Papel = PapelEnvolvido.Envolvido);

// CPF e telefone saem SEMPRE mascarados (LGPD); o valor em claro só por ObterDadosPessoaisPessoaExternaQuery,
// que exige permissão própria e grava auditoria.
public record AcidentePessoaExternaDto(
    Guid Id,
    string Nome,
    VinculoPessoaExterna Vinculo,
    string? Empresa,
    string? CpfMascarado,
    string? TelefoneMascarado,
    int? Idade,
    string? Lesao,
    string? Atendimento,
    ResultadoPessoaExterna Resultado);

// Item do checklist de providências em caso de óbito (ver ProvidenciasObitoAcidente).
public record ProvidenciaObitoDto(string Codigo, string Descricao, bool Concluida, DateTime? ConcluidaEm, string? Observacao);

public class AcidenteDto
{
    public Guid Id { get; set; }
    public TipoOcorrencia Tipo { get; set; }
    // Calculado no servidor (AvancarStatusAcidenteCommandHandler.ExigeTresFotos); a tela só segue o valor.
    public bool ExigeTresFotos { get; set; }

    public Guid ObraId { get; set; }
    public string? ObraNome { get; set; }

    public Guid? TrabalhadorId { get; set; }
    public string? TrabalhadorNome { get; set; }
    // Só funcionários com papel Envolvido (os que podem ir ao G-RH); testemunhas ficam em Testemunhas.
    public List<AcidenteEnvolvidoDto> Envolvidos { get; set; } = new();
    public List<AcidenteEnvolvidoDto> Testemunhas { get; set; } = new();
    public List<AcidentePessoaExternaDto> PessoasExternas { get; set; } = new();

    public Guid? AtividadeId { get; set; }
    public string? AtividadeNome { get; set; }

    public string Local { get; set; } = string.Empty;
    public DateTime Data { get; set; }
    public TimeSpan? Hora { get; set; }

    public string Descricao { get; set; } = string.Empty;
    public string? Lesao { get; set; }
    // Preenchido só no detalhe (ObterAcidenteDetalheQuery).
    public List<string> RegioesCorpo { get; set; } = new();
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
    public string? ConclusaoInvestigacao { get; set; }
    public string? LaudoObservacao { get; set; }
    public DateTime? AguardandoLaudoDesde { get; set; }
    // Preenchido só no detalhe.
    public List<ProvidenciaObitoDto> ProvidenciasObito { get; set; } = new();
}

// Composição por query, não por tabela nova — mesmo princípio já usado em NaoConformidadeDetalheDto.
// As ações do plano vinculadas são lidas de AcaoPlano filtrando por OrigemTipo=nameof(Acidente)/OrigemId=Id.
public class AcidenteFotoDto
{
    public Guid Id { get; set; }
    public int Ordem { get; set; }
    public AAHBRANT.SST.Application.Common.DadosCapturaFoto? DadosFoto { get; set; }
}

public class AcidenteDetalheDto
{
    public List<AcidenteFotoDto> Fotos { get; set; } = new();
    public AcidenteDto Acidente { get; set; } = null!;
    public List<AcaoPlanoDto> AcoesPlano { get; set; } = new();
    public ReuniaoAnaliseDto? Reuniao { get; set; }
    // Quem já assinou a ocorrência (23/11/2026: técnico e engenheiro de segurança), para a faixa de assinatura.
    public bool AssinaturaTecnicoFeita { get; set; }
    public bool AssinaturaEngenheiroFeita { get; set; }
}

// Reunião de análise no Teams (só acidentes registrados por relato).
public record ReuniaoAnaliseDto(
    DateTime Inicio,
    DateTime Fim,
    IReadOnlyList<string> Participantes,
    SituacaoReuniaoTeams Situacao,
    string? LinkTeams,
    string? MotivoFalha,
    DateTime? RealizadaEm = null,
    IReadOnlyList<string>? Presentes = null,
    string? Ata = null);
