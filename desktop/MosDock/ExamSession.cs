namespace MosDock;

static class ExamSession
{
    public static string Mode { get; set; } = "training";
    public static string DisplayName { get; set; } = "";
    public static string? ProjectId { get; set; }
    public static string? AttemptId { get; set; }
    public static string? LocalPath { get; set; }

    public static string DataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MOS",
        "KulKul");
}
