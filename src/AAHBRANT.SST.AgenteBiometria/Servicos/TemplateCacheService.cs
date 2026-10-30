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

    // Motivo da última sincronização que falhou (null quando a última deu certo). Antes a falha só ia para o
    // log do agente e a tela mostrava "Rode /api/sincronizar" — caso Beira Rio (30/10): o posto buscava as
    // digitais, não conseguia abrir e ficava com o cache vazio sem ninguém saber por quê.
    public string? UltimoErroSincronizacao { get; private set; }

    public async Task SincronizarAsync(CancellationToken ct)
    {
        try
        {
            var templates = await _backendClient.SincronizarTemplatesAsync(ct);
            var chave = LerChave();

            _cache = templates
                .Select(t => new TemplateCacheado(t.TrabalhadorId, t.TrabalhadorNome, DescriptografarComMotivo(t.TemplateCriptografado, chave)))
                .ToList();
            UltimoErroSincronizacao = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            UltimoErroSincronizacao = ex is InvalidOperationException ? ex.Message
                : "não foi possível buscar as digitais no servidor (verifique a internet e o cadastro do posto).";
            throw;
        }
    }

    private byte[] LerChave()
    {
        try
        {
            var chave = Convert.FromBase64String(_options.ChaveCriptografiaBiometriaBase64);
            if (chave.Length == 32)
                return chave;
        }
        catch (FormatException)
        {
        }
        throw new InvalidOperationException("a chave de criptografia da biometria deste posto está vazia ou inválida. Reinstale o agente com a chave do servidor.");
    }

    private static byte[] DescriptografarComMotivo(string cifradoBase64, byte[] chave)
    {
        try
        {
            return DescriptografarTemplate(cifradoBase64, chave);
        }
        catch (CryptographicException)
        {
            throw new InvalidOperationException("a chave de criptografia da biometria deste posto é diferente da do servidor. Reinstale o agente com a chave certa.");
        }
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
