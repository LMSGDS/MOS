using System.Text.Json;

namespace MosDock;

readonly record struct MosProject(
    string Id,
    string Title,
    string Skill,
    string Filename,
    int TimeLimitSec,
    string[] Steps,
    string RubricVersion);

/// <summary>
/// Tải đề MOS từ PostgreSQL API, mở trên Office máy, nộp bài + telemetry.
/// </summary>
static class ExamHub
{
    static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

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
                steps,
                p.TryGetProperty("rubric_version", out var rv) ? rv.GetString() ?? "" : ""));
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
            var dir = Path.Combine(ExamSession.DataDir, "attempts", Guid.NewGuid().ToString("n"), chosen.Id);
            Directory.CreateDirectory(dir);
            var local = Path.Combine(dir, string.IsNullOrWhiteSpace(chosen.Filename) ? chosen.Id + ".bin" : chosen.Filename);
            await File.WriteAllBytesAsync(local, bytes);
            try
            {
                using var rubricDoc = await Portal.GetJsonAsync($"/api/v1/projects/{Uri.EscapeDataString(chosen.Id)}/rubric");
                if (rubricDoc.RootElement.TryGetProperty("rubric", out var rubricEl))
                {
                    var rubricPath = Path.Combine(dir, "rubric.json");
                    await File.WriteAllTextAsync(rubricPath, rubricEl.GetRawText());
                    ExamSession.Rubric = JsonSerializer.Deserialize<JsonRubric>(rubricEl.GetRawText(), JsonOpts);
                }
            }
            catch
            {
                ExamSession.Rubric = null;
            }

            using var started = await Portal.PostJsonAsync("/api/v1/attempts", new
            {
                project_id = chosen.Id,
                mode = ExamSession.Mode,
            });
            ExamSession.ProjectId = chosen.Id;
            ExamSession.AttemptId = started.RootElement.GetProperty("attempt_id").GetString();
            ExamSession.LocalPath = local;
            ExamSession.RubricVersion = chosen.RubricVersion;
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
            var scoreEl = submitted.RootElement.GetProperty("score");
            var score = scoreEl.TryGetProperty("verified", out var ver) && ver.TryGetDouble(out var v)
                ? v
                : scoreEl.GetProperty("score").GetDouble();
            var pending = scoreEl.TryGetProperty("pending", out var pe) && pe.TryGetDouble(out var p) ? p : 0;
            var max = scoreEl.TryGetProperty("max_score", out var mx) && mx.TryGetDouble(out var m) ? m : 100;
            return (true, $"Đã nộp · {score}/{max} đã xác minh" + (pending > 0 ? $" · {pending} chưa xác minh" : ""));
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

    public static async Task<(bool Ok, string Summary, IReadOnlyList<LocalCriterion> Criteria)> CheckTasksAsync(string program)
    {
        if (string.IsNullOrWhiteSpace(ExamSession.AttemptId) || string.IsNullOrWhiteSpace(ExamSession.LocalPath))
        {
            return (false, "Chưa mở đề MOS.", []);
        }

        var path = OfficeCapture.SaveActive(program);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return (false, "Không lưu được đúng tài liệu bài thi. Đóng tệp Office khác rồi thử lại.", []);
        }

        var snapDir = Path.Combine(Path.GetDirectoryName(ExamSession.LocalPath)!, "snapshots");
        Directory.CreateDirectory(snapDir);
        var snap = Path.Combine(snapDir, DateTime.UtcNow.ToString("yyyyMMddHHmmss") + Path.GetExtension(path));
        File.Copy(path, snap, overwrite: true);

        var local = WordGrade.Evaluate(snap, ExamSession.Rubric);
        try
        {
            using var posted = await Portal.PostFileAsync(
                $"/api/v1/attempts/{ExamSession.AttemptId}/checkpoints",
                snap);
            var root = posted.RootElement;
            if (root.TryGetProperty("score", out var scoreEl))
            {
                var criteria = ParseCriteria(scoreEl);
                var verified = GetDouble(scoreEl, "verified", GetDouble(scoreEl, "score", local.Verified));
                var pending = GetDouble(scoreEl, "pending", local.Pending);
                var summary = $"{verified}/100 đã xác minh · {pending} chưa xác minh (Find/Go To chưa ghi nhận).";
                return (true, summary, criteria.Count > 0 ? criteria : local.Criteria);
            }

            var hidden = root.TryGetProperty("message", out var msg) ? msg.GetString() : "Đã lưu checkpoint.";
            return (true, hidden ?? "Đã lưu checkpoint.", local.Criteria);
        }
        catch (Exception ex)
        {
            var summary = $"{local.Verified}/100 đã xác minh trên máy · {local.Pending} chưa xác minh. Chưa gửi máy chủ: {ex.Message}";
            return (true, summary, local.Criteria);
        }
    }

    static List<LocalCriterion> ParseCriteria(JsonElement scoreEl)
    {
        var list = new List<LocalCriterion>();
        if (!scoreEl.TryGetProperty("criteria", out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var c in arr.EnumerateArray())
        {
            list.Add(new LocalCriterion(
                c.TryGetProperty("criterion_id", out var id) ? id.GetString() ?? "" : "",
                c.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "",
                GetDouble(c, "earned", 0),
                GetDouble(c, "possible", 0),
                c.TryGetProperty("message", out var m) ? m.GetString() ?? "" : ""));
        }

        return list;
    }

    static double GetDouble(JsonElement el, string name, double fallback)
    {
        if (el.TryGetProperty(name, out var p) && p.TryGetDouble(out var v))
        {
            return v;
        }

        return fallback;
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
