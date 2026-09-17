using System.Text.Json;

namespace MosDock;

/// <summary>
/// Tải đề MOS từ PostgreSQL API, mở trên Office máy, nộp bài + telemetry.
/// </summary>
static class ExamHub
{
    public static async Task<(bool Ok, string Message)> StartProjectAsync(string program, string? projectId = null)
    {
        try
        {
            using var list = await Portal.GetJsonAsync("/api/v1/projects?program=" + Uri.EscapeDataString(program));
            JsonElement chosen = default;
            var found = false;
            foreach (var p in list.RootElement.GetProperty("projects").EnumerateArray())
            {
                var id = p.GetProperty("id").GetString();
                if (!string.IsNullOrWhiteSpace(projectId) && id != projectId)
                {
                    continue;
                }

                chosen = p;
                found = true;
                if (string.IsNullOrWhiteSpace(projectId) || id == projectId)
                {
                    break;
                }
            }

            if (!found)
            {
                return (false, "Không có đề MOS trên máy chủ.");
            }

            var pid = chosen.GetProperty("id").GetString()!;
            var filename = chosen.GetProperty("filename").GetString() ?? (pid + ".bin");
            var bytes = await Portal.GetBytesAsync($"/api/v1/projects/{Uri.EscapeDataString(pid)}/file");
            var dir = Path.Combine(ExamSession.DataDir, "projects", pid);
            Directory.CreateDirectory(dir);
            var local = Path.Combine(dir, filename);
            await File.WriteAllBytesAsync(local, bytes);

            using var started = await Portal.PostJsonAsync("/api/v1/attempts", new
            {
                project_id = pid,
                mode = ExamSession.Mode,
            });
            ExamSession.ProjectId = pid;
            ExamSession.AttemptId = started.RootElement.GetProperty("attempt_id").GetString();
            ExamSession.LocalPath = local;
            await TrackAsync("open", new { file = filename, program });
            WordWindow.Launch(program, local);
            return (true, chosen.GetProperty("title").GetString() ?? pid);
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
