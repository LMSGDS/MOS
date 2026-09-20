using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MosDock;

/// <summary>
/// Tìm cửa sổ chính Word / Excel / PowerPoint, bỏ maximize, đặt đúng ô còn lại của dock.
/// </summary>
static class WordWindow
{
    const uint SWP_FRAMECHANGED = 0x0020;
    const uint SWP_NOACTIVATE = 0x0010;
    const uint SWP_NOZORDER = 0x0004;
    const int SW_SHOWNOACTIVATE = 4;
    const int GWL_STYLE = -16;
    const int GWL_EXSTYLE = -20;
    const int GW_OWNER = 4;
    const int WS_MAXIMIZE = 0x01000000;
    const int WS_EX_TOOLWINDOW = 0x00000080;
    const int WS_EX_NOACTIVATE = 0x08000000;
    const int PlaceSlack = 12;

    static CancellationTokenSource? _place;

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
        var hwnd = ResolveExamWindow(app);
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        if (AlreadyPlaced(hwnd, target))
        {
            return true;
        }

        ForceBounds(hwnd, target);
        return true;
    }

    public static void CancelPlace()
    {
        try
        {
            _place?.Cancel();
        }
        catch
        {
            // already disposed
        }
    }

    public static void ApplySoon(Rect target, string? app = null, int timeoutMs = 12000)
    {
        CancelPlace();
        var cts = new CancellationTokenSource();
        _place = cts;
        var token = cts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                while (DateTime.UtcNow < until && !token.IsCancellationRequested)
                {
                    if (Apply(target, app))
                    {
                        await Task.Delay(400, token);
                        if (Apply(target, app))
                        {
                            return;
                        }
                    }

                    await Task.Delay(300, token);
                }

                if (!token.IsCancellationRequested)
                {
                    Apply(target, app);
                }
            }
            catch (TaskCanceledException)
            {
                // replaced by a newer place, or left dock
            }
        }, token);
    }

    public static void Launch(string? app, string? documentUrl = null)
    {
        var spec = OfficeApp.Resolve(app);
        var uri = spec.Protocol;
        if (!string.IsNullOrWhiteSpace(documentUrl))
        {
            uri = spec.Protocol.TrimEnd(':') + ":nft|u|" + documentUrl;
        }

        if (!string.IsNullOrWhiteSpace(documentUrl) && File.Exists(documentUrl))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = documentUrl,
                    UseShellExecute = true,
                });
            }
            catch
            {
                // Office chưa liên kết phần mở rộng
            }

            return;
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

    public static bool IsOfficeOrDock(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            _ = GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0)
            {
                return false;
            }

            var name = Process.GetProcessById(pid).ProcessName.ToLowerInvariant();
            return name is "winword" or "excel" or "powerpnt" or "mos-kulkul" or "mosdock";
        }
        catch
        {
            return false;
        }
    }

    public static IntPtr ForegroundWindow() => GetForegroundWindow();

    public static void FlashFeedback(Color color, Rect? word = null)
    {
        try
        {
            var area = word ?? new Rect(0, 0, Screen.PrimaryScreen?.WorkingArea.Width ?? 400, 8);
            var flash = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Bounds = new Rectangle(area.X, area.Y, Math.Max(80, area.W), word is null ? 10 : 48),
                BackColor = color,
                TopMost = true,
                ShowInTaskbar = false,
            };
            flash.Show();
            var ticks = 0;
            var timer = new System.Windows.Forms.Timer { Interval = 160 };
            timer.Tick += (_, _) =>
            {
                ticks++;
                flash.Visible = ticks % 2 == 1;
                if (ticks >= 6)
                {
                    timer.Stop();
                    flash.Close();
                    flash.Dispose();
                    timer.Dispose();
                }
            };
            timer.Start();
        }
        catch
        {
            // overlay is best-effort on Windows exam PCs
        }
    }

    public static void FlashRibbonHint(Rect word) =>
        FlashFeedback(Color.FromArgb(220, 38, 38), word);

    public static bool TitleMatchesExam(string? title, string? localPath)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(localPath))
        {
            return false;
        }

        var stem = Path.GetFileNameWithoutExtension(localPath);
        var name = Path.GetFileName(localPath);
        if (string.IsNullOrWhiteSpace(stem))
        {
            return false;
        }

        return title.IndexOf(stem, StringComparison.OrdinalIgnoreCase) >= 0
            || (!string.IsNullOrWhiteSpace(name) && title.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    static IntPtr ResolveExamWindow(string? app)
    {
        var spec = OfficeApp.Resolve(app);
        var wanted = ExamSession.LocalPath;
        if (spec.Id == "word")
        {
            var comHwnd = HwndFromExamDocument();
            if (comHwnd != IntPtr.Zero)
            {
                return comHwnd;
            }
        }

        if (string.IsNullOrWhiteSpace(wanted))
        {
            return IntPtr.Zero;
        }

        foreach (var hwnd in FindMainWindows(spec))
        {
            if (TitleMatchesExam(Caption(hwnd), wanted))
            {
                return hwnd;
            }
        }

        return IntPtr.Zero;
    }

    static IntPtr HwndFromExamDocument()
    {
        try
        {
            if (!WordCom.TryBind(out _, out dynamic? doc) || doc is null)
            {
                return IntPtr.Zero;
            }

            try
            {
                return new IntPtr((int)doc.Windows[1].Hwnd);
            }
            catch
            {
                return new IntPtr((int)doc.ActiveWindow.Hwnd);
            }
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    static string Caption(IntPtr hwnd)
    {
        var text = new StringBuilder(512);
        GetWindowText(hwnd, text, text.Capacity);
        return text.ToString();
    }

    static bool AlreadyPlaced(IntPtr hwnd, Rect target)
    {
        if (!GetWindowRect(hwnd, out var rc))
        {
            return false;
        }

        return Math.Abs(rc.Left - target.X) <= PlaceSlack
            && Math.Abs(rc.Top - target.Y) <= PlaceSlack
            && Math.Abs((rc.Right - rc.Left) - target.W) <= PlaceSlack * 2
            && Math.Abs((rc.Bottom - rc.Top) - target.H) <= PlaceSlack * 2;
    }

    static void ForceBounds(IntPtr hwnd, Rect target)
    {
        var style = GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
        if ((style & WS_MAXIMIZE) != 0)
        {
            ShowWindow(hwnd, SW_SHOWNOACTIVATE);
            SetWindowLongPtr(hwnd, GWL_STYLE, (nint)(style & ~WS_MAXIMIZE));
        }

        var wp = new WINDOWPLACEMENT();
        wp.length = Marshal.SizeOf<WINDOWPLACEMENT>();
        GetWindowPlacement(hwnd, ref wp);
        wp.showCmd = SW_SHOWNOACTIVATE;
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
            SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
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
            if (!pids.Contains(pid) || !IsWindowVisible(hWnd) || GetWindow(hWnd, GW_OWNER) != IntPtr.Zero)
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

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

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
