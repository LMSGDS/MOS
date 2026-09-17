using System.Net;
using System.Text;
using System.Text.Json;

namespace MosDock;

static class Portal
{
    public static string Origin =>
        Environment.GetEnvironmentVariable("MOS_PORTAL")?.TrimEnd('/')
        ?? "https://mos.gds.edu.vn";

    public static CookieContainer Cookies { get; } = new();

    static readonly HttpClient Http = CreateClient();

    static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler
        {
            CookieContainer = Cookies,
            UseCookies = true,
        };
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "MOS-KulKul/1.1");
        return http;
    }

    public static async Task<(bool Ok, string? Error, string Name, string App)> LoginAsync(
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
        using var resp = await Http.PostAsync(Origin + "/api/dang-nhap", content);
        var text = await resp.Content.ReadAsStringAsync();
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            var ok = root.TryGetProperty("ok", out var okEl) && okEl.GetBoolean();
            if (!ok)
            {
                return (false, "Tên đăng nhập hoặc mật khẩu không đúng.", "", app);
            }

            var name = username;
            if (root.TryGetProperty("user", out var user) && user.TryGetProperty("name", out var n))
            {
                name = n.GetString() ?? username;
            }

            if (root.TryGetProperty("program", out var program) && program.TryGetProperty("id", out var id))
            {
                app = id.GetString() ?? app;
            }

            return (true, null, name, app);
        }
        catch (Exception ex)
        {
            return (false, "Không kết nối được MOS-KulKul: " + ex.Message, "", app);
        }
    }
}
