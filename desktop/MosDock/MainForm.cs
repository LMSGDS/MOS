namespace MosDock;

/// <summary>
/// MOS-KulKul: trang chủ kiểu GMetrix SMS — Bài mới / Tiếp tục / Đã nộp, rồi mới vào khung bài thi.
/// </summary>
sealed class MainForm : Form
{
    readonly Panel _header = new();
    readonly Label _crumb = new();
    readonly Label _user = new();
    readonly Button _back = new();
    readonly Button _signOut = new();
    readonly Panel _body = new();
    readonly Panel _home = new();
    readonly Panel _catalog = new();
    readonly FlowLayoutPanel _products = new();
    readonly FlowLayoutPanel _tests = new();
    readonly Panel _resume = new();
    readonly FlowLayoutPanel _resumeList = new();
    readonly Panel _done = new();
    readonly FlowLayoutPanel _doneList = new();
    readonly Panel _exam = new();
    readonly Label _examTitle = new();
    readonly Label _examMeta = new();
    readonly ListView _tasks = new();
    readonly Label _examStatus = new();
    readonly Panel _dockChrome = new();
    readonly Button _dockPos = Ui.DockSquare(NavIcon.Dock, "Vị trí thanh", Ui.DockBlue);
    readonly Button _dockSave = Ui.DockSquare(NavIcon.Save, "Lưu bài", Ui.DockBlue);
    readonly Button _dockTasks = Ui.DockSquare(NavIcon.Tasks, "Danh sách nhiệm vụ", Ui.DockBlue);
    readonly Button _dockCheck = Ui.DockSquare(NavIcon.Refresh, "Kiểm tra nhiệm vụ", Ui.DockBlue);
    readonly Button _dockPin = Ui.DockSquare(NavIcon.Pin, "Ghim luôn trên cùng", Ui.DockBlue);
    readonly Button _dockMenu = Ui.DockSquare(NavIcon.Menu, "Về trang chủ", Ui.DockTeal);
    readonly Button _dockHint = Ui.DockSquare(NavIcon.Hint, "Hiện hướng dẫn", Ui.DockTeal);
    readonly Button _dockShare = Ui.DockSquare(NavIcon.Share, "Nộp bài", Ui.DockBlue);
    readonly Button _dockBack = Ui.DockSquare(NavIcon.Back, "Nhiệm vụ trước", Ui.DockBlue);
    readonly Button _dockNext = Ui.DockSquare(NavIcon.Next, "Nhiệm vụ sau", Ui.DockGreen);
    readonly ContextMenuStrip _dockMenuStrip = new();
    readonly Panel _helpPane = new();
    readonly Label _taskPrompt = new();
    readonly Label _helpTitle = new();
    readonly RichTextBox _helpBody = new();
    readonly Button _aaaBtn = Ui.AaaButton();
    readonly System.Windows.Forms.Timer _keepWord = new();
    LocalAgent? _agent;
    string _state = "bottom";
    string _app = "word";
    bool _compact;
    bool _docking;
    bool _pinned = true;
    bool _helpVisible = true;
    int _helpScale;
    int _taskIndex;
    Rectangle? _savedWorkspace;
    readonly bool _launchOnStart;
    readonly string? _fileOnStart;
    IReadOnlyList<MosProject> _items = [];
    enum HubPage { Home, Catalog, Resume, Done, Exam }
    HubPage _view = HubPage.Home;

    public bool SignOutRequested { get; private set; }

    public MainForm(string? initialState, string? initialApp = null, bool launchOnStart = false, string? fileOnStart = null)
    {
        if (!string.IsNullOrWhiteSpace(initialState))
        {
            _state = initialState.ToLowerInvariant();
        }

        _app = OfficeApp.Resolve(initialApp).Id;
        _launchOnStart = launchOnStart;
        _fileOnStart = fileOnStart;
        Text = "MOS-KulKul";
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        ClientSize = new Size(1080, 700);
        MinimumSize = new Size(960, 600);
        BackColor = Ui.PageBg;
        Font = Ui.BodyFont;

        BuildHeader();
        BuildHome();
        BuildCatalog();
        BuildListPage(_resume, _resumeList, "Tiếp tục bài", "Chọn bài đang làm dở để mở lại trên Office máy.");
        BuildListPage(_done, _doneList, "Bài đã nộp", "Điểm hiển thị phần đã xác minh. Find/Go To có thể còn chưa xác minh.");
        BuildExam();

        _body.Dock = DockStyle.Fill;
        _body.BackColor = Ui.PageBg;
        _body.Padding = new Padding(28, 20, 28, 24);
        _body.Controls.Add(_home);
        _body.Controls.Add(_catalog);
        _body.Controls.Add(_resume);
        _body.Controls.Add(_done);

        Controls.Add(_exam);
        Controls.Add(_body);
        Controls.Add(_header);

        Load += async (_, _) =>
        {
            Protocol.Register();
            try
            {
                _agent = new LocalAgent(this, OnPlaceRequest);
            }
            catch
            {
                // agent optional
            }

            ShowHome();
            if (_launchOnStart)
            {
                WordWindow.Launch(_app, _fileOnStart);
                ShowExamUi();
                EnterDock(compact: true);
            }

            try
            {
                await OfflineQueue.FlushAsync();
            }
            catch
            {
                // offline
            }
        };

        FormClosed += (_, _) => _agent?.Dispose();
        _keepWord.Interval = 700;
        _keepWord.Tick += (_, _) => ApplyWordOnly();
    }

    void BuildHeader()
    {
        _header.Dock = DockStyle.Top;
        _header.Height = 52;
        _header.BackColor = Ui.Nav;
        _header.Padding = new Padding(16, 8, 16, 8);

        _signOut.Text = "Đăng xuất";
        _signOut.AutoSize = false;
        _signOut.Size = new Size(Math.Max(128, Ui.MeasureW("Đăng xuất", Ui.BtnFont) + 24), 32);
        _signOut.Dock = DockStyle.Right;
        _signOut.FlatStyle = FlatStyle.Flat;
        _signOut.BackColor = Ui.NavDark;
        _signOut.ForeColor = Color.White;
        _signOut.Font = Ui.BtnFont;
        _signOut.FlatAppearance.BorderSize = 0;
        _signOut.UseMnemonic = false;
        _signOut.TextAlign = ContentAlignment.MiddleCenter;
        _signOut.Click += (_, _) =>
        {
            SignOutRequested = true;
            Close();
        };

        _user.AutoSize = false;
        _user.Dock = DockStyle.Right;
        _user.Width = 220;
        _user.ForeColor = Color.White;
        _user.Text = ExamSession.DisplayName;
        _user.TextAlign = ContentAlignment.MiddleRight;
        _user.AutoEllipsis = true;
        _user.Padding = new Padding(0, 0, 12, 0);
        _user.UseMnemonic = false;

        _back.Text = "Trang chủ";
        _back.AutoSize = false;
        _back.Size = new Size(0, 32);
        _back.Dock = DockStyle.Left;
        _back.FlatStyle = FlatStyle.Flat;
        _back.BackColor = Ui.NavDark;
        _back.ForeColor = Color.White;
        _back.Font = Ui.BtnFont;
        _back.FlatAppearance.BorderSize = 0;
        _back.Visible = false;
        _back.UseMnemonic = false;
        _back.TextAlign = ContentAlignment.MiddleCenter;
        _back.Margin = new Padding(0, 0, 12, 0);
        _back.Click += (_, _) => ShowHome();

        _crumb.Text = "MOS-KulKul";
        _crumb.ForeColor = Color.White;
        _crumb.Font = Ui.NavFont;
        _crumb.AutoSize = false;
        _crumb.Dock = DockStyle.Fill;
        _crumb.TextAlign = ContentAlignment.MiddleLeft;
        _crumb.AutoEllipsis = true;
        _crumb.Padding = new Padding(8, 0, 8, 0);
        _crumb.UseMnemonic = false;

        _header.Controls.Add(_crumb);
        _header.Controls.Add(_back);
        _header.Controls.Add(_user);
        _header.Controls.Add(_signOut);
    }

    void BuildHome()
    {
        _home.Dock = DockStyle.Fill;
        _home.BackColor = Ui.PageBg;
        var tiles = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = true,
            AutoScroll = true,
            BackColor = Ui.PageBg,
            Padding = new Padding(0),
        };
        tiles.Controls.Add(Ui.Tile(
            "Bài mới",
            "Chọn Word, Excel hoặc PowerPoint, rồi Luyện tập hoặc Thi.",
            Ui.Primary,
            ShowCatalog));
        tiles.Controls.Add(Ui.Tile(
            "Tiếp tục bài",
            "Mở bài đang làm dở, không tạo lần làm mới.",
            Ui.Success,
            () => _ = ShowResume()));
        tiles.Controls.Add(Ui.Tile(
            "Bài đã nộp",
            "Xem điểm đã xác minh và bài đã gửi lên máy chủ.",
            Ui.Warning,
            () => _ = ShowCompleted()));
        _home.Controls.Add(Ui.StackPage(
            "Trang chủ",
            "Chọn một ô bên dưới. Đề MOS mở trên Microsoft Office đã cài trên máy — không dùng Office Online.",
            tiles));
    }

    void BuildCatalog()
    {
        _catalog.Dock = DockStyle.Fill;
        _catalog.BackColor = Ui.PageBg;
        _products.Dock = DockStyle.Top;
        _products.AutoSize = true;
        _products.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _products.WrapContents = true;
        _products.BackColor = Ui.PageBg;
        _products.Controls.Add(Ui.Tile("Word", "Microsoft Word trên máy — mở ứng dụng đã cài.", Ui.Word, () => _ = LoadCatalog("word")));
        _products.Controls.Add(Ui.Tile("Excel", "Microsoft Excel trên máy — mở ứng dụng đã cài.", Ui.Excel, () => _ = LoadCatalog("excel")));
        _products.Controls.Add(Ui.Tile("PowerPoint", "Microsoft PowerPoint trên máy — mở ứng dụng đã cài.", Ui.Ppt, () => _ = LoadCatalog("powerpoint")));
        _tests.Dock = DockStyle.Fill;
        _tests.AutoScroll = true;
        _tests.WrapContents = true;
        _tests.BackColor = Ui.PageBg;
        _tests.Resize += (_, _) => Ui.FitCards(_tests);
        var body = new Panel { Dock = DockStyle.Fill, BackColor = Ui.PageBg };
        body.Controls.Add(_tests);
        body.Controls.Add(_products);
        _catalog.Controls.Add(Ui.StackPage(
            "Bài mới",
            "Chọn chương trình, chọn đề, rồi Luyện tập hoặc Thi.",
            body));
    }

    void BuildListPage(Panel page, FlowLayoutPanel list, string title, string lead)
    {
        page.Dock = DockStyle.Fill;
        page.BackColor = Ui.PageBg;
        list.WrapContents = true;
        list.AutoScroll = true;
        list.BackColor = Ui.PageBg;
        list.Resize += (_, _) => Ui.FitCards(list);
        page.Controls.Add(Ui.StackPage(title, lead, list));
    }

    void BuildExam()
    {
        _exam.Dock = DockStyle.Fill;
        _exam.BackColor = Color.White;
        _exam.Padding = Padding.Empty;
        _exam.Visible = false;

        _dockChrome.Dock = DockStyle.Bottom;
        _dockChrome.Height = LayoutMath.ClusterH;
        _dockChrome.BackColor = Color.FromArgb(236, 239, 241);
        _dockChrome.Padding = new Padding(8, 6, 8, 6);

        var row1 = Ui.DockChip();
        var row2 = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
        };

        _dockPos.Click += (_, _) =>
        {
            _dockMenuStrip.Show(_dockPos, new Point(0, 0), ToolStripDropDownDirection.AboveRight);
        };
        _dockSave.Click += (_, _) => ExamHub.SaveInPlace(_app);
        _dockTasks.Click += (_, _) => ToggleTaskList();
        _dockCheck.Click += async (_, _) => await CheckTasks();
        _dockPin.Click += (_, _) =>
        {
            _pinned = !_pinned;
            TopMost = _pinned;
            HighlightDockIcons();
        };
        _dockMenu.Click += (_, _) => SaveAndHome();
        _dockHint.Click += (_, _) => ToggleHelp();
        _dockShare.Click += async (_, _) => await SubmitExam();
        _dockBack.Click += (_, _) => StepTask(-1);
        _dockNext.Click += (_, _) => StepTask(1);

        row1.Controls.Add(_dockPos);
        row1.Controls.Add(_dockSave);
        row1.Controls.Add(_dockTasks);
        row1.Controls.Add(_dockCheck);
        row1.Controls.Add(_dockPin);
        row2.Controls.Add(_dockMenu);
        row2.Controls.Add(_dockHint);
        row2.Controls.Add(_dockShare);
        row2.Controls.Add(_dockBack);
        row2.Controls.Add(_dockNext);

        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
        };
        stack.Controls.Add(row1);
        stack.Controls.Add(row2);
        _dockChrome.Controls.Add(stack);

        _dockMenuStrip.Items.Add(DockMenuItem("←   Trái", "left"));
        _dockMenuStrip.Items.Add(DockMenuItem("→   Phải", "right"));
        _dockMenuStrip.Items.Add(DockMenuItem("↑   Trên", "top"));
        _dockMenuStrip.Items.Add(DockMenuItem("↓   Dưới", "bottom"));
        _dockMenuStrip.Items.Add(new ToolStripSeparator());
        var undock = new ToolStripMenuItem("⤢   Tháo dock");
        undock.Click += (_, _) => UnDock();
        _dockMenuStrip.Items.Add(undock);

        _examTitle.Dock = DockStyle.Top;
        _examTitle.Font = Ui.HeadFont;
        _examTitle.ForeColor = Ui.Text;
        _examTitle.UseMnemonic = false;
        Ui.BindWrap(_examTitle, 8);

        _examMeta.Dock = DockStyle.Top;
        _examMeta.ForeColor = Ui.Muted;
        _examMeta.UseMnemonic = false;
        Ui.BindWrap(_examMeta, 8);

        _examStatus.Dock = DockStyle.Top;
        _examStatus.ForeColor = Ui.Text;
        _examStatus.UseMnemonic = false;
        Ui.BindWrap(_examStatus, 8);

        _tasks.Dock = DockStyle.Fill;
        _tasks.View = System.Windows.Forms.View.Details;
        _tasks.FullRowSelect = true;
        _tasks.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _tasks.Columns.Add("Nhiệm vụ", 240);
        _tasks.Columns.Add("Kết quả", 90);
        _tasks.BorderStyle = BorderStyle.FixedSingle;
        _tasks.BackColor = Color.White;
        _tasks.SelectedIndexChanged += (_, _) =>
        {
            if (_tasks.SelectedIndices.Count > 0)
            {
                _taskIndex = _tasks.SelectedIndices[0];
                RenderHelp();
            }
        };

        BuildHelpPane();

        _exam.Controls.Add(_tasks);
        _exam.Controls.Add(_dockChrome);
        _exam.Controls.Add(_helpPane);
        _exam.Controls.Add(_examStatus);
        _exam.Controls.Add(_examMeta);
        _exam.Controls.Add(_examTitle);
    }

    void BuildHelpPane()
    {
        _helpPane.Dock = DockStyle.Top;
        _helpPane.Height = LayoutMath.HelpH;
        _helpPane.BackColor = Color.FromArgb(245, 247, 249);
        _helpPane.Padding = new Padding(14, 10, 14, 6);
        _helpPane.Visible = false;

        _taskPrompt.Dock = DockStyle.Top;
        _taskPrompt.AutoSize = false;
        _taskPrompt.UseMnemonic = false;
        _taskPrompt.ForeColor = Ui.Text;
        _taskPrompt.Padding = new Padding(2, 0, 2, 8);
        Ui.BindWrap(_taskPrompt, 12);

        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(1),
        };
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(210, 214, 218));
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        var header = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.White, Padding = new Padding(16, 10, 16, 0) };
        header.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 230, 234));
            e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
        };
        _helpTitle.Text = "Hướng dẫn";
        _helpTitle.Dock = DockStyle.Fill;
        _helpTitle.TextAlign = ContentAlignment.MiddleLeft;
        _helpTitle.ForeColor = Ui.Text;
        _helpTitle.UseMnemonic = false;
        header.Controls.Add(_helpTitle);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            BackColor = Color.FromArgb(248, 249, 250),
            Padding = new Padding(12, 6, 8, 6),
            WrapContents = false,
        };
        footer.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 230, 234));
            e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
        };
        _aaaBtn.Margin = new Padding(0, 0, 0, 0);
        _aaaBtn.Click += (_, _) =>
        {
            _helpScale = (_helpScale + 1) % 3;
            ApplyHelpFonts();
            RenderHelp();
        };
        footer.Controls.Add(_aaaBtn);

        _helpBody.Dock = DockStyle.Fill;
        _helpBody.BorderStyle = BorderStyle.None;
        _helpBody.ReadOnly = true;
        _helpBody.DetectUrls = false;
        _helpBody.TabStop = false;
        _helpBody.BackColor = Color.White;
        _helpBody.ForeColor = Ui.Text;
        _helpBody.ScrollBars = RichTextBoxScrollBars.Vertical;
        _helpBody.HideSelection = true;
        _helpBody.ShortcutsEnabled = false;
        _helpBody.Cursor = Cursors.Default;
        _helpBody.Margin = new Padding(0);
        _helpBody.Padding = new Padding(0);
        var bodyWrap = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(18, 14, 18, 10),
        };
        bodyWrap.Controls.Add(_helpBody);

        card.Controls.Add(bodyWrap);
        card.Controls.Add(footer);
        card.Controls.Add(header);
        _helpPane.Controls.Add(card);
        _helpPane.Controls.Add(_taskPrompt);
        ApplyHelpFonts();
    }

    bool HelpOpen => _helpVisible && ExamSession.Mode != "testing";

    void ToggleHelp()
    {
        if (ExamSession.Mode == "testing")
        {
            return;
        }

        _helpVisible = !_helpVisible;
        if (_docking)
        {
            ApplyDock(waitForWord: true);
        }
        else
        {
            ApplyExamChrome();
        }
    }

    void ToggleTaskList()
    {
        if (!_docking)
        {
            return;
        }

        _compact = !_compact;
        EnterDock(_compact);
    }

    void ApplyHelpFonts()
    {
        var promptPt = _helpScale switch { 2 => 18f, 1 => 15f, _ => 13f };
        var bodyPt = _helpScale switch { 2 => 13f, 1 => 11.5f, _ => 10f };
        var titlePt = _helpScale switch { 2 => 24f, 1 => 21f, _ => 18f };
        _taskPrompt.Font = new Font("Segoe UI", promptPt, FontStyle.Bold);
        _helpTitle.Font = new Font("Segoe UI", titlePt, FontStyle.Regular);
        _helpBody.Font = new Font("Segoe UI", bodyPt);
    }

    void RenderHelp()
    {
        var bodyPt = _helpScale switch { 2 => 13f, 1 => 11.5f, _ => 10f };
        var criteria = ExamSession.Rubric?.Criteria;
        if (criteria is not { Count: > 0 })
        {
            _taskPrompt.Text = ExamSession.ProjectTitle ?? "Bài MOS";
            SetHelpBody(["Làm đúng yêu cầu trên đề trong Microsoft Office đã cài trên máy."], bodyPt);
            return;
        }

        _taskIndex = Math.Clamp(_taskIndex, 0, criteria.Count - 1);
        var item = criteria[_taskIndex];
        var prompt = string.IsNullOrWhiteSpace(item.Prompt) ? item.Id : item.Prompt;
        _taskPrompt.Text = Ui.StripMarks(prompt);
        if (item.HelpSteps is { Count: > 0 })
        {
            SetHelpBody(item.HelpSteps, bodyPt);
        }
        else
        {
            SetHelpBody(["Làm đúng yêu cầu trên đề trong Microsoft Office đã cài trên máy."], bodyPt);
        }
    }

    void SetHelpBody(IReadOnlyList<string> steps, float bodyPt)
    {
        try
        {
            _helpBody.Rtf = Ui.HelpStepsRtf(steps, bodyPt);
        }
        catch
        {
            _helpBody.Text = string.Join("\n", steps.Select((s, i) => $"{i + 1}. {Ui.StripMarks(s)}"));
        }
    }

    ToolStripMenuItem DockMenuItem(string text, string state)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += (_, _) => MoveDock(state);
        item.Tag = state;
        return item;
    }

    void StepTask(int delta)
    {
        if (_tasks.Items.Count == 0)
        {
            return;
        }

        var i = _tasks.SelectedIndices.Count > 0 ? _tasks.SelectedIndices[0] : 0;
        i = Math.Clamp(i + delta, 0, _tasks.Items.Count - 1);
        _tasks.SelectedIndices.Clear();
        _tasks.Items[i].Selected = true;
        _tasks.EnsureVisible(i);
        _taskIndex = i;
        RenderHelp();
    }

    void MoveDock(string state)
    {
        _state = state;
        _compact = true;
        if (!_docking)
        {
            EnterDock(compact: true);
            return;
        }

        ApplyDock(waitForWord: true);
        HighlightDockIcons();
    }

    void UnDock()
    {
        var switching = _docking;
        _docking = false;
        _compact = false;
        _keepWord.Stop();
        _header.Visible = false;
        _body.Visible = false;
        _exam.Visible = true;
        TopMost = _pinned;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = true;
        MaximizeBox = true;
        ControlBox = true;
        Text = "MOS-KulKul";
        ApplyExamChrome();
        if (_savedWorkspace is { } saved && saved.Width > 200 && saved.Height > 200)
        {
            Bounds = saved;
        }
        else
        {
            var wa = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
            var w = Math.Min(980, wa.Width - 40);
            var h = Math.Min(640, wa.Height - 40);
            Bounds = new Rectangle(wa.X + (wa.Width - w) / 2, wa.Y + (wa.Height - h) / 2, w, h);
        }

        if (switching)
        {
            Show();
        }
    }

    void HighlightDockIcons()
    {
        foreach (ToolStripItem item in _dockMenuStrip.Items)
        {
            if (item.Tag is string state)
            {
                item.BackColor = state == _state ? Ui.DockBlue : Color.White;
                item.ForeColor = state == _state ? Color.White : Ui.Text;
            }
        }

        Ui.SetIconActive(_dockPin, _pinned);
        _dockPin.BackColor = _pinned ? Ui.DockBlue : Color.FromArgb(148, 163, 184);
        Ui.DockTips.SetToolTip(_dockTasks, _compact ? "Danh sách nhiệm vụ" : "Thu nhỏ thanh dock");
        Ui.DockTips.SetToolTip(_dockHint, HelpOpen ? "Ẩn hướng dẫn" : "Hiện hướng dẫn");
    }

    void ApplyExamChrome()
    {
        var showTasks = !_compact || !_docking;
        var showHelp = HelpOpen;
        _exam.Padding = showTasks ? new Padding(12, 8, 12, 0) : Padding.Empty;
        _exam.BackColor = showTasks ? Color.White : Color.FromArgb(245, 247, 249);
        _dockChrome.Visible = true;
        _helpPane.Visible = showHelp;
        if (showHelp && _compact && _docking)
        {
            _helpPane.Dock = DockStyle.Fill;
        }
        else
        {
            _helpPane.Dock = DockStyle.Top;
            _helpPane.Height = LayoutMath.HelpH;
        }
        _tasks.Visible = showTasks;
        _examTitle.Visible = showTasks;
        _examMeta.Visible = showTasks;
        _examStatus.Visible = showTasks;
        _dockTasks.Visible = true;
        _dockHint.Visible = ExamSession.Mode != "testing";
        RenderHelp();
        HighlightDockIcons();
        if (_tasks.Visible && _tasks.Columns.Count >= 1)
        {
            _tasks.Columns[0].Width = Math.Max(160, _exam.ClientSize.Width - 120);
        }
    }

    void ShowPage(HubPage view, string title)
    {
        _view = view;
        var exam = view == HubPage.Exam;
        _body.Visible = !exam;
        _exam.Visible = exam;
        _home.Visible = view == HubPage.Home;
        _catalog.Visible = view == HubPage.Catalog;
        _resume.Visible = view == HubPage.Resume;
        _done.Visible = view == HubPage.Done;
        _back.Visible = view is HubPage.Catalog or HubPage.Resume or HubPage.Done;
        _back.Width = _back.Visible ? Math.Max(132, Ui.MeasureW("Trang chủ", Ui.BtnFont) + 24) : 0;
        _crumb.Text = title;
        _header.Visible = !exam || !_docking;
        if (!exam && _docking)
        {
            LeaveDock();
        }
    }

    void ShowHome()
    {
        ShowPage(HubPage.Home, "MOS-KulKul");
    }

    void ShowCatalog()
    {
        _tests.Controls.Clear();
        _tests.Controls.Add(new Label
        {
            Text = "Chọn Word, Excel hoặc PowerPoint ở trên để xem đề.",
            AutoSize = true,
            ForeColor = Ui.Muted,
            Margin = new Padding(12),
        });
        ShowPage(HubPage.Catalog, "Bài mới");
    }

    async Task LoadCatalog(string program)
    {
        _app = program;
        _tests.Controls.Clear();
        _tests.Controls.Add(new Label
        {
            Text = "Đang tải đề " + Ui.AppName(program) + "…",
            AutoSize = true,
            ForeColor = Ui.Muted,
            Margin = new Padding(12),
        });
        try
        {
            _items = await ExamHub.ListProjectsAsync(program);
        }
        catch (Exception ex)
        {
            _tests.Controls.Clear();
            _tests.Controls.Add(new Label { Text = "Không tải được đề: " + ex.Message, AutoSize = true, ForeColor = Color.Firebrick, Margin = new Padding(12) });
            return;
        }

        _tests.Controls.Clear();
        if (_items.Count == 0)
        {
            _tests.Controls.Add(new Label { Text = "Chưa có đề cho chương trình này.", AutoSize = true, ForeColor = Ui.Muted, Margin = new Padding(12) });
            return;
        }

        foreach (var project in _items)
        {
            _tests.Controls.Add(TestCard(project));
        }
    }

    Panel TestCard(MosProject project)
    {
        var mins = Math.Max(1, project.TimeLimitSec / 60);
        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Ui.Card,
        };
        var train = Ui.PrimaryBtn("Luyện tập", 120);
        train.Click += async (_, _) => await ConfirmStart(project, "training");
        var test = Ui.PrimaryBtn("Thi", 120);
        test.BackColor = Ui.Orange;
        test.Click += async (_, _) => await ConfirmStart(project, "testing");
        actions.Controls.Add(train);
        actions.Controls.Add(test);
        var card = Ui.ListCard(
            project.Title,
            (string.IsNullOrWhiteSpace(project.Skill) ? Ui.AppName(project.Program) : project.Skill) + " · " + mins + " phút",
            actions);
        card.Width = Math.Max(640, _tests.ClientSize.Width - 24);
        Ui.FitCards(_tests);
        return card;
    }

    async Task ShowResume()
    {
        ShowPage(HubPage.Resume, "Tiếp tục bài");
        await FillAttempts(_resumeList, running: true);
    }

    async Task ShowCompleted()
    {
        ShowPage(HubPage.Done, "Bài đã nộp");
        await FillAttempts(_doneList, running: false);
    }

    async Task FillAttempts(FlowLayoutPanel list, bool running)
    {
        list.Controls.Clear();
        list.Controls.Add(new Label { Text = "Đang tải…", AutoSize = true, ForeColor = Ui.Muted, Margin = new Padding(12) });
        IReadOnlyList<MosAttempt> rows;
        try
        {
            rows = await ExamHub.ListAttemptsAsync();
        }
        catch (Exception ex)
        {
            list.Controls.Clear();
            list.Controls.Add(new Label { Text = "Không tải được: " + ex.Message, AutoSize = true, ForeColor = Color.Firebrick, Margin = new Padding(12) });
            return;
        }

        var filtered = rows.Where(a => running ? a.Status is "running" : a.Status is not "running").ToList();
        list.Controls.Clear();
        if (filtered.Count == 0)
        {
            list.Controls.Add(new Label
            {
                Text = running ? "Không có bài đang làm dở." : "Chưa nộp bài nào.",
                AutoSize = true,
                ForeColor = Ui.Muted,
                Margin = new Padding(12),
            });
            return;
        }

        foreach (var row in filtered)
        {
            list.Controls.Add(AttemptCard(row, running));
        }

        Ui.FitCards(list);
    }

    Panel AttemptCard(MosAttempt attempt, bool resume)
    {
        var detail = resume
            ? $"{Ui.AppName(attempt.Program)} · {Ui.ModeLabel(attempt.Mode)} · {attempt.StartedAt}"
            : $"{Ui.ModeLabel(attempt.Mode)} · {attempt.StartedAt} · {(attempt.Score is { } s ? $"{s}/{attempt.MaxScore} đã xác minh" : "chưa có điểm")}";
        Button? go = null;
        if (resume)
        {
            go = Ui.PrimaryBtn("Tiếp tục", 120);
            go.BackColor = Ui.Success;
            go.Click += async (_, _) =>
            {
                var (ok, msg) = await ExamHub.ResumeAttemptAsync(attempt);
                if (!ok)
                {
                    MessageBox.Show(msg, "MOS-KulKul", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _app = attempt.Program;
                ShowExamUi();
                EnterDock(compact: true);
            };
        }

        return Ui.ListCard(attempt.Title, detail, go);
    }

    async Task ConfirmStart(MosProject project, string mode)
    {
        var modeText = mode == "testing"
            ? "Thi: ẩn điểm và hướng dẫn cho đến khi nộp bài."
            : "Luyện tập: hiện hướng dẫn từng bước, nút AAA đổi cỡ chữ, và Kiểm tra nhiệm vụ.";
        var ask = MessageBox.Show(
            "Mở «" + project.Title + "» trên " + Ui.AppName(project.Program) + " đã cài trên máy?\n\n" + modeText,
            "Bắt đầu bài MOS",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Question);
        if (ask != DialogResult.OK)
        {
            return;
        }

        Cursor = Cursors.WaitCursor;
        var (ok, msg) = await ExamHub.StartProjectAsync(project.Program, project.Id, mode);
        Cursor = Cursors.Default;
        if (!ok)
        {
            MessageBox.Show(msg, "MOS-KulKul", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _app = project.Program;
        ShowExamUi();
        EnterDock(compact: true);
    }

    void ShowExamUi()
    {
        _examTitle.Text = ExamSession.ProjectTitle ?? "Bài MOS";
        _examMeta.Text = Ui.AppName(ExamSession.Program) + " · " + Ui.ModeLabel(ExamSession.Mode)
            + (ExamSession.Mode == "testing" ? " · điểm ẩn đến khi nộp" : " · có hướng dẫn và kiểm tra nhiệm vụ");
        var train = ExamSession.Mode != "testing";
        _dockCheck.Visible = train;
        _dockHint.Visible = train;
        _helpVisible = train;
        _taskIndex = 0;
        _examStatus.Text = train
            ? "Bóng đèn hiện hướng dẫn. AAA đổi cỡ chữ. Làm bài trên cửa sổ Office."
            : "Làm bài trên cửa sổ Office. Chế độ thi ẩn hướng dẫn và điểm.";
        _tasks.Items.Clear();
        var lines = ExamSession.Rubric?.Criteria is { Count: > 0 } criteria
            ? criteria.Select(c => (c.Id, Ui.StripMarks(string.IsNullOrWhiteSpace(c.Prompt) ? c.Id : c.Prompt))).ToList()
            : ExamSession.Steps.Select((s, i) => ($"{i + 1}", s)).ToList();
        if (lines.Count == 0)
        {
            _tasks.Items.Add(new ListViewItem(["Chưa có danh sách nhiệm vụ.", ""]));
        }
        else
        {
            foreach (var (id, text) in lines)
            {
                _tasks.Items.Add(new ListViewItem([text, "—"]) { Tag = id });
            }
        }

        if (_tasks.Items.Count > 0)
        {
            _tasks.Items[0].Selected = true;
        }

        RenderHelp();

        if (_tasks.Columns.Count >= 1)
        {
            _tasks.Columns[0].Width = Math.Max(180, _exam.ClientSize.Width - 140);
        }

        ShowPage(HubPage.Exam, "Bài thi");
    }

    async Task CheckTasks()
    {
        if (_compact)
        {
            _compact = false;
            EnterDock(compact: false);
        }

        if (ExamSession.Mode == "testing")
        {
            _examStatus.Text = "Chế độ thi ẩn kết quả. Nộp bài khi xong.";
            return;
        }

        _examStatus.Text = "Đang lưu đúng tài liệu bài thi và chấm…";
        var (ok, summary, criteria) = await ExamHub.CheckTasksAsync(_app);
        _examStatus.Text = summary;
        foreach (ListViewItem item in _tasks.Items)
        {
            var id = item.Tag as string;
            var hit = criteria.FirstOrDefault(c => c.Id == id);
            if (string.IsNullOrEmpty(hit.Id) && criteria.Count == _tasks.Items.Count)
            {
                hit = criteria[_tasks.Items.IndexOf(item)];
            }

            if (string.IsNullOrEmpty(hit.Id))
            {
                continue;
            }

            item.SubItems[1].Text = hit.Status switch
            {
                "pass" => "Đạt",
                "fail" => "Chưa đạt",
                "unverified" => "Chưa XN",
                "error" => "Lỗi",
                _ => hit.Status,
            };
            item.ForeColor = hit.Status switch
            {
                "pass" => Ui.Success,
                "fail" => Ui.Danger,
                "unverified" => Ui.Muted,
                _ => Ui.Text,
            };
        }

        if (!ok)
        {
            MessageBox.Show(summary, "MOS-KulKul", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    async Task SubmitExam()
    {
        _examStatus.Text = "Đang lưu và nộp bài…";
        var (ok, msg) = await ExamHub.SubmitAsync(_app);
        if (!ok)
        {
            _examStatus.Text = msg;
            MessageBox.Show(msg, "MOS-KulKul", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        MessageBox.Show("Đã nộp bài.\n\n" + msg, "MOS-KulKul", MessageBoxButtons.OK, MessageBoxIcon.Information);
        ExamSession.ClearExam();
        await ShowCompleted();
    }

    void SaveAndHome()
    {
        ExamHub.SaveInPlace(_app);
        ShowHome();
    }

    void OnPlaceRequest(string state, string? app = null, bool launch = false, string? file = null, bool compact = false)
    {
        if (state is "left" or "right" or "minimized" or "bottom" or "top")
        {
            _state = state;
        }

        if (!string.IsNullOrWhiteSpace(app))
        {
            _app = OfficeApp.Resolve(app).Id;
        }

        if (launch)
        {
            WordWindow.Launch(_app, file);
        }

        ShowExamUi();
        EnterDock(compact || state == "minimized");
    }

    void LeaveDock()
    {
        var switching = _docking;
        _docking = false;
        _compact = false;
        _keepWord.Stop();
        _header.Visible = true;
        _body.Visible = true;
        _exam.Visible = false;
        TopMost = false;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = true;
        MaximizeBox = true;
        ControlBox = true;
        Text = "MOS-KulKul";
        if (_savedWorkspace is { } saved && saved.Width > 200 && saved.Height > 200)
        {
            Bounds = saved;
        }
        else
        {
            var wa = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
            var w = Math.Min(980, wa.Width - 40);
            var h = Math.Min(640, wa.Height - 40);
            Bounds = new Rectangle(wa.X + (wa.Width - w) / 2, wa.Y + (wa.Height - h) / 2, w, h);
        }

        if (switching)
        {
            Show();
        }
    }

    void EnterDock(bool compact)
    {
        var switching = !_docking;
        if (switching && FormBorderStyle != FormBorderStyle.None)
        {
            _savedWorkspace = Bounds;
            Hide();
        }

        _docking = true;
        _compact = compact;
        _view = HubPage.Exam;
        _body.Visible = false;
        _exam.Visible = true;
        _header.Visible = false;
        FormBorderStyle = FormBorderStyle.None;
        ControlBox = false;
        TopMost = _pinned;
        if (!_keepWord.Enabled)
        {
            _keepWord.Start();
        }

        ApplyDock(waitForWord: true);
        if (switching)
        {
            Show();
            TopMost = _pinned;
        }
    }

    void ApplyDock(bool waitForWord)
    {
        if (!_docking)
        {
            return;
        }

        var wa = Screen.FromHandle(IsHandleCreated ? Handle : IntPtr.Zero).WorkingArea;
        var work = new Rect(wa.X, wa.Y, wa.Width, wa.Height);
        var (dock, word) = LayoutMath.Compute(work, _state, _compact);
        if (_compact && HelpOpen)
        {
            dock = LayoutMath.GrowForHelp(dock, work, _state);
        }

        Bounds = new Rectangle(dock.X, dock.Y, dock.W, dock.H);
        TopMost = _pinned;
        ApplyExamChrome();
        if (_tasks.Visible && _tasks.Columns.Count >= 1)
        {
            _tasks.Columns[0].Width = Math.Max(160, _exam.ClientSize.Width - 120);
        }

        if (waitForWord)
        {
            WordWindow.ApplySoon(word, _app);
        }
        else
        {
            WordWindow.Apply(word, _app);
        }
    }

    void ApplyWordOnly()
    {
        if (!Visible || !_docking)
        {
            return;
        }

        var wa = Screen.FromHandle(Handle).WorkingArea;
        var work = new Rect(wa.X, wa.Y, wa.Width, wa.Height);
        var (_, word) = LayoutMath.Compute(work, _state, _compact);
        WordWindow.Apply(word, _app);
        TopMost = true;
    }
}
