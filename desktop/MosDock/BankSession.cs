using System.Text.Json;

namespace MosDock;

sealed class BankTask
{
    public string TaskId { get; init; } = "";
    public string Instruction { get; init; } = "";
    public string[] HintTiers { get; init; } = [];
}

sealed class BankProjectBlock
{
    public string ProjectId { get; init; } = "";
    public string SourceProjectId { get; init; } = "";
    public string Title { get; init; } = "";
    public int Order { get; init; }
    public List<BankTask> Tasks { get; init; } = [];
}

/// <summary>JSON ngân hàng đề từ POST /api/v1/attempts — luyện tập vs Certiport mock.</summary>
sealed class BankPayload
{
    public bool Hints { get; init; } = true;
    public bool ElapsedOnly { get; init; } = true;
    public bool FocusLock { get; init; }
    public bool ForceSubmit { get; init; }
    public bool MarkForReview { get; init; }
    public bool RestartProject { get; init; }
    public bool CertiportSplit { get; init; }
    public int? DurationMinutes { get; init; }
    public int CutScore { get; init; } = 700;
    public int ScoreScale { get; init; } = 1000;
    public string ExamId { get; init; } = "";
    public string VersionHash { get; init; } = "";
    public string Ui { get; init; } = "practice";
    public List<BankProjectBlock> Projects { get; init; } = [];
    public DateTime? ServerStartedUtc { get; set; }
    public int? RemainingSec { get; set; }
    public int? TimeLimitSec { get; set; }

    public static BankPayload FromMode(string mode) =>
        mode == "testing"
            ? new BankPayload
            {
                Hints = false,
                ElapsedOnly = false,
                FocusLock = true,
                ForceSubmit = true,
                MarkForReview = true,
                RestartProject = true,
                CertiportSplit = true,
                DurationMinutes = 50,
                Ui = "certiport_split",
            }
            : new BankPayload();

    public static BankPayload Parse(JsonElement root, string fallbackMode)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return FromMode(fallbackMode);
        }

        var hints = GetBool(root, "hints", fallbackMode != "testing");
        var ui = GetString(root, "ui");
        var nav = root.TryGetProperty("navigation", out var navEl) && navEl.ValueKind == JsonValueKind.Object
            ? navEl
            : default;
        var projects = new List<BankProjectBlock>();
        if (root.TryGetProperty("projects", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in arr.EnumerateArray())
            {
                var tasks = new List<BankTask>();
                if (p.TryGetProperty("tasks", out var tarr) && tarr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var t in tarr.EnumerateArray())
                    {
                        tasks.Add(new BankTask
                        {
                            TaskId = GetString(t, "task_id"),
                            Instruction = GetString(t, "instruction"),
                            HintTiers = StringArray(t, "hint_tiers"),
                        });
                    }
                }

                projects.Add(new BankProjectBlock
                {
                    ProjectId = GetString(p, "project_id"),
                    SourceProjectId = GetString(p, "source_project_id"),
                    Title = GetString(p, "title"),
                    Order = GetInt(p, "order", projects.Count + 1),
                    Tasks = tasks,
                });
            }
        }

        return new BankPayload
        {
            Hints = hints,
            ElapsedOnly = GetBool(root, "elapsed_only", hints),
            FocusLock = GetBool(root, "focus_lock", !hints),
            ForceSubmit = GetBool(root, "force_submit", !hints),
            MarkForReview = nav.ValueKind == JsonValueKind.Object
                ? GetBool(nav, "mark_for_review", !hints)
                : !hints,
            RestartProject = nav.ValueKind == JsonValueKind.Object && GetBool(nav, "restart_project", !hints),
            CertiportSplit = string.Equals(ui, "certiport_split", StringComparison.OrdinalIgnoreCase) || !hints,
            DurationMinutes = GetIntOrNull(root, "duration_minutes"),
            CutScore = GetInt(root, "cut_score", 700),
            ScoreScale = GetInt(root, "score_scale", 1000),
            ExamId = GetString(root, "exam_id"),
            VersionHash = GetString(root, "version_hash"),
            Ui = string.IsNullOrWhiteSpace(ui) ? (hints ? "practice" : "certiport_split") : ui,
            Projects = projects,
        };
    }

    public string[] HintTiers(int taskIndex, string? projectId)
    {
        BankProjectBlock? block = null;
        if (Projects.Count > 0)
        {
            block = Projects.FirstOrDefault(p =>
                string.Equals(p.SourceProjectId, projectId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(p.ProjectId, projectId, StringComparison.OrdinalIgnoreCase));
            block ??= Projects[0];
            if (taskIndex >= 0 && taskIndex < block.Tasks.Count && block.Tasks[taskIndex].HintTiers.Length > 0)
            {
                return block.Tasks[taskIndex].HintTiers;
            }
        }

        return [];
    }

    public string ProjectCaption(string? projectId)
    {
        if (Projects.Count == 0)
        {
            return "";
        }

        var idx = Projects.FindIndex(p =>
            string.Equals(p.SourceProjectId, projectId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(p.ProjectId, projectId, StringComparison.OrdinalIgnoreCase));
        if (idx < 0)
        {
            idx = 0;
        }

        return $"Project {idx + 1} of {Projects.Count}";
    }

    static bool GetBool(JsonElement el, string name, bool fallback)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var p))
        {
            return fallback;
        }

        return p.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => fallback,
        };
    }

    static string GetString(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.String)
        {
            return "";
        }

        return p.GetString() ?? "";
    }

    static int GetInt(JsonElement el, string name, int fallback)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var p) || !p.TryGetInt32(out var n))
        {
            return fallback;
        }

        return n;
    }

    static int? GetIntOrNull(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var p) || !p.TryGetInt32(out var n))
        {
            return null;
        }

        return n;
    }

    static string[] StringArray(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return p.EnumerateArray()
            .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() ?? "" : "")
            .Where(s => s.Length > 0)
            .ToArray();
    }
}
