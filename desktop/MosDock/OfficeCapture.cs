using System.Runtime.InteropServices;

namespace MosDock;

/// <summary>
/// Lưu bài đang mở trên Word/Excel/PowerPoint qua COM (không dùng Office Online).
/// </summary>
static class OfficeCapture
{
    [DllImport("ole32.dll", CharSet = CharSet.Unicode)]
    static extern int CLSIDFromProgID(string lpszProgID, out Guid pclsid);

    [DllImport("oleaut32.dll")]
    static extern int GetActiveObject(ref Guid rclsid, IntPtr reserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

    public static string? SaveActive(string? app)
    {
        var spec = OfficeApp.Resolve(app);
        var progId = spec.Id switch
        {
            "excel" => "Excel.Application",
            "powerpoint" => "PowerPoint.Application",
            _ => "Word.Application",
        };
        try
        {
            var com = Active(progId);
            if (com is null)
            {
                return ExistingPath();
            }

            dynamic office = com;
            string? path = spec.Id switch
            {
                "excel" => SaveExcel(office),
                "powerpoint" => SavePpt(office),
                _ => SaveWord(office),
            };
            return string.IsNullOrWhiteSpace(path) ? ExistingPath() : path;
        }
        catch
        {
            return ExistingPath();
        }
    }

    static string? ExistingPath() =>
        !string.IsNullOrWhiteSpace(ExamSession.LocalPath) && File.Exists(ExamSession.LocalPath)
            ? ExamSession.LocalPath
            : null;

    static string? SaveWord(dynamic word)
    {
        dynamic doc = word.ActiveDocument;
        doc.Save();
        return (string)doc.FullName;
    }

    static string? SaveExcel(dynamic excel)
    {
        dynamic book = excel.ActiveWorkbook;
        book.Save();
        return (string)book.FullName;
    }

    static string? SavePpt(dynamic ppt)
    {
        dynamic pres = ppt.ActivePresentation;
        pres.Save();
        return (string)pres.FullName;
    }

    static object? Active(string progId)
    {
        if (CLSIDFromProgID(progId, out var clsid) != 0)
        {
            return null;
        }

        return GetActiveObject(ref clsid, IntPtr.Zero, out var obj) == 0 ? obj : null;
    }
}
