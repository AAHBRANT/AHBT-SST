using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Infrastructure.Integracao.Grh;

// Contrato de fio único do G-RH (acordado em 2026-09-09): mesmo shape tanto na carga inicial
// (GET /api/integracoes/sst/colaboradores, consumido por ColaboradorGrhClient) quanto no evento
// contínuo publicado na fila "colaborador-grh" (consumido por ServiceBusColaboradorGrhProcessor) —
// o G-RH reaproveita a mesma serialização nos dois casos, sem precisar manter dois formatos.
internal class ColaboradorGrhPayload
{
    [JsonPropertyName("cpf")] public string Cpf { get; set; } = string.Empty;
    [JsonPropertyName("nome")] public string Nome { get; set; } = string.Empty;
    [JsonPropertyName("pis")] public string? Pis { get; set; }
    [JsonPropertyName("ctps")] public string? Ctps { get; set; }
    [JsonPropertyName("nascimento")] [JsonConverter(typeof(DataGrhLenienteConverter))] public DateTime? Nascimento { get; set; }
    [JsonPropertyName("nomeMae")] public string? NomeMae { get; set; }
    [JsonPropertyName("endereco")] public string? Endereco { get; set; }
    [JsonPropertyName("municipio")] public string? Municipio { get; set; }
    [JsonPropertyName("uf")] public string? Uf { get; set; }
    [JsonPropertyName("cep")] public string? Cep { get; set; }
    [JsonPropertyName("matricula")] public string? Matricula { get; set; }
    [JsonPropertyName("admissao")] public DateTime Admissao { get; set; }
    [JsonPropertyName("desligamento")] [JsonConverter(typeof(DataGrhLenienteConverter))] public DateTime? Desligamento { get; set; }
    [JsonPropertyName("situacao")] public string? Situacao { get; set; }
    [JsonPropertyName("salario")] public decimal? Salario { get; set; }
    [JsonPropertyName("experiencia1Fim")] [JsonConverter(typeof(DataGrhLenienteConverter))] public DateTime? Experiencia1Fim { get; set; }
    [JsonPropertyName("experiencia2Fim")] [JsonConverter(typeof(DataGrhLenienteConverter))] public DateTime? Experiencia2Fim { get; set; }
    [JsonPropertyName("tamanhoBlusa")] public string? TamanhoBlusa { get; set; }
    [JsonPropertyName("tamanhoCalca")] public string? TamanhoCalca { get; set; }
    [JsonPropertyName("tamanhoCalcado")] public string? TamanhoCalcado { get; set; }
    [JsonPropertyName("obraNome")] public string? ObraNome { get; set; }
    [JsonPropertyName("cargo")] public CargoGrhPayload? Cargo { get; set; }
}

internal class CargoGrhPayload
{
    [JsonPropertyName("nome")] public string? Nome { get; set; }
    [JsonPropertyName("cbo")] public string? Cbo { get; set; }
}

// Incidente real (22/09): um colaborador trouxe "experiencia2Fim" num formato que
// System.Text.Json não reconhece (ex.: vazio ou fora do ISO 8601) — como a carga inicial
// desserializa o array inteiro de uma vez (ReadFromJsonAsync<List<...>>), isso quebrava a
// importação de TODOS os colaboradores, não só desse um. Datas opcionais (Nascimento/
// Desligamento/Experiencia1Fim/Experiencia2Fim) usam este conversor tolerante: um valor que não
// dá pra interpretar vira null (campo "não informado") em vez de derrubar o lote inteiro.
// Admissao continua estrita (é obrigatória) — um colaborador sem data de admissão válida é
// mesmo um registro inválido.
internal class DataGrhLenienteConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType != JsonTokenType.String) return null;

        var texto = reader.GetString();
        if (string.IsNullOrWhiteSpace(texto)) return null;

        return DateTime.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
            ? data
            : null;
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value.HasValue) writer.WriteStringValue(value.Value);
        else writer.WriteNullValue();
    }
}

internal static class ColaboradorGrhPayloadMapper
{
    public static ColaboradorGrhDto Mapear(ColaboradorGrhPayload p) => new(
        Cpf: p.Cpf,
        Nome: p.Nome,
        Pis: p.Pis,
        Ctps: p.Ctps,
        DataNascimento: p.Nascimento,
        NomeMae: p.NomeMae,
        Endereco: p.Endereco,
        Municipio: p.Municipio,
        Uf: p.Uf,
        Cep: p.Cep,
        Matricula: p.Matricula,
        DataAdmissao: p.Admissao,
        DataDemissao: p.Desligamento,
        Situacao: MapearSituacao(p.Situacao),
        Salario: p.Salario,
        DataFimExperiencia1: p.Experiencia1Fim,
        DataFimExperiencia2: p.Experiencia2Fim,
        TamanhoBlusaEpi: p.TamanhoBlusa,
        TamanhoCalcaEpi: p.TamanhoCalca,
        TamanhoCalcadoEpi: p.TamanhoCalcado,
        ObraNome: p.ObraNome,
        CargoNome: p.Cargo?.Nome,
        CargoCboCodigo: p.Cargo?.Cbo);

    private static SituacaoTrabalhador MapearSituacao(string? situacao) => situacao?.Trim().ToUpperInvariant() switch
    {
        "ATIVO" => SituacaoTrabalhador.Ativo,
        "AFASTADO" => SituacaoTrabalhador.Afastado,
        "DESLIGADO" => SituacaoTrabalhador.Desligado,
        _ => throw new InvalidOperationException($"Situação '{situacao}' recebida do G-RH não é reconhecida (esperado ATIVO|AFASTADO|DESLIGADO)."),
    };
}
