using System.Net.Http;
using System.Runtime.InteropServices;

namespace MosDock;

static class Program
{
    const string MutexName = @"Local\MOS-KulKul.GDS";

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool SetDllDirectory(string lpPathName);

    [STAThread]
    static void Main(string[] args)
    {
        EnsureWebView2Loader();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ShowError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                ShowError(ex);
            }
        };

        var parsed = Protocol.Parse(args);
        using var mutex = new Mutex(true, MutexName, out var created);
        if (!created)
        {
            ForwardToRunningInstance(parsed.State ?? "bottom", parsed.App ?? "word", parsed.Launch, parsed.File);
            return;
        }

        ApplicationConfiguration.Initialize();
        string appId = parsed.App ?? "word";
        using (var login = new LoginForm(appId))
        {
            if (login.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            appId = login.SelectedApp;
            ExamSession.Mode = login.Mode;
            ExamSession.DisplayName = login.DisplayName;
        }

        Application.Run(new MainForm(parsed.State, appId, parsed.Launch, parsed.File));
    }

    static void EnsureWebView2Loader()
    {
        var baseDir = AppContext.BaseDirectory;
        var dest = Path.Combine(baseDir, "WebView2Loader.dll");
        if (!File.Exists(dest))
        {
            var found = Directory
                .EnumerateFiles(baseDir, "WebView2Loader.dll", SearchOption.AllDirectories)
                .FirstOrDefault();
            if (found != null)
            {
                try
                {
                    File.Copy(found, dest, overwrite: true);
                }
                catch
                {
                    SetDllDirectory(Path.GetDirectoryName(found)!);
                    return;
                }
            }
        }

        SetDllDirectory(baseDir);
    }

    static void ShowError(Exception ex)
    {
        MessageBox.Show(
            "MOS-KulKul thiếu thư viện WebView2 (Dll was not found).\n\n"
            + "Gỡ bản cũ, tải lại Setup.exe tại https://mos.gds.edu.vn/cai-dat rồi cài lại.\n"
            + "Nếu vẫn lỗi, cài Microsoft Edge WebView2 Runtime.\n\n"
            + ex.GetType().Name + ": " + ex.Message,
            "MOS-KulKul",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    static void ForwardToRunningInstance(string state, string app, bool launch, string? file)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var path = launch ? "open" : "place";
            var url = $"http://127.0.0.1:{LocalAgent.Port}/{path}?state={Uri.EscapeDataString(state)}&app={Uri.EscapeDataString(app)}";
            if (!string.IsNullOrWhiteSpace(file))
            {
                url += "&file=" + Uri.EscapeDataString(file);
            }

            http.PostAsync(url, null).GetAwaiter().GetResult();
        }
        catch
        {
            // first instance may still be starting
        }
    }
}
