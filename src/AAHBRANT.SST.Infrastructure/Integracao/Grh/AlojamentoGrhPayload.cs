using System.Text.Json.Serialization;
using AAHBRANT.SST.Application.Common.Interfaces;

namespace AAHBRANT.SST.Infrastructure.Integracao.Grh;

// Contrato de fio único do G-RH para Alojamento (mesmo padrão de ColaboradorGrhPayload): mesmo shape
// tanto na carga inicial (GET /api/integracoes/sst/alojamentos, consumido por AlojamentoGrhClient)
// quanto no evento contínuo publicado na fila "alojamento-grh" (consumido por
// ServiceBusAlojamentoGrhProcessor). "id" mapeia para Alojamento.GrhAlojamentoId — chave de
// correspondência da sincronização (ver SincronizarAlojamentoGrhCommand).
internal class AlojamentoGrhPayload
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("nome")] public string Nome { get; set; } = string.Empty;
    [JsonPropertyName("obraNome")] public string? ObraNome { get; set; }
    [JsonPropertyName("endereco")] public string? Endereco { get; set; }
    [JsonPropertyName("moradores")] public List<AlojamentoMoradorGrhPayload> Moradores { get; set; } = new();
}

internal class AlojamentoMoradorGrhPayload
{
    [JsonPropertyName("cpf")] public string Cpf { get; set; } = string.Empty;
    [JsonPropertyName("matricula")] public string? Matricula { get; set; }
    [JsonPropertyName("desde")] public DateTime Desde { get; set; }
}

internal static class AlojamentoGrhPayloadMapper
{
    public static AlojamentoGrhDto Mapear(AlojamentoGrhPayload p) => new(
        GrhAlojamentoId: p.Id,
        Nome: p.Nome,
        ObraNome: p.ObraNome,
        Endereco: p.Endereco,
        Moradores: p.Moradores.ConvertAll(m => new AlojamentoMoradorGrhDto(m.Cpf, m.Matricula, m.Desde)));
}
