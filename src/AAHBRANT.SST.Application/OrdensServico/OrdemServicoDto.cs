using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.OrdensServico;

// Minuta da Ordem de Serviço de SST (NR-01 1.4.1 "c") de um trabalhador na obra dele (uma OS por
// trabalhador por obra — decisão do usuário, 20/10/2026). Só entra conteúdo validado; o que falta
// vira pendência e bloqueia a emissão.
public class MinutaOrdemServicoDto
{
    public Guid TrabalhadorId { get; set; }
    public bool PodeEmitir => Pendencias.Count == 0;

    public ObraOsDto Obra { get; set; } = new();
    public TrabalhadorOsDto Trabalhador { get; set; } = new();
    public PgrOsDto? Pgr { get; set; }
    // Descrição das atividades da função, por GHE, como está no PGR.
    public List<GheOsDto> Ghes { get; set; } = new();
    public List<AtividadeOsDto> Atividades { get; set; } = new();
    public List<EpiOsDto> Epis { get; set; } = new();
    public List<TreinamentoOsDto> Treinamentos { get; set; } = new();
    public AsoOsDto? Aso { get; set; }
    public List<PoliticaOsDto> Politicas { get; set; } = new();
    public List<PendenciaOsDto> Pendencias { get; set; } = new();
}

public class ObraOsDto
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Cliente { get; set; }
    public string? Cnpj { get; set; }
    public string? Endereco { get; set; }
}

public class TrabalhadorOsDto
{
    public string Nome { get; set; } = string.Empty;
    public string? Matricula { get; set; }
    public string CpfMascarado { get; set; } = string.Empty;
    public string Funcao { get; set; } = string.Empty;
    public string? Cbo { get; set; }
    public DateTime DataAdmissao { get; set; }
}

public record PgrOsDto(Guid Id, string Nome, DateTime DataElaboracao, int? Revisao);

public record GheOsDto(int Numero, string? DescricaoAtividades);

public class AtividadeOsDto
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Condicoes { get; set; }
    public List<string> Ferramentas { get; set; } = new();
    public List<string> Epcs { get; set; } = new();
    public List<string> Procedimentos { get; set; } = new();
    public List<RiscoOsDto> Riscos { get; set; } = new();
    public List<string> InterromperQuando { get; set; } = new();
    public List<RestricaoOsDto> Restricoes { get; set; } = new();
    public List<string> Autorizacoes { get; set; } = new();
}

public class RiscoOsDto
{
    public Guid Id { get; set; }
    public string Perigo { get; set; } = string.Empty;
    public NivelRisco Nivel { get; set; }
    public string? Consequencia { get; set; }
    public List<string> Antes { get; set; } = new();
    public List<string> Durante { get; set; } = new();
    public List<string> Apos { get; set; } = new();
}

public record RestricaoOsDto(string Texto, string? Requisito);

public record EpiOsDto(string Nome, string? Ca);

// Situacao: "Valido", "Vencido" ou "NaoRealizado".
public record TreinamentoOsDto(string Curso, string? Norma, DateTime? Validade, string Situacao);

public record AsoOsDto(ResultadoAso Resultado, DateTime? Validade, bool Valido);

public record PoliticaOsDto(string Titulo, string? Codigo, string? Revisao, string? FormaConsulta);

// QuemResolve: "SST" ou "RH". Destino/DestinoId: onde corrigir (ex.: "pgr" + pgrId, "treinamentos").
public record PendenciaOsDto(string Tipo, string Titulo, string PorQueBloqueia, string QuemResolve, string? Destino, Guid? DestinoId);
