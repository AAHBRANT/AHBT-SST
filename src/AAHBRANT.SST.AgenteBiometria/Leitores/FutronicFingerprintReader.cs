using System.Runtime.InteropServices;

namespace AAHBRANT.SST.AgenteBiometria.Leitores;

// Captura real via Futronic ScanAPI (ftrScanAPI.dll, 32 bits — o agente precisa rodar como x86).
// Devolve a imagem bruta em tons de cinza (1 byte por pixel, largura x altura do próprio leitor).
public class FutronicFingerprintReader : IFingerprintReader
{
    private static readonly TimeSpan IntervaloPolling = TimeSpan.FromMilliseconds(100);

    private readonly TimeSpan _timeoutDedo;
    private readonly SemaphoreSlim _acessoExclusivo = new(1, 1);

    public FutronicFingerprintReader(TimeSpan? timeoutDedo = null)
    {
        _timeoutDedo = timeoutDedo ?? TimeSpan.FromSeconds(15);
    }

    public async Task<byte[]> CapturarAsync(CancellationToken ct)
    {
        // O driver só aceita um handle aberto por vez; requisições simultâneas ao agente esperam a vez.
        await _acessoExclusivo.WaitAsync(ct);
        try
        {
            return await Task.Run(() => CapturarBloqueante(ct), ct);
        }
        finally
        {
            _acessoExclusivo.Release();
        }
    }

    private byte[] CapturarBloqueante(CancellationToken ct)
    {
        var handle = NativeMethods.ftrScanOpenDevice();
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"Leitor Futronic não encontrado ou em uso por outro programa (erro {NativeMethods.ftrScanGetLastError()}).");
        }

        try
        {
            if (!NativeMethods.ftrScanGetImageSize(handle, out var tamanho))
            {
                throw new InvalidOperationException($"Falha ao obter o tamanho da imagem (erro {NativeMethods.ftrScanGetLastError()}).");
            }

            var buffer = new byte[tamanho.nImageSize];
            var limite = DateTime.UtcNow + _timeoutDedo;

            while (!NativeMethods.ftrScanIsFingerPresent(handle, IntPtr.Zero))
            {
                ct.ThrowIfCancellationRequested();
                if (DateTime.UtcNow > limite)
                {
                    throw new TimeoutException("Nenhum dedo detectado no leitor dentro do tempo limite.");
                }

                Thread.Sleep(IntervaloPolling);
            }

            if (!NativeMethods.ftrScanGetFrame(handle, buffer, IntPtr.Zero))
            {
                throw new InvalidOperationException($"Falha ao capturar a digital (erro {NativeMethods.ftrScanGetLastError()}).");
            }

            return buffer;
        }
        finally
        {
            NativeMethods.ftrScanCloseDevice(handle);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct FtrScanImageSize
    {
        public int nWidth;
        public int nHeight;
        public int nImageSize;
    }

    private static class NativeMethods
    {
        private const string Dll = "ftrScanAPI.dll";

        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr ftrScanOpenDevice();

        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
        public static extern void ftrScanCloseDevice(IntPtr handle);

        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
        public static extern uint ftrScanGetLastError();

        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ftrScanGetImageSize(IntPtr handle, out FtrScanImageSize tamanho);

        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ftrScanIsFingerPresent(IntPtr handle, IntPtr frameParameters);

        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ftrScanGetFrame(IntPtr handle, [Out] byte[] buffer, IntPtr frameParameters);
    }
}
