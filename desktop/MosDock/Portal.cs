using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MosDock;

static class Portal
{
    public static string Origin =>
        Environment.GetEnvironmentVariable("MOS_PORTAL")?.TrimEnd('/')
        ?? "https://mos.gds.edu.vn";

    public static CookieContainer Cookies { get; } = new();

    public static string? Token { get; set; }

    static readonly HttpClient Http = CreateClient();

    static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler
        {
            CookieContainer = Cookies,
            UseCookies = true,
        };
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "MOS-KulKul/1.4");
        return http;
    }

    static void ApplyAuth()
    {
        Http.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(Token)
            ? null
            : new AuthenticationHeaderValue("Bearer", Token);
    }

    public static async Task<(bool Ok, string? Error, string Name, string App, string? Token)> LoginAsync(
        string username,
        string password,
        string app)
    {
        var payload = JsonSerializer.Serialize(new
        {
            username,
            password,
            chuong_trinh = app,
        });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        HttpResponseMessage resp;
        try
        {
            resp = await Http.PostAsync(Origin + "/api/v1/auth/login", content);
        }
        catch (Exception ex)
        {
            return (false, "Không kết nối được MOS-KulKul: " + ex.Message, "", app, null);
        }

        using (resp)
        {
            var text = await resp.Content.ReadAsStringAsync();
            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                var ok = root.TryGetProperty("ok", out var okEl) && okEl.GetBoolean();
                if (!ok || resp.StatusCode != HttpStatusCode.OK)
                {
                    return (false, "Tên đăng nhập hoặc mật khẩu không đúng.", "", app, null);
                }

                var name = username;
                if (root.TryGetProperty("user", out var user) && user.TryGetProperty("name", out var n))
                {
                    name = n.GetString() ?? username;
                }

                if (root.TryGetProperty("program", out var program))
                {
                    if (program.ValueKind == JsonValueKind.String)
                    {
                        app = program.GetString() ?? app;
                    }
                    else if (program.TryGetProperty("id", out var id))
                    {
                        app = id.GetString() ?? app;
                    }
                }

                var token = root.TryGetProperty("token", out var tok) ? tok.GetString() : null;
                Token = token;
                ApplyAuth();
                return (true, null, name, app, token);
            }
            catch (Exception ex)
            {
                return (false, "Không kết nối được MOS-KulKul: " + ex.Message, "", app, null);
            }
        }
    }

    public static async Task<JsonDocument> GetJsonAsync(string path)
    {
        ApplyAuth();
        using var resp = await Http.GetAsync(Origin + path);
        var text = await resp.Content.ReadAsStringAsync();
        resp.EnsureSuccessStatusCode();
        return JsonDocument.Parse(text);
    }

    public static async Task<byte[]> GetBytesAsync(string path)
    {
        ApplyAuth();
        using var resp = await Http.GetAsync(Origin + path);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsByteArrayAsync();
    }

    public static Dictionary<string, string> SyncHeaders(string? filePath = null, string? updatedAt = null)
    {
        var headers = new Dictionary<string, string>
        {
            ["X-MOS-Updated-At"] = string.IsNullOrWhiteSpace(updatedAt) ? LocalExamStore.NowIso() : updatedAt,
        };
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            headers["X-MOS-Artifact-SHA256"] = LocalExamStore.Sha256File(filePath);
        }

        return headers;
    }

    public static async Task<JsonDocument> PostJsonAsync(
        string path,
        object body,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        ApplyAuth();
        var payload = JsonSerializer.Serialize(body);
        using var req = new HttpRequestMessage(HttpMethod.Post, Origin + path);
        req.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        ApplyExtra(req, headers);
        using var resp = await Http.SendAsync(req);
        var text = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"MOS {resp.StatusCode}: {text}");
        }

        return JsonDocument.Parse(text);
    }

    public static async Task<JsonDocument> PostFileAsync(
        string path,
        string filePath,
        IReadOnlyDictionary<string, string>? fields = null,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        ApplyAuth();
        using var form = new MultipartFormDataContent();
        var bytes = await File.ReadAllBytesAsync(filePath);
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", Path.GetFileName(filePath));
        if (fields is not null)
        {
            foreach (var kv in fields)
            {
                form.Add(new StringContent(kv.Value), kv.Key);
            }
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, Origin + path);
        req.Content = form;
        ApplyExtra(req, headers);
        using var resp = await Http.SendAsync(req);
        var text = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"MOS {resp.StatusCode}: {text}");
        }

        return JsonDocument.Parse(text);
    }

    static void ApplyExtra(HttpRequestMessage req, IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null)
        {
            return;
        }

        foreach (var kv in headers)
        {
            req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
        }
    }
}
