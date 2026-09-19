using System.Text.Json;

namespace MosDock;

/// <summary>
/// Radar tự nhận thức Word / Excel / PowerPoint trên trang chủ.
/// </summary>
sealed class HomeRadar : Panel
{
    double _word = 8;
    double _excel = 8;
    double _ppt = 8;
    string _caption = "Đang tải tiến độ Word · Excel · PowerPoint…";

    public HomeRadar()
    {
        Height = 196;
        Dock = DockStyle.Top;
        BackColor = Ui.Card;
        Padding = new Padding(16);
        DoubleBuffered = true;
        Paint += OnPaintRadar;
    }

    public async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(Portal.Token))
        {
            _caption = "Đăng nhập để xem radar năng lực ba chương trình.";
            Invalidate();
            return;
        }

        try
        {
            using var doc = await Portal.GetJsonAsync("/api/v1/progress/radar");
            foreach (var axis in doc.RootElement.GetProperty("axes").EnumerateArray())
            {
                var program = axis.TryGetProperty("program", out var p) ? p.GetString() : "";
                var score = axis.TryGetProperty("score", out var s) && s.TryGetDouble(out var v) ? v : 0;
                if (program == "word")
                {
                    _word = score;
                }
                else if (program == "excel")
                {
                    _excel = score;
                }
                else if (program == "powerpoint")
                {
                    _ppt = score;
                }
            }

            _caption = $"Word {_word:0} · Excel {_excel:0} · PowerPoint {_ppt:0} — chênh lệch kỹ năng để tự chọn lộ trình ôn.";
        }
        catch
        {
            _caption = "Chưa lấy được radar từ máy chủ. Vào Bài mới để luyện tập.";
        }

        Invalidate();
    }

    void OnPaintRadar(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var box = new Rectangle(16, 12, 168, 168);
        var cx = box.Left + box.Width / 2f;
        var cy = box.Top + box.Height / 2f;
        var r = box.Width / 2f - 10;
        PointF Pt(int deg, double pct)
        {
            var rad = Math.PI * (deg - 90) / 180.0;
            var t = Math.Clamp(pct, 0, 100) / 100.0;
            return new PointF(cx + (float)(Math.Cos(rad) * r * t), cy + (float)(Math.Sin(rad) * r * t));
        }

        using (var ring = new Pen(Ui.Line, 1f))
        {
            g.DrawEllipse(ring, box.Left + 10, box.Top + 10, box.Width - 20, box.Height - 20);
        }

        var poly = new[] { Pt(0, _word), Pt(120, _excel), Pt(240, _ppt) };
        using (var fill = new SolidBrush(Color.FromArgb(70, Ui.Primary)))
        using (var edge = new Pen(Ui.Primary, 2f))
        {
            g.FillPolygon(fill, poly);
            g.DrawPolygon(edge, poly);
        }

        using var font = Ui.SmallFont;
        using var brush = new SolidBrush(Ui.Text);
        g.DrawString("Word", font, brush, Pt(0, 112));
        g.DrawString("Excel", font, brush, Pt(120, 112));
        g.DrawString("PPT", font, brush, Pt(240, 112));
        var text = new Rectangle(200, 28, Math.Max(80, Width - 220), Height - 48);
        TextRenderer.DrawText(g, _caption, Ui.BodyFont, text, Ui.Muted, TextFormatFlags.WordBreak);
    }
}
