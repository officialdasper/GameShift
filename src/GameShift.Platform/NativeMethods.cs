using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GameShift.Platform;

internal static partial class NativeMethods
{
    public const int SystemMemoryListInformation = 80;
    public const int MemoryPurgeStandbyList = 4;
    const uint TokenAdjustPrivileges = 0x0020;
    const uint TokenQuery = 0x0008;
    const int SePrivilegeEnabled = 0x2;

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct TokenPrivileges
    {
        public int Count;
        public long Luid;
        public int Attributes;
    }

    [LibraryImport("ntdll.dll")]
    public static partial int NtSetSystemInformation(int infoClass, ref int info, int length);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

    [LibraryImport("advapi32.dll", EntryPoint = "LookupPrivilegeValueW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool LookupPrivilegeValue(string? system, string name, out long luid);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AdjustTokenPrivileges(IntPtr token, [MarshalAs(UnmanagedType.Bool)] bool disableAll,
        ref TokenPrivileges newState, int bufferLength, IntPtr previous, IntPtr returnLength);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr handle);

    public static bool EnablePrivilege(string name)
    {
        using var self = Process.GetCurrentProcess();
        if (!OpenProcessToken(self.Handle, TokenAdjustPrivileges | TokenQuery, out var token)) return false;
        try
        {
            if (!LookupPrivilegeValue(null, name, out var luid)) return false;
            var tp = new TokenPrivileges { Count = 1, Luid = luid, Attributes = SePrivilegeEnabled };
            return AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero)
                   && Marshal.GetLastPInvokeError() == 0;
        }
        finally
        {
            CloseHandle(token);
        }
    }
}
