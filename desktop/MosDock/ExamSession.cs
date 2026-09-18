namespace MosDock;

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

    public static string DataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MOS",
        "KulKul");

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
    }
}
