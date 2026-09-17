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
            ForwardToRunningInstance(parsed.State ?? "bottom", parsed.App ?? "word", parsed.Launch, parsed.File);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(parsed.State, parsed.App, parsed.Launch, parsed.File));
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
