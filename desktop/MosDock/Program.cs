using System.Net.Http;

namespace MosDock;

static class Program
{
    const string MutexName = @"Local\MosDock.GDS";

    [STAThread]
    static void Main(string[] args)
    {
        var state = Protocol.ParseState(args);
        using var mutex = new Mutex(true, MutexName, out var created);
        if (!created)
        {
            ForwardToRunningInstance(state ?? "bottom");
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(state));
    }

    static void ForwardToRunningInstance(string state)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            http.PostAsync(
                $"http://127.0.0.1:{LocalAgent.Port}/place?state={Uri.EscapeDataString(state)}",
                null).GetAwaiter().GetResult();
        }
        catch
        {
            // first instance may still be starting
        }
    }
}
