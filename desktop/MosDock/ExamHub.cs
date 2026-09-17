using System.Text.Json;

namespace MosDock;

readonly record struct MosProject(
    string Id,
    string Title,
    string Skill,
    string Filename,
    int TimeLimitSec,
    string[] Steps);

/// <summary>
/// Tải đề MOS từ PostgreSQL API, mở trên Office máy, nộp bài + telemetry.
/// </summary>
static class ExamHub
{
    public static async Task<IReadOnlyList<MosProject>> ListProjectsAsync(string program)
    {
        using var list = await Portal.GetJsonAsync("/api/v1/projects?program=" + Uri.EscapeDataString(program));
        var items = new List<MosProject>();
        foreach (var p in list.RootElement.GetProperty("projects").EnumerateArray())
        {
            var steps = Array.Empty<string>();
            if (p.TryGetProperty("steps", out var raw) && raw.ValueKind == JsonValueKind.Array)
            {
                steps = raw.EnumerateArray()
                    .Select(x => x.GetString() ?? "")
                    .Where(x => x.Length > 0)
                    .ToArray();
            }

            items.Add(new MosProject(
                p.GetProperty("id").GetString() ?? "",
                p.GetProperty("title").GetString() ?? "",
                p.TryGetProperty("skill_domain", out var skill) ? skill.GetString() ?? "" : "",
                p.TryGetProperty("filename", out var fn) ? fn.GetString() ?? "" : "",
                p.TryGetProperty("time_limit_sec", out var tl) && tl.TryGetInt32(out var sec) ? sec : 1800,
                steps));
        }

        return items;
    }

    public static async Task<(bool Ok, string Message)> StartProjectAsync(string program, string? projectId = null)
    {
        try
        {
            var projects = await ListProjectsAsync(program);
            var chosen = string.IsNullOrWhiteSpace(projectId)
                ? projects.FirstOrDefault()
                : projects.FirstOrDefault(p => p.Id == projectId);
            if (string.IsNullOrWhiteSpace(chosen.Id))
            {
                return (false, "Không có đề MOS trên máy chủ.");
            }

            var bytes = await Portal.GetBytesAsync($"/api/v1/projects/{Uri.EscapeDataString(chosen.Id)}/file");
            var dir = Path.Combine(ExamSession.DataDir, "projects", chosen.Id);
            Directory.CreateDirectory(dir);
            var local = Path.Combine(dir, string.IsNullOrWhiteSpace(chosen.Filename) ? chosen.Id + ".bin" : chosen.Filename);
            await File.WriteAllBytesAsync(local, bytes);

            using var started = await Portal.PostJsonAsync("/api/v1/attempts", new
            {
                project_id = chosen.Id,
                mode = ExamSession.Mode,
            });
            ExamSession.ProjectId = chosen.Id;
            ExamSession.AttemptId = started.RootElement.GetProperty("attempt_id").GetString();
            ExamSession.LocalPath = local;
            await TrackAsync("open", new { file = chosen.Filename, program });
            WordWindow.Launch(program, local);
            return (true, chosen.Title);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public static async Task<(bool Ok, string Message)> SubmitAsync(string program)
    {
        if (string.IsNullOrWhiteSpace(ExamSession.AttemptId))
        {
            return (false, "Chưa mở đề MOS.");
        }

        var path = OfficeCapture.SaveActive(program) ?? ExamSession.LocalPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return (false, "Không tìm thấy tệp bài làm trên máy.");
        }

        try
        {
            await FlushOrQueueAsync();
            using var submitted = await Portal.PostFileAsync(
                $"/api/v1/attempts/{ExamSession.AttemptId}/submit",
                path);
            var score = submitted.RootElement.GetProperty("score").GetProperty("score").GetDouble();
            return (true, $"Đã nộp · điểm {score}");
        }
        catch (Exception ex)
        {
            OfflineQueue.Enqueue(ExamSession.AttemptId!, new
            {
                events = new object[] { new { skill = "", action = "submit-offline", detail = new { error = ex.Message } } },
            });
            return (false, "Chưa gửi được, đã xếp hàng đợi: " + ex.Message);
        }
    }

    public static async Task TrackAsync(string action, object? detail = null)
    {
        if (string.IsNullOrWhiteSpace(ExamSession.AttemptId))
        {
            return;
        }

        var payload = new
        {
            events = new object[]
            {
                new { skill = "", action, detail = detail ?? new { } },
            },
        };
        try
        {
            using var posted = await Portal.PostJsonAsync(
                $"/api/v1/attempts/{ExamSession.AttemptId}/telemetry",
                payload);
            await OfflineQueue.FlushAsync();
        }
        catch
        {
            OfflineQueue.Enqueue(ExamSession.AttemptId, payload);
        }
    }

    static async Task FlushOrQueueAsync()
    {
        try
        {
            await OfflineQueue.FlushAsync();
        }
        catch
        {
            // giữ hàng đợi
        }
    }
}
