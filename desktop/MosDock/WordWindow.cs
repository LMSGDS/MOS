using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MosDock;

static class WordWindow
{
    const uint SWP_NOZORDER = 0x0004;
    const uint SWP_NOACTIVATE = 0x0010;
    const uint SWP_SHOWWINDOW = 0x0040;
    const int SW_RESTORE = 9;

    public static void Apply(Rect word)
    {
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
        }
    }

    static IEnumerable<IntPtr> FindWordMainWindows()
    {
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

    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr hWnd);
}
