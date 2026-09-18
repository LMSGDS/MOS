using System.Drawing.Drawing2D;

namespace MosDock;

/// <summary>
/// Bảng điều khiển Trang chủ: tiến độ, Word/Excel/PowerPoint, bài dở và 3 bài gần nhất.
/// </summary>
sealed class HomeDash : FlowLayoutPanel
{
    readonly Label _hello = new();
    readonly Label _bestValue = new();
    readonly Label _bestHint = new();
    readonly Label _doneValue = new();
    readonly Label _doneHint = new();
    readonly Label _openValue = new();
    readonly Label _openHint = new();
    readonly RadarView _radar = new();
    readonly Label _resumeTitle = new();
    readonly Label _resumeLead = new();
    readonly ProgressBar _resumeBar = new();
    readonly Button _resumeGo;
    readonly FlowLayoutPanel _recent = new();
    readonly TableLayoutPanel _apps = new();
    readonly TableLayoutPanel _progress = new();
    readonly TableLayoutPanel _bottom = new();
    MosAttempt? _resume;

    public Action<string>? OpenProgram { get; set; }
    public Action? OpenResumeList { get; set; }
    public Action? OpenDoneList { get; set; }
    public Func<MosAttempt, Task>? ResumeAttempt { get; set; }

    public HomeDash()
    {
        Dock = DockStyle.Fill;
        AutoScroll = true;
        WrapContents = false;
        FlowDirection = FlowDirection.TopDown;
        BackColor = Ui.PageBg;
        Padding = new Padding(0, 0, 8, 8);

        _resumeGo = Ui.PrimaryBtn("Tiếp tục", 120);
        _resumeGo.BackColor = Ui.Success;
        _resumeGo.Click += async (_, _) =>
        {
            if (_resume is { } attempt && ResumeAttempt is not null)
            {
                await ResumeAttempt(attempt);
            }
            else
            {
                OpenProgram?.Invoke("word");
            }
        };

        Controls.Add(Ui.InfoBanner(
            "Đề MOS mở trên Microsoft Office đã cài trên máy — không dùng Office Online."));
        Controls.Add(Title("Trang chủ"));
        Controls.Add(Hello());
        Controls.Add(ProgressCard());
        Controls.Add(Ui.SectionLabel("Bài mới"));
        Controls.Add(AppRow());
        Controls.Add(BottomRow());

        Resize += (_, _) => Fit();
        HandleCreated += (_, _) => Fit();
        ShowLoading();
    }

    static Label Title(string text) => new()
    {
        Text = text,
        Font = Ui.TitleFont,
        ForeColor = Ui.Text,
        AutoSize = false,
        Height = 40,
        Margin = new Padding(0, 0, 0, 2),
        UseMnemonic = false,
    };

    Label Hello()
    {
        _hello.Font = Ui.BodyFont;
        _hello.ForeColor = Ui.Muted;
        _hello.AutoSize = false;
        _hello.Height = 28;
        _hello.Margin = new Padding(0, 0, 0, 12);
        _hello.UseMnemonic = false;
        return _hello;
    }

    Control ProgressCard()
    {
        var shell = Ui.SoftCard(out var inner);
        shell.Margin = new Padding(0, 0, 0, 8);
        shell.Height = 214;
        inner.Padding = new Padding(16, 12, 12, 12);

        var head = new Label
        {
            Text = "Tổng quan tiến độ",
            Font = Ui.HeadFont,
            ForeColor = Ui.Text,
            Dock = DockStyle.Top,
            Height = 28,
            UseMnemonic = false,
        };

        _progress.Dock = DockStyle.Fill;
        _progress.ColumnCount = 2;
        _progress.RowCount = 1;
        _progress.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        _progress.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        _progress.BackColor = Ui.Card;

        var stats = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Ui.Card,
            Padding = new Padding(0, 4, 8, 0),
        };
        stats.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
        stats.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
        stats.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
        stats.Controls.Add(StatBlock("Điểm cao nhất gần đây", _bestValue, _bestHint, Ui.Primary), 0, 0);
        stats.Controls.Add(StatBlock("Hoàn thành module", _doneValue, _doneHint, Ui.Success), 0, 1);
        stats.Controls.Add(StatBlock("Đang làm dở", _openValue, _openHint, Ui.Warning), 0, 2);

        var radarHost = new Panel { Dock = DockStyle.Fill, BackColor = Ui.Card, Padding = new Padding(4) };
        var radarTitle = new Label
        {
            Text = "Kỹ năng theo chương trình",
            Font = Ui.SmallFont,
            ForeColor = Ui.Muted,
            Dock = DockStyle.Top,
            Height = 20,
            TextAlign = ContentAlignment.MiddleCenter,
            UseMnemonic = false,
        };
        _radar.Dock = DockStyle.Fill;
        radarHost.Controls.Add(_radar);
        radarHost.Controls.Add(radarTitle);

        _progress.Controls.Add(stats, 0, 0);
        _progress.Controls.Add(radarHost, 1, 0);
        inner.Controls.Add(_progress);
        inner.Controls.Add(head);
        return shell;
    }

    static Panel StatBlock(string label, Label value, Label hint, Color accent)
    {
        var row = new Panel { Dock = DockStyle.Fill, BackColor = Ui.Card, Padding = new Padding(10, 6, 8, 6) };
        var bar = new Panel { Dock = DockStyle.Left, Width = 4, BackColor = accent };
        value.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
        value.ForeColor = Ui.Text;
        value.Dock = DockStyle.Top;
        value.Height = 26;
        value.UseMnemonic = false;
        hint.Font = Ui.SmallFont;
        hint.ForeColor = Ui.Muted;
        hint.Dock = DockStyle.Fill;
        hint.AutoEllipsis = true;
        hint.UseMnemonic = false;
        var caption = new Label
        {
            Text = label,
            Font = Ui.SmallFont,
            ForeColor = Ui.Muted,
            Dock = DockStyle.Top,
            Height = 18,
            UseMnemonic = false,
        };
        var copy = new Panel { Dock = DockStyle.Fill, BackColor = Ui.Card, Padding = new Padding(10, 0, 0, 0) };
        copy.Controls.Add(hint);
        copy.Controls.Add(value);
        copy.Controls.Add(caption);
        row.Controls.Add(copy);
        row.Controls.Add(bar);
        return row;
    }

    Control AppRow()
    {
        _apps.Height = 148;
        _apps.Margin = new Padding(0, 0, 0, 8);
        _apps.ColumnCount = 3;
        _apps.RowCount = 1;
        _apps.BackColor = Ui.PageBg;
        _apps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
        _apps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
        _apps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.4f));
        var word = Ui.AppLaunchTile("Word", "Soạn thảo văn bản MOS — Luyện tập hoặc Thi.", Ui.Word, "W", () => OpenProgram?.Invoke("word"));
        var excel = Ui.AppLaunchTile("Excel", "Bảng tính MOS — Luyện tập hoặc Thi.", Ui.Excel, "X", () => OpenProgram?.Invoke("excel"));
        var ppt = Ui.AppLaunchTile("PowerPoint", "Trình bày MOS — Luyện tập hoặc Thi.", Ui.Ppt, "P", () => OpenProgram?.Invoke("powerpoint"));
        word.Margin = new Padding(0, 0, 8, 0);
        excel.Margin = new Padding(4, 0, 8, 0);
        ppt.Margin = new Padding(4, 0, 0, 0);
        word.Dock = excel.Dock = ppt.Dock = DockStyle.Fill;
        _apps.Controls.Add(word, 0, 0);
        _apps.Controls.Add(excel, 1, 0);
        _apps.Controls.Add(ppt, 2, 0);
        return _apps;
    }

    Control BottomRow()
    {
        _bottom.Height = 228;
        _bottom.Margin = new Padding(0, 0, 0, 8);
        _bottom.ColumnCount = 2;
        _bottom.RowCount = 1;
        _bottom.BackColor = Ui.PageBg;
        _bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _bottom.Controls.Add(ResumeCard(), 0, 0);
        _bottom.Controls.Add(RecentCard(), 1, 0);
        return _bottom;
    }

    Control ResumeCard()
    {
        var shell = Ui.SoftCard(out var inner);
        shell.Dock = DockStyle.Fill;
        shell.Margin = new Padding(0, 0, 8, 0);
        inner.Padding = new Padding(16, 12, 16, 12);

        var head = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = Ui.Card };
        var title = new Label
        {
            Text = "Tiếp tục bài",
            Font = Ui.HeadFont,
            ForeColor = Ui.Text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false,
        };
        var all = Ui.TextLink("Xem tất cả", () => OpenResumeList?.Invoke());
        all.AutoSize = false;
        all.Dock = DockStyle.Right;
        all.Width = 88;
        head.Controls.Add(title);
        head.Controls.Add(all);

        var actions = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = Ui.Card };
        _resumeGo.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
        _resumeGo.Location = new Point(0, 6);
        actions.Controls.Add(_resumeGo);

        _resumeBar.Dock = DockStyle.Bottom;
        _resumeBar.Height = 8;
        _resumeBar.Style = ProgressBarStyle.Continuous;
        _resumeBar.Maximum = 100;
        _resumeBar.Margin = new Padding(0);

        _resumeTitle.Font = Ui.HeadFont;
        _resumeTitle.ForeColor = Ui.Text;
        _resumeTitle.Dock = DockStyle.Top;
        _resumeTitle.Height = 28;
        _resumeTitle.AutoEllipsis = true;
        _resumeTitle.UseMnemonic = false;

        _resumeLead.Font = Ui.BodyFont;
        _resumeLead.ForeColor = Ui.Muted;
        _resumeLead.Dock = DockStyle.Fill;
        _resumeLead.UseMnemonic = false;

        inner.Controls.Add(_resumeLead);
        inner.Controls.Add(_resumeTitle);
        inner.Controls.Add(_resumeBar);
        inner.Controls.Add(actions);
        inner.Controls.Add(head);
        return shell;
    }

    Control RecentCard()
    {
        var shell = Ui.SoftCard(out var inner);
        shell.Dock = DockStyle.Fill;
        shell.Margin = new Padding(8, 0, 0, 0);
        inner.Padding = new Padding(16, 12, 16, 12);

        var head = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = Ui.Card };
        var title = new Label
        {
            Text = "Bài đã nộp",
            Font = Ui.HeadFont,
            ForeColor = Ui.Text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false,
        };
        var all = Ui.TextLink("Xem tất cả", () => OpenDoneList?.Invoke());
        all.AutoSize = false;
        all.Dock = DockStyle.Right;
        all.Width = 88;
        head.Controls.Add(title);
        head.Controls.Add(all);

        _recent.Dock = DockStyle.Fill;
        _recent.FlowDirection = FlowDirection.TopDown;
        _recent.WrapContents = false;
        _recent.BackColor = Ui.Card;
        _recent.Padding = new Padding(0, 8, 0, 0);

        inner.Controls.Add(_recent);
        inner.Controls.Add(head);
        return shell;
    }

    void Fit()
    {
        var inner = Math.Max(280, ClientSize.Width - Padding.Horizontal - 8);
        foreach (Control child in Controls)
        {
            child.Width = inner;
        }

        foreach (Control row in _recent.Controls)
        {
            row.Width = Math.Max(120, _recent.ClientSize.Width);
        }
    }

    public void ShowLoading()
    {
        _hello.Text = "Đang tải tiến độ học tập…";
        _bestValue.Text = "—";
        _bestHint.Text = "Điểm đã xác minh gần đây";
        _doneValue.Text = "—";
        _doneHint.Text = "Tỷ lệ module đã nộp";
        _openValue.Text = "—";
        _openHint.Text = "Bài đang mở trên máy";
        _radar.SetValues(0, 0, 0);
        _resume = null;
        _resumeTitle.Text = "Đang tải…";
        _resumeLead.Text = "";
        _resumeBar.Value = 0;
        _resumeGo.Text = "Tiếp tục";
        _recent.Controls.Clear();
    }

    public void ShowError(string message)
    {
        _hello.Text = "Không tải được bảng điều khiển. " + message;
        _resumeTitle.Text = "Chưa có dữ liệu";
        _resumeLead.Text = message;
        _resumeGo.Text = "Thử Word";
    }

    public void Bind(
        IReadOnlyList<MosAttempt> attempts,
        IReadOnlyDictionary<string, MosProgress> progress,
        string displayName)
    {
        var name = string.IsNullOrWhiteSpace(displayName) ? "bạn" : displayName.Trim();
        var open = attempts.Where(a => a.IsOpen).ToList();
        var done = attempts.Where(a => !a.IsOpen).ToList();
        _hello.Text = "Xin chào, " + name + " — đây là bảng điều khiển ôn luyện của bạn.";

        var best = done.Where(a => a.Score is not null).OrderByDescending(a => a.Score).FirstOrDefault();
        if (best.Score is { } score)
        {
            _bestValue.Text = best.ScoreLabel;
            _bestHint.Text = best.DisplayTitle + " · " + Ui.AppName(best.Program);
        }
        else
        {
            _bestValue.Text = "—";
            _bestHint.Text = "Nộp bài đầu tiên để thấy điểm cao nhất";
        }

        var assigned = progress.Values.Sum(p => Math.Max(p.Assigned, p.Started));
        var completed = progress.Values.Sum(p => p.Completed);
        if (assigned <= 0)
        {
            assigned = attempts.Select(a => a.ProjectId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            completed = done.Select(a => a.ProjectId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        }

        var pct = assigned > 0 ? (int)Math.Round(100.0 * completed / assigned) : 0;
        _doneValue.Text = assigned > 0 ? pct + "%" : "0%";
        _doneHint.Text = completed + "/" + Math.Max(assigned, 0) + " module đã nộp";

        _openValue.Text = open.Count.ToString();
        _openHint.Text = open.Count == 0
            ? "Không có bài đang làm dở"
            : open.Count == 1 ? "1 bài đang mở" : open.Count + " bài đang mở";

        _radar.SetValues(
            AxisValue(progress, attempts, "word"),
            AxisValue(progress, attempts, "excel"),
            AxisValue(progress, attempts, "powerpoint"));

        BindResume(open);
        BindRecent(done);
        Fit();
    }

    void BindResume(List<MosAttempt> open)
    {
        _resume = open.Count > 0 ? open[0] : null;
        if (_resume is not { } attempt)
        {
            _resumeTitle.Text = "Chưa có bài đang làm dở";
            _resumeLead.Text = "Bắt đầu Word, Excel hoặc PowerPoint ở mục Bài mới — không tạo lần làm mới khi bạn quay lại.";
            _resumeBar.Value = 0;
            _resumeGo.Text = "Bài mới";
            return;
        }

        var pct = attempt.ProgressPct;
        _resumeTitle.Text = attempt.DisplayTitle;
        _resumeLead.Text = pct is { } n
            ? Ui.AppName(attempt.Program) + " · " + Ui.ModeLabel(attempt.Mode) + " — Đã hoàn thành " + n + "%"
            : Ui.AppName(attempt.Program) + " · " + Ui.ModeLabel(attempt.Mode) + " — Đang làm dở";
        _resumeBar.Value = pct ?? 0;
        _resumeGo.Text = "Tiếp tục";
    }

    void BindRecent(List<MosAttempt> done)
    {
        _recent.Controls.Clear();
        var rows = done.Take(3).ToList();
        if (rows.Count == 0)
        {
            _recent.Controls.Add(new Label
            {
                Text = "Chưa nộp bài nào. Điểm 3 bài gần nhất sẽ hiện tại đây.",
                Font = Ui.BodyFont,
                ForeColor = Ui.Muted,
                AutoSize = false,
                Width = Math.Max(160, _recent.ClientSize.Width),
                Height = 48,
                UseMnemonic = false,
            });
            return;
        }

        foreach (var row in rows)
        {
            var item = row;
            var line = Ui.MiniScoreRow(
                item.DisplayTitle,
                item.ScoreLabel,
                Ui.AppColor(item.Program),
                () => OpenDoneList?.Invoke());
            line.Width = Math.Max(160, _recent.ClientSize.Width);
            _recent.Controls.Add(line);
        }
    }

    static float AxisValue(
        IReadOnlyDictionary<string, MosProgress> progress,
        IReadOnlyList<MosAttempt> attempts,
        string program)
    {
        float fromProgress = 0;
        if (progress.TryGetValue(program, out var row) && row.OverallScore is { } overall)
        {
            fromProgress = (float)(overall / 100.0);
        }

        var fromAttempts = attempts
            .Where(a => string.Equals(a.Program, program, StringComparison.OrdinalIgnoreCase) && a.Score is not null)
            .Select(a => a.Score!.Value / Math.Max(1, a.MaxScore))
            .DefaultIfEmpty(0)
            .Max();
        return (float)Math.Clamp(Math.Max(fromProgress, fromAttempts), 0, 1);
    }

    sealed class RadarView : Panel
    {
        float _word;
        float _excel;
        float _ppt;

        public RadarView()
        {
            DoubleBuffered = true;
            BackColor = Ui.Card;
            ResizeRedraw = true;
        }

        public void SetValues(float word, float excel, float ppt)
        {
            _word = Math.Clamp(word, 0, 1);
            _excel = Math.Clamp(excel, 0, 1);
            _ppt = Math.Clamp(ppt, 0, 1);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Ui.Card);
            var w = ClientSize.Width;
            var h = ClientSize.Height;
            if (w < 40 || h < 40)
            {
                return;
            }

            var cx = w / 2f;
            var cy = h / 2f + 4;
            var radius = Math.Min(w, h) / 2f - 22;
            var axes = new[]
            {
                (Label: "Word", Value: _word, Color: Ui.Word),
                (Label: "Excel", Value: _excel, Color: Ui.Excel),
                (Label: "PPT", Value: _ppt, Color: Ui.Ppt),
            };

            using (var grid = new Pen(Color.FromArgb(220, 210, 198), 1f))
            {
                for (var ring = 1; ring <= 4; ring++)
                {
                    var pts = AxisPoints(cx, cy, radius * ring / 4f, 1, 1, 1);
                    g.DrawPolygon(grid, pts);
                }
            }

            var fillPts = AxisPoints(cx, cy, radius, _word, _excel, _ppt);
            using (var fill = new SolidBrush(Color.FromArgb(70, Ui.Primary)))
            using (var edge = new Pen(Ui.Primary, 1.8f))
            {
                g.FillPolygon(fill, fillPts);
                g.DrawPolygon(edge, fillPts);
            }

            for (var i = 0; i < axes.Length; i++)
            {
                var angle = -Math.PI / 2 + i * 2 * Math.PI / 3;
                var tx = cx + (float)Math.Cos(angle) * (radius + 14);
                var ty = cy + (float)Math.Sin(angle) * (radius + 14);
                var label = axes[i].Label;
                var size = TextRenderer.MeasureText(label, Ui.SmallFont);
                TextRenderer.DrawText(
                    g,
                    label,
                    Ui.SmallFont,
                    new Point((int)(tx - size.Width / 2f), (int)(ty - size.Height / 2f)),
                    axes[i].Color);
            }
        }

        static PointF[] AxisPoints(float cx, float cy, float radius, float word, float excel, float ppt)
        {
            var values = new[] { word, excel, ppt };
            var pts = new PointF[3];
            for (var i = 0; i < 3; i++)
            {
                var angle = -Math.PI / 2 + i * 2 * Math.PI / 3;
                var r = radius * Math.Clamp(values[i], 0.04f, 1f);
                pts[i] = new PointF(cx + (float)Math.Cos(angle) * r, cy + (float)Math.Sin(angle) * r);
            }

            return pts;
        }
    }
}
