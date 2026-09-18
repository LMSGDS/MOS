using System.Drawing.Drawing2D;

namespace MosDock;

/// <summary>
/// Bảng điều khiển Trang chủ: tiến độ, Word/Excel/PowerPoint, bài dở và 3 bài gần nhất.
/// Dock Fill — lấp cửa sổ 1080×700, không để khoảng trống kiểu empty-state.
/// </summary>
sealed class HomeDash : Panel
{
    static readonly Dictionary<string, string> Levels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chua_bat_dau"] = "Chưa bắt đầu",
        ["bat_dau"] = "Bắt đầu",
        ["dang_tien_bo"] = "Đang tiến bộ",
        ["dat"] = "Đạt yêu cầu",
        ["xuat_sac"] = "Xuất sắc",
    };

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
    readonly Ui.PercentTrack _resumeBar = new();
    readonly Button _resumeGo;
    readonly Panel _recentHost = new();
    readonly FlowLayoutPanel _recent = new();
    MosAttempt? _resume;

    public Action<string>? OpenProgram { get; set; }
    public Action? OpenResumeList { get; set; }
    public Action? OpenDoneList { get; set; }
    public Func<MosAttempt, Task>? ResumeAttempt { get; set; }

    public HomeDash()
    {
        Dock = DockStyle.Fill;
        BackColor = Ui.PageBg;
        Padding = Padding.Empty;

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

        var banner = Ui.InfoBanner(
            "Đề MOS mở trên Microsoft Office đã cài trên máy — không dùng Office Online.");
        banner.Dock = DockStyle.Top;
        banner.Margin = Padding.Empty;

        var head = Header();
        var section = Ui.SectionLabel("Bài mới");
        section.Dock = DockStyle.Top;
        section.Margin = Padding.Empty;
        var apps = AppRow();
        var main = MainRow();

        Controls.Add(main);
        Controls.Add(apps);
        Controls.Add(section);
        Controls.Add(head);
        Controls.Add(banner);
        Resize += (_, _) => FitRecent();
        ShowLoading();
    }

    Panel Header()
    {
        var box = new Panel
        {
            Dock = DockStyle.Top,
            Height = 62,
            BackColor = Ui.PageBg,
            Padding = new Padding(0, 4, 0, 4),
        };
        var title = new Label
        {
            Text = "Trang chủ",
            Font = Ui.TitleFont,
            ForeColor = Ui.Text,
            Dock = DockStyle.Top,
            Height = 34,
            UseMnemonic = false,
        };
        _hello.Font = Ui.BodyFont;
        _hello.ForeColor = Ui.Muted;
        _hello.Dock = DockStyle.Fill;
        _hello.UseMnemonic = false;
        box.Controls.Add(_hello);
        box.Controls.Add(title);
        return box;
    }

    TableLayoutPanel AppRow()
    {
        var apps = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 138,
            Margin = new Padding(0, 0, 0, 8),
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Ui.PageBg,
        };
        apps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
        apps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
        apps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.4f));
        var word = Ui.AppLaunchTile("Word", "Soạn thảo văn bản MOS — Luyện tập hoặc Thi.", Ui.Word, "W", () => OpenProgram?.Invoke("word"));
        var excel = Ui.AppLaunchTile("Excel", "Bảng tính MOS — Luyện tập hoặc Thi.", Ui.Excel, "X", () => OpenProgram?.Invoke("excel"));
        var ppt = Ui.AppLaunchTile("PowerPoint", "Trình bày MOS — Luyện tập hoặc Thi.", Ui.Ppt, "P", () => OpenProgram?.Invoke("powerpoint"));
        word.Margin = new Padding(0, 0, 8, 8);
        excel.Margin = new Padding(4, 0, 8, 8);
        ppt.Margin = new Padding(4, 0, 0, 8);
        word.Dock = excel.Dock = ppt.Dock = DockStyle.Fill;
        apps.Controls.Add(word, 0, 0);
        apps.Controls.Add(excel, 1, 0);
        apps.Controls.Add(ppt, 2, 0);
        return apps;
    }

    TableLayoutPanel MainRow()
    {
        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Ui.PageBg,
            Padding = new Padding(0, 4, 0, 0),
        };
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31));
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31));
        main.Controls.Add(ProgressCard(), 0, 0);
        main.Controls.Add(ResumeCard(), 1, 0);
        main.Controls.Add(RecentCard(), 2, 0);
        return main;
    }

    Control ProgressCard()
    {
        var shell = Ui.SoftCard(out var inner);
        shell.Dock = DockStyle.Fill;
        shell.Margin = new Padding(0, 0, 8, 0);
        inner.Padding = new Padding(14, 12, 12, 12);

        var head = new Label
        {
            Text = "Tổng quan tiến độ",
            Font = Ui.HeadFont,
            ForeColor = Ui.Text,
            Dock = DockStyle.Top,
            Height = 26,
            UseMnemonic = false,
        };

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Ui.Card,
        };
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 52));

        var stats = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Ui.Card,
        };
        stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        stats.Controls.Add(StatBlock("Điểm cao nhất", _bestValue, _bestHint, Ui.Primary), 0, 0);
        stats.Controls.Add(StatBlock("Hoàn thành", _doneValue, _doneHint, Ui.Success), 1, 0);
        stats.Controls.Add(StatBlock("Đang làm dở", _openValue, _openHint, Ui.Warning), 2, 0);

        var radarHost = new Panel { Dock = DockStyle.Fill, BackColor = Ui.Card };
        var radarTitle = new Label
        {
            Text = "Kỹ năng theo chương trình",
            Font = Ui.SmallFont,
            ForeColor = Ui.Muted,
            Dock = DockStyle.Top,
            Height = 18,
            TextAlign = ContentAlignment.MiddleCenter,
            UseMnemonic = false,
        };
        _radar.Dock = DockStyle.Fill;
        radarHost.Controls.Add(_radar);
        radarHost.Controls.Add(radarTitle);

        body.Controls.Add(stats, 0, 0);
        body.Controls.Add(radarHost, 0, 1);
        inner.Controls.Add(body);
        inner.Controls.Add(head);
        return shell;
    }

    static Panel StatBlock(string label, Label value, Label hint, Color accent)
    {
        var row = new Panel { Dock = DockStyle.Fill, BackColor = Ui.Card, Padding = new Padding(6, 4, 6, 4) };
        var bar = new Panel { Dock = DockStyle.Left, Width = 4, BackColor = accent };
        value.Font = new Font("Segoe UI", 15f, FontStyle.Bold);
        value.ForeColor = Ui.Text;
        value.Dock = DockStyle.Top;
        value.Height = 24;
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
            Height = 16,
            UseMnemonic = false,
        };
        var copy = new Panel { Dock = DockStyle.Fill, BackColor = Ui.Card, Padding = new Padding(8, 0, 0, 0) };
        copy.Controls.Add(hint);
        copy.Controls.Add(value);
        copy.Controls.Add(caption);
        row.Controls.Add(copy);
        row.Controls.Add(bar);
        return row;
    }

    Control ResumeCard()
    {
        var shell = Ui.SoftCard(out var inner);
        shell.Dock = DockStyle.Fill;
        shell.Margin = new Padding(4, 0, 8, 0);
        inner.Padding = new Padding(14, 12, 14, 12);

        var head = CardHead("Tiếp tục bài", () => OpenResumeList?.Invoke());
        var actions = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = Ui.Card };
        _resumeGo.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
        _resumeGo.Location = new Point(0, 6);
        actions.Controls.Add(_resumeGo);

        _resumeBar.Dock = DockStyle.Bottom;
        _resumeBar.Height = 22;

        _resumeTitle.Font = Ui.HeadFont;
        _resumeTitle.ForeColor = Ui.Text;
        _resumeTitle.Dock = DockStyle.Top;
        _resumeTitle.Height = 48;
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
        shell.Margin = new Padding(4, 0, 0, 0);
        inner.Padding = new Padding(14, 12, 14, 12);
        _recentHost.Dock = DockStyle.Fill;
        _recentHost.BackColor = Ui.Card;
        _recentHost.Padding = new Padding(0, 8, 0, 0);
        inner.Controls.Add(_recentHost);
        inner.Controls.Add(CardHead("Bài đã nộp", () => OpenDoneList?.Invoke()));

        _recent.Dock = DockStyle.Fill;
        _recent.FlowDirection = FlowDirection.TopDown;
        _recent.WrapContents = false;
        _recent.BackColor = Ui.Card;
        _recent.Resize += (_, _) => FitRecent();
        return shell;
    }

    static Panel CardHead(string text, Action onAll)
    {
        var head = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = Ui.Card };
        var title = new Label
        {
            Text = text,
            Font = Ui.HeadFont,
            ForeColor = Ui.Text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false,
        };
        var all = Ui.TextLink("Xem tất cả", onAll);
        all.AutoSize = false;
        all.Dock = DockStyle.Right;
        all.Width = 88;
        head.Controls.Add(title);
        head.Controls.Add(all);
        return head;
    }

    void FitRecent()
    {
        var w = Math.Max(120, _recent.ClientSize.Width);
        foreach (Control row in _recent.Controls)
        {
            row.Width = w;
        }
    }

    public void ShowLoading()
    {
        _hello.Text = "Đang tải tiến độ học tập…";
        _bestValue.Text = "—";
        _bestHint.Text = "Điểm đã xác minh";
        _doneValue.Text = "—";
        _doneHint.Text = "Module đã nộp";
        _openValue.Text = "—";
        _openHint.Text = "Bài đang mở";
        _radar.SetValues(0, 0, 0);
        _resume = null;
        _resumeTitle.Text = "Đang tải…";
        _resumeLead.Text = "";
        _resumeBar.Set(null, "—");
        _resumeGo.Text = "Tiếp tục";
        _recent.Controls.Clear();
        _recentHost.Controls.Clear();
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
        var sets = ExamHub.GroupAttempts(attempts);
        var open = sets.Open.ToList();
        var done = sets.Submitted.ToList();
        var levelKey = progress.Values
            .Select(p => p.Level)
            .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l) && l != "chua_bat_dau")
            ?? progress.Values.Select(p => p.Level).FirstOrDefault()
            ?? "";
        var level = Levels.TryGetValue(levelKey, out var label) ? label : "";
        _hello.Text = string.IsNullOrEmpty(level)
            ? "Xin chào, " + name + " — bảng điều khiển ôn luyện của bạn."
            : "Xin chào, " + name + " — mức đánh giá: " + level + ".";

        var best = done.Where(a => a.Score is not null).OrderByDescending(a => a.Score).FirstOrDefault();
        if (best.Score is not null)
        {
            _bestValue.Text = best.ScoreLabel;
            _bestHint.Text = best.DisplayTitle;
        }
        else
        {
            _bestValue.Text = "—";
            _bestHint.Text = "Chưa có điểm";
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
        _doneHint.Text = completed + "/" + Math.Max(assigned, 0) + " module";

        _openValue.Text = open.Count.ToString();
        _openHint.Text = OpenHint(open.Count, sets.ArchivedOpen);

        _radar.SetValues(
            AxisValue(progress, attempts, "word"),
            AxisValue(progress, attempts, "excel"),
            AxisValue(progress, attempts, "powerpoint"));

        BindResume(open);
        BindRecent(done);
        FitRecent();
    }

    static string OpenHint(int open, int archived)
    {
        if (open == 0)
        {
            return archived > 0 ? archived + " lần mở cũ đã gom" : "Không có bài dở";
        }

        if (archived > 0)
        {
            return open + " đề · " + archived + " lần cũ đã gom";
        }

        return open == 1 ? "1 bài đang mở" : open + " bài đang mở";
    }

    void BindResume(List<MosAttempt> open)
    {
        _resume = open.Count > 0 ? open[0] : null;
        if (_resume is not { } attempt)
        {
            _resumeTitle.Text = "Chưa có bài đang làm dở";
            _resumeLead.Text = "Bắt đầu Word, Excel hoặc PowerPoint ở trên — quay lại đây để mở tiếp, không tạo lần làm mới.";
            _resumeBar.Set(null, "—");
            _resumeGo.Text = "Bài mới";
            return;
        }

        _resumeTitle.Text = attempt.DisplayTitle;
        var appMode = Ui.AppName(attempt.Program) + " · " + Ui.ModeLabel(attempt.Mode);
        if (attempt.ProgressPct is { } n)
        {
            _resumeLead.Text = appMode + " — Đã hoàn thành " + n + "%";
            _resumeBar.Set(n, n + "%");
        }
        else
        {
            _resumeLead.Text = appMode + " — Đang làm dở";
            _resumeBar.Set(12, "Chưa chấm");
        }

        _resumeGo.Text = "Tiếp tục";
    }

    void BindRecent(List<MosAttempt> done)
    {
        _recent.Controls.Clear();
        _recentHost.Controls.Clear();
        var rows = done.Take(3).ToList();
        if (rows.Count == 0)
        {
            _recentHost.Controls.Add(Ui.EmptyHint(
                "Bạn chưa hoàn thành bài thi nào",
                "Các bài thi đã nộp sẽ hiển thị ở đây."));
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

        _recentHost.Controls.Add(_recent);
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
            .Select(a => a.Score!.Value / Math.Max(1, a.DisplayMax))
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
            var cy = h / 2f + 2;
            var radius = Math.Min(w, h) / 2f - 20;
            var axes = new[]
            {
                (Label: "Word", Color: Ui.Word),
                (Label: "Excel", Color: Ui.Excel),
                (Label: "PPT", Color: Ui.Ppt),
            };

            using (var grid = new Pen(Color.FromArgb(220, 210, 198), 1f))
            {
                for (var ring = 1; ring <= 4; ring++)
                {
                    g.DrawPolygon(grid, AxisPoints(cx, cy, radius * ring / 4f, 1, 1, 1));
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
                var size = TextRenderer.MeasureText(axes[i].Label, Ui.SmallFont);
                TextRenderer.DrawText(
                    g,
                    axes[i].Label,
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
