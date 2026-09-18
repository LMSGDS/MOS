using System.Drawing.Imaging;

namespace MosDock;

/// <summary>
/// Chụp đúng vùng layout MOS đang chiếm (dock ∪ Word), không phải cả desktop.
/// </summary>
static class LayoutShot
{
    public static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MOS", "KulKul", "Screenshots");

    public static string Grab(Rect region)
    {
        var w = Math.Max(1, region.W);
        var h = Math.Max(1, region.H);
        Directory.CreateDirectory(Folder);
        var path = Path.Combine(Folder, $"MOS-{DateTime.Now:yyyyMMdd-HHmmss}.png");
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(region.X, region.Y, 0, 0, new Size(w, h), CopyPixelOperation.SourceCopy);
        }

        bmp.Save(path, ImageFormat.Png);
        try
        {
            Clipboard.SetImage(bmp);
        }
        catch
        {
            // clipboard busy
        }

        return path;
    }
}
