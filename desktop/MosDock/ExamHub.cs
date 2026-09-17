using System.Text.Json;

namespace MosDock;

readonly record struct MosProject(
    string Id,
    string Title,
    string Program,
    string Skill,
    string Filename,
    int TimeLimitSec,
    string[] Steps,
    string RubricVersion);

readonly record struct MosAttempt(
    string Id,
    string ProjectId,
    string Title,
    string Program,
    string Filename,
    string Mode,
    string Status,
    double? Score,
    double? Pending,
    double MaxScore,
    string StartedAt,
    string? SubmittedAt);

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

    public static async Task<IReadOnlyList<MosProject>> ListProjectsAsync(string? program = null)
    {
        var path = string.IsNullOrWhiteSpace(program)
            ? "/api/v1/projects"
            : "/api/v1/projects?program=" + Uri.EscapeDataString(program);
        using var list = await Portal.GetJsonAsync(path);
        var items = new List<MosProject>();
        foreach (var p in list.RootElement.GetProperty("projects").EnumerateArray())
        {
            items.Add(ReadProject(p));
        }

        return items;
    }

    static MosProject ReadProject(JsonElement p)
    {
        var steps = Array.Empty<string>();
        if (p.TryGetProperty("steps", out var raw) && raw.ValueKind == JsonValueKind.Array)
        {
            steps = raw.EnumerateArray()
                .Select(x => x.GetString() ?? "")
                .Where(x => x.Length > 0)
                .ToArray();
        }

        return new MosProject(
            p.GetProperty("id").GetString() ?? "",
            p.GetProperty("title").GetString() ?? "",
            p.TryGetProperty("program", out var prog) ? prog.GetString() ?? "word" : "word",
            p.TryGetProperty("skill_domain", out var skill) ? skill.GetString() ?? "" : "",
            p.TryGetProperty("filename", out var fn) ? fn.GetString() ?? "" : "",
            p.TryGetProperty("time_limit_sec", out var tl) && tl.TryGetInt32(out var sec) ? sec : 1800,
            steps,
            p.TryGetProperty("rubric_version", out var rv) ? rv.GetString() ?? "" : "");
    }

    public static async Task<IReadOnlyList<MosAttempt>> ListAttemptsAsync()
    {
        using var doc = await Portal.GetJsonAsync("/api/v1/attempts");
        var items = new List<MosAttempt>();
        if (!doc.RootElement.TryGetProperty("attempts", out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return items;
        }

        foreach (var a in arr.EnumerateArray())
        {
            items.Add(new MosAttempt(
                a.GetProperty("id").GetString() ?? "",
                a.TryGetProperty("project_id", out var pid) ? pid.GetString() ?? "" : "",
                a.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "",
                a.TryGetProperty("program", out var prog) ? prog.GetString() ?? "word" : "word",
                a.TryGetProperty("filename", out var fn) ? fn.GetString() ?? "" : "",
                a.TryGetProperty("mode", out var mode) ? mode.GetString() ?? "training" : "training",
                a.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "",
                GetDoubleOrNull(a, "verified_score") ?? GetDoubleOrNull(a, "score"),
                GetDoubleOrNull(a, "pending_score"),
                GetDoubleOrNull(a, "max_score") ?? 100,
                FormatTime(a, "started_at"),
                a.TryGetProperty("submitted_at", out var sub) && sub.ValueKind is JsonValueKind.String
                    ? sub.GetString()
                    : null));
        }

        return items;
    }

    public static async Task<(bool Ok, string Message)> StartProjectAsync(string program, string projectId, string mode)
    {
        try
        {
            var projects = await ListProjectsAsync(program);
            var chosen = projects.FirstOrDefault(p => p.Id == projectId);
            if (string.IsNullOrWhiteSpace(chosen.Id))
            {
                return (false, "Không có đề MOS trên máy chủ.");
            }

            ExamSession.Mode = mode is "testing" ? "testing" : "training";
            ExamSession.Program = program;
            using var started = await Portal.PostJsonAsync("/api/v1/attempts", new
            {
                project_id = chosen.Id,
                mode = ExamSession.Mode,
            });
            var attemptId = started.RootElement.GetProperty("attempt_id").GetString() ?? Guid.NewGuid().ToString("n");
            var dir = Path.Combine(ExamSession.DataDir, "attempts", attemptId);
            Directory.CreateDirectory(dir);
            var bytes = await Portal.GetBytesAsync($"/api/v1/projects/{Uri.EscapeDataString(chosen.Id)}/file");
            var local = Path.Combine(dir, string.IsNullOrWhiteSpace(chosen.Filename) ? chosen.Id + ".bin" : chosen.Filename);
            await File.WriteAllBytesAsync(local, bytes);
            await LoadRubricAsync(chosen.Id, dir);
            BindSession(chosen, attemptId, local);
            WriteMeta(dir);
            await TrackAsync("open", new { file = chosen.Filename, program });
            WordWindow.Launch(program, local);
            return (true, chosen.Title);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public static async Task<(bool Ok, string Message)> ResumeAttemptAsync(MosAttempt attempt)
    {
        try
        {
            var dir = Path.Combine(ExamSession.DataDir, "attempts", attempt.Id);
            Directory.CreateDirectory(dir);
            var filename = string.IsNullOrWhiteSpace(attempt.Filename) ? attempt.ProjectId + ".bin" : attempt.Filename;
            var local = Path.Combine(dir, filename);
            if (!File.Exists(local))
            {
                var bytes = await Portal.GetBytesAsync($"/api/v1/projects/{Uri.EscapeDataString(attempt.ProjectId)}/file");
                await File.WriteAllBytesAsync(local, bytes);
            }

            var projects = await ListProjectsAsync(attempt.Program);
            var chosen = projects.FirstOrDefault(p => p.Id == attempt.ProjectId);
            await LoadRubricAsync(attempt.ProjectId, dir);
            ExamSession.Mode = attempt.Mode is "testing" ? "testing" : "training";
            ExamSession.Program = attempt.Program;
            BindSession(
                string.IsNullOrWhiteSpace(chosen.Id)
                    ? new MosProject(attempt.ProjectId, attempt.Title, attempt.Program, "", filename, 1800, [], "")
                    : chosen,
                attempt.Id,
                local);
            WriteMeta(dir);
            WordWindow.Launch(attempt.Program, local);
            return (true, attempt.Title);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    static void BindSession(MosProject chosen, string attemptId, string local)
    {
        ExamSession.ProjectId = chosen.Id;
        ExamSession.ProjectTitle = chosen.Title;
        ExamSession.Steps = chosen.Steps;
        ExamSession.AttemptId = attemptId;
        ExamSession.LocalPath = local;
        ExamSession.RubricVersion = chosen.RubricVersion;
        ExamSession.Program = chosen.Program;
    }

    static async Task LoadRubricAsync(string projectId, string dir)
    {
        try
        {
            using var rubricDoc = await Portal.GetJsonAsync($"/api/v1/projects/{Uri.EscapeDataString(projectId)}/rubric");
            if (rubricDoc.RootElement.TryGetProperty("rubric", out var rubricEl))
            {
                await File.WriteAllTextAsync(Path.Combine(dir, "rubric.json"), rubricEl.GetRawText());
                ExamSession.Rubric = JsonSerializer.Deserialize<JsonRubric>(rubricEl.GetRawText(), JsonOpts);
            }
        }
        catch
        {
            ExamSession.Rubric = null;
        }
    }

    static void WriteMeta(string dir)
    {
        try
        {
            File.WriteAllText(Path.Combine(dir, "meta.json"), JsonSerializer.Serialize(new
            {
                attempt_id = ExamSession.AttemptId,
                project_id = ExamSession.ProjectId,
                program = ExamSession.Program,
                title = ExamSession.ProjectTitle,
                mode = ExamSession.Mode,
                local_path = ExamSession.LocalPath,
            }));
        }
        catch
        {
            // local cache only
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
            return (true, $"{score}/{max} đã xác minh" + (pending > 0 ? $" · {pending} chưa xác minh" : ""));
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

    public static void SaveInPlace(string program)
    {
        OfficeCapture.SaveActive(program);
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
                var summary = $"{verified}/100 đã xác minh · {pending} chưa xác minh.";
                return (true, summary, criteria.Count > 0 ? criteria : local.Criteria);
            }

            var hidden = root.TryGetProperty("message", out var msg) ? msg.GetString() : "Đã lưu bài.";
            return (true, hidden ?? "Đã lưu bài.", local.Criteria);
        }
        catch (Exception ex)
        {
            var summary = $"{local.Verified}/100 đã xác minh trên máy · {local.Pending} chưa xác minh. {ex.Message}";
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

    static double? GetDoubleOrNull(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var p) && p.ValueKind is JsonValueKind.Number && p.TryGetDouble(out var v))
        {
            return v;
        }

        return null;
    }

    static string FormatTime(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.String)
        {
            return "";
        }

        var raw = p.GetString() ?? "";
        return DateTimeOffset.TryParse(raw, out var dt) ? dt.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : raw;
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
