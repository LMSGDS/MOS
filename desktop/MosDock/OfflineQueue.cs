using System.Text.Json;

namespace MosDock;

/// <summary>
/// Hàng đợi telemetry store-and-forward khi GPU/mạng chưa sẵn sàng.
/// </summary>
static class OfflineQueue
{
    static string Dir => Path.Combine(ExamSession.DataDir, "queue");

    public static void Enqueue(string attemptId, object payload)
    {
        Directory.CreateDirectory(Dir);
        var name = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.json";
        var path = Path.Combine(Dir, name);
        var json = JsonSerializer.Serialize(new { attempt_id = attemptId, payload });
        File.WriteAllText(path, json);
    }

    public static async Task FlushAsync()
    {
        if (!Directory.Exists(Dir) || string.IsNullOrWhiteSpace(Portal.Token))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(Dir, "*.json"))
        {
            try
            {
                using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(file));
                var root = doc.RootElement;
                var attemptId = root.GetProperty("attempt_id").GetString();
                if (string.IsNullOrWhiteSpace(attemptId))
                {
                    File.Delete(file);
                    continue;
                }

                using var posted = await Portal.PostJsonAsync(
                    $"/api/v1/attempts/{attemptId}/telemetry",
                    root.GetProperty("payload"));
                File.Delete(file);
            }
            catch
            {
                // giữ file, gửi lại sau
            }
        }
    }
}
