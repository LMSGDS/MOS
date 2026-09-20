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

    public static async Task<(bool Ok, string Kind, string? Error, string Name, string App, string? Token)> LoginAsync(
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
        catch (Exception)
        {
            return (false, "status", StatusBusy, "", app, null);
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
                    var (kind, message) = ClassifyLogin(resp.StatusCode, ReadLoginDetail(root));
                    return (false, kind, message, "", app, null);
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
                return (true, "", null, name, app, token);
            }
            catch (JsonException)
            {
                var (kind, message) = ClassifyLogin(resp.StatusCode, text);
                return (false, kind, message, "", app, null);
            }
        }
    }

    public const string AuthWrong = "Tài khoản hoặc mật khẩu không chính xác. Vui lòng thử lại.";
    public const string StatusDenied = "Tài khoản của bạn chưa được cấp quyền thi môn này. Vui lòng liên hệ giám thị hoặc giáo viên Tin học để được hỗ trợ.";
    public const string StatusBusy = "Hệ thống đang bận. Em chờ giây lát rồi thử lại, hoặc báo giám thị / giáo viên Tin học.";

    public static (string Kind, string Message) ClassifyLogin(HttpStatusCode status, string? detail)
    {
        var code = (int)status;
        var d = (detail ?? "").Trim().ToLowerInvariant();
        if (code >= 500 || d.Contains("timeout") || d.Contains("connect") || d.Contains("network"))
        {
            return ("status", StatusBusy);
        }

        if (code == 403
            || d.Contains("forbidden")
            || d.Contains("lock")
            || d.Contains("khoa")
            || d.Contains("inactive")
            || d.Contains("disabled")
            || d.Contains("chua")
            || d.Contains("denied"))
        {
            return ("status", StatusDenied);
        }

        if (code == 401 || d is "sai" or "user" or "token" || d.Contains("sai"))
        {
            return ("auth", AuthWrong);
        }

        if (code == 0)
        {
            return ("status", StatusBusy);
        }

        return ("auth", AuthWrong);
    }

    static string ReadLoginDetail(JsonElement root)
    {
        if (root.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.String)
        {
            return err.GetString() ?? "";
        }

        if (root.TryGetProperty("detail", out var detail))
        {
            if (detail.ValueKind == JsonValueKind.String)
            {
                return detail.GetString() ?? "";
            }

            if (detail.ValueKind == JsonValueKind.Object && detail.TryGetProperty("msg", out var msg))
            {
                return msg.GetString() ?? "";
            }
        }

        return "";
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

        if (!string.IsNullOrWhiteSpace(ExamSession.Bank.VersionHash))
        {
            headers["X-MOS-Bank-Hash"] = ExamSession.Bank.VersionHash;
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
        var bytes = LockedFile.ReadAllBytes(filePath);
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
