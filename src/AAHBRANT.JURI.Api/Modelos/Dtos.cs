using System.Text.Json.Serialization;

namespace AAHBRANT.JURI.Api.Modelos;

// ---------- Configuração (appsettings.json) ----------
public class ParteMonitoradaConfig
{
    public string Nome { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public List<string> Termos { get; set; } = new();
}

public class DjenOptions
{
    public string BaseUrl { get; set; } = "https://comunicaapi.pje.jus.br";
    public int ItensPorPagina { get; set; } = 100;
    public int MaxPaginas { get; set; } = 50;
}

public class DataJudOptions
{
    public string BaseUrl { get; set; } = "https://api-publica.datajud.cnj.jus.br";
    public string ApiKey { get; set; } = string.Empty;
}

// Chave de acesso da própria API G-JURI (não confundir com DataJud:ApiKey, que é do CNJ).
// Vazia = sem checagem (uso local). Em qualquer deploy público, configurar Seguranca__ApiKey como
// segredo do Container App — os dados retornados (partes, CNPJ, texto de intimação) são sensíveis.
public class SegurancaOptions
{
    public string ApiKey { get; set; } = string.Empty;
}

// ---------- Payload bruto do DJEN (comunicaapi.pje.jus.br/api/v1/comunicacao) ----------
public class DjenResposta
{
    public string? Status { get; set; }
    public int Count { get; set; }
    public List<DjenComunicacao> Items { get; set; } = new();
}

public class DjenComunicacao
{
    public long Id { get; set; }
    [JsonPropertyName("data_disponibilizacao")] public string? DataDisponibilizacao { get; set; }
    public string? SiglaTribunal { get; set; }
    public string? TipoComunicacao { get; set; }
    public string? NomeOrgao { get; set; }
    public string? Texto { get; set; }
    [JsonPropertyName("numero_processo")] public string? NumeroProcesso { get; set; }
    [JsonPropertyName("numeroprocessocommascara")] public string? NumeroProcessoComMascara { get; set; }
    public string? Link { get; set; }
    public string? TipoDocumento { get; set; }
    public string? NomeClasse { get; set; }
    public string? CodigoClasse { get; set; }
    public string? Meio { get; set; }
    public List<DjenDestinatario> Destinatarios { get; set; } = new();
    [JsonPropertyName("destinatarioadvogados")] public List<DjenDestinatarioAdvogado> DestinatarioAdvogados { get; set; } = new();
}

public class DjenDestinatario
{
    public string? Nome { get; set; }
    public string? Polo { get; set; }
}

public class DjenDestinatarioAdvogado
{
    public DjenAdvogado? Advogado { get; set; }
}

public class DjenAdvogado
{
    public string? Nome { get; set; }
    [JsonPropertyName("numero_oab")] public string? NumeroOab { get; set; }
    [JsonPropertyName("uf_oab")] public string? UfOab { get; set; }
}

// ---------- Resposta da API G-JURI ----------
public record ParteDto(string Nome, string Polo);
public record AdvogadoDto(string Nome, string Oab);

public record ComunicacaoDto(
    long Id,
    string? Data,
    string? Tipo,
    string? TipoDocumento,
    string? Orgao,
    string? Link,
    string? Texto);

public class ProcessoResumoDto
{
    public string Numero { get; set; } = string.Empty;
    public string NumeroFormatado { get; set; } = string.Empty;
    public string Tribunal { get; set; } = string.Empty;
    public string Justica { get; set; } = string.Empty;
    public string? Orgao { get; set; }
    public string? Classe { get; set; }
    public List<ParteDto> Partes { get; set; } = new();
    public List<AdvogadoDto> Advogados { get; set; } = new();
    public int TotalComunicacoes { get; set; }
    public string? PrimeiraComunicacao { get; set; }
    public string? UltimaComunicacao { get; set; }
    public ComunicacaoDto? UltimaComunicacaoDetalhe { get; set; }
    public List<ComunicacaoDto>? Comunicacoes { get; set; }
}

public class BuscaProcessosResultado
{
    public string Termo { get; set; } = string.Empty;
    public string Justica { get; set; } = string.Empty;
    public int TotalComunicacoes { get; set; }
    public int TotalProcessos { get; set; }
    public Dictionary<string, int> PorTribunal { get; set; } = new();
    public List<ProcessoResumoDto> Processos { get; set; } = new();
}

public class ParteMonitoradaResultado
{
    public string Nome { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public List<string> Termos { get; set; } = new();
    public int TotalProcessos { get; set; }
    public Dictionary<string, int> PorTribunal { get; set; } = new();
    public List<ProcessoResumoDto> Processos { get; set; } = new();
    public string? Erro { get; set; }
}

public record MovimentoDto(int? Codigo, string? Nome, string? DataHora, List<string> Complementos);

public class ProcessoDataJudDto
{
    public string Tribunal { get; set; } = string.Empty;
    public string? Grau { get; set; }
    public string? Classe { get; set; }
    public List<string> Assuntos { get; set; } = new();
    public string? OrgaoJulgador { get; set; }
    public string? DataAjuizamento { get; set; }
    public string? Sistema { get; set; }
    public string? Formato { get; set; }
    public string? UltimaAtualizacao { get; set; }
    public int TotalMovimentos { get; set; }
    public List<MovimentoDto> Movimentos { get; set; } = new();
}

public class ProcessoDetalheDto
{
    public string Numero { get; set; } = string.Empty;
    public string NumeroFormatado { get; set; } = string.Empty;
    public string? Tribunal { get; set; }
    public string? AliasDataJud { get; set; }
    public string Justica { get; set; } = string.Empty;
    public List<ProcessoDataJudDto> DataJud { get; set; } = new();
    public string? DataJudErro { get; set; }
    public ProcessoResumoDto? Djen { get; set; }
    public string? DjenErro { get; set; }
}
