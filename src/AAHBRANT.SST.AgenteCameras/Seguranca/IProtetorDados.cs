using System.Security.Cryptography;
using System.Text;

namespace AAHBRANT.SST.AgenteCameras.Seguranca;

public interface IProtetorDados
{
    byte[] Proteger(byte[] dados);
    byte[] Desproteger(byte[] dados);
}

// DPAPI no escopo da máquina: só este PC consegue ler (qualquer conta local, por isso serve ao serviço e ao console).
public sealed class ProtetorDpapi : IProtetorDados
{
    private static readonly byte[] Entropia = Encoding.UTF8.GetBytes("AAHBRANT.SST.AgenteCameras/v1");

    public byte[] Proteger(byte[] dados) => ProtectedData.Protect(dados, Entropia, DataProtectionScope.LocalMachine);

    public byte[] Desproteger(byte[] dados) => ProtectedData.Unprotect(dados, Entropia, DataProtectionScope.LocalMachine);
}
