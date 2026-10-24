using AAHBRANT.SST.Application.Common.Interfaces;

namespace AAHBRANT.SST.Application.Tests.PlanosIntegracao;

public sealed class GeradorNumeroSequencialFalso : IGeradorNumeroDocumentoService
{
    private int _proximo = 1;
    public Task<string> GerarAsync(string prefixo, CancellationToken ct) => Task.FromResult($"{prefixo}-2026-{_proximo++:0000}");
}
