namespace AAHBRANT.SST.Application.Common.Interfaces;

// Abstração do endpoint de carga inicial do G-RH para Alojamento (mesmo padrão de
// IColaboradorGrhClient — GET /api/integracoes/sst/alojamentos, contrato acordado com o time do
// G-RH, ver docs/superpowers/2026-09-15-pedido-integracao-alojamento-grh.md). Implementação real em
// Infrastructure (AlojamentoGrhClient). Usado só por ImportarAlojamentosGrhCommand — a atualização
// contínua depois da carga inicial é por evento via Service Bus (ServiceBusAlojamentoGrhProcessor),
// não por chamadas repetidas a este cliente.
public interface IAlojamentoGrhClient
{
    Task<IReadOnlyList<AlojamentoGrhDto>> ListarTodosAsync(CancellationToken ct = default);
}

public record AlojamentoGrhDto(
    string GrhAlojamentoId,
    string Nome,
    string? ObraNome,
    string? Endereco,
    IReadOnlyList<AlojamentoMoradorGrhDto> Moradores);

public record AlojamentoMoradorGrhDto(string Cpf, string? Matricula, DateTime Desde);
