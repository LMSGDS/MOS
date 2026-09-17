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

    public static string? ParseState(string[] args)
    {
        string? pending = null;
        foreach (var arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg))
            {
                continue;
            }

            if (arg.StartsWith("--state=", StringComparison.OrdinalIgnoreCase))
            {
                return arg["--state=".Length..];
            }

            if (arg.Equals("--place", StringComparison.OrdinalIgnoreCase)
                || arg.Equals("--state", StringComparison.OrdinalIgnoreCase))
            {
                pending = "next";
                continue;
            }

            if (pending == "next")
            {
                return arg;
            }

            if (arg.StartsWith(Name + ":", StringComparison.OrdinalIgnoreCase))
            {
                return QueryValue(arg, "state") ?? "bottom";
            }
        }

        return null;
    }

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
