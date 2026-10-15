using System.Runtime.InteropServices;

namespace GameShift.Platform;

internal static partial class NativeMethods
{
    const int GwOwner = 4;
    const int GwlExStyle = -20;
    const long WsExToolWindow = 0x80;
    const int DwmwaCloaked = 14;

    delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    static extern IntPtr GetWindow(IntPtr hwnd, int cmd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextLengthW")]
    static extern int GetWindowTextLength(IntPtr hwnd);

    [DllImport("dwmapi.dll")]
    static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);

    /// <summary>
    /// PID из candidates, у которых есть видимое окно верхнего уровня с заголовком (без владельца,
    /// не панель инструментов, не скрытое DWM). Сначала по окну узнаётся только PID; окна остальных
    /// процессов (игры, античиты, стоп-лист) дальше не опрашиваются. Хэндлы процессов не открываются.
    /// </summary>
    public static IReadOnlySet<int> PidsWithVisibleWindows(IReadOnlySet<int> candidates)
    {
        var result = new HashSet<int>();
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out var raw);
            var pid = (int)raw;
            if (!candidates.Contains(pid) || result.Contains(pid)) return true;
            if (!IsWindowVisible(hwnd) || GetWindow(hwnd, GwOwner) != IntPtr.Zero) return true;
            if ((GetWindowLongPtr(hwnd, GwlExStyle).ToInt64() & WsExToolWindow) != 0) return true;
            if (DwmGetWindowAttribute(hwnd, DwmwaCloaked, out var cloaked, sizeof(int)) == 0 && cloaked != 0) return true;
            if (GetWindowTextLength(hwnd) == 0) return true;
            result.Add(pid);
            return true;
        }, IntPtr.Zero);
        return result;
    }
}
