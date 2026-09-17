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
        var wanted = ExamSession.LocalPath;
        dynamic docs = word.Documents;
        int count = (int)docs.Count;
        for (int i = 1; i <= count; i++)
        {
            dynamic doc = docs[i];
            string full = (string)doc.FullName;
            if (SamePath(full, wanted))
            {
                doc.Save();
                return full;
            }
        }

        if (count == 1 && string.IsNullOrWhiteSpace(wanted))
        {
            dynamic doc = docs[1];
            doc.Save();
            return (string)doc.FullName;
        }

        return null;
    }

    static string? SaveExcel(dynamic excel)
    {
        var wanted = ExamSession.LocalPath;
        dynamic books = excel.Workbooks;
        int count = (int)books.Count;
        for (int i = 1; i <= count; i++)
        {
            dynamic book = books[i];
            string full = (string)book.FullName;
            if (SamePath(full, wanted))
            {
                book.Save();
                return full;
            }
        }

        return null;
    }

    static string? SavePpt(dynamic ppt)
    {
        var wanted = ExamSession.LocalPath;
        dynamic presos = ppt.Presentations;
        int count = (int)presos.Count;
        for (int i = 1; i <= count; i++)
        {
            dynamic pres = presos[i];
            string full = (string)pres.FullName;
            if (SamePath(full, wanted))
            {
                pres.Save();
                return full;
            }
        }

        return null;
    }

    static bool SamePath(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
        {
            return false;
        }

        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
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
