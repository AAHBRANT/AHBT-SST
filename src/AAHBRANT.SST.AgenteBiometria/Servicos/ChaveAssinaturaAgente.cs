using System.Security.Cryptography;
using System.Text;
using AAHBRANT.SST.AgenteBiometria.Opcoes;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.AgenteBiometria.Servicos;

// Par de chaves ECDSA P-256 do agente. A chave privada é gerada aqui na primeira execução e nunca sai
// deste PC: no Windows fica gravada protegida pelo DPAPI da máquina (só este computador consegue ler).
// O servidor recebe só a chave pública (RegistroChavePublicaService) e com ela confere cada leitura
// assinada — assim quem só tem acesso ao navegador não consegue inventar um trabalhador ou uma nota.
public sealed class ChaveAssinaturaAgente : IDisposable
{
    private static readonly byte[] Entropia = Encoding.UTF8.GetBytes("AAHBRANT.SST.AgenteBiometria/chave-assinatura");
    private readonly ECDsa _chave;

    public ChaveAssinaturaAgente(IOptions<AgenteOptions> options)
    {
        var caminho = string.IsNullOrWhiteSpace(options.Value.CaminhoChaveAssinatura)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "AAHBRANT", "AgenteBiometria", "chave-assinatura.bin")
            : options.Value.CaminhoChaveAssinatura;
        _chave = CarregarOuCriar(caminho);
        ChavePublicaBase64 = Convert.ToBase64String(_chave.ExportSubjectPublicKeyInfo());
    }

    public string ChavePublicaBase64 { get; }

    public string Assinar(string conteudo) =>
        Convert.ToBase64String(_chave.SignData(Encoding.UTF8.GetBytes(conteudo), HashAlgorithmName.SHA256));

    public void Dispose() => _chave.Dispose();

    private static ECDsa CarregarOuCriar(string caminho)
    {
        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        if (File.Exists(caminho))
        {
            ecdsa.ImportPkcs8PrivateKey(Desproteger(File.ReadAllBytes(caminho)), out _);
            return ecdsa;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        File.WriteAllBytes(caminho, Proteger(ecdsa.ExportPkcs8PrivateKey()));
        return ecdsa;
    }

    private static byte[] Proteger(byte[] dados) => OperatingSystem.IsWindows()
        ? ProtectedData.Protect(dados, Entropia, DataProtectionScope.LocalMachine)
        : dados;

    private static byte[] Desproteger(byte[] dados) => OperatingSystem.IsWindows()
        ? ProtectedData.Unprotect(dados, Entropia, DataProtectionScope.LocalMachine)
        : dados;
}
