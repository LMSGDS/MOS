using System.Text.Json;

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
    readonly HomeRadar _radar = new();
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
    readonly FlowLayoutPanel _objTabs = new();
    readonly ListView _tasks = new();
    readonly Label _examStatus = new();
    readonly Panel _dockChrome = new();
    readonly Panel _navGrip = new();
    readonly FlowLayoutPanel _dockFlow = new();
    readonly Panel _taskBar = new();
    readonly FlowLayoutPanel _taskFlow = new();
    readonly Label _projectLabel = new();
    readonly Label _clockLabel = new();
    readonly Button _dockHelp = Ui.DockSquare(NavIcon.Help, "Accessibility — đổi cỡ chữ đề bài (AA).", Ui.Primary);
    readonly Button _dockAaa = Ui.AaaButton();
    readonly Button _dockPos = Ui.DockSquare(NavIcon.Dock, "Dock the test runner to different positions.", Ui.Primary);
    readonly Button _dockSave = Ui.RunnerBtn("Save Project", Ui.Primary, 118);
    readonly Button _dockTasks = Ui.RunnerBtn("Summary", Ui.Primary, 92);
    readonly Button _dockRestart = Ui.RunnerBtn("Restart Project", Color.FromArgb(194, 120, 3), 130);
    readonly Button _dockGrade = Ui.RunnerBtn("Grade Project", Ui.Primary, 118);
    readonly Button _dockCheck = Ui.DockSquare(NavIcon.Refresh, "Kiểm tra nhiệm vụ", Ui.Primary);
    readonly Button _dockPin = Ui.DockSquare(NavIcon.Pin, "Ghim luôn trên cùng", Ui.Primary);
    readonly Button _dockMenu = Ui.DockSquare(NavIcon.Menu, "Menu tùy chọn thêm", Ui.Primary);
    readonly Button _dockHint = Ui.RunnerBtn("Help", Ui.Primary, 88);
    readonly Button _dockShare = Ui.RunnerBtn("Mark for review", Color.FromArgb(232, 156, 36), 128);
    readonly Button _dockDone = Ui.RunnerBtn("Mark Completed", Color.FromArgb(0, 186, 181), 128);
    readonly Button _dockBack = Ui.RunnerBtn("Previous Task", Color.FromArgb(70, 74, 80), 118);
    readonly Button _dockNext = Ui.RunnerBtn("Next Task", Color.FromArgb(70, 74, 80), 100);
    readonly Panel _setup = new();
    readonly FlowLayoutPanel _setupResume = new();
    readonly ComboBox _setupMode = new();
    readonly Label _setupQ = new();
    readonly Label _setupMin = new();
    readonly Label _setupCut = new();
    MosProject? _setupProject;
    readonly ContextMenuStrip _dockMenuStrip = new();
    readonly ContextMenuStrip _extraMenu = new();
    readonly Panel _helpPane = new();
    readonly Panel _tips = new();
    readonly Label _tipsTitle = new();
    readonly Label _tipsLead = new();
    readonly ListView _tipsList = new();
    readonly Button _tipsClose = Ui.PrimaryBtn("Đóng", 120);
    readonly Label _taskPrompt = new();
    readonly Label _helpTitle = new();
    readonly RichTextBox _helpBody = new();
    readonly Button _aaaBtn = Ui.AaaButton();
    readonly Panel _summary = new();
    readonly Label _summaryTitle = new();
    readonly Label _summaryGroup = new();
    readonly TextBox _summarySearch = new();
    readonly ListView _summaryList = new();
    readonly Button _summaryCancel = Ui.PrimaryBtn("Hủy", 120);
    readonly Button _summaryGo = Ui.PrimaryBtn("Đến", 120);
    readonly Button _summarySave = Ui.PrimaryBtn("Lưu bài", 120);
    readonly Button _summaryFinish = Ui.PrimaryBtn("Finish Test", 130);
    readonly Button _summaryCheck = Ui.PrimaryBtn("Chấm lại", 120);
    readonly Button _summaryRestart = Ui.PrimaryBtn("Làm lại Project", 150);
    readonly Label _summaryStats = new();
    readonly ProgressBar _summaryBar = new();
    readonly ComboBox _summaryFilter = new();
    readonly FlowLayoutPanel _reviewGrid = new();
    readonly Panel _summarySplit = new();
    readonly Panel _summaryDetail = new();
    readonly Label _detailHead = new();
    readonly Label _detailStatus = new();
    readonly Label _detailScore = new();
    readonly TextBox _detailAnalysis = new();
    readonly Label _detailHint = new();
    readonly System.Windows.Forms.Timer _keepWord = new();
    readonly System.Windows.Forms.Timer _examClock = new() { Interval = 1000 };
    int _clockTicks;
    bool _forceSubmitting;
    bool _focusPrompt;
    LocalAgent? _agent;
    string _state = "bottom";
    string _app = "word";
    bool _compact;
    bool _docking;
    bool _pinned = true;
    bool _helpVisible = false;
    bool _tipsOpen;
    bool _summaryOpen;
    bool _navResizing;
    int? _navThickness;
    int _navResizeOrigin;
    int _navResizeStart;
    int _helpScale;
    int _objTab = -1;
    int _taskIndex;
    NavMetrics _nav = LayoutMath.Measure(new Rect(0, 0, LayoutMath.RefWorkW, LayoutMath.RefWorkH));
    Rectangle? _savedWorkspace;
    readonly bool _launchOnStart;
    readonly bool _demoOnStart;
    readonly string? _fileOnStart;
    readonly Panel _helpHeader = new();
    readonly FlowLayoutPanel _helpFooter = new();
    bool _demoRunning;
    IReadOnlyList<MosProject> _items = [];
    enum HubPage { Home, Catalog, Resume, Done, Setup, Exam }
    HubPage _view = HubPage.Home;

    public bool SignOutRequested { get; private set; }

    public MainForm(string? initialState, string? initialApp = null, bool launchOnStart = false, string? fileOnStart = null, bool demoOnStart = false)
    {
        if (!string.IsNullOrWhiteSpace(initialState))
        {
            _state = initialState.ToLowerInvariant();
        }

        _app = OfficeApp.Resolve(initialApp).Id;
        _launchOnStart = launchOnStart;
        _fileOnStart = fileOnStart;
        _demoOnStart = demoOnStart;
        Text = "MOS-KulKul";
        KeyPreview = true;
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        ClientSize = new Size(1080, 700);
        MinimumSize = new Size(LayoutMath.HubMinW, LayoutMath.HubMinH);
        BackColor = Ui.PageBg;
        Font = Ui.BodyFont;
        Ui.ApplyWindowIcon(this);
        Deactivate += OnExamDeactivate;
        _examClock.Tick += (_, _) => TickExamClock();

        BuildHeader();
        BuildHome();
        BuildCatalog();
        BuildListPage(_resume, _resumeList, "Tiếp tục bài", "Chọn bài đang làm dở để mở lại trên Office máy.");
        BuildListPage(_done, _doneList, "Bài đã nộp", "Điểm hiển thị phần đã xác minh. Find/Go To có thể còn chưa xác minh.");
        BuildSetup();
        BuildExam();
        LoadNavThickness();

        _body.Dock = DockStyle.Fill;
        _body.BackColor = Ui.PageBg;
        _body.Padding = new Padding(28, 20, 28, 24);
        _body.Controls.Add(_home);
        _body.Controls.Add(_catalog);
        _body.Controls.Add(_resume);
        _body.Controls.Add(_done);
        _body.Controls.Add(_setup);

        Controls.Add(_exam);
        Controls.Add(_body);
        Controls.Add(_header);

        Load += async (_, _) =>
        {
            Protocol.Register();
            try
            {
                _agent = new LocalAgent(this, OnPlaceRequest, () => _ = RunActionDemo());
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

            if (_demoOnStart && _docking)
            {
                BeginInvoke(async () => await RunActionDemo());
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

        FormClosed += (_, _) =>
        {
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            _agent?.Dispose();
        };
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        DpiChanged += (_, _) => RelayoutDock();
        _keepWord.Interval = 1200;
        _keepWord.Tick += (_, _) =>
        {
            if (_navResizing)
            {
                return;
            }
            ApplyWordOnly();
            try
            {
                WordActionProbe.Poll();
            }
            catch
            {
                // Word busy
            }
        };
    }

    void OnDisplaySettingsChanged(object? sender, EventArgs e) => RelayoutDock();

    void RelayoutDock()
    {
        if (IsDisposed || !IsHandleCreated || !_docking)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(RelayoutDock);
            return;
        }

        ApplyDock(waitForWord: true);
    }

    Rect CurrentWork()
    {
        var screen = IsHandleCreated ? Screen.FromHandle(Handle) : Screen.PrimaryScreen;
        var wa = (screen ?? Screen.PrimaryScreen)?.WorkingArea ?? new Rectangle(0, 0, LayoutMath.RefWorkW, LayoutMath.RefWorkH);
        return LayoutMath.FromScreen(wa);
    }

    Button[] DockButtons() =>
    [
        _dockHelp, _dockAaa, _dockPos, _dockTasks, _dockRestart, _dockHint, _dockShare, _dockDone,
        _dockBack, _dockNext, _dockSave, _dockGrade,
    ];

    void ApplyNavChrome(NavMetrics nav)
    {
        _nav = nav;
        _dockChrome.Padding = new Padding(6, 2, 6, 2);
        _dockChrome.Height = 40;
        foreach (var btn in new[] { _dockHelp, _dockAaa, _dockPos })
        {
            btn.Size = new Size(32, 32);
            btn.Margin = new Padding(2, 2, 2, 2);
        }

        OrientNav();
    }

    void OrientNav()
    {
        _dockChrome.Dock = DockStyle.Top;
        _dockChrome.Height = 40;
        _taskBar.Dock = DockStyle.Bottom;
        _taskBar.Height = 42;
        _dockFlow.FlowDirection = FlowDirection.LeftToRight;
        _dockFlow.WrapContents = false;
        _taskFlow.FlowDirection = FlowDirection.LeftToRight;
        _navGrip.Visible = _docking && _compact && !_summaryOpen;
        if (_navGrip.Visible)
        {
            _navGrip.Dock = _state == "top" ? DockStyle.Bottom : DockStyle.Top;
            _navGrip.Height = 6;
            _navGrip.Cursor = Cursors.SizeNS;
            _navGrip.BringToFront();
        }
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
        var logo = new PictureBox
        {
            Size = new Size(32, 32),
            SizeMode = PictureBoxSizeMode.Zoom,
            Dock = DockStyle.Left,
            Image = Ui.BrandMark(32),
            Margin = new Padding(0, 0, 8, 0),
        };
        _header.Controls.Add(logo);
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
            "Chọn môn → bài thi → New Test hoặc Resume Test → Training / Testing.",
            Ui.Primary,
            ShowCatalog));
        tiles.Controls.Add(Ui.Tile(
            "Tiếp tục bài",
            "Resume Test — mở phiên đã lưu, không tạo lần làm mới.",
            Ui.Success,
            () => _ = ShowResume()));
        tiles.Controls.Add(Ui.Tile(
            "Bài đã nộp",
            "Xem điểm đã xác minh sau Grade Project / Finish Test.",
            Ui.Warning,
            () => _ = ShowCompleted()));
        var homeBody = new Panel { Dock = DockStyle.Fill, BackColor = Ui.PageBg };
        tiles.Dock = DockStyle.Fill;
        _radar.Dock = DockStyle.Top;
        homeBody.Controls.Add(tiles);
        homeBody.Controls.Add(_radar);
        _home.Controls.Add(Ui.StackPage(
            "Trang chủ",
            "Quy trình MOS-KulKul: đăng nhập → chọn môn → chọn bài → New Test / Resume Test → Training hoặc Testing → Test Runner (thanh dock).",
            homeBody));
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
            "Chọn môn học",
            "Thẻ Word / Excel / PowerPoint. Bấm Start trên bài thi, rồi New Test hoặc Resume Test.",
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

        _dockChrome.Dock = DockStyle.Top;
        _dockChrome.Height = 40;
        _dockChrome.BackColor = Ui.Primary;
        _dockChrome.Padding = new Padding(6, 2, 6, 2);

        _dockFlow.Dock = DockStyle.Fill;
        _dockFlow.FlowDirection = FlowDirection.LeftToRight;
        _dockFlow.WrapContents = false;
        _dockFlow.BackColor = Color.Transparent;
        _dockFlow.Padding = Padding.Empty;
        _dockFlow.Margin = Padding.Empty;

        _dockHelp.Click += (_, _) => CycleTypeSize();
        _dockAaa.Click += (_, _) => CycleTypeSize();
        Ui.DockTips.SetToolTip(_dockAaa, "AA — đổi cỡ chữ hướng dẫn trong thanh dock.");
        _dockAaa.AccessibleName = "AA — đổi cỡ chữ hướng dẫn";
        _dockPos.Click += (_, _) =>
        {
            _dockMenuStrip.Show(_dockPos, new Point(0, _dockPos.Height), ToolStripDropDownDirection.BelowRight);
        };
        _dockSave.Click += (_, _) => SaveAndHome();
        _dockTasks.Click += (_, _) => ShowSummary(true);
        _dockRestart.Click += async (_, _) => await RestartCurrentProject();
        _dockGrade.Click += async (_, _) => await CheckTasks();
        _dockCheck.Click += async (_, _) => await CheckTasks();
        _dockPin.Click += (_, _) =>
        {
            _pinned = !_pinned;
            TopMost = _pinned;
            HighlightDockIcons();
        };
        _dockMenu.Click += (_, _) =>
        {
            _extraMenu.Show(_dockMenu, new Point(0, -4), ToolStripDropDownDirection.AboveRight);
        };
        _dockHint.Click += (_, _) => ToggleHelp();
        _dockShare.Click += (_, _) => ToggleMarkForReview();
        _dockDone.Click += (_, _) => ToggleMarkCompleted();
        _dockBack.Click += (_, _) => StepTask(-1);
        _dockNext.Click += (_, _) => StepTask(1);

        _projectLabel.AutoSize = false;
        _projectLabel.Width = 120;
        _projectLabel.Height = 32;
        _projectLabel.ForeColor = Color.White;
        _projectLabel.TextAlign = ContentAlignment.MiddleLeft;
        _projectLabel.Font = Ui.SmallFont;
        _projectLabel.UseMnemonic = false;
        _clockLabel.AutoSize = false;
        _clockLabel.Width = 88;
        _clockLabel.Height = 32;
        _clockLabel.ForeColor = Color.White;
        _clockLabel.TextAlign = ContentAlignment.MiddleCenter;
        _clockLabel.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        _clockLabel.UseMnemonic = false;
        _clockLabel.Text = "00:00:00";

        foreach (var btn in new Control[]
        {
            _dockHelp, _dockAaa, _dockPos, _projectLabel, _clockLabel,
            _dockTasks, _dockRestart, _dockSave, _dockGrade,
        })
        {
            _dockFlow.Controls.Add(btn);
        }

        _dockChrome.Controls.Add(_dockFlow);

        _taskBar.Dock = DockStyle.Bottom;
        _taskBar.Height = 42;
        _taskBar.BackColor = Color.FromArgb(45, 45, 48);
        _taskBar.Padding = new Padding(8, 4, 8, 4);
        _taskFlow.Dock = DockStyle.Fill;
        _taskFlow.FlowDirection = FlowDirection.LeftToRight;
        _taskFlow.WrapContents = false;
        _taskFlow.BackColor = Color.Transparent;
        foreach (var btn in new[] { _dockBack, _dockDone, _dockShare, _dockNext, _dockHint })
        {
            _taskFlow.Controls.Add(btn);
        }

        _taskBar.Controls.Add(_taskFlow);

        _navGrip.BackColor = Ui.PrimaryDark;
        _navGrip.Height = 6;
        _navGrip.Cursor = Cursors.SizeNS;
        Ui.DockTips.SetToolTip(_navGrip, "Kéo để đổi kích thước thanh Navigation");
        _navGrip.MouseDown += OnNavGripDown;
        _navGrip.MouseMove += OnNavGripMove;
        _navGrip.MouseUp += OnNavGripUp;

        _dockMenuStrip.Font = new Font("Segoe UI", 10f);
        _dockMenuStrip.Items.Add(DockMenuItem("↑  Top", "top"));
        _dockMenuStrip.Items.Add(DockMenuItem("↓  Bottom", "bottom"));
        var undockItem = new ToolStripMenuItem("Un-dock");
        undockItem.Click += (_, _) => UnDock();
        _dockMenuStrip.Items.Add(undockItem);

        _extraMenu.Font = new Font("Segoe UI", 10f);
        var extraHome = new ToolStripMenuItem("Trang chủ");
        extraHome.Click += (_, _) => SaveAndHome();
        var extraUndock = new ToolStripMenuItem("Tháo dock");
        extraUndock.Click += (_, _) => UnDock();
        var extraThicker = new ToolStripMenuItem("Thanh Navigation dày hơn");
        extraThicker.Click += (_, _) => NudgeNavThickness(12);
        var extraThinner = new ToolStripMenuItem("Thanh Navigation mỏng hơn");
        extraThinner.Click += (_, _) => NudgeNavThickness(-12);
        var extraResetNav = new ToolStripMenuItem("Đặt lại kích thước thanh Navigation");
        extraResetNav.Click += (_, _) =>
        {
            _navThickness = null;
            SaveNavThickness();
            if (_docking)
            {
                ApplyDock(waitForWord: true);
            }
        };
        var extraSubmit = new ToolStripMenuItem("Nộp bài");
        extraSubmit.Click += async (_, _) => await SubmitExam();
        var extraCheck = new ToolStripMenuItem("Kiểm tra nhiệm vụ");
        extraCheck.Click += async (_, _) => await CheckTasks();
        var extraDemo = new ToolStripMenuItem("Demo tất cả bài tập");
        extraDemo.Click += async (_, _) => await RunActionDemo();
        _extraMenu.Items.Add(extraCheck);
        _extraMenu.Items.Add(extraDemo);
        _extraMenu.Items.Add(extraSubmit);
        _extraMenu.Items.Add(new ToolStripSeparator());
        _extraMenu.Items.Add(extraThicker);
        _extraMenu.Items.Add(extraThinner);
        _extraMenu.Items.Add(extraResetNav);
        _extraMenu.Items.Add(extraUndock);
        _extraMenu.Items.Add(extraHome);

        _examTitle.Dock = DockStyle.Top;
        _examTitle.Font = new Font("Segoe UI", 16f, FontStyle.Regular);
        _examTitle.ForeColor = Ui.Text;
        _examTitle.TextAlign = ContentAlignment.MiddleCenter;
        _examTitle.UseMnemonic = false;
        Ui.BindWrap(_examTitle, 10);

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
        _tasks.SelectedIndexChanged += OnTaskSelected;

        _objTabs.Dock = DockStyle.Top;
        _objTabs.WrapContents = true;
        _objTabs.AutoSize = true;
        _objTabs.Padding = new Padding(8, 4, 8, 6);
        _objTabs.BackColor = Color.FromArgb(248, 249, 251);
        _objTabs.FlowDirection = FlowDirection.LeftToRight;

        BuildHelpPane();
        BuildTipsPane();
        BuildSummaryPane();

        _exam.Controls.Add(_tips);
        _exam.Controls.Add(_summary);
        _exam.Controls.Add(_tasks);
        _exam.Controls.Add(_helpPane);
        _exam.Controls.Add(_taskPrompt);
        _exam.Controls.Add(_objTabs);
        _exam.Controls.Add(_navGrip);
        _exam.Controls.Add(_taskBar);
        _exam.Controls.Add(_dockChrome);
        _exam.Controls.Add(_examStatus);
        _exam.Controls.Add(_examMeta);
        _exam.Controls.Add(_examTitle);
    }

    void BuildHelpPane()
    {
        _helpPane.Dock = DockStyle.Top;
        _helpPane.Height = LayoutMath.HelpH;
        _helpPane.BackColor = Color.FromArgb(245, 247, 249);
        _helpPane.Padding = new Padding(8, 6, 8, 4);
        _helpPane.Visible = false;

        _taskPrompt.Dock = DockStyle.Top;
        _taskPrompt.AutoSize = false;
        _taskPrompt.UseMnemonic = false;
        _taskPrompt.ForeColor = Ui.Text;
        _taskPrompt.Padding = new Padding(16, 4, 16, 8);
        _taskPrompt.TextAlign = ContentAlignment.TopCenter;
        Ui.BindWrap(_taskPrompt, 8, 72);

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

        _helpHeader.Dock = DockStyle.Top;
        _helpHeader.Height = 22;
        _helpHeader.BackColor = Color.White;
        _helpHeader.Padding = new Padding(8, 0, 8, 0);
        _helpHeader.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 230, 234));
            e.Graphics.DrawLine(pen, 0, _helpHeader.Height - 1, _helpHeader.Width, _helpHeader.Height - 1);
        };
        _helpTitle.Text = "Hướng dẫn";
        _helpTitle.Dock = DockStyle.Fill;
        _helpTitle.TextAlign = ContentAlignment.MiddleLeft;
        _helpTitle.ForeColor = Ui.Text;
        _helpTitle.UseMnemonic = false;
        _helpHeader.Controls.Add(_helpTitle);

        _helpFooter.Dock = DockStyle.Bottom;
        _helpFooter.Height = 28;
        _helpFooter.BackColor = Color.FromArgb(248, 249, 250);
        _helpFooter.Padding = new Padding(6, 2, 6, 2);
        _helpFooter.WrapContents = false;
        _helpFooter.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 230, 234));
            e.Graphics.DrawLine(pen, 0, 0, _helpFooter.Width, 0);
        };
        _aaaBtn.Margin = new Padding(0, 0, 0, 0);
        _aaaBtn.Click += (_, _) => CycleTypeSize();
        _helpFooter.Controls.Add(_aaaBtn);

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
            Padding = new Padding(8, 4, 8, 4),
        };
        bodyWrap.Controls.Add(_helpBody);

        card.Controls.Add(bodyWrap);
        card.Controls.Add(_helpFooter);
        card.Controls.Add(_helpHeader);
        _helpPane.Controls.Add(card);
        _taskPrompt.BackColor = Color.White;
        _taskPrompt.MinimumSize = new Size(0, 48);
        ApplyHelpFonts();
    }

    void BuildTipsPane()
    {
        _tips.Dock = DockStyle.Fill;
        _tips.BackColor = Color.White;
        _tips.Padding = new Padding(28, 20, 28, 20);
        _tips.Visible = false;

        _tipsTitle.Text = "Test Runner — mẹo giao diện";
        _tipsTitle.Dock = DockStyle.Top;
        _tipsTitle.Font = Ui.TitleFont;
        _tipsTitle.ForeColor = Ui.Text;
        _tipsTitle.Height = 44;
        _tipsTitle.TextAlign = ContentAlignment.MiddleLeft;
        _tipsTitle.UseMnemonic = false;

        _tipsLead.Text = "? / AA đổi cỡ chữ. Hai ô vuông: Top, Bottom, Un-dock. Help (bóng đèn) chỉ Training — bung bước SOP ngay dưới đề. Testing ẩn Help.";
        _tipsLead.Dock = DockStyle.Top;
        _tipsLead.ForeColor = Ui.Muted;
        _tipsLead.UseMnemonic = false;
        Ui.BindWrap(_tipsLead, 10);

        _tipsList.Dock = DockStyle.Fill;
        _tipsList.View = System.Windows.Forms.View.Details;
        _tipsList.FullRowSelect = true;
        _tipsList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _tipsList.BorderStyle = BorderStyle.None;
        _tipsList.Columns.Add("Mục", 160);
        _tipsList.Columns.Add("Mô tả", 420);
        _tipsList.Columns.Add("Phím", 80);
        foreach (var row in new[]
        {
            ("? / AA", "Accessibility: đổi cỡ chữ đề bài và Help.", "Alt+F / Alt+T"),
            ("Dock", "Dock the test runner to different positions: Top, Bottom, Un-dock.", "Alt+D"),
            ("Summary", "Modal cây Project / Task — Viewed, Review, Complete, Finish Test.", "Alt+L"),
            ("Restart Project", "Khôi phục file gốc của Project hiện tại.", ""),
            ("Save Project", "Lưu snapshot để Resume Test sau.", "Alt+S"),
            ("Grade Project", "Training: chấm ngay. Testing: nộp / ẩn điểm đến Finish Test.", "Alt+G"),
            ("Previous / Next Task", "Điều hướng tuần tự trong Project.", "Alt+B / Alt+N"),
            ("Help", "Training only: bung bước SOP in đậm ngay dưới đề bài.", "Alt+H"),
        })
        {
            _tipsList.Items.Add(new ListViewItem([row.Item1, row.Item2, row.Item3]));
        }

        _tipsClose.Click += (_, _) => ShowTips(false);
        var foot = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.White,
            Padding = new Padding(0, 8, 0, 0),
        };
        foot.Controls.Add(_tipsClose);

        _tips.Controls.Add(_tipsList);
        _tips.Controls.Add(foot);
        _tips.Controls.Add(_tipsLead);
        _tips.Controls.Add(_tipsTitle);
        _tips.Resize += (_, _) =>
        {
            if (_tipsList.Columns.Count >= 2)
            {
                _tipsList.Columns[1].Width = Math.Max(200, _tips.ClientSize.Width - 280);
            }
        };
    }

    void BuildSummaryPane()
    {
        _summary.Dock = DockStyle.Fill;
        _summary.BackColor = Color.White;
        _summary.Padding = new Padding(24, 16, 24, 16);
        _summary.Visible = false;

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.White,
            Padding = new Padding(0, 8, 0, 0),
        };
        _summaryCancel.BackColor = Ui.Primary;
        _summaryGo.BackColor = Color.FromArgb(156, 200, 236);
        _summaryGo.ForeColor = Color.White;
        _summarySave.BackColor = Ui.Primary;
        _summaryFinish.BackColor = Ui.Primary;
        _summaryCancel.Click += (_, _) => ShowSummary(false);
        _summaryGo.Click += async (_, _) => await JumpSelectedSummary();
        _summarySave.Click += (_, _) =>
        {
            ExamHub.SaveInPlace(_app);
            _examStatus.Text = "Đã lưu bài.";
        };
        _summaryFinish.Click += async (_, _) =>
        {
            ShowSummary(false);
            await SubmitExam();
        };
        _summaryCheck.Click += async (_, _) => await CheckTasks();
        _summaryRestart.Click += async (_, _) => await RestartCurrentProject();
        Ui.DockTips.SetToolTip(_summaryGo, "Đến nhiệm vụ đang chọn");
        Ui.DockTips.SetToolTip(_summarySave, "Lưu bài, chưa nộp");
        Ui.DockTips.SetToolTip(_summaryFinish, "Nộp bài và kết thúc");
        Ui.DockTips.SetToolTip(_summaryCheck, "Chấm lại tệp Word và phân tích từng kỹ năng");
        Ui.DockTips.SetToolTip(_summaryRestart, "Restart Project — khôi phục file gốc nếu lỡ làm hỏng");
        _summaryCheck.BackColor = Ui.Success;
        _summaryRestart.BackColor = Color.FromArgb(194, 120, 3);
        foreach (var btn in new[] { _summaryCancel, _summaryGo, _summaryCheck, _summaryRestart, _summarySave, _summaryFinish })
        {
            btn.Margin = new Padding(0, 0, 10, 0);
            btn.Height = 40;
            actions.Controls.Add(btn);
        }

        _summaryList.Dock = DockStyle.Fill;
        _summaryList.View = System.Windows.Forms.View.Details;
        _summaryList.FullRowSelect = true;
        _summaryList.HideSelection = false;
        _summaryList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _summaryList.BorderStyle = BorderStyle.None;
        _summaryList.Columns.Add("#", 44);
        _summaryList.Columns.Add("Name", 240);
        _summaryList.Columns.Add("Viewed", 70);
        _summaryList.Columns.Add("Review", 70);
        _summaryList.Columns.Add("Complete", 80);
        _summaryList.DoubleClick += async (_, _) => await JumpSelectedSummary();
        _summaryList.SelectedIndexChanged += (_, _) => RenderSummaryDetail();

        var listPane = new Panel { Dock = DockStyle.Left, Width = 420, Padding = new Padding(0, 0, 12, 0) };
        listPane.Controls.Add(_summaryList);

        _summaryDetail.Dock = DockStyle.Fill;
        _summaryDetail.BackColor = Ui.PageBg;
        _summaryDetail.Padding = new Padding(16, 12, 16, 12);
        _detailHead.Dock = DockStyle.Top;
        _detailHead.Font = Ui.HeadFont;
        _detailHead.ForeColor = Ui.Text;
        _detailHead.UseMnemonic = false;
        Ui.BindWrap(_detailHead, 8);
        _detailStatus.Dock = DockStyle.Top;
        _detailStatus.Font = Ui.BtnFont;
        _detailStatus.UseMnemonic = false;
        Ui.BindWrap(_detailStatus, 6);
        _detailScore.Dock = DockStyle.Top;
        _detailScore.ForeColor = Ui.Muted;
        _detailScore.UseMnemonic = false;
        Ui.BindWrap(_detailScore, 4);
        _detailHint.Dock = DockStyle.Bottom;
        _detailHint.ForeColor = Ui.Muted;
        _detailHint.UseMnemonic = false;
        Ui.BindWrap(_detailHint, 8);
        _detailAnalysis.Dock = DockStyle.Fill;
        _detailAnalysis.Multiline = true;
        _detailAnalysis.ReadOnly = true;
        _detailAnalysis.BorderStyle = BorderStyle.None;
        _detailAnalysis.BackColor = Color.White;
        _detailAnalysis.ForeColor = Ui.Text;
        _detailAnalysis.ScrollBars = ScrollBars.Vertical;
        _detailAnalysis.Font = Ui.BodyFont;
        var analysisWrap = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(12),
            Margin = new Padding(0, 8, 0, 8),
        };
        analysisWrap.Controls.Add(_detailAnalysis);
        _summaryDetail.Controls.Add(analysisWrap);
        _summaryDetail.Controls.Add(_detailHint);
        _summaryDetail.Controls.Add(_detailScore);
        _summaryDetail.Controls.Add(_detailStatus);
        _summaryDetail.Controls.Add(_detailHead);

        _summarySplit.Dock = DockStyle.Fill;
        _summarySplit.Controls.Add(_summaryDetail);
        _summarySplit.Controls.Add(listPane);
        _summary.Resize += (_, _) =>
        {
            listPane.Width = Math.Clamp(_summary.ClientSize.Width * 46 / 100, 280, 520);
            if (_summaryList.Columns.Count >= 2)
            {
                _summaryList.Columns[1].Width = Math.Max(160, listPane.ClientSize.Width - 150);
            }
        };

        var searchRow = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.White };
        var find = Ui.PrimaryBtn("Tìm", 88);
        find.Dock = DockStyle.Right;
        find.Height = 36;
        find.Click += (_, _) => FillSummary();
        _summaryFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        _summaryFilter.Width = 148;
        _summaryFilter.Dock = DockStyle.Right;
        _summaryFilter.Items.AddRange(["Tất cả", "Đã đạt", "Chưa đạt", "Chưa xác minh", "Chưa chấm", "Đánh dấu xem lại"]);
        _summaryFilter.SelectedIndex = 0;
        _summaryFilter.SelectedIndexChanged += (_, _) => FillSummary();
        _summarySearch.Dock = DockStyle.Fill;
        _summarySearch.Font = Ui.BodyFont;
        _summarySearch.PlaceholderText = "Tìm tên hoặc mã kỹ năng";
        _summarySearch.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                FillSummary();
                e.Handled = true;
            }
        };
        searchRow.Controls.Add(_summarySearch);
        searchRow.Controls.Add(_summaryFilter);
        searchRow.Controls.Add(find);

        var meta = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Color.White };
        _summaryBar.Dock = DockStyle.Bottom;
        _summaryBar.Height = 10;
        _summaryBar.Style = ProgressBarStyle.Continuous;
        _summaryStats.Dock = DockStyle.Fill;
        _summaryStats.ForeColor = Ui.Muted;
        _summaryStats.TextAlign = ContentAlignment.MiddleLeft;
        _summaryStats.UseMnemonic = false;
        meta.Controls.Add(_summaryStats);
        meta.Controls.Add(_summaryBar);

        _reviewGrid.Dock = DockStyle.Top;
        _reviewGrid.AutoSize = true;
        _reviewGrid.WrapContents = true;
        _reviewGrid.Padding = new Padding(0, 0, 0, 8);
        _reviewGrid.Visible = false;

        _summaryTitle.Dock = DockStyle.Top;
        _summaryTitle.Height = 40;
        _summaryTitle.Font = Ui.HeadFont;
        _summaryTitle.TextAlign = ContentAlignment.MiddleCenter;
        _summaryTitle.ForeColor = Ui.Text;
        _summaryTitle.UseMnemonic = false;

        _summaryGroup.Dock = DockStyle.Top;
        _summaryGroup.Height = 36;
        _summaryGroup.BackColor = Ui.Primary;
        _summaryGroup.ForeColor = Color.White;
        _summaryGroup.Font = Ui.HeadFont;
        _summaryGroup.TextAlign = ContentAlignment.MiddleLeft;
        _summaryGroup.Padding = new Padding(12, 0, 12, 0);
        _summaryGroup.UseMnemonic = false;

        _summary.Controls.Add(_summarySplit);
        _summary.Controls.Add(actions);
        _summary.Controls.Add(searchRow);
        _summary.Controls.Add(meta);
        _summary.Controls.Add(_reviewGrid);
        _summary.Controls.Add(_summaryGroup);
        _summary.Controls.Add(_summaryTitle);
    }

    bool HelpOpen => _helpVisible && ExamSession.HintsAllowed;

    void CycleTypeSize()
    {
        _helpScale = (_helpScale + 1) % 3;
        ApplyHelpFonts();
        RenderBrief();
        RenderHelp();
    }

    void ToggleHelp()
    {
        if (!ExamSession.HintsAllowed)
        {
            return;
        }

        _tipsOpen = false;
        _helpVisible = !_helpVisible;
        ExamSession.HintTier = _helpVisible ? 3 : 0;
        if (_helpVisible)
        {
            var elapsed = Math.Max(0, (int)(DateTime.UtcNow - ExamSession.OpenedUtc).TotalMilliseconds);
            _ = ExamHub.TrackAsync("hint", new
            {
                elapsed_ms = elapsed,
                source = "dock",
                tier = ExamSession.HintTier,
                task_id = CurrentTaskId(),
            });
        }

        RelayoutExam();
        RenderHelp();
        HighlightDockIcons();
    }

    string CurrentTaskId()
    {
        if (_taskIndex >= 0 && _taskIndex < _tasks.Items.Count)
        {
            return _tasks.Items[_taskIndex].Tag as string ?? "";
        }

        return "";
    }

    void ShowTips(bool open)
    {
        _tipsOpen = open;
        if (open)
        {
            _summaryOpen = false;
        }

        RelayoutExamOverlay(open);
    }

    void RelayoutExam()
    {
        if (_docking)
        {
            ApplyDock(waitForWord: true);
        }
        else
        {
            ApplyExamChrome();
        }
    }

    void RelayoutExamOverlay(bool open)
    {
        if (_docking)
        {
            if (open)
            {
                var work = CurrentWork();
                var nav = LayoutMath.Measure(work);
                var w = Math.Min(nav.SummaryW, work.W - 40);
                var h = Math.Min(nav.SummaryH, work.H - 40);
                FitOverlay(work.X + (work.W - w) / 2, work.Y + (work.H - h) / 2, w, h);
                ApplyExamChrome();
            }
            else
            {
                ApplyDock(waitForWord: true);
            }
        }
        else
        {
            ApplyExamChrome();
        }
    }

    void ShowSummary(bool open)
    {
        _summaryOpen = open;
        if (open)
        {
            _tipsOpen = false;
            var examWide = ExamSession.Bank.Projects.Count > 0;
            _summaryTitle.Text = examWide
                ? "Exam Summary — lưới nhiệm vụ toàn đề"
                : (ExamSession.ProjectTitle ?? "Danh sách kỹ năng") + " — Tổng hợp";
            _summaryGroup.Text = "  " + (examWide
                ? $"{ExamSession.Bank.Projects.Count} Project · cờ cam = Mark for Review"
                : ExamSession.ProjectTitle ?? "Bài thi");
            _summarySearch.Text = "";
            _summaryCheck.Visible = ExamSession.Mode != "testing";
            _summaryRestart.Visible = ExamSession.Bank.RestartProject || ExamSession.Mode == "testing";
            FillSummary();
        }

        RelayoutExamOverlay(open);
    }

    void FillSummary()
    {
        var q = (_summarySearch.Text ?? "").Trim();
        var filter = _summaryFilter.SelectedItem as string ?? "Tất cả";
        var rows = ExamSession.LastCheck;
        var currentId = ExamSession.ProjectId ?? "";
        _summaryList.BeginUpdate();
        _summaryList.Items.Clear();
        var pass = 0;
        var fail = 0;
        var pending = 0;
        var ungraded = 0;
        var markedN = 0;
        var total = 0;
        var examWide = ExamSession.Bank.Projects.Count > 0;
        if (examWide)
        {
            var global = 0;
            foreach (var block in ExamSession.Bank.Projects)
            {
                var src = ExamSession.Bank.SourceId(block);
                var current = string.Equals(src, currentId, StringComparison.OrdinalIgnoreCase);
                var n = current ? Math.Max(block.Tasks.Count, _tasks.Items.Count) : block.Tasks.Count;
                for (var t = 0; t < n; t++)
                {
                    global++;
                    total++;
                    var name = t < block.Tasks.Count && !string.IsNullOrWhiteSpace(block.Tasks[t].Instruction)
                        ? block.Tasks[t].Instruction
                        : current && t < _tasks.Items.Count
                            ? _tasks.Items[t].Text
                            : block.Title;
                    var id = current && t < _tasks.Items.Count ? _tasks.Items[t].Tag as string ?? "" : "";
                    var hit = current ? SkillReview.Find(rows, id, t) : default;
                    var status = hit.Status ?? "";
                    if (status == "pass")
                    {
                        pass++;
                    }
                    else if (status is "fail" or "error")
                    {
                        fail++;
                    }
                    else if (status == "unverified")
                    {
                        pending++;
                    }
                    else
                    {
                        ungraded++;
                    }

                    var marked = ExamSession.IsMarked(src, t);
                    if (marked)
                    {
                        markedN++;
                    }

                    if (q.Length > 0 &&
                        name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 &&
                        id.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 &&
                        global.ToString().IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    var wanted = filter switch
                    {
                        "Đã đạt" => status == "pass",
                        "Chưa đạt" => status is "fail" or "error",
                        "Chưa xác minh" => status == "unverified",
                        "Chưa chấm" => string.IsNullOrWhiteSpace(status),
                        "Đánh dấu xem lại" => marked,
                        _ => true,
                    };
                    if (!wanted)
                    {
                        continue;
                    }

                    var viewed = ExamSession.IsViewed(src, t);
                    var done = ExamSession.IsCompleted(src, t);
                    var row = new ListViewItem([
                        global.ToString(),
                        $"P{block.Order} · {name}",
                        viewed ? "✓" : "",
                        marked ? "✓" : "",
                        done ? "✓" : "",
                    ])
                    {
                        Tag = (src, t),
                        ForeColor = marked ? Color.FromArgb(194, 120, 3) : SkillReview.ColorOf(status),
                    };
                    if (current && t == _taskIndex)
                    {
                        row.Selected = true;
                    }

                    _summaryList.Items.Add(row);
                }
            }
        }
        else
        {
            for (var i = 0; i < _tasks.Items.Count; i++)
            {
                var src = _tasks.Items[i];
                var name = src.Text;
                var id = src.Tag as string ?? "";
                var hit = SkillReview.Find(rows, id, i);
                var status = hit.Status;
                if (string.IsNullOrWhiteSpace(status) && src.SubItems.Count > 1)
                {
                    status = src.SubItems[1].Text switch
                    {
                        "Đạt" => "pass",
                        "Chưa đạt" => "fail",
                        "Chưa XN" => "unverified",
                        "Lỗi" => "error",
                        _ => "",
                    };
                }

                if (status == "pass")
                {
                    pass++;
                }
                else if (status == "fail" || status == "error")
                {
                    fail++;
                }
                else if (status == "unverified")
                {
                    pending++;
                }
                else
                {
                    ungraded++;
                }

                var marked = ExamSession.IsMarked(currentId, i);
                if (marked)
                {
                    markedN++;
                }

                if (q.Length > 0 &&
                    name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 &&
                    id.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 &&
                    (i + 1).ToString().IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var wanted = filter switch
                {
                    "Đã đạt" => status == "pass",
                    "Chưa đạt" => status is "fail" or "error",
                    "Chưa xác minh" => status == "unverified",
                    "Chưa chấm" => string.IsNullOrWhiteSpace(status),
                    "Đánh dấu xem lại" => marked,
                    _ => true,
                };
                if (!wanted)
                {
                    continue;
                }

                var viewed = ExamSession.IsViewed(currentId, i);
                var done = ExamSession.IsCompleted(currentId, i);
                var row = new ListViewItem([
                    (i + 1).ToString(),
                    name,
                    viewed ? "✓" : "",
                    marked ? "✓" : "",
                    done ? "✓" : "",
                ]) { Tag = (currentId, i) };
                row.ForeColor = marked ? Color.FromArgb(194, 120, 3) : SkillReview.ColorOf(status);
                if (i == _taskIndex)
                {
                    row.Selected = true;
                }

                _summaryList.Items.Add(row);
            }

            total = _tasks.Items.Count;
        }

        _summaryList.EndUpdate();
        PaintReviewGrid();
        var checkedN = pass + fail + pending;
        _summaryBar.Maximum = Math.Max(1, total);
        _summaryBar.Value = ExamSession.HideLiveScore
            ? Math.Min(_summaryBar.Maximum, markedN)
            : Math.Min(_summaryBar.Maximum, checkedN);
        _summaryStats.Text = ExamSession.HideLiveScore
            ? $"Chế độ thi: ẩn điểm. Cờ xem lại {markedN}/{Math.Max(1, total)} nhiệm vụ."
            : $"Hoàn thành kiểm tra: {checkedN}/{Math.Max(1, total)} nhiệm vụ · không hiện điểm số khi đang làm.";
        if (_summaryList.Items.Count == 0)
        {
            RenderSummaryDetail();
        }
        else if (_summaryList.SelectedItems.Count == 0)
        {
            _summaryList.Items[0].Selected = true;
        }
        else
        {
            RenderSummaryDetail();
        }
    }

    void PaintReviewGrid()
    {
        _reviewGrid.SuspendLayout();
        _reviewGrid.Controls.Clear();
        var examWide = ExamSession.Bank.Projects.Count > 0;
        _reviewGrid.Visible = examWide || ExamSession.Mode == "testing";
        if (_reviewGrid.Visible)
        {
            var n = 0;
            if (examWide)
            {
                foreach (var block in ExamSession.Bank.Projects)
                {
                    var src = ExamSession.Bank.SourceId(block);
                    for (var t = 0; t < block.Tasks.Count; t++)
                    {
                        n++;
                        _reviewGrid.Controls.Add(ReviewCell(n, src, t));
                    }
                }
            }
            else
            {
                var pid = ExamSession.ProjectId ?? "";
                for (var i = 0; i < _tasks.Items.Count; i++)
                {
                    _reviewGrid.Controls.Add(ReviewCell(i + 1, pid, i));
                }
            }
        }

        _reviewGrid.ResumeLayout();
    }

    Button ReviewCell(int number, string projectId, int taskIndex)
    {
        var marked = ExamSession.IsMarked(projectId, taskIndex);
        var current = string.Equals(projectId, ExamSession.ProjectId, StringComparison.OrdinalIgnoreCase)
            && taskIndex == _taskIndex;
        var btn = new Button
        {
            Text = number.ToString(),
            Width = 36,
            Height = 28,
            Margin = new Padding(2),
            FlatStyle = FlatStyle.Flat,
            BackColor = marked
                ? Color.FromArgb(234, 140, 24)
                : current ? Color.FromArgb(45, 45, 48) : Color.FromArgb(226, 230, 236),
            ForeColor = marked || current ? Color.White : Ui.Text,
            Tag = (projectId, taskIndex),
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += async (_, _) =>
        {
            foreach (ListViewItem item in _summaryList.Items)
            {
                if (item.Tag is ValueTuple<string, int> pair
                    && string.Equals(pair.Item1, projectId, StringComparison.OrdinalIgnoreCase)
                    && pair.Item2 == taskIndex)
                {
                    item.Selected = true;
                    item.EnsureVisible();
                    break;
                }
            }

            await JumpToTask(projectId, taskIndex);
        };
        return btn;
    }

    void RenderSummaryDetail()
    {
        if (_summaryList.SelectedItems.Count == 0)
        {
            _detailHead.Text = "Chọn một kỹ năng bên trái.";
            _detailStatus.Text = "";
            _detailScore.Text = "";
            _detailAnalysis.Text = "";
            _detailHint.Text = "";
            return;
        }

        var tag = _summaryList.SelectedItems[0].Tag;
        var projectId = tag is ValueTuple<string, int> loc ? loc.Item1 : ExamSession.ProjectId ?? "";
        var i = tag is int idx
            ? idx
            : tag is ValueTuple<string, int> pair ? pair.Item2 : 0;
        var current = string.Equals(projectId, ExamSession.ProjectId, StringComparison.OrdinalIgnoreCase);
        var name = _summaryList.SelectedItems[0].SubItems.Count > 1
            ? _summaryList.SelectedItems[0].SubItems[1].Text
            : "";
        if (current && _tasks.Items.Count > 0)
        {
            i = Math.Clamp(i, 0, Math.Max(0, _tasks.Items.Count - 1));
            name = _tasks.Items[i].Text;
        }

        var id = current && _tasks.Items.Count > i ? _tasks.Items[i].Tag as string : "";
        var item = ExamSession.Rubric?.Criteria?.FirstOrDefault(c => c.Id == id);
        if (current && item == null && ExamSession.Rubric?.Criteria is { Count: > 0 } list && i < list.Count)
        {
            item = list[i];
        }

        var hit = current ? SkillReview.Find(ExamSession.LastCheck, id, i) : default;
        var marked = ExamSession.IsMarked(projectId, i);
        _detailHead.Text = $"{_summaryList.SelectedItems[0].Text}. {name}";
        _detailStatus.Text = marked
            ? "Đã đánh dấu xem lại — bấm Đến để nhảy về đúng Project."
            : current ? SkillReview.Headline(hit.Status) : "Nhiệm vụ thuộc Project khác — bấm Đến để mở file đó.";
        _detailStatus.ForeColor = marked ? Color.FromArgb(194, 120, 3) : SkillReview.ColorOf(hit.Status);
        _detailScore.Text = string.IsNullOrWhiteSpace(id) ? "" : id + (hit.Possible > 0 && !SkillReview.HideScores ? $"  ·  {hit.Earned:0}/{hit.Possible:0} điểm" : "");
        _detailAnalysis.Text = current ? SkillReview.Analysis(hit, item) : "Chuyển Project để xem phân tích Q-Matrix của nhiệm vụ này.";
        _detailHint.Text = current ? SkillReview.Hint(hit, item) : "";
    }

    async Task JumpSelectedSummary()
    {
        if (_summaryList.SelectedItems.Count == 0)
        {
            ShowSummary(false);
            return;
        }

        var tag = _summaryList.SelectedItems[0].Tag;
        if (tag is ValueTuple<string, int> jump)
        {
            await JumpToTask(jump.Item1, jump.Item2);
            return;
        }

        var i = tag is int idx ? idx : 0;
        if (_tasks.Items.Count > 0)
        {
            i = Math.Clamp(i, 0, _tasks.Items.Count - 1);
            _tasks.SelectedIndices.Clear();
            _tasks.Items[i].Selected = true;
            _tasks.EnsureVisible(i);
            _taskIndex = i;
            _objTab = i;
            HighlightObjectiveTabs();
            RenderBrief();
            RenderHelp();
        }

        ShowSummary(false);
    }

    async Task JumpToTask(string projectId, int taskIndex)
    {
        if (!string.IsNullOrWhiteSpace(projectId)
            && !string.Equals(projectId, ExamSession.ProjectId, StringComparison.OrdinalIgnoreCase))
        {
            ShowSummary(false);
            var block = ExamSession.Bank.BlockFor(projectId);
            if (block is not null)
            {
                await SwitchBankProject(block, taskIndex);
            }

            return;
        }

        if (_tasks.Items.Count > 0)
        {
            var i = Math.Clamp(taskIndex, 0, _tasks.Items.Count - 1);
            _tasks.SelectedIndices.Clear();
            _tasks.Items[i].Selected = true;
            _tasks.EnsureVisible(i);
            _taskIndex = i;
            _objTab = i;
            HighlightObjectiveTabs();
            RenderBrief();
            RenderHelp();
        }

        ShowSummary(false);
    }

    void ApplyHelpFonts()
    {
        var compact = _docking && _compact;
        var promptPt = compact
            ? 9.5f
            : _helpScale switch { 2 => 16f, 1 => 14f, _ => 12f };
        var bodyPt = compact
            ? 8.5f
            : _helpScale switch { 2 => 12f, 1 => 10.5f, _ => 9.5f };
        var titlePt = compact
            ? 9f
            : _helpScale switch { 2 => 20f, 1 => 17f, _ => 15f };
        _taskPrompt.Font = new Font("Segoe UI", promptPt, FontStyle.Bold);
        _helpTitle.Font = new Font("Segoe UI", titlePt, FontStyle.Regular);
        _helpBody.Font = new Font("Segoe UI", bodyPt);
    }

    void FillObjectiveTabs()
    {
        _objTabs.SuspendLayout();
        _objTabs.Controls.Clear();
        _objTabs.Controls.Add(ObjectiveTab("Overview", -1));
        var n = _tasks.Items.Count;
        for (var i = 0; i < n; i++)
        {
            _objTabs.Controls.Add(ObjectiveTab("Task " + (i + 1), i));
        }

        _objTabs.ResumeLayout();
        HighlightObjectiveTabs();
    }

    Button ObjectiveTab(string text, int index)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 28,
            MinimumSize = new Size(88, 28),
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 0, 4, 2),
            Padding = new Padding(10, 0, 10, 0),
            Tag = index,
            Cursor = Cursors.Hand,
            UseMnemonic = false,
            Font = Ui.SmallFont,
            ForeColor = Color.White,
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (_, _) => SelectObjectiveTab(index);
        return btn;
    }

    void SelectObjectiveTab(int index)
    {
        _objTab = index;
        if (index >= 0 && _tasks.Items.Count > 0)
        {
            var i = Math.Clamp(index, 0, _tasks.Items.Count - 1);
            _taskIndex = i;
            if (_tasks.SelectedIndices.Count != 1 || _tasks.SelectedIndices[0] != i)
            {
                _tasks.SelectedIndexChanged -= OnTaskSelected;
                _tasks.SelectedIndices.Clear();
                _tasks.Items[i].Selected = true;
                _tasks.EnsureVisible(i);
                _tasks.SelectedIndexChanged += OnTaskSelected;
            }
        }

        HighlightObjectiveTabs();
        if (index >= 0)
        {
            ExamSession.MarkViewed(ExamSession.ProjectId, index);
        }

        RenderBrief();
        RenderHelp();
        HighlightDockIcons();
    }

    void OnTaskSelected(object? sender, EventArgs e)
    {
        if (_tasks.SelectedIndices.Count == 0)
        {
            return;
        }

        _taskIndex = _tasks.SelectedIndices[0];
        _objTab = _taskIndex;
        ExamSession.MarkViewed(ExamSession.ProjectId, _taskIndex);
        HighlightObjectiveTabs();
        RenderBrief();
        RenderHelp();
        HighlightDockIcons();
    }

    void HighlightObjectiveTabs()
    {
        foreach (Control child in _objTabs.Controls)
        {
            if (child is not Button btn || btn.Tag is not int idx)
            {
                continue;
            }

            var on = idx == _objTab;
            var marked = ExamSession.IsMarked(ExamSession.ProjectId, idx);
            btn.BackColor = marked
                ? Color.FromArgb(234, 140, 24)
                : on ? Color.FromArgb(45, 45, 48) : Color.FromArgb(110, 116, 124);
            btn.ForeColor = Color.White;
            btn.FlatAppearance.MouseOverBackColor = marked
                ? Color.FromArgb(194, 120, 3)
                : on ? Color.FromArgb(32, 32, 36) : Color.FromArgb(90, 96, 104);
        }
    }

    void RenderBrief()
    {
        var title = ExamSession.Rubric?.Title;
        if (string.IsNullOrWhiteSpace(title))
        {
            title = ExamSession.ProjectTitle ?? "Bài MOS";
        }

        _examTitle.Text = title;
        if (_objTab < 0)
        {
            _taskPrompt.Font = new Font("Segoe UI", _compact && _docking ? 9.5f : 11f, FontStyle.Regular);
            _taskPrompt.Text = ObjectiveOverview();
            return;
        }

        var criteria = ExamSession.Rubric?.Criteria;
        if (criteria is { Count: > 0 })
        {
            var i = Math.Clamp(_objTab, 0, criteria.Count - 1);
            var item = criteria[i];
            _taskPrompt.Font = new Font("Segoe UI", _compact && _docking ? 9.5f : 11f, FontStyle.Bold);
            _taskPrompt.Text = Ui.StripMarks(string.IsNullOrWhiteSpace(item.Prompt) ? item.Id : item.Prompt);
            return;
        }

        if (_tasks.Items.Count > 0)
        {
            var i = Math.Clamp(_objTab, 0, _tasks.Items.Count - 1);
            _taskPrompt.Font = new Font("Segoe UI", _compact && _docking ? 9.5f : 11f, FontStyle.Bold);
            _taskPrompt.Text = _tasks.Items[i].Text;
            return;
        }

        _taskPrompt.Text = ObjectiveOverview();
    }

    string ObjectiveOverview() =>
        ExamHub.OverviewText(ExamSession.Rubric, ExamSession.ProjectTitle, _tasks.Items.Count);

    void RenderHelp()
    {
        var bodyPt = (_docking && _compact)
            ? 8.5f
            : _helpScale switch { 2 => 12f, 1 => 10.5f, _ => 9.5f };
        var criteria = ExamSession.Rubric?.Criteria;
        if (_objTab < 0)
        {
            SetHelpBody(["Chọn Task 1 để xem bước SOP ngay dưới đề bài (Training). Testing ẩn Help."], bodyPt);
            return;
        }

        if (criteria is not { Count: > 0 })
        {
            SetHelpBody(["Làm đúng yêu cầu trên đề trong Microsoft Office đã cài trên máy."], bodyPt);
            return;
        }

        _taskIndex = Math.Clamp(_taskIndex, 0, criteria.Count - 1);
        var item = criteria[_taskIndex];
        var bankTiers = ExamSession.Bank.HintTiers(_taskIndex, ExamSession.ProjectId);
        IReadOnlyList<string> steps = item.HelpSteps is { Count: > 0 }
            ? item.HelpSteps
            : bankTiers.Length > 0 ? bankTiers : SkillReview.HintSteps(item);
        if (steps.Count > 0)
        {
            SetHelpBody(steps, bodyPt);
            return;
        }

        SetHelpBody(["Làm đúng yêu cầu trên đề trong Microsoft Office đã cài trên máy."], bodyPt);
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
        if (delta > 0 && !string.IsNullOrWhiteSpace(ExamSession.HardStopReason))
        {
            _examStatus.Text = ExamSession.HardStopReason;
            return;
        }

        var last = Math.Max(-1, _tasks.Items.Count - 1);
        var next = _objTab + delta;
        if (delta > 0 && next > last)
        {
            var nxt = ExamSession.Bank.NextBlock(ExamSession.ProjectId);
            if (nxt is not null)
            {
                _ = SwitchBankProject(nxt);
                return;
            }
        }

        SelectObjectiveTab(Math.Clamp(next, -1, last));
    }

    async Task SwitchBankProject(BankProjectBlock block, int taskIndex = 0)
    {
        var source = ExamSession.Bank.SourceId(block);
        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        Cursor = Cursors.WaitCursor;
        var (ok, msg) = await ExamHub.StartProjectAsync(ExamSession.Program, source, ExamSession.Mode);
        Cursor = Cursors.Default;
        if (!ok)
        {
            MessageBox.Show(msg, "MOS-KulKul", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ShowExamUi();
        EnterDock(compact: true);
        SelectTaskIndex(taskIndex);
        _examStatus.Text = "Đã chuyển " + ExamSession.Bank.ProjectCaption(ExamSession.ProjectId);
    }

    async Task RestartCurrentProject()
    {
        if (!ExamSession.Bank.RestartProject && ExamSession.Mode != "testing")
        {
            return;
        }

        var ask = MessageBox.Show(
            "Xóa bài làm hiện tại và mở lại file gốc của Project này?",
            "Restart Project",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Question);
        if (ask != DialogResult.OK)
        {
            return;
        }

        Cursor = Cursors.WaitCursor;
        var (ok, msg) = await ExamHub.RestartProjectAsync(_app);
        Cursor = Cursors.Default;
        _examStatus.Text = msg;
        if (!ok)
        {
            MessageBox.Show(msg, "MOS-KulKul", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
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
        WordWindow.CancelPlace();
        _header.Visible = false;
        _body.Visible = false;
        _exam.Visible = true;
        TopMost = _pinned;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = true;
        MaximizeBox = true;
        ControlBox = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "MOS-KulKul";
        ApplyExamChrome();
        RestoreHubWindow();
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
                item.BackColor = state == _state ? Ui.Primary : Color.White;
                item.ForeColor = state == _state ? Color.White : Ui.Text;
            }
            else if (item.Text == "Un-dock")
            {
                item.BackColor = !_docking ? Ui.Primary : Color.White;
                item.ForeColor = !_docking ? Color.White : Ui.Text;
            }
        }

        foreach (var btn in DockButtons())
        {
            btn.BackColor = Ui.Primary;
        }

        Ui.DockTips.SetToolTip(_dockHelp, "Accessibility / font size (AA).");
        Ui.DockTips.SetToolTip(_dockAaa, "AA — đổi cỡ chữ hướng dẫn trong thanh dock.");
        Ui.DockTips.SetToolTip(_dockHint, !ExamSession.HintsAllowed
            ? "Testing — Help SOP đã tắt"
            : HelpOpen ? "Ẩn Help" : "Help — bước SOP ngay dưới đề bài");
        Ui.DockTips.SetToolTip(_dockTasks, "Summary — Viewed / Review / Complete");
        Ui.DockTips.SetToolTip(_dockRestart, "Restart Project — khôi phục file gốc");
        Ui.DockTips.SetToolTip(_dockGrade, "Grade Project");
        Ui.DockTips.SetToolTip(_dockSave, "Save Project — snapshot để Resume Test");
        Ui.DockTips.SetToolTip(_dockBack, "Previous Task");
        Ui.DockTips.SetToolTip(_dockNext, "Next Task");
        Ui.DockTips.SetToolTip(_dockPos, "Dock the test runner to different positions.");
        Ui.DockTips.SetToolTip(_dockDone, "Mark Completed");
        Ui.DockTips.SetToolTip(_dockShare, "Mark for review");
        _dockHint.BackColor = HelpOpen ? Color.FromArgb(20, 20, 24) : Ui.Primary;
        _dockDone.BackColor = ExamSession.IsCompleted(ExamSession.ProjectId, Math.Max(0, _taskIndex))
            ? Color.FromArgb(0, 140, 136)
            : Color.FromArgb(0, 186, 181);
        _dockShare.BackColor = ExamSession.IsMarked(ExamSession.ProjectId, Math.Max(0, _taskIndex))
            ? Color.FromArgb(194, 120, 3)
            : Color.FromArgb(232, 156, 36);
    }

    void ApplyExamChrome()
    {
        var showTips = _tipsOpen;
        var showSummary = _summaryOpen && !showTips;
        var showTasks = !showSummary && !showTips && (!_compact || !_docking);
        var showBrief = !showSummary && !showTips;
        var showHelp = !showSummary && !showTips && HelpOpen;
        _exam.Padding = showTasks || showSummary || showTips ? new Padding(12, 8, 12, 0) : Padding.Empty;
        _exam.BackColor = ExamSession.Bank.CertiportSplit && !showSummary && !showTips
            ? Color.FromArgb(196, 200, 204)
            : showTasks || showSummary || showTips ? Color.White : Color.FromArgb(245, 247, 249);
        _tips.Visible = showTips;
        _summary.Visible = showSummary;
        _dockChrome.Visible = !showSummary && !showTips;
        _taskBar.Visible = !showSummary && !showTips;
        _navGrip.Visible = !showSummary && !showTips && _docking && _compact;
        if (_docking && _compact && !showSummary)
        {
            OrientNav();
            _dockChrome.Padding = new Padding(_nav.ChromePad);
        }
        _helpPane.Visible = showHelp;
        if (showHelp && _compact && _docking)
        {
            _helpPane.Dock = DockStyle.Fill;
            _helpPane.Padding = new Padding(4, 4, 4, 2);
            _helpHeader.Height = 20;
            _helpFooter.Visible = false;
            _helpFooter.Height = 0;
        }
        else
        {
            _helpPane.Dock = DockStyle.Top;
            _helpPane.Height = _nav.HelpH;
            _helpPane.Padding = new Padding(8, 6, 8, 4);
            _helpHeader.Height = 32;
            _helpFooter.Visible = true;
            _helpFooter.Height = 28;
        }
        ApplyHelpFonts();
        _tasks.Visible = showTasks;
        _examTitle.Visible = showBrief;
        _examTitle.Font = new Font("Segoe UI", _compact && _docking ? 12f : 16f, FontStyle.Regular);
        _examMeta.Visible = showTasks;
        _examStatus.Visible = showTasks;
        _objTabs.Visible = showBrief;
        _taskPrompt.Visible = showBrief;
        _dockTasks.Visible = true;
        _dockHelp.Visible = true;
        _dockAaa.Visible = true;
        _dockRestart.Visible = ExamSession.Bank.RestartProject || ExamSession.Mode == "testing";
        _dockGrade.Visible = true;
        _dockHint.Visible = ExamSession.HintsAllowed;
        _dockCheck.Visible = false;
        _dockPin.Visible = false;
        _dockMenu.Visible = false;
        _dockDone.Visible = true;
        _dockShare.Visible = true;
        RenderBrief();
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
        _setup.Visible = view == HubPage.Setup;
        _back.Visible = view is HubPage.Catalog or HubPage.Resume or HubPage.Done or HubPage.Setup;
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
        _ = _radar.LoadAsync();
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

        foreach (var group in ExamHub.GroupByObjective(_items))
        {
            _tests.Controls.Add(group.Projects.Count == 1
                ? TestCard(group.Projects[0])
                : ObjectiveGroupCard(group));
        }
        Ui.FitCards(_tests);
    }

    Panel ObjectiveGroupCard(ProjectGroup group)
    {
        var box = new Panel
        {
            Width = Math.Max(640, _tests.ClientSize.Width - 24),
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, 12),
            Tag = "card",
            Padding = new Padding(0, 0, 0, 6),
        };
        var body = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.TopDown,
            BackColor = Color.White,
            Padding = new Padding(0, 4, 0, 4),
            Visible = true,
        };
        foreach (var project in group.Projects)
        {
            var card = TestCard(project, nested: true);
            body.Controls.Add(card);
        }

        var head = new Button
        {
            Text = "  " + group.Title,
            Dock = DockStyle.Top,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Ui.Primary,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = Ui.HeadFont,
            Cursor = Cursors.Hand,
            UseMnemonic = false,
        };
        head.FlatAppearance.BorderSize = 0;
        head.FlatAppearance.MouseOverBackColor = Ui.PrimaryDark;
        head.Click += (_, _) =>
        {
            body.Visible = !body.Visible;
            FitGroupCard(box, head, body);
        };
        box.Controls.Add(body);
        box.Controls.Add(head);
        FitGroupCard(box, head, body);
        return box;
    }

    static void FitGroupCard(Panel box, Control head, FlowLayoutPanel body)
    {
        var inner = 0;
        if (body.Visible)
        {
            foreach (Control child in body.Controls)
            {
                inner += child.Height + child.Margin.Vertical;
            }
        }

        box.Height = head.Height + (body.Visible ? inner + body.Padding.Vertical + 10 : 0);
    }

    Panel TestCard(MosProject project, bool nested = false)
    {
        var mins = Math.Max(1, project.TimeLimitSec / 60);
        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Ui.Card,
        };
        var start = Ui.PrimaryBtn("Start", 120);
        start.Click += async (_, _) => await OpenExamEntry(project);
        actions.Controls.Add(start);
        var card = Ui.ListCard(
            project.Title,
            (string.IsNullOrWhiteSpace(project.Skill) ? Ui.AppName(project.Program) : project.Skill) + " · " + mins + " phút",
            actions);
        card.Width = Math.Max(nested ? 520 : 640, _tests.ClientSize.Width - (nested ? 48 : 24));
        card.Margin = nested ? new Padding(0, 0, 0, 4) : new Padding(0, 0, 0, 14);
        if (!nested)
        {
            Ui.FitCards(_tests);
        }

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

    async Task OpenExamEntry(MosProject project)
    {
        _setupProject = project;
        _setupMode.SelectedIndex = 0;
        ShowPage(HubPage.Setup, project.Title);
        await FillSetupPage();
    }

    void BuildSetup()
    {
        _setup.Dock = DockStyle.Fill;
        _setup.Visible = false;
        _setup.BackColor = Ui.PageBg;
        _setupResume.WrapContents = true;
        _setupResume.AutoScroll = true;
        _setupResume.BackColor = Ui.PageBg;
        _setupResume.Dock = DockStyle.Fill;

        _setupMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _setupMode.Width = 220;
        _setupMode.Items.AddRange(["Training", "Testing"]);
        _setupMode.SelectedIndex = 0;
        _setupMode.SelectedIndexChanged += (_, _) => RefreshSetupStats();

        var stats = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 110,
            BackColor = Ui.PageBg,
            WrapContents = true,
        };
        stats.Controls.Add(StatCard(_setupQ, "Questions"));
        stats.Controls.Add(StatCard(_setupMin, "Minutes"));
        stats.Controls.Add(StatCard(_setupCut, "Passing Score"));

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Ui.PageBg,
            Padding = new Padding(0, 8, 0, 0),
        };
        var start = Ui.PrimaryBtn("Start Test", 140);
        start.Click += async (_, _) => await ConfirmStartFromSetup();
        var cancel = Ui.OutlineBtn("Cancel", Ui.Primary, 120);
        cancel.Click += (_, _) => ShowCatalog();
        var fresh = Ui.PrimaryBtn("New Test", 120);
        fresh.Click += (_, _) => { _setupMode.SelectedIndex = 0; RefreshSetupStats(); };
        actions.Controls.Add(start);
        actions.Controls.Add(cancel);
        actions.Controls.Add(fresh);

        var modeRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Ui.PageBg,
            WrapContents = false,
        };
        var modeLbl = new Label
        {
            Text = "Mode",
            AutoSize = true,
            Font = Ui.BtnFont,
            ForeColor = Ui.Text,
            Margin = new Padding(0, 8, 12, 0),
        };
        modeRow.Controls.Add(modeLbl);
        modeRow.Controls.Add(_setupMode);

        var lead = new Label
        {
            Dock = DockStyle.Top,
            Height = 48,
            Text = "New Test tạo phiên mới. Resume Test mở bài đang làm dở. Training hiện Help SOP; Testing khóa gợi ý và đếm giờ.",
            ForeColor = Ui.Muted,
            UseMnemonic = false,
        };
        Ui.BindWrap(lead, 8);

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 40,
            Text = "Tạo mới hoặc Tiếp tục",
            Font = Ui.TitleFont,
            ForeColor = Ui.Text,
            UseMnemonic = false,
        };

        _setup.Controls.Add(_setupResume);
        _setup.Controls.Add(actions);
        _setup.Controls.Add(stats);
        _setup.Controls.Add(modeRow);
        _setup.Controls.Add(lead);
        _setup.Controls.Add(title);
    }

    static Panel StatCard(Label value, string caption)
    {
        value.Text = "—";
        value.Dock = DockStyle.Top;
        value.Height = 40;
        value.TextAlign = ContentAlignment.MiddleCenter;
        value.Font = new Font("Segoe UI", 18f, FontStyle.Bold);
        value.ForeColor = Ui.Text;
        value.UseMnemonic = false;
        var cap = new Label
        {
            Text = caption,
            Dock = DockStyle.Bottom,
            Height = 22,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Ui.Muted,
            UseMnemonic = false,
        };
        var box = new Panel
        {
            Width = 160,
            Height = 88,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 16, 8),
            Padding = new Padding(8),
        };
        box.Controls.Add(value);
        box.Controls.Add(cap);
        return box;
    }

    async Task FillSetupPage()
    {
        RefreshSetupStats();
        _setupResume.Controls.Clear();
        if (_setupProject is not { } project)
        {
            return;
        }

        IReadOnlyList<MosAttempt> rows;
        try
        {
            rows = await ExamHub.ListAttemptsAsync();
        }
        catch
        {
            rows = [];
        }

        var open = rows.Where(a =>
            a.Status == "running"
            && string.Equals(a.ProjectId, project.Id, StringComparison.OrdinalIgnoreCase)).ToList();
        if (open.Count == 0)
        {
            _setupResume.Controls.Add(new Label
            {
                Text = "Chưa có phiên dở. Chọn Mode rồi Start Test.",
                AutoSize = true,
                ForeColor = Ui.Muted,
                Margin = new Padding(4),
            });
            return;
        }

        foreach (var row in open)
        {
            var go = Ui.PrimaryBtn("Resume Test", 140);
            go.BackColor = Ui.Success;
            var attempt = row;
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
            _setupResume.Controls.Add(Ui.ListCard(
                attempt.Title,
                $"Last saved: {attempt.StartedAt} · Mode: {Ui.ModeLabel(attempt.Mode)}",
                go));
        }

        Ui.FitCards(_setupResume);
    }

    void RefreshSetupStats()
    {
        var mins = _setupProject is { } p ? Math.Max(1, p.TimeLimitSec / 60) : 50;
        var testing = _setupMode.SelectedIndex == 1;
        var questions = _setupProject is { Steps.Length: > 0 } q ? q.Steps.Length : (testing ? 35 : 0);
        _setupQ.Text = questions > 0 ? questions.ToString() : "—";
        _setupMin.Text = _setupProject is { TimeLimitSec: <= 0 } && testing ? "50" : mins.ToString();
        _setupCut.Text = "700";
    }

    async Task ConfirmStartFromSetup()
    {
        if (_setupProject is not { } project)
        {
            return;
        }

        var mode = _setupMode.SelectedIndex == 1 ? "testing" : "training";
        await ConfirmStart(project, mode);
    }

    async Task ConfirmStart(MosProject project, string mode)
    {
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
        if (_demoOnStart && ExamSession.Mode != "testing")
        {
            BeginInvoke(async () => await RunActionDemo());
        }
    }

    void ShowExamUi()
    {
        _examTitle.Text = ExamSession.Rubric?.Title ?? ExamSession.ProjectTitle ?? "Bài MOS";
        var caption = ExamSession.Bank.ProjectCaption(ExamSession.ProjectId);
        _examMeta.Text = Ui.AppName(ExamSession.Program) + " · " + Ui.ModeLabel(ExamSession.Mode)
            + (string.IsNullOrWhiteSpace(caption) ? "" : " · " + caption)
            + (ExamSession.HintsAllowed ? " · gợi ý 3 tầng, đồng hồ đếm tiến" : " · 50 phút, khóa gợi ý");
        _dockHint.Visible = ExamSession.HintsAllowed;
        _helpVisible = false;
        ExamSession.HintTier = 0;
        if (ExamSession.Bank.ServerStartedUtc is null)
        {
            ExamSession.OpenedUtc = DateTime.UtcNow;
        }

        _summaryOpen = false;
        ExamSession.LastCheck = [];
        ActionEvidence.Begin(ExamSession.AttemptId);
        WordActionProbe.Reset();
        _taskIndex = 0;
        _objTab = -1;
        _examStatus.Text = ExamSession.HintsAllowed
            ? "Training: Help bung SOP dưới đề. Summary / Save / Grade trên dải xanh."
            : "Testing: Help tắt. Hết giờ máy chủ tự nộp. Finish Test trong Summary.";
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

        FillObjectiveTabs();
        RenderBrief();
        RenderHelp();

        if (_tasks.Columns.Count >= 1)
        {
            _tasks.Columns[0].Width = Math.Max(180, _exam.ClientSize.Width - 140);
        }

        ShowPage(HubPage.Exam, "Bài thi");
        _clockTicks = 0;
        _examClock.Start();
        TickExamClock();
    }

    async Task RunActionDemo()
    {
        if (_demoRunning)
        {
            return;
        }

        _demoRunning = true;
        try
        {
            _state = string.IsNullOrWhiteSpace(_state) || _state == "minimized" ? "bottom" : _state;
            _compact = true;
            _summaryOpen = false;
            _helpVisible = true;
            if (!_docking)
            {
                ShowExamUi();
                EnterDock(compact: true);
            }
            else
            {
                ApplyDock(waitForWord: true);
            }

            _examStatus.Text = "Đang demo tất cả bài Word…";
            Cursor = Cursors.WaitCursor;
            string report;
            try
            {
                if (!string.IsNullOrWhiteSpace(ExamSession.AttemptId))
                {
                    try
                    {
                        WordActionDemo.Drive(SelectTaskIndex);
                    }
                    catch
                    {
                        // bulk demo still runs from keyed files
                    }
                }

                report = await ExamHub.DemoAllAsync(msg =>
                {
                    _examStatus.Text = msg;
                    Application.DoEvents();
                });
            }
            catch (Exception ex)
            {
                report = "Demo gặp lỗi: " + ex.Message;
            }

            Cursor = Cursors.Default;
            _examStatus.Text = report.Split('\n')[0];
            ShowExamUi();
            if (!_docking)
            {
                EnterDock(compact: true);
            }
            else
            {
                ApplyDock(waitForWord: true);
            }

            if (!string.IsNullOrWhiteSpace(ExamSession.AttemptId) && !string.IsNullOrWhiteSpace(ExamSession.LocalPath))
            {
                await CheckTasks();
            }

            ShowSummary(true);
        }
        finally
        {
            _demoRunning = false;
        }
    }

    void SelectTaskIndex(int index)
    {
        if (InvokeRequired)
        {
            Invoke(() => SelectTaskIndex(index));
            return;
        }

        if (_tasks.Items.Count == 0)
        {
            _taskIndex = index;
            RenderHelp();
            return;
        }

        var i = Math.Clamp(index, 0, _tasks.Items.Count - 1);
        _tasks.SelectedIndices.Clear();
        _tasks.Items[i].Selected = true;
        _tasks.EnsureVisible(i);
        _taskIndex = i;
        _objTab = i;
        HighlightObjectiveTabs();
        RenderBrief();
        RenderHelp();
        ApplyDock(waitForWord: false);
        Application.DoEvents();
    }

    async Task CheckTasks()
    {
        if (ExamSession.Mode == "testing")
        {
            _examStatus.Text = "Chế độ thi ẩn kết quả. Nộp bài khi xong.";
            ShowSummary(true);
            return;
        }

        _examStatus.Text = "Đang lưu đúng tài liệu bài thi và chấm…";
        Cursor = Cursors.WaitCursor;
        var (ok, summary, criteria) = await ExamHub.CheckTasksAsync(_app);
        Cursor = Cursors.Default;
        ExamSession.LastCheck = criteria;
        _examStatus.Text = summary;
        foreach (ListViewItem item in _tasks.Items)
        {
            var id = item.Tag as string;
            var hit = SkillReview.Find(criteria, id, _tasks.Items.IndexOf(item));
            if (string.IsNullOrEmpty(hit.Id) && string.IsNullOrEmpty(hit.Status))
            {
                continue;
            }

            item.SubItems[1].Text = SkillReview.Label(hit.Status);
            item.ForeColor = SkillReview.ColorOf(hit.Status);
        }

        if (!ok)
        {
            MessageBox.Show(summary, "MOS-KulKul", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        var failed = criteria.Any(c => c.Status is "fail" or "error");
        var allPass = criteria.Count > 0 && criteria.All(c => c.Status == "pass");
        var partial = criteria.Any(c => c.Status == "pass") && failed;
        if (ExamSession.HintsAllowed)
        {
            if (allPass)
            {
                System.Media.SystemSounds.Asterisk.Play();
                WordWindow.FlashFeedback(Ui.Success);
            }
            else if (failed || !string.IsNullOrWhiteSpace(ExamSession.HardStopReason))
            {
                System.Media.SystemSounds.Hand.Play();
                WordWindow.FlashFeedback(partial ? Ui.Warning : Ui.Danger);
                ExamSession.HintTier = 1;
                _helpVisible = true;
                _ = ExamHub.TrackAsync("hint", new { tier = 1, source = "auto_wrong_check", task_id = CurrentTaskId() });
                RelayoutExam();
                RenderHelp();
            }
        }

        ShowSummary(true);
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
        _examClock.Stop();
        ExamHub.SaveInPlace(_app);
        ShowHome();
    }

    void ToggleMarkForReview()
    {
        var i = Math.Max(0, _taskIndex);
        var flagged = ExamSession.ToggleMark(ExamSession.ProjectId, i);
        FillSummary();
        HighlightObjectiveTabs();
        HighlightDockIcons();
        _examStatus.Text = flagged
            ? "Mark for review — Task " + (i + 1)
            : "Bỏ Mark for review — Task " + (i + 1) + ".";
    }

    void ToggleMarkCompleted()
    {
        var i = Math.Max(0, _taskIndex);
        ExamSession.MarkViewed(ExamSession.ProjectId, i);
        var done = ExamSession.ToggleComplete(ExamSession.ProjectId, i);
        FillSummary();
        HighlightDockIcons();
        _examStatus.Text = done
            ? "Mark Completed — Task " + (i + 1)
            : "Bỏ Mark Completed — Task " + (i + 1) + ".";
    }

    void TickExamClock()
    {
        if (_view != HubPage.Exam)
        {
            return;
        }

        var elapsed = DateTime.UtcNow - ExamSession.OpenedUtc;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        var caption = ExamSession.Bank.ProjectCaption(ExamSession.ProjectId);
        _projectLabel.Text = string.IsNullOrWhiteSpace(caption) ? "Project 1 / 1" : caption;
        var prefix = Ui.AppName(ExamSession.Program) + " · " + Ui.ModeLabel(ExamSession.Mode)
            + (string.IsNullOrWhiteSpace(caption) ? "" : " · " + caption);
        if (ExamSession.Bank.ElapsedOnly || ExamSession.HintsAllowed)
        {
            _examMeta.Text = prefix + " · đã làm " + FormatClock(elapsed);
            _clockLabel.Text = FormatClock(elapsed);
        }
        else
        {
            var limit = ExamSession.Bank.TimeLimitSec
                ?? (ExamSession.Bank.DurationMinutes ?? 50) * 60;
            var remain = ExamSession.Bank.RemainingSec
                ?? Math.Max(0, limit - (int)elapsed.TotalSeconds);
            remain = Math.Max(0, remain - (_clockTicks > 0 ? 0 : 0));
            var shown = TimeSpan.FromSeconds(Math.Max(0, limit - (int)elapsed.TotalSeconds));
            if (ExamSession.Bank.RemainingSec is int serverRemain)
            {
                shown = TimeSpan.FromSeconds(Math.Max(0, serverRemain - _clockTicks));
            }

            _examMeta.Text = prefix + " · còn " + FormatClock(shown);
            _clockLabel.Text = FormatClock(shown);
            if (shown <= TimeSpan.Zero && ExamSession.Bank.ForceSubmit)
            {
                _ = ForceSubmitExam();
            }
        }

        _clockTicks++;
        if (_clockTicks % 15 == 0)
        {
            _ = SyncExamClockAsync();
        }
    }

    static string FormatClock(TimeSpan span)
    {
        var total = Math.Max(0, (int)span.TotalSeconds);
        return $"{total / 3600:00}:{total / 60 % 60:00}:{total % 60:00}";
    }

    async Task SyncExamClockAsync()
    {
        if (string.IsNullOrWhiteSpace(ExamSession.AttemptId))
        {
            return;
        }

        try
        {
            using var doc = await Portal.GetJsonAsync("/api/v1/attempts/" + ExamSession.AttemptId + "/clock");
            var root = doc.RootElement;
            if (root.TryGetProperty("remaining_sec", out var rem) && rem.TryGetInt32(out var left))
            {
                ExamSession.Bank.RemainingSec = left;
                _clockTicks = 0;
            }

            if (root.TryGetProperty("force_submit", out var fs) && fs.ValueKind == JsonValueKind.True)
            {
                await ForceSubmitExam();
            }
        }
        catch
        {
            // offline: keep local elapsed
        }
    }

    async Task ForceSubmitExam()
    {
        if (_forceSubmitting || _view != HubPage.Exam)
        {
            return;
        }

        _forceSubmitting = true;
        _examClock.Stop();
        try
        {
            var (ok, msg) = await ExamHub.SubmitAsync(_app);
            MessageBox.Show(
                ok ? "Hết giờ — hệ thống đã thu bài.\n\n" + msg : "Hết giờ. " + msg,
                "MOS-KulKul",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            ExamSession.ClearExam();
            await ShowCompleted();
        }
        finally
        {
            _forceSubmitting = false;
        }
    }

    void OnExamDeactivate(object? sender, EventArgs e)
    {
        if (_view != HubPage.Exam || !ExamSession.Bank.FocusLock || _forceSubmitting || _focusPrompt)
        {
            return;
        }

        BeginInvoke(() =>
        {
            if (_view != HubPage.Exam || !ExamSession.Bank.FocusLock || _focusPrompt)
            {
                return;
            }

            var fg = WordWindow.ForegroundWindow();
            if (fg == Handle || WordWindow.IsOfficeOrDock(fg))
            {
                return;
            }

            ExamSession.FocusStrikes++;
            _ = ExamHub.TrackAsync("focus_loss", new { n = ExamSession.FocusStrikes });
            _focusPrompt = true;
            try
            {
                if (ExamSession.FocusStrikes >= 3)
                {
                    MessageBox.Show(
                        "Bạn đang vi phạm quy chế thi. Lần vi phạm thứ 3 — bài thi tự động hủy.",
                        "MOS-KulKul",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    _ = ForceSubmitExam();
                    return;
                }

                MessageBox.Show(
                    "Bạn đang vi phạm quy chế thi. Lần vi phạm thứ " + ExamSession.FocusStrikes + "/3.",
                    "MOS-KulKul",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            finally
            {
                _focusPrompt = false;
            }
        });
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
        _examClock.Stop();
        WordWindow.CancelPlace();
        _header.Visible = true;
        _body.Visible = true;
        _exam.Visible = false;
        TopMost = false;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = true;
        MaximizeBox = true;
        ControlBox = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "MOS-KulKul";
        RestoreHubWindow();
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
            MaximumSize = Size.Empty;
            MinimumSize = new Size(LayoutMath.OverlayMinW, LayoutMath.OverlayMinH);
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
        AutoScaleMode = AutoScaleMode.None;
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
            ApplyDock(waitForWord: true);
        }
    }

    void RestoreHubWindow()
    {
        MaximumSize = Size.Empty;
        MinimumSize = new Size(LayoutMath.HubMinW, LayoutMath.HubMinH);
        if (_savedWorkspace is { } saved && saved.Width > 200 && saved.Height > 200)
        {
            Bounds = saved;
            return;
        }

        var wa = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, LayoutMath.RefWorkW, LayoutMath.RefWorkH);
        var w = Math.Min(980, wa.Width - 40);
        var h = Math.Min(640, wa.Height - 40);
        Bounds = new Rectangle(wa.X + (wa.Width - w) / 2, wa.Y + (wa.Height - h) / 2, w, h);
    }

    void FitOverlay(int x, int y, int w, int h)
    {
        w = Math.Max(LayoutMath.OverlayMinW, w);
        h = Math.Max(LayoutMath.OverlayMinH, h);
        AutoScaleMode = AutoScaleMode.None;
        AutoSize = false;
        SuspendLayout();
        MaximumSize = Size.Empty;
        MinimumSize = Size.Empty;
        Bounds = new Rectangle(x, y, w, h);
        if (IsHandleCreated)
        {
            SetWindowPos(Handle, IntPtr.Zero, x, y, w, h, SwpNozorder | SwpNoactivate | SwpFramechanged);
        }

        MinimumSize = new Size(LayoutMath.OverlayMinW, LayoutMath.OverlayMinH);
        MaximumSize = Size.Empty;
        ResumeLayout(true);
    }

    static string NavSizePath()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MOS-KulKul");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "nav-thickness.txt");
    }

    void LoadNavThickness()
    {
        try
        {
            var raw = File.ReadAllText(NavSizePath()).Trim();
            if (int.TryParse(raw, out var t) && t >= LayoutMath.OverlayMinH)
            {
                _navThickness = t;
            }
        }
        catch
        {
            // first run
        }
    }

    void SaveNavThickness()
    {
        try
        {
            var path = NavSizePath();
            if (_navThickness is int t)
            {
                File.WriteAllText(path, t.ToString());
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignore
        }
    }

    void NudgeNavThickness(int delta)
    {
        if (!_docking || !_compact)
        {
            return;
        }

        var work = CurrentWork();
        var current = _navThickness ?? LayoutMath.ThicknessOf(DockAndWord(work).Dock, _state);
        _navThickness = LayoutMath.ClampThickness(work, _state, current + delta);
        SaveNavThickness();
        ApplyDock(waitForWord: true);
    }

    void OnNavGripDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !_docking || !_compact)
        {
            return;
        }

        _navResizing = true;
        _navGrip.Capture = true;
        var work = CurrentWork();
        _navResizeOrigin = _navThickness ?? LayoutMath.ThicknessOf(DockAndWord(work).Dock, _state);
        _navResizeStart = LayoutMath.Horizontal(_state) ? Cursor.Position.Y : Cursor.Position.X;
    }

    void OnNavGripMove(object? sender, MouseEventArgs e)
    {
        if (!_navResizing)
        {
            return;
        }

        var work = CurrentWork();
        var pos = LayoutMath.Horizontal(_state) ? Cursor.Position.Y : Cursor.Position.X;
        var delta = pos - _navResizeStart;
        var next = _state switch
        {
            "bottom" => _navResizeOrigin - delta,
            "top" => _navResizeOrigin + delta,
            "left" => _navResizeOrigin + delta,
            "right" => _navResizeOrigin - delta,
            _ => _navResizeOrigin - delta,
        };
        _navThickness = LayoutMath.ClampThickness(work, _state, next);
        ApplyDock(waitForWord: false);
    }

    void OnNavGripUp(object? sender, MouseEventArgs e)
    {
        if (!_navResizing)
        {
            return;
        }

        _navResizing = false;
        _navGrip.Capture = false;
        SaveNavThickness();
        ApplyDock(waitForWord: true);
    }

    void ApplyDock(bool waitForWord)
    {
        if (!_docking)
        {
            return;
        }

        if (_summaryOpen || _tipsOpen)
        {
            ApplyExamChrome();
            return;
        }

        var work = CurrentWork();
        var nav = LayoutMath.Measure(work);
        ApplyNavChrome(nav);
        var (dock, word) = DockAndWord(work);
        dock = LayoutMath.PinToWork(dock, work, _state);
        word = LayoutMath.WordBeside(work, dock, _state);
        FitOverlay(dock.X, dock.Y, dock.W, dock.H);
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

        var work = CurrentWork();
        var (_, word) = DockAndWord(work);
        WordWindow.Apply(word, _app);
        if (_pinned && !TopMost)
        {
            TopMost = true;
        }
    }

    (Rect Dock, Rect Word) DockAndWord(Rect work)
    {
        if (ExamSession.Bank.CertiportSplit)
        {
            return LayoutMath.CertiportSplit(work);
        }

        var (dock, _) = LayoutMath.Compute(work, _state, _compact, thickness: _navThickness);
        if (_compact && HelpOpen)
        {
            dock = LayoutMath.GrowForHelp(dock, work, _state);
        }
        else if (_compact)
        {
            dock = LayoutMath.GrowForPrompt(dock, work, _state);
        }

        dock = LayoutMath.PinToWork(dock, work, _state);
        return (dock, LayoutMath.WordBeside(work, dock, _state));
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_view != HubPage.Exam)
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        if (keyData == (Keys.Alt | Keys.F))
        {
            CycleTypeSize();
            return true;
        }

        if (keyData == (Keys.Alt | Keys.T))
        {
            CycleTypeSize();
            return true;
        }

        if (keyData == (Keys.Alt | Keys.D))
        {
            _dockMenuStrip.Show(_dockPos, new Point(0, 0), ToolStripDropDownDirection.AboveRight);
            return true;
        }

        if (keyData == (Keys.Alt | Keys.L))
        {
            ShowSummary(!_summaryOpen);
            return true;
        }

        if (keyData == (Keys.Alt | Keys.S))
        {
            SaveAndHome();
            return true;
        }

        if (keyData == (Keys.Alt | Keys.G))
        {
            _ = CheckTasks();
            return true;
        }

        if (keyData == (Keys.Alt | Keys.B))
        {
            StepTask(-1);
            return true;
        }

        if (keyData == (Keys.Alt | Keys.N))
        {
            StepTask(1);
            return true;
        }

        if (keyData == (Keys.Alt | Keys.H))
        {
            ToggleHelp();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    const uint SwpNoactivate = 0x0010;
    const uint SwpNozorder = 0x0004;
    const uint SwpFramechanged = 0x0020;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
}
