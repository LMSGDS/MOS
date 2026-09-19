using System.Drawing.Drawing2D;

namespace MosDock;

/// <summary>
/// Trang chủ: auto-fit course tiles (min 250), widgets 400/300/300 then stack under 1024.
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

    readonly FlowLayoutPanel _page = new();
    readonly FlowLayoutPanel _apps = new();
    readonly FlowLayoutPanel _widgets = new();
    readonly Panel _head;
    readonly Label _title = new();
    readonly Label _hello = new();
    readonly Label _bestValue = new();
    readonly Label _bestHint = new();
    readonly Label _doneValue = new();
    readonly Label _doneHint = new();
    readonly Label _openValue = new();
    readonly Label _openHint = new();
    readonly FlowLayoutPanel _stats = new();
    readonly Panel _radarHost = new();
    readonly RadarView _radar = new();
    readonly FlowLayoutPanel _resumeItems = new();
    readonly Panel _resumeActions = new();
    readonly Button _resumeAll;
    readonly Button _resumeGo;
    readonly Panel _recentHost = new();
    readonly FlowLayoutPanel _recent = new();
    int _resumeShown;
    bool _fitting;

    public Action<string>? OpenProgram { get; set; }
    public Action? OpenResumeList { get; set; }
    public Action? OpenDoneList { get; set; }
    public Func<MosAttempt, Task>? ResumeAttempt { get; set; }

    public HomeDash()
    {
        Dock = DockStyle.Fill;
        BackColor = Ui.PageBg;
        AutoScroll = true;
        Padding = Padding.Empty;

        _resumeGo = Ui.PrimaryBtn("Tiếp tục", 120);
        _resumeGo.BackColor = Ui.Success;
        _resumeGo.FlatAppearance.MouseOverBackColor = Color.FromArgb(2, 110, 48);
        _resumeGo.Click += (_, _) => OpenProgram?.Invoke("word");
        _resumeAll = Ui.TextLink("Xem tất cả", () => OpenResumeList?.Invoke());
        _resumeAll.AutoSize = false;
        _resumeAll.Dock = DockStyle.Right;
        _resumeAll.Width = 128;

        _page.FlowDirection = FlowDirection.TopDown;
        _page.WrapContents = false;
        _page.AutoSize = false;
        _page.BackColor = Ui.PageBg;
        _page.Margin = Padding.Empty;

        _apps.FlowDirection = FlowDirection.LeftToRight;
        _apps.WrapContents = true;
        _apps.AutoSize = false;
        _apps.BackColor = Ui.PageBg;
        _apps.Margin = Padding.Empty;

        _widgets.FlowDirection = FlowDirection.LeftToRight;
        _widgets.WrapContents = true;
        _widgets.AutoSize = false;
        _widgets.BackColor = Ui.PageBg;
        _widgets.Margin = Padding.Empty;

        var banner = Ui.InfoBanner(
            "Đề MOS mở trên Microsoft Office đã cài trên máy — không dùng Office Online.");
        banner.Margin = new Padding(0, 0, 0, 8);
        _head = Header();
        var section = Ui.SectionLabel("Bài mới");
        section.Margin = new Padding(0, 4, 0, 6);

        var word = Ui.AppLaunchTile("Word", "Soạn thảo văn bản MOS — Luyện tập hoặc Thi.", Ui.Word, "W", () => OpenProgram?.Invoke("word"));
        var excel = Ui.AppLaunchTile("Excel", "Bảng tính MOS — Luyện tập hoặc Thi.", Ui.Excel, "X", () => OpenProgram?.Invoke("excel"));
        var ppt = Ui.AppLaunchTile("PowerPoint", "Trình bày MOS — Luyện tập hoặc Thi.", Ui.Ppt, "P", () => OpenProgram?.Invoke("powerpoint"));
        _apps.Controls.Add(word);
        _apps.Controls.Add(excel);
        _apps.Controls.Add(ppt);

        _widgets.Controls.Add(ProgressCard());
        _widgets.Controls.Add(ResumeCard());
        _widgets.Controls.Add(RecentCard());

        _page.Controls.Add(banner);
        _page.Controls.Add(_head);
        _page.Controls.Add(section);
        _page.Controls.Add(_apps);
        _page.Controls.Add(_widgets);
        Controls.Add(_page);
        Resize += (_, _) => Relayout();
        ShowLoading();
    }

    Panel Header()
    {
        var box = new Panel
        {
            Height = 72,
            BackColor = Ui.PageBg,
            Margin = new Padding(0, 0, 0, 4),
            Padding = new Padding(0, 6, 0, 4),
        };
        _title.Text = "Trang chủ";
        _title.Font = Ui.TitleFont;
        _title.ForeColor = Ui.Text;
        _title.Dock = DockStyle.Top;
        _title.UseMnemonic = false;
        Ui.BindWrap(_title, 6);
        _hello.Font = Ui.BodyFont;
        _hello.ForeColor = Ui.Muted;
        _hello.Dock = DockStyle.Top;
        _hello.UseMnemonic = false;
        Ui.BindWrap(_hello, 4);
        box.Controls.Add(_hello);
        box.Controls.Add(_title);
        void FitHead(object? _, EventArgs e)
        {
            var h = box.Padding.Vertical + _title.Height + _hello.Height + 4;
            if (box.Height != h)
            {
                box.Height = Math.Max(64, h);
            }
        }

        box.Resize += FitHead;
        _title.SizeChanged += FitHead;
        _hello.SizeChanged += FitHead;
        return box;
    }

    Control ProgressCard()
    {
        var shell = Ui.SoftCard(out var inner);
        shell.Margin = new Padding(0, 0, LayoutMath.DashWidgetGap, LayoutMath.DashWidgetGap);
        inner.Padding = new Padding(14, 12, 12, 12);
        inner.AutoScroll = false;

        var head = new Label
        {
            Text = "Tổng quan tiến độ",
            Font = Ui.HeadFont,
            ForeColor = Ui.Text,
            Dock = DockStyle.Top,
            Height = 28,
            UseMnemonic = false,
        };

        _stats.FlowDirection = FlowDirection.LeftToRight;
        _stats.WrapContents = true;
        _stats.Dock = DockStyle.Top;
        _stats.Height = 116;
        _stats.BackColor = Ui.Card;
        _stats.Margin = Padding.Empty;
        _stats.Controls.Add(StatBlock("Điểm cao nhất", _bestValue, _bestHint, Ui.Primary));
        _stats.Controls.Add(StatBlock("Hoàn thành", _doneValue, _doneHint, Ui.Success));
        _stats.Controls.Add(StatBlock("Đang làm dở", _openValue, _openHint, Ui.Warning));

        _radarHost.Dock = DockStyle.Fill;
        _radarHost.BackColor = Ui.Card;
        _radarHost.MinimumSize = new Size(120, 180);
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
        _radarHost.Controls.Add(_radar);
        _radarHost.Controls.Add(radarTitle);

        inner.Controls.Add(_radarHost);
        inner.Controls.Add(_stats);
        inner.Controls.Add(head);
        return shell;
    }

    static Panel StatBlock(string label, Label value, Label hint, Color accent)
    {
        var row = new Panel
        {
            Size = new Size(120, 108),
            MinimumSize = new Size(96, 100),
            BackColor = Ui.Card,
            Padding = new Padding(6, 4, 6, 4),
            Margin = new Padding(0, 0, 8, 8),
        };
        var bar = new Panel { Dock = DockStyle.Left, Width = 4, BackColor = accent };
        value.Font = new Font("Segoe UI", 18f, FontStyle.Bold);
        value.ForeColor = Ui.Text;
        value.Dock = DockStyle.Top;
        value.Height = 40;
        value.AutoEllipsis = false;
        value.UseMnemonic = false;
        value.TextAlign = ContentAlignment.MiddleLeft;
        hint.Font = Ui.SmallFont;
        hint.ForeColor = Ui.Muted;
        hint.Dock = DockStyle.Fill;
        hint.AutoEllipsis = false;
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
        shell.Margin = new Padding(0, 0, LayoutMath.DashWidgetGap, LayoutMath.DashWidgetGap);
        inner.Padding = new Padding(14, 12, 14, 12);

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
        head.Controls.Add(title);
        head.Controls.Add(_resumeAll);

        _resumeItems.Dock = DockStyle.Fill;
        _resumeItems.FlowDirection = FlowDirection.TopDown;
        _resumeItems.WrapContents = false;
        _resumeItems.BackColor = Ui.Card;
        _resumeItems.Padding = new Padding(0, 6, 0, 0);

        _resumeActions.Dock = DockStyle.Bottom;
        _resumeActions.Height = 44;
        _resumeActions.BackColor = Ui.Card;
        _resumeGo.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
        _resumeGo.Location = new Point(0, 6);
        _resumeActions.Controls.Add(_resumeGo);

        inner.Controls.Add(_resumeItems);
        inner.Controls.Add(_resumeActions);
        inner.Controls.Add(head);
        return shell;
    }

    Control RecentCard()
    {
        var shell = Ui.SoftCard(out var inner);
        shell.Margin = new Padding(0, 0, 0, LayoutMath.DashGap);
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

    void Relayout()
    {
        if (_fitting)
        {
            return;
        }

        _fitting = true;
        try
        {
            var gutter = VScroll ? SystemInformation.VerticalScrollBarWidth : 0;
            var inner = Math.Max(LayoutMath.DashCourseMin, ClientSize.Width - gutter);
            var gap = LayoutMath.DashWidgetGap;
            var widths = LayoutMath.WidgetWidths(inner, _widgets.Controls.Count);
            var stacked = LayoutMath.WidgetStack(inner);

            _page.Width = inner;
            _apps.Width = inner;
            _widgets.Width = inner;
            foreach (Control child in _page.Controls)
            {
                if (child != _apps && child != _widgets)
                {
                    child.Width = inner;
                }
            }

            _page.PerformLayout();
            Ui.FitWrapRow(_apps, LayoutMath.DashCourseMin, LayoutMath.DashAppH, LayoutMath.DashGap);

            var progressW = widths.Length > 0 ? widths[0] : inner;
            var statInner = Math.Max(90, progressW - 40);
            var statWide = !stacked && statInner >= 340;
            var statW = statWide ? Math.Max(96, (statInner - 16) / 3) : statInner;
            foreach (Control stat in _stats.Controls)
            {
                stat.Width = statW;
                stat.Height = 108;
            }

            _stats.Height = statWide ? 116 : 108 * 3 + 16;
            var radarH = Math.Max(180, statWide ? 200 : 220);
            var progressH = 28 + 16 + _stats.Height + 20 + radarH + 24;
            var resumeH = 28 + 16 + _resumeActions.Height + (_resumeShown == 0 ? 120 : _resumeShown * 132) + 20;
            var recentH = 240;
            var widgetH = Math.Max(progressH, Math.Max(resumeH, recentH));

            for (var i = 0; i < _widgets.Controls.Count; i++)
            {
                var card = _widgets.Controls[i];
                var last = i == _widgets.Controls.Count - 1;
                card.Width = i < widths.Length ? widths[i] : inner;
                card.Height = stacked ? (i == 0 ? progressH : i == 1 ? resumeH : recentH) : widgetH;
                card.Margin = new Padding(0, 0, stacked || last ? 0 : gap, gap);
            }

            if (stacked)
            {
                var widgetRows = _widgets.Controls.Count;
                _widgets.Height = progressH + resumeH + recentH + gap * widgetRows;
            }
            else
            {
                _widgets.Height = widgetH + gap;
            }

            var y = 0;
            foreach (Control child in _page.Controls)
            {
                y += child.Margin.Top + child.Height + child.Margin.Bottom;
            }

            _page.Height = y;
            _page.Location = new Point(0, 0);
            AutoScrollMinSize = new Size(LayoutMath.DashCourseMin, y);
            FitRecent();
            var resumeW = Math.Max(160, _resumeItems.ClientSize.Width);
            foreach (Control row in _resumeItems.Controls)
            {
                row.Width = resumeW;
            }
        }
        finally
        {
            _fitting = false;
        }
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
        _resumeShown = 0;
        _resumeItems.Controls.Clear();
        _resumeItems.Controls.Add(ResumeEmpty("Đang tải…", "Đang lấy bài làm dở gần nhất."));
        _resumeGo.Visible = true;
        _resumeGo.Text = "Tiếp tục";
        _resumeActions.Height = 44;
        _resumeAll.Text = "Xem tất cả";
        _recent.Controls.Clear();
        _recentHost.Controls.Clear();
        Relayout();
    }

    public void ShowError(string message)
    {
        _hello.Text = "Không tải được bảng điều khiển. " + message;
        _resumeShown = 0;
        _resumeItems.Controls.Clear();
        _resumeItems.Controls.Add(ResumeEmpty("Chưa có dữ liệu", message));
        _resumeGo.Visible = true;
        _resumeGo.Text = "Thử Word";
        _resumeActions.Height = 44;
        Relayout();
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
        Relayout();
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
        _resumeShown = Math.Min(2, open.Count);
        _resumeAll.Text = open.Count > 2 ? "Xem tất cả (" + open.Count + "+)" : "Xem tất cả";
        _resumeItems.Controls.Clear();
        if (open.Count == 0)
        {
            _resumeItems.Controls.Add(ResumeEmpty(
                "Chưa có bài đang làm dở",
                "Bắt đầu Word, Excel hoặc PowerPoint ở trên — quay lại đây để mở tiếp, không tạo lần làm mới."));
            _resumeGo.Visible = true;
            _resumeGo.Text = "Bài mới";
            _resumeActions.Height = 44;
            return;
        }

        _resumeGo.Visible = false;
        _resumeActions.Height = 0;
        foreach (var attempt in open.Take(2))
        {
            _resumeItems.Controls.Add(ResumeRow(attempt));
        }
    }

    Panel ResumeRow(MosAttempt attempt)
    {
        var row = new Panel
        {
            Height = 124,
            Width = Math.Max(180, _resumeItems.ClientSize.Width),
            BackColor = Ui.Card,
            Margin = new Padding(0, 0, 0, 8),
        };
        var go = Ui.PrimaryBtn("Tiếp tục", 120);
        go.BackColor = Ui.Success;
        go.FlatAppearance.MouseOverBackColor = Color.FromArgb(2, 110, 48);
        go.Dock = DockStyle.Bottom;
        go.Height = 36;
        var item = attempt;
        go.Click += async (_, _) =>
        {
            if (ResumeAttempt is not null)
            {
                await ResumeAttempt(item);
            }
        };
        var bar = new Ui.PercentTrack { Dock = DockStyle.Bottom, Height = 22 };
        var title = new Label
        {
            Text = attempt.DisplayTitle,
            Font = Ui.HeadFont,
            ForeColor = Ui.Text,
            Dock = DockStyle.Top,
            Height = 28,
            AutoEllipsis = true,
            UseMnemonic = false,
        };
        var lead = new Label
        {
            Font = Ui.BodyFont,
            ForeColor = Ui.Muted,
            Dock = DockStyle.Fill,
            UseMnemonic = false,
        };
        var appMode = Ui.AppName(attempt.Program) + " · " + Ui.ModeLabel(attempt.Mode);
        if (attempt.ProgressPct is { } n)
        {
            lead.Text = appMode + " — Đã hoàn thành " + n + "%";
            bar.Set(n, n + "%");
        }
        else
        {
            lead.Text = appMode + " — Đang làm dở";
            bar.Set(12, "Chưa chấm");
        }

        row.Controls.Add(lead);
        row.Controls.Add(title);
        row.Controls.Add(bar);
        row.Controls.Add(go);
        return row;
    }

    static Panel ResumeEmpty(string title, string lead)
    {
        var box = new Panel { Height = 110, Dock = DockStyle.Top, BackColor = Ui.Card };
        var h = new Label
        {
            Text = title,
            Font = Ui.HeadFont,
            ForeColor = Ui.Text,
            Dock = DockStyle.Top,
            Height = 36,
            UseMnemonic = false,
        };
        var p = new Label
        {
            Text = lead,
            Font = Ui.BodyFont,
            ForeColor = Ui.Muted,
            Dock = DockStyle.Fill,
            UseMnemonic = false,
        };
        box.Controls.Add(p);
        box.Controls.Add(h);
        return box;
    }

    void BindRecent(List<MosAttempt> done)
    {
        _recent.Controls.Clear();
        _recentHost.Controls.Clear();
        var rows = done.Take(3).ToList();
        if (rows.Count == 0)
        {
            _recentHost.Controls.Add(Ui.EmptyHint(
                "Bạn chưa hoàn thành bài luyện tập nào.",
                "Nộp bài xong, điểm và nút Xem Review sẽ hiện ở đây."));
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
