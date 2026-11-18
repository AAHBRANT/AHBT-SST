using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace AAHBRANT.SST.AgenteCameras.Quadros;

// Windows Job Object com KILL_ON_JOB_CLOSE: quando o serviço termina (inclusive por queda ou kill do processo),
// o sistema encerra todos os ffmpeg filhos. Garante que não sobra ffmpeg órfão segurando a câmera.
[SupportedOSPlatform("windows")]
internal static class JobObjectFilhos
{
    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JobObjectLimitKillOnJobClose = 0x2000;

    private static readonly Lazy<IntPtr> Job = new(Criar);

    /// <summary>Associa o processo ao job. Devolve false (sem lançar) se o sistema recusar.</summary>
    public static bool Associar(Process processo)
    {
        try
        {
            var job = Job.Value;
            return job != IntPtr.Zero && AssignProcessToJobObject(job, processo.Handle);
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    private static IntPtr Criar()
    {
        var job = CreateJobObject(IntPtr.Zero, null);
        if (job == IntPtr.Zero) return IntPtr.Zero;

        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
        info.BasicLimitInformation.LimitFlags = JobObjectLimitKillOnJobClose;
        var tamanho = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        var ponteiro = Marshal.AllocHGlobal(tamanho);
        try
        {
            Marshal.StructureToPtr(info, ponteiro, false);
            if (!SetInformationJobObject(job, JobObjectExtendedLimitInformation, ponteiro, (uint)tamanho))
                return IntPtr.Zero;
        }
        finally
        {
            Marshal.FreeHGlobal(ponteiro);
        }
        return job; // o handle vive até o fim do processo; ao fechar, o sistema mata os filhos
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr atributos, string? nome);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr job, int classe, IntPtr info, uint tamanho);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr processo);

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }
}
