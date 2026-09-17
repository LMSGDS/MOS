using System.Net;
using System.Text;

namespace MosDock;

/// <summary>
/// Agent local để trang MOS (Chrome hoặc WebView) gọi sau khi mở Word:
/// POST http://127.0.0.1:17331/place?state=left|right|bottom|minimized
/// </summary>
sealed class LocalAgent : IDisposable
{
    public const int Port = 17331;
    readonly HttpListener _http = new();
    readonly Action<string> _onPlace;
    readonly Control _ui;

    public LocalAgent(Control ui, Action<string> onPlace)
    {
        _ui = ui;
        _onPlace = onPlace;
        _http.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _http.Start();
        _ = Listen();
    }

    async Task Listen()
    {
        while (_http.IsListening)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _http.GetContextAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (HttpListenerException)
            {
                break;
            }

            _ = Handle(ctx);
        }
    }

    async Task Handle(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        var res = ctx.Response;
        try
        {
            AllowCors(req, res);
            if (req.HttpMethod == "OPTIONS")
            {
                res.StatusCode = 204;
                res.Close();
                return;
            }

            var path = (req.Url?.AbsolutePath ?? "/").TrimEnd('/');
            if (path.Length == 0)
            {
                path = "/";
            }

            if (path is "/health" or "/")
            {
                await WriteJson(res, 200, """{"ok":true,"service":"mos-dock"}""");
                return;
            }

            if (path == "/place")
            {
                var state = req.QueryString["state"] ?? "bottom";
                _ui.BeginInvoke(() => _onPlace(state));
                await WriteJson(res, 200, $$"""{"ok":true,"state":"{{Sanitize(state)}}","placed":true}""");
                return;
            }

            await WriteJson(res, 404, """{"ok":false}""");
        }
        catch
        {
            try
            {
                res.StatusCode = 500;
                res.Close();
            }
            catch
            {
                // listener gone
            }
        }
    }

    static string Sanitize(string state)
    {
        state = (state ?? "bottom").ToLowerInvariant();
        return state is "left" or "right" or "minimized" or "bottom" ? state : "bottom";
    }

    static void AllowCors(HttpListenerRequest req, HttpListenerResponse res)
    {
        var origin = req.Headers["Origin"] ?? "";
        if (origin.StartsWith("https://mos.gds.edu.vn", StringComparison.OrdinalIgnoreCase)
            || origin.StartsWith("http://127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || origin.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase))
        {
            res.Headers["Access-Control-Allow-Origin"] = origin;
        }
        else
        {
            res.Headers["Access-Control-Allow-Origin"] = "https://mos.gds.edu.vn";
        }

        res.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
        res.Headers["Access-Control-Allow-Headers"] = "Content-Type";
        res.Headers["Access-Control-Allow-Private-Network"] = "true";
    }

    static async Task WriteJson(HttpListenerResponse res, int code, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        res.StatusCode = code;
        res.ContentType = "application/json; charset=utf-8";
        res.ContentLength64 = bytes.Length;
        await res.OutputStream.WriteAsync(bytes);
        res.Close();
    }

    public void Dispose()
    {
        try
        {
            _http.Stop();
        }
        catch
        {
            // already stopped
        }

        _http.Close();
    }
}
