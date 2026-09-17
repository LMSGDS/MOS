using System.Net.Http;

namespace MosDock;

static class Program
{
    const string MutexName = @"Local\MOS-KulKul.GDS";

    [STAThread]
    static void Main(string[] args)
    {
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
        var first = true;
        while (true)
        {
            using var login = new LoginForm();
            if (login.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            ExamSession.DisplayName = login.DisplayName;
            using var main = new MainForm(
                parsed.State ?? "bottom",
                parsed.App ?? "word",
                first && parsed.Launch,
                first ? parsed.File : null);
            first = false;
            Application.Run(main);
            Portal.Token = null;
            ExamSession.ClearExam();
            if (!main.SignOutRequested)
            {
                return;
            }
        }
    }

    static void ShowError(Exception ex)
    {
        MessageBox.Show(
            "MOS-KulKul gặp lỗi.\n\n" + ex.GetType().Name + ": " + ex.Message,
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
