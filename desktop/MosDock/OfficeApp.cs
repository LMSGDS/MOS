namespace MosDock;

readonly record struct OfficeApp(string Id, string Process, string ClassName, string Protocol)
{
    public static OfficeApp Resolve(string? id)
    {
        var key = (id ?? "word").Trim().ToLowerInvariant();
        return key switch
        {
            "excel" or "xlsx" or "xls" => new OfficeApp("excel", "EXCEL", "XLMAIN", "ms-excel:"),
            "powerpoint" or "ppt" or "pptx" or "powerpnt" => new OfficeApp("powerpoint", "POWERPNT", "PPTFrameClass", "ms-powerpoint:"),
            _ => new OfficeApp("word", "WINWORD", "OpusApp", "ms-word:"),
        };
    }
}
