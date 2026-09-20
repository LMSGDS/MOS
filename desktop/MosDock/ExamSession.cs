using System.Text.Json;

namespace MosDock;

readonly record struct ReviewMark(string ProjectId, int TaskIndex);

static class ExamSession
{
    public static string Mode { get; set; } = "training";
    public static string DisplayName { get; set; } = "";
    public static string Program { get; set; } = "word";
    public static string? ProjectId { get; set; }
    public static string? ProjectTitle { get; set; }
    public static string[] Steps { get; set; } = [];
    public static string? AttemptId { get; set; }
    public static string? LocalPath { get; set; }
    public static string? RubricVersion { get; set; }
    public static JsonRubric? Rubric { get; set; }
    public static IReadOnlyList<LocalCriterion> LastCheck { get; set; } = [];
    public static DateTime OpenedUtc { get; set; } = DateTime.UtcNow;
    public static BankPayload Bank { get; set; } = BankPayload.FromMode("training");
    public static int FocusStrikes { get; set; }
    public static HashSet<ReviewMark> MarkedTasks { get; } = [];
    public static HashSet<ReviewMark> CompletedTasks { get; } = [];
    public static HashSet<ReviewMark> ViewedTasks { get; } = [];
    public static int HintTier { get; set; }
    public static string HardStopReason { get; set; } = "";

    public static bool HintsAllowed => Bank.Hints && Mode != "testing";
    public static bool HideLiveScore => Mode == "testing";
    public static bool HideNumericScore => true;

    public static string DataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MOS",
        "KulKul");

    public static void BindBank(JsonElement root, string mode)
    {
        var prevExam = Bank.ExamId;
        var bankEl = root.TryGetProperty("bank", out var raw) ? raw : root;
        Bank = BankPayload.Parse(bankEl, mode);
        if (root.TryGetProperty("time_limit_sec", out var tl) && tl.TryInt(out var sec) && sec > 0)
        {
            Bank.TimeLimitSec = sec;
        }

        if (root.TryGetProperty("remaining_sec", out var rem) && rem.TryInt(out var left))
        {
            Bank.RemainingSec = left;
        }

        if (root.TryGetProperty("started_at", out var sa) && sa.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(sa.GetString(), out var started))
        {
            Bank.ServerStartedUtc = started.UtcDateTime;
            OpenedUtc = started.UtcDateTime;
        }

        if (root.TryGetProperty("clock", out var clock) && clock.ValueKind == JsonValueKind.Object)
        {
            if (clock.TryGetProperty("remaining_sec", out var cr) && cr.TryInt(out var cleft))
            {
                Bank.RemainingSec = cleft;
            }

            if (clock.TryGetProperty("started_at", out var cs) && cs.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(cs.GetString(), out var cstart))
            {
                Bank.ServerStartedUtc = cstart.UtcDateTime;
                OpenedUtc = cstart.UtcDateTime;
            }
        }

        HintTier = 0;
        HardStopReason = "";
        if (!string.Equals(prevExam, Bank.ExamId, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(Bank.ExamId))
        {
            FocusStrikes = 0;
            MarkedTasks.Clear();
            CompletedTasks.Clear();
            ViewedTasks.Clear();
        }
    }

    public static bool IsMarked(string? projectId, int taskIndex) =>
        MarkedTasks.Contains(new ReviewMark(projectId ?? "", taskIndex));

    public static bool IsCompleted(string? projectId, int taskIndex) =>
        CompletedTasks.Contains(new ReviewMark(projectId ?? "", taskIndex));

    public static bool IsViewed(string? projectId, int taskIndex) =>
        ViewedTasks.Contains(new ReviewMark(projectId ?? "", taskIndex));

    public static void MarkViewed(string? projectId, int taskIndex) =>
        ViewedTasks.Add(new ReviewMark(projectId ?? "", taskIndex));

    public static bool ToggleMark(string? projectId, int taskIndex)
    {
        var mark = new ReviewMark(projectId ?? "", taskIndex);
        if (!MarkedTasks.Add(mark))
        {
            MarkedTasks.Remove(mark);
            return false;
        }

        return true;
    }

    public static bool ToggleComplete(string? projectId, int taskIndex)
    {
        var mark = new ReviewMark(projectId ?? "", taskIndex);
        if (!CompletedTasks.Add(mark))
        {
            CompletedTasks.Remove(mark);
            return false;
        }

        return true;
    }

    public static void ClearExam()
    {
        ProjectId = null;
        ProjectTitle = null;
        Steps = [];
        AttemptId = null;
        LocalPath = null;
        RubricVersion = null;
        Rubric = null;
        LastCheck = [];
        OpenedUtc = DateTime.UtcNow;
        Bank = BankPayload.FromMode("training");
        HintTier = 0;
        FocusStrikes = 0;
        HardStopReason = "";
        MarkedTasks.Clear();
        CompletedTasks.Clear();
        ViewedTasks.Clear();
        ActionEvidence.Clear();
        WordActionProbe.Reset();
    }
}
