using System.Net.Http;

namespace MosDock;

static class Program
{
    const string MutexName = @"Local\MosDock.GDS";

    [STAThread]
    static void Main(string[] args)
    {
        var parsed = Protocol.Parse(args);
        using var mutex = new Mutex(true, MutexName, out var created);
        if (!created)
        {
            ForwardToRunningInstance(parsed.State ?? "bottom", parsed.App ?? "word");
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(parsed.State, parsed.App));
    }

    static void ForwardToRunningInstance(string state, string app)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            http.PostAsync(
                $"http://127.0.0.1:{LocalAgent.Port}/place?state={Uri.EscapeDataString(state)}&app={Uri.EscapeDataString(app)}",
                null).GetAwaiter().GetResult();
        }
        catch
        {
            // first instance may still be starting
        }
    }
}
