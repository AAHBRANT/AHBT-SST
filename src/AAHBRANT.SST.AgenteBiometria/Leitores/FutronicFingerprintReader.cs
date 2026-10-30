using System.Runtime.InteropServices;

namespace AAHBRANT.SST.AgenteBiometria.Leitores;

// Captura real via Futronic ScanAPI (ftrScanAPI.dll, 32 bits — o agente precisa rodar como x86).
// Devolve a imagem bruta em tons de cinza (1 byte por pixel, largura x altura do próprio leitor).
public class FutronicFingerprintReader : IFingerprintReader
{
    private const uint ErroQuadroVazio = 4306;
    private const int RecursoLfd = 1;               // FTR_SCANNER_FEATURE_LFD
    private const uint OpcaoDetectarDedoFalso = 1;  // FTR_OPTIONS_DETECT_FAKE_FINGER
    private static readonly TimeSpan IntervaloPolling = TimeSpan.FromMilliseconds(100);

    // Captura direta (29/10, Beira Rio): com digital seca ou gasta o sensor de presença do FS80H nunca
    // dispara, mas a foto com luz 4 sai nítida e casa 100 com ela mesma e com a leitura normal. Vidro
    // vazio na luz 4 tem média ~9; com dedo, ~100 — 40 separa os dois com folga.
    private const int DoseCapturaDireta = 4;
    private const double MediaMinimaComDedo = 40;

    // Captura contínua da assinatura (30/10, Beira Rio): uma funcionária ficava abaixo da média 40 na luz 4 e
    // nunca era lida. Quando quem chama valida cada foto contra o cache (aceitar), o filtro de brilho vira só
    // um descarte de vidro vazio (~9) e a luz alterna entre 4 e 6 — quem decide se é dedo é a comparação.
    private static readonly int[] DosesCapturaContinua = { 4, 6 };
    private const double MediaMinimaCapturaContinua = 15;

    private readonly TimeSpan _timeoutDedo;
    private readonly bool _detectarDedoFalso;
    private readonly SemaphoreSlim _acessoExclusivo = new(1, 1);

    // detectarDedoFalso: liga a detecção de dedo vivo (LFD) do próprio leitor. Desligada por padrão: no FS80H
    // testado ela recusou a maioria dos dedos verdadeiros (0 de 3 no teste A/B com o mesmo dedo), e um dedo
    // recusado não gera erro — o leitor só nunca "detecta" o dedo. Ligue só depois de validar no piloto.
    public FutronicFingerprintReader(TimeSpan? timeoutDedo = null, bool detectarDedoFalso = false)
    {
        _timeoutDedo = timeoutDedo ?? TimeSpan.FromSeconds(15);
        _detectarDedoFalso = detectarDedoFalso;
    }

    public async Task<byte[]> CapturarAsync(CancellationToken ct, bool exigirNovoToque = false, Func<byte[], bool>? aceitar = null)
    {
        // O driver só aceita um handle aberto por vez; requisições simultâneas ao agente esperam a vez.
        await _acessoExclusivo.WaitAsync(ct);
        try
        {
            return await Task.Run(() => CapturarBloqueante(ct, exigirNovoToque, aceitar), ct);
        }
        finally
        {
            _acessoExclusivo.Release();
        }
    }

    private byte[] CapturarBloqueante(CancellationToken ct, bool exigirNovoToque, Func<byte[], bool>? aceitar)
    {
        var handle = NativeMethods.ftrScanOpenDevice();
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"Leitor Futronic não encontrado ou em uso por outro programa (erro {NativeMethods.ftrScanGetLastError()}).");
        }

        try
        {
            if (_detectarDedoFalso)
            {
                if (!NativeMethods.ftrScanIsScannerFeaturePresent(handle, RecursoLfd, out var temLfd) || !temLfd)
                {
                    throw new InvalidOperationException(
                        "Este leitor não tem detecção de dedo vivo (LFD). Desligue a opção DetectarDedoFalso do agente.");
                }

                if (!NativeMethods.ftrScanSetOptions(handle, OpcaoDetectarDedoFalso, OpcaoDetectarDedoFalso))
                {
                    throw new InvalidOperationException(
                        $"Não foi possível ligar a detecção de dedo vivo (erro {NativeMethods.ftrScanGetLastError()}).");
                }
            }

            if (!NativeMethods.ftrScanGetImageSize(handle, out var tamanho))
            {
                throw new InvalidOperationException($"Falha ao obter o tamanho da imagem (erro {NativeMethods.ftrScanGetLastError()}).");
            }

            var buffer = new byte[tamanho.nImageSize];
            var limite = DateTime.UtcNow + _timeoutDedo;
            var dedoDetectado = false;
            var quadrosVazios = 0;
            var capturasDiretasSeguidas = 0;
            byte[]? ultimaLida = null;
            var tentativaContinua = 0;

            // Segunda leitura do cadastro: o dedo da leitura anterior ainda pode estar no vidro. Espera sair
            // (dentro do mesmo tempo limite) para que a confirmação seja uma nova apoiada de verdade.
            while (exigirNovoToque && (NativeMethods.ftrScanIsFingerPresent(handle, IntPtr.Zero) || DedoNaCapturaDireta(handle, new byte[buffer.Length])))
            {
                ct.ThrowIfCancellationRequested();
                if (DateTime.UtcNow > limite)
                {
                    throw new TimeoutException("O dedo não foi retirado do leitor. Retire o dedo e tente novamente.");
                }

                Thread.Sleep(IntervaloPolling);
            }

            // Repete até o timeout: o dedo pode ser detectado antes de assentar e o quadro vir vazio
            // (erro 4306, EMPTY_FRAME) — nesse caso é só esperar e tentar de novo.
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                if (DateTime.UtcNow > limite && ultimaLida is not null)
                {
                    return ultimaLida;
                }

                if (DateTime.UtcNow > limite)
                {
                    throw new TimeoutException(dedoDetectado
                        ? $"Dedo detectado, mas o leitor só devolveu quadros vazios ({quadrosVazios}x). Reposicione o dedo cobrindo bem o vidro."
                        : _detectarDedoFalso
                            ? "Nenhum dedo detectado (ou o leitor não reconheceu o dedo como vivo). Limpe o dedo e o vidro e tente de novo."
                            : "Nenhum dedo detectado no leitor dentro do tempo limite.");
                }

                // Sem LFD ligado, quando o sensor não vê o dedo (ou só devolve quadro vazio) tenta a foto direta.
                // Duas seguidas com dedo = dedo já assentado; usa a segunda. Com LFD ligado não: a foto direta
                // pularia a checagem de dedo vivo.
                byte[]? TentarCapturaDireta()
                {
                    var direta = new byte[buffer.Length];
                    if (_detectarDedoFalso || !DedoNaCapturaDireta(handle, direta))
                    {
                        capturasDiretasSeguidas = 0;
                        return null;
                    }

                    return capturasDiretasSeguidas++ > 0 ? direta : null;
                }

                if (aceitar is not null && !_detectarDedoFalso)
                {
                    var foto = new byte[buffer.Length];
                    var dose = DosesCapturaContinua[tentativaContinua++ % DosesCapturaContinua.Length];
                    if (NativeMethods.ftrScanIsFingerPresent(handle, IntPtr.Zero) && NativeMethods.ftrScanGetFrame(handle, foto, IntPtr.Zero))
                    {
                        dedoDetectado = true;
                    }
                    else if (!CapturaDiretaComDedo(handle, foto, dose, MediaMinimaCapturaContinua))
                    {
                        Thread.Sleep(IntervaloPolling);
                        continue;
                    }

                    ultimaLida = foto;
                    if (aceitar(foto))
                    {
                        return foto;
                    }

                    continue;
                }

                if (!NativeMethods.ftrScanIsFingerPresent(handle, IntPtr.Zero))
                {
                    if (TentarCapturaDireta() is { } semPresenca)
                    {
                        return semPresenca;
                    }

                    Thread.Sleep(IntervaloPolling);
                    continue;
                }

                dedoDetectado = true;
                if (NativeMethods.ftrScanGetFrame(handle, buffer, IntPtr.Zero))
                {
                    if (aceitar is null || aceitar(buffer))
                    {
                        break;
                    }

                    ultimaLida = buffer.ToArray();
                    continue;
                }

                var erro = NativeMethods.ftrScanGetLastError();
                if (erro != ErroQuadroVazio)
                {
                    throw new InvalidOperationException($"Falha ao capturar a digital (erro {erro}).");
                }

                quadrosVazios++;
                if (TentarCapturaDireta() is { } aposQuadroVazio)
                {
                    return aposQuadroVazio;
                }

                Thread.Sleep(IntervaloPolling);
            }

            return buffer;
        }
        finally
        {
            NativeMethods.ftrScanCloseDevice(handle);
        }
    }

    private static bool DedoNaCapturaDireta(IntPtr handle, byte[] destino) =>
        CapturaDiretaComDedo(handle, destino, DoseCapturaDireta, MediaMinimaComDedo);

    private static bool CapturaDiretaComDedo(IntPtr handle, byte[] destino, int dose, double mediaMinima)
    {
        if (!NativeMethods.ftrScanGetImage(handle, dose, destino))
        {
            return false;
        }

        long soma = 0;
        foreach (var pixel in destino)
        {
            soma += pixel;
        }

        return soma >= mediaMinima * destino.Length;
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
        public static extern bool ftrScanIsScannerFeaturePresent(IntPtr handle, int recurso, [MarshalAs(UnmanagedType.Bool)] out bool presente);

        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ftrScanSetOptions(IntPtr handle, uint mascara, uint flags);

        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ftrScanIsFingerPresent(IntPtr handle, IntPtr frameParameters);

        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ftrScanGetFrame(IntPtr handle, [Out] byte[] buffer, IntPtr frameParameters);

        // Foto direta com intensidade de luz (dose) escolhida, sem depender do sensor de presença.
        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ftrScanGetImage(IntPtr handle, int dose, [Out] byte[] buffer);
    }
}
