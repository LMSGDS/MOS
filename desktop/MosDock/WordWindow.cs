using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MosDock;

/// <summary>
/// Tìm cửa sổ chính Word / Excel / PowerPoint, bỏ maximize, đặt đúng ô còn lại của dock.
/// </summary>
static class WordWindow
{
    const uint SWP_SHOWWINDOW = 0x0040;
    const uint SWP_FRAMECHANGED = 0x0020;
    const uint SWP_NOACTIVATE = 0x0010;
    const int SW_RESTORE = 9;
    const int SW_SHOWNORMAL = 1;
    const int GWL_STYLE = -16;
    const int GWL_EXSTYLE = -20;
    const int GW_OWNER = 4;
    const int WS_MAXIMIZE = 0x01000000;
    const int WS_EX_TOOLWINDOW = 0x00000080;
    const int WS_EX_NOACTIVATE = 0x08000000;

    static readonly string[] SkipClasses =
    [
        "MsoSplash",
        "MsoOpen",
        "OfficeTooltip",
        "Net UI Tool Window",
        "tooltips_class32",
        "IME",
    ];

    public static bool Apply(Rect target, string? app = null)
    {
        var spec = OfficeApp.Resolve(app);
        var moved = false;
        foreach (var hwnd in FindMainWindows(spec))
        {
            ForceBounds(hwnd, target);
            moved = true;
        }

        return moved;
    }

    public static void ApplySoon(Rect target, string? app = null, int timeoutMs = 20000)
    {
        _ = Task.Run(async () =>
        {
            var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < until)
            {
                Apply(target, app);
                await Task.Delay(200);
            }

            Apply(target, app);
        });
    }

    public static void Launch(string? app, string? documentUrl = null)
    {
        var spec = OfficeApp.Resolve(app);
        var uri = spec.Protocol;
        if (!string.IsNullOrWhiteSpace(documentUrl))
        {
            uri = spec.Protocol.TrimEnd(':') + ":nft|u|" + documentUrl;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri,
                UseShellExecute = true,
            });
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = spec.Process,
                    UseShellExecute = true,
                });
            }
            catch
            {
                // Office chưa cài
            }
        }
    }

    static void ForceBounds(IntPtr hwnd, Rect target)
    {
        ShowWindow(hwnd, SW_RESTORE);

        var style = GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
        if ((style & WS_MAXIMIZE) != 0)
        {
            SetWindowLongPtr(hwnd, GWL_STYLE, (nint)(style & ~WS_MAXIMIZE));
        }

        var wp = new WINDOWPLACEMENT();
        wp.length = Marshal.SizeOf<WINDOWPLACEMENT>();
        GetWindowPlacement(hwnd, ref wp);
        wp.showCmd = SW_SHOWNORMAL;
        wp.flags = 0;
        wp.rcNormalPosition = new RECT
        {
            Left = target.X,
            Top = target.Y,
            Right = target.X + target.W,
            Bottom = target.Y + target.H,
        };
        SetWindowPlacement(hwnd, ref wp);
        MoveWindow(hwnd, target.X, target.Y, target.W, target.H, true);
        SetWindowPos(
            hwnd,
            IntPtr.Zero,
            target.X,
            target.Y,
            target.W,
            target.H,
            SWP_SHOWWINDOW | SWP_FRAMECHANGED | SWP_NOACTIVATE);
    }

    static IEnumerable<IntPtr> FindMainWindows(OfficeApp spec)
    {
        var pids = new HashSet<int>();
        foreach (var proc in Process.GetProcessesByName(spec.Process))
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

        var matched = new List<(IntPtr Hwnd, int Area, bool ClassOk)>();
        EnumWindows((hWnd, _) =>
        {
            GetWindowThreadProcessId(hWnd, out var pid);
            if (!pids.Contains((int)pid) || !IsWindowVisible(hWnd) || GetWindow(hWnd, GW_OWNER) != IntPtr.Zero)
            {
                return true;
            }

            var ex = GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64();
            if ((ex & WS_EX_TOOLWINDOW) != 0 || (ex & WS_EX_NOACTIVATE) != 0)
            {
                return true;
            }

            var cls = new StringBuilder(64);
            GetClassName(hWnd, cls, cls.Capacity);
            var className = cls.ToString();
            if (SkipClasses.Contains(className, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!GetWindowRect(hWnd, out var rc))
            {
                return true;
            }

            var area = Math.Max(0, rc.Right - rc.Left) * Math.Max(0, rc.Bottom - rc.Top);
            if (area < 200 * 150)
            {
                return true;
            }

            var classOk = spec.Classes.Any(c => c.Equals(className, StringComparison.OrdinalIgnoreCase));
            matched.Add((hWnd, area, classOk));
            return true;
        }, IntPtr.Zero);

        var chosen = matched.Any(m => m.ClassOk)
            ? matched.Where(m => m.ClassOk).OrderByDescending(m => m.Area)
            : matched.OrderByDescending(m => m.Area);

        foreach (var item in chosen)
        {
            yield return item.Hwnd;
        }
    }

    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public Point ptMinPosition;
        public Point ptMaxPosition;
        public RECT rcNormalPosition;
    }

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

    [DllImport("user32.dll")]
    static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll")]
    static extern bool SetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll")]
    static extern IntPtr GetWindow(IntPtr hWnd, int uCmd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    static extern nint GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    static extern nint SetWindowLongPtr(IntPtr hWnd, int nIndex, nint dwNewLong);
}
