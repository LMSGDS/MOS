using Microsoft.Win32;

namespace MosDock;

static class Protocol
{
    public const string Name = "mosdock";

    public static void Register()
    {
        var exe = Application.ExecutablePath;
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + Name);
        key.SetValue("", "URL:MOS Dock");
        key.SetValue("URL Protocol", "");
        using var cmd = key.CreateSubKey(@"shell\open\command");
        cmd.SetValue("", $"\"{exe}\" \"%1\"");
    }

    public static (string? State, string? App, bool Launch, string? File) Parse(string[] args)
    {
        string? state = null;
        string? app = null;
        string? file = null;
        var launch = false;
        string? pending = null;
        foreach (var arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg))
            {
                continue;
            }

            if (arg.Equals("--open", StringComparison.OrdinalIgnoreCase))
            {
                launch = true;
                continue;
            }

            if (arg.StartsWith("--state=", StringComparison.OrdinalIgnoreCase))
            {
                state = arg["--state=".Length..];
                continue;
            }

            if (arg.StartsWith("--app=", StringComparison.OrdinalIgnoreCase))
            {
                app = arg["--app=".Length..];
                continue;
            }

            if (arg.Equals("--place", StringComparison.OrdinalIgnoreCase)
                || arg.Equals("--state", StringComparison.OrdinalIgnoreCase)
                || arg.Equals("--app", StringComparison.OrdinalIgnoreCase)
                || arg.Equals("--file", StringComparison.OrdinalIgnoreCase))
            {
                pending = arg.TrimStart('-').ToLowerInvariant();
                continue;
            }

            if (pending is "state" or "place")
            {
                state = arg;
                pending = null;
                continue;
            }

            if (pending == "app")
            {
                app = arg;
                pending = null;
                continue;
            }

            if (pending == "file")
            {
                file = arg;
                pending = null;
                continue;
            }

            if (arg.StartsWith(Name + ":", StringComparison.OrdinalIgnoreCase))
            {
                launch = launch || arg.Contains(":open", StringComparison.OrdinalIgnoreCase);
                state = QueryValue(arg, "state") ?? state;
                app = QueryValue(arg, "app") ?? app;
                file = QueryValue(arg, "file") ?? file;
            }
        }

        return (state, app, launch, file);
    }

    public static string? ParseState(string[] args) => Parse(args).State;

    static string? QueryValue(string uri, string key)
    {
        var q = uri.IndexOf('?');
        if (q < 0)
        {
            return null;
        }

        foreach (var part in uri[(q + 1)..].Split('&'))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && Uri.UnescapeDataString(kv[0]) == key)
            {
                return Uri.UnescapeDataString(kv[1]).Trim();
            }
        }

        return null;
    }
}
