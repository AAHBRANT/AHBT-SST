using AAHBRANT.SST.Infrastructure.Seguranca;

namespace AAHBRANT.SST.Application.Tests.TestSupport;

// Chave fixa de teste das credenciais de câmera (estado estático global, como a do CPF: ver ChavesCpfDeTeste).
public static class ChaveCredencialCameraDeTeste
{
    private static readonly byte[] Chave = Enumerable.Repeat((byte)3, 32).ToArray();

    public static void Configurar() => CredencialCameraCriptografiaContexto.Configurar(Chave);
}
