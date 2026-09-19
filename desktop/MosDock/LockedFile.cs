namespace MosDock;

/// <summary>
/// Đọc/sao chép .docx khi Word/Excel/PowerPoint đang mở tệp (sharing violation).
/// </summary>
static class LockedFile
{
    public static byte[] ReadAllBytes(string path, int tries = 10)
    {
        IOException? last = null;
        for (var i = 0; i < tries; i++)
        {
            try
            {
                using var src = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var ms = new MemoryStream();
                src.CopyTo(ms);
                if (ms.Length == 0 && new FileInfo(path).Length > 0)
                {
                    throw new IOException("empty_copy");
                }

                return ms.ToArray();
            }
            catch (IOException ex)
            {
                last = ex;
                Thread.Sleep(120 + i * 80);
            }
            catch (UnauthorizedAccessException ex)
            {
                last = new IOException(ex.Message, ex);
                Thread.Sleep(120 + i * 80);
            }
        }

        throw last ?? new IOException(path);
    }

    public static void Copy(string source, string dest, int tries = 10)
    {
        var bytes = ReadAllBytes(source, tries);
        var dir = Path.GetDirectoryName(dest);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllBytes(dest, bytes);
    }

    public static string Snapshot(string source, string destDir)
    {
        Directory.CreateDirectory(destDir);
        var dest = Path.Combine(
            destDir,
            DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + Path.GetExtension(source));
        Copy(source, dest);
        return dest;
    }

    public static bool IsSharing(Exception ex)
    {
        if (ex is UnauthorizedAccessException)
        {
            return true;
        }

        if (ex is not IOException io)
        {
            return ex.InnerException is not null && IsSharing(ex.InnerException);
        }

        const int sharing = unchecked((int)0x80070020);
        const int lockViolation = unchecked((int)0x80070021);
        if (io.HResult is sharing or lockViolation)
        {
            return true;
        }

        var text = io.Message ?? "";
        return text.Contains("being used by another process", StringComparison.OrdinalIgnoreCase)
            || text.Contains("used by another process", StringComparison.OrdinalIgnoreCase)
            || text.Contains("đang được sử dụng", StringComparison.OrdinalIgnoreCase);
    }
}
