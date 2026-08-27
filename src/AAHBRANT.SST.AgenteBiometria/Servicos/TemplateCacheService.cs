using System.Security.Cryptography;
using AAHBRANT.SST.AgenteBiometria.Opcoes;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.AgenteBiometria.Servicos;

public record TemplateCacheado(Guid TrabalhadorId, string TrabalhadorNome, byte[] TemplateBruto);

public class TemplateCacheService
{
    private const int TamanhoNonce = 12;
    private const int TamanhoTag = 16;

    private readonly BackendClient _backendClient;
    private readonly AgenteOptions _options;
    private List<TemplateCacheado> _cache = new();

    public TemplateCacheService(BackendClient backendClient, IOptions<AgenteOptions> options)
    {
        _backendClient = backendClient;
        _options = options.Value;
    }

    public IReadOnlyList<TemplateCacheado> Templates => _cache;

    public async Task SincronizarAsync(CancellationToken ct)
    {
        var templates = await _backendClient.SincronizarTemplatesAsync(ct);
        var chave = Convert.FromBase64String(_options.ChaveCriptografiaBiometriaBase64);

        _cache = templates
            .Select(t => new TemplateCacheado(t.TrabalhadorId, t.TrabalhadorNome, DescriptografarTemplate(t.TemplateCriptografado, chave)))
            .ToList();
    }

    // Duplica deliberadamente o layout nonce|cifrado|tag de TemplateBiometricoCriptografiaConversor
    // (backend) — o agente não referencia AAHBRANT.SST.Infrastructure por ser um executável standalone.
    public static byte[] DescriptografarTemplate(string cifradoBase64, byte[] chave)
    {
        var bytes = Convert.FromBase64String(cifradoBase64);
        var nonce = bytes[..TamanhoNonce];
        var tag = bytes[^TamanhoTag..];
        var cifrado = bytes[TamanhoNonce..^TamanhoTag];
        var textoPlano = new byte[cifrado.Length];

        using var aesGcm = new AesGcm(chave, TamanhoTag);
        aesGcm.Decrypt(nonce, cifrado, tag, textoPlano);

        return textoPlano;
    }
}
