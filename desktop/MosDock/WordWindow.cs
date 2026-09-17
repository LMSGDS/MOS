using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MosDock;

static class WordWindow
{
    const uint SWP_NOZORDER = 0x0004;
    const uint SWP_NOACTIVATE = 0x0010;
    const uint SWP_SHOWWINDOW = 0x0040;
    const int SW_RESTORE = 9;

    public static bool Apply(Rect word)
    {
        var moved = false;
        foreach (var hwnd in FindWordMainWindows())
        {
            ShowWindow(hwnd, SW_RESTORE);
            SetWindowPos(
                hwnd,
                IntPtr.Zero,
                word.X,
                word.Y,
                word.W,
                word.H,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            moved = true;
        }

        return moved;
    }

    /// <summary>
    /// Word mở chậm (splash). Lặp cho đến khi có cửa sổ OpusApp rồi đặt đúng vị trí đã chọn.
    /// </summary>
    public static void ApplySoon(Rect word, int timeoutMs = 12000)
    {
        _ = Task.Run(async () =>
        {
            var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < until)
            {
                if (Apply(word))
                {
                    return;
                }

                await Task.Delay(250);
            }

            Apply(word);
        });
    }

    static IEnumerable<IntPtr> FindWordMainWindows()
    {
        var pids = new HashSet<int>();
        foreach (var proc in Process.GetProcessesByName("WINWORD"))
        {
            try
            {
                pids.Add(proc.Id);
            }
            catch
            {
                // process exited
            }
        }

        if (pids.Count == 0)
        {
            yield break;
        }

        var found = new List<IntPtr>();
        EnumWindows((hWnd, _) =>
        {
            GetWindowThreadProcessId(hWnd, out var pid);
            if (!pids.Contains((int)pid) || !IsWindowVisible(hWnd))
            {
                return true;
            }

            var cls = new StringBuilder(64);
            GetClassName(hWnd, cls, cls.Capacity);
            if (cls.ToString() == "OpusApp")
            {
                found.Add(hWnd);
            }

            return true;
        }, IntPtr.Zero);

        if (found.Count > 0)
        {
            foreach (var hwnd in found)
            {
                yield return hwnd;
            }

            yield break;
        }

        foreach (var proc in Process.GetProcessesByName("WINWORD"))
        {
            IntPtr hwnd;
            try
            {
                hwnd = proc.MainWindowHandle;
            }
            catch
            {
                continue;
            }

            if (hwnd != IntPtr.Zero && IsWindowVisible(hwnd))
            {
                yield return hwnd;
            }
        }
    }

    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr hWnd);
}
