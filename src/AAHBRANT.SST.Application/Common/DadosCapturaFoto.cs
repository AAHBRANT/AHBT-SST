using System.Text.Json;
using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Common;

// Snapshot da captura. A obra vem do registro no servidor, nunca do nome enviado pelo cliente.
public record DadosCapturaFoto
{
    public DateTimeOffset? CapturadaEm { get; init; }
    public int? FusoMinutos { get; init; }
    public string Origem { get; init; } = "arquivo";
    public string? Local { get; init; }
    public Guid? ObraId { get; init; }
    public string? ObraNome { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public double? PrecisaoMetros { get; init; }
    public DateTimeOffset? LocalizacaoObtidaEm { get; init; }
    public string? MotivoLocalizacao { get; init; }
    public DateTimeOffset? RecebidaEm { get; init; }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static DadosCapturaFoto? Ler(string? json) => string.IsNullOrWhiteSpace(json)
        ? null : JsonSerializer.Deserialize<DadosCapturaFoto>(json, Json);

    public static async Task<string> PrepararAsync(string? json, Guid? obraId, IAppDbContext db, CancellationToken ct)
    {
        if (json?.Length > 8000) throw new InvalidOperationException("Os dados da foto excedem o limite permitido.");
        DadosCapturaFoto dados;
        try { dados = Ler(json) ?? new(); }
        catch (JsonException) { throw new InvalidOperationException("Os dados de captura da foto são inválidos."); }
        if (dados.Local?.Length > 200) throw new InvalidOperationException("Informe o local da foto com até 200 caracteres.");
        var agora = DateTimeOffset.UtcNow;
        if (dados.CapturadaEm > agora.AddMinutes(5))
            throw new InvalidOperationException("A data da foto está no futuro. Confira o relógio do aparelho.");
        if (dados.Origem != "camera") dados = dados with { CapturadaEm = null, Latitude = null, Longitude = null, LocalizacaoObtidaEm = null };
        var obraNome = obraId.HasValue
            ? await db.Obras.Where(o => o.Id == obraId).Select(o => o.Nome).FirstOrDefaultAsync(ct)
            : null;
        // Não preencher com o horário do envio: ele não é a data/hora da captura.
        dados = dados with { ObraId = obraId, ObraNome = obraNome, RecebidaEm = agora, Local = dados.Local?.Trim() };
        return JsonSerializer.Serialize(dados, Json);
    }

    public static IReadOnlyList<string> Pendencias(string? json)
    {
        var dados = Ler(json);
        var faltas = new List<string>();
        if (dados?.CapturadaEm is null) faltas.Add("data e hora da captura");
        if (dados?.ObraId is null || string.IsNullOrWhiteSpace(dados.ObraNome)) faltas.Add("obra vinculada");
        if (string.IsNullOrWhiteSpace(dados?.Local)) faltas.Add("descrição do local");
        if (!LocalizacaoValida(dados)) faltas.Add("geolocalização da captura");
        return faltas;
    }

    private static bool LocalizacaoValida(DadosCapturaFoto? d) =>
        d is { Latitude: >= -90 and <= 90, Longitude: >= -180 and <= 180, CapturadaEm: not null, LocalizacaoObtidaEm: not null }
        && (d.PrecisaoMetros is null || d.PrecisaoMetros is >= 0 and <= 100)
        && Math.Abs((d.CapturadaEm.Value - d.LocalizacaoObtidaEm.Value).TotalSeconds) <= 60;

    public static void ExigirCompleta(string? json, string rotulo)
    {
        var pendencias = Pendencias(json);
        if (pendencias.Count > 0)
            throw new InvalidOperationException($"{rotulo}: falta regularizar {string.Join(", ", pendencias)}. O registro permanece em andamento. Faça nova captura no local com os dados completos antes de finalizar.");
    }

    // Fotos existentes antes da adoção deste contrato não têm como recuperar a posição original.
    // Elas permanecem legadas; toda nova gravação passa por PrepararAsync e recebe um JSON, mesmo
    // quando a captura ainda está pendente.
    public static void ExigirCompletaSeInformada(string? json, string rotulo)
    {
        if (!string.IsNullOrWhiteSpace(json)) ExigirCompleta(json, rotulo);
    }
}
