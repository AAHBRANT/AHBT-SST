using AAHBRANT.SST.Application.Alertas;

namespace AAHBRANT.SST.Application.Tests.Terceirizados;

public class TecnicosSegurancaPorObraServiceFalso : ITecnicosSegurancaPorObraService
{
    public List<Guid> UsuarioIdsARetornar { get; set; } = new();

    public Task<List<Guid>> ObterUsuarioIdsAsync(Guid obraId, CancellationToken ct = default) =>
        Task.FromResult(UsuarioIdsARetornar);
}
