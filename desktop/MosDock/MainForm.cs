namespace MosDock;

/// <summary>
/// MOS-KulKul: trang chủ dashboard — tiến độ, Word/Excel/PowerPoint, bài dở và bài đã nộp.
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
    readonly HomeDash _dash = new();
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
    readonly Panel _navGrip = new();
    readonly FlowLayoutPanel _dockFlow = new();
    readonly Button _dockPos = Ui.DockSquare(NavIcon.Dock, "Gắn thanh bài thi sang vị trí khác", Ui.DockBlue);
    readonly Button _dockSave = Ui.DockSquare(NavIcon.Save, "Lưu và thoát bài", Ui.DockBlue);
    readonly Button _dockTasks = Ui.DockSquare(NavIcon.Tasks, "Hiện danh sách nhiệm vụ", Ui.DockBlue);
    readonly Button _dockCheck = Ui.DockSquare(NavIcon.Refresh, "Kiểm tra nhiệm vụ", Ui.DockBlue);
    readonly Button _dockPin = Ui.DockSquare(NavIcon.Pin, "Ghim luôn trên cùng", Ui.DockBlue);
    readonly Button _dockMenu = Ui.DockSquare(NavIcon.Menu, "Menu tùy chọn thêm", Ui.DockTeal);
    readonly Button _dockHint = Ui.DockSquare(NavIcon.Hint, "Hiện hướng dẫn", Ui.DockTeal);
    readonly Button _dockShare = Ui.DockSquare(NavIcon.Share, "Bỏ qua chấm, sang nhiệm vụ sau", Ui.DockBlue);
    readonly Button _dockBack = Ui.DockSquare(NavIcon.Back, "Nhiệm vụ trước, không chấm", Ui.DockBlue);
    readonly Button _dockNext = Ui.DockSquare(NavIcon.Next, "Sang nhiệm vụ sau", Ui.DockGreen);
    readonly ContextMenuStrip _dockMenuStrip = new();
    readonly ContextMenuStrip _extraMenu = new();
    readonly Panel _helpPane = new();
    readonly Panel _promptCard = new();
    readonly Label _promptTitle = new();
    readonly TextBox _promptBody = new();
    readonly Label _helpTitle = new();
    readonly RichTextBox _helpBody = new();
    readonly Button _aaSmaller = Ui.AaSizeButton("A−", "Thu nhỏ nội dung hướng dẫn");
    readonly Button _aaBigger = Ui.AaSizeButton("A+", "Phóng to nội dung hướng dẫn");
    readonly Panel _summary = new();
    readonly Label _summaryTitle = new();
    readonly TextBox _summarySearch = new();
    readonly ListView _summaryList = new();
    readonly Button _summaryCancel = Ui.PrimaryBtn("Hủy", 72);
    readonly Button _summaryGo = Ui.PrimaryBtn("Chuyển tới vị trí câu hỏi", 220);
    readonly Button _summarySave = Ui.PrimaryBtn("Lưu bài", 88);
    readonly Button _summaryFinish = Ui.PrimaryBtn("Nộp bài", 88);
    readonly Button _summaryCheck = Ui.PrimaryBtn("Chấm lại", 88);
    readonly Label _summaryStats = new();
    readonly ProgressBar _summaryBar = new();
    readonly ComboBox _summaryFilter = new();
    readonly Panel _summarySplit = new();
    readonly Panel _summaryListPane = new();
    readonly Panel _summaryDetail = new();
    readonly Panel _summaryFooter = new();
    readonly Panel _summarySearchRow = new();
    readonly Panel _summaryFilterRow = new();
    readonly TableLayoutPanel _summaryActions = new();
    readonly FlowLayoutPanel _detailBlocks = new();
    readonly Label _detailHead = new();
    readonly Label _detailStatus = new();
    readonly Label _detailScore = new();
    readonly Button _detailHintBtn = Ui.PrimaryBtn("Xem gợi ý", 148);
    readonly RichTextBox _detailHintBox = new();
    bool _hintExpanded;
    readonly System.Windows.Forms.Timer _keepWord = new();
    LocalAgent? _agent;
    string _state = "bottom";
    string _app = "word";
    bool _compact;
    bool _docking;
    bool _pinned = true;
    bool _helpVisible = true;
    bool _summaryOpen;
    bool _navResizing;
    int? _navThickness;
    int _navResizeOrigin;
    int _navResizeStart;
    int _helpScale = 1;
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
    enum HubPage { Home, Catalog, Resume, Done, Exam }
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
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        ClientSize = new Size(1080, 700);
        MinimumSize = new Size(LayoutMath.HubMinW, LayoutMath.HubMinH);
        BackColor = Ui.PageBg;
        Font = Ui.BodyFont;
        Ui.ApplyWindowIcon(this);

        BuildHeader();
        BuildHome();
        BuildCatalog();
        BuildListPage(_resume, _resumeList, "Tiếp tục bài", "Chọn bài đang làm dở để mở lại trên Office máy.");
        BuildListPage(_done, _doneList, "Bài đã nộp", "Điểm hiển thị phần đã xác minh. Find/Go To có thể còn chưa xác minh.");
        BuildExam();
        LoadNavThickness();

        _body.Dock = DockStyle.Fill;
        _body.BackColor = Ui.PageBg;
        _body.Padding = new Padding(20, 14, 20, 16);
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
        _dockPos, _dockSave, _dockTasks, _dockCheck, _dockPin,
        _dockMenu, _dockHint, _dockShare, _dockBack, _dockNext,
    ];

    void ApplyNavChrome(NavMetrics nav)
    {
        _nav = nav;
        _dockChrome.Padding = new Padding(nav.ChromePad);
        foreach (var btn in DockButtons())
        {
            btn.Size = new Size(nav.Icon, nav.Icon);
            btn.Margin = new Padding(nav.IconGap);
        }

        OrientNav();
    }

    void OrientNav()
    {
        var grip = _docking && _compact ? 6 : 0;
        var keepHelp = _docking && _compact && HelpOpen && !_summaryOpen;
        if (_docking && _compact && _state is "left" or "right")
        {
            _dockChrome.Dock = _state == "left" ? DockStyle.Left : DockStyle.Right;
            _dockChrome.Width = keepHelp
                ? _nav.ClusterW
                : Math.Max(_nav.ClusterW, Math.Max(8, ClientSize.Width - grip));
            _dockFlow.FlowDirection = FlowDirection.TopDown;
            _dockFlow.WrapContents = false;
            _navGrip.Visible = true;
            _navGrip.Dock = _state == "left" ? DockStyle.Right : DockStyle.Left;
            _navGrip.Width = 6;
            _navGrip.Cursor = Cursors.SizeWE;
            _navGrip.BringToFront();
            return;
        }

        _dockChrome.Dock = _state == "top" && _docking && _compact ? DockStyle.Top : DockStyle.Bottom;
        _dockChrome.Height = keepHelp
            ? _nav.ClusterH
            : Math.Max(_nav.ClusterH, Math.Max(8, ClientSize.Height - grip));
        _dockFlow.FlowDirection = FlowDirection.LeftToRight;
        _dockFlow.WrapContents = false;
        _navGrip.Visible = _docking && _compact;
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
        _dash.Dock = DockStyle.Fill;
        _dash.OpenProgram = program =>
        {
            ShowCatalog();
            _ = LoadCatalog(program);
        };
        _dash.OpenResumeList = () => _ = ShowResume();
        _dash.OpenDoneList = () => _ = ShowCompleted();
        _dash.ResumeAttempt = ResumeOpenAttempt;
        _home.Controls.Add(_dash);
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
        _dockChrome.Padding = new Padding(6, 4, 6, 4);

        _dockFlow.Dock = DockStyle.Fill;
        _dockFlow.FlowDirection = FlowDirection.LeftToRight;
        _dockFlow.WrapContents = false;
        _dockFlow.BackColor = Color.Transparent;
        _dockFlow.Padding = Padding.Empty;
        _dockFlow.Margin = Padding.Empty;

        _dockPos.Click += (_, _) =>
        {
            _dockMenuStrip.Show(_dockPos, new Point(0, 0), ToolStripDropDownDirection.AboveRight);
        };
        _dockSave.Click += (_, _) => SaveAndHome();
        _dockTasks.Click += async (_, _) => await CheckTasks();
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
        _dockShare.Click += (_, _) => StepTask(1);
        _dockBack.Click += (_, _) => StepTask(-1);
        _dockNext.Click += (_, _) => StepTask(1);

        foreach (var btn in DockButtons())
        {
            _dockFlow.Controls.Add(btn);
        }

        _dockChrome.Controls.Add(_dockFlow);

        _navGrip.BackColor = Color.FromArgb(176, 190, 197);
        _navGrip.Height = 6;
        _navGrip.Cursor = Cursors.SizeNS;
        Ui.DockTips.SetToolTip(_navGrip, "Kéo để đổi kích thước thanh Navigation");
        _navGrip.MouseDown += OnNavGripDown;
        _navGrip.MouseMove += OnNavGripMove;
        _navGrip.MouseUp += OnNavGripUp;

        _dockMenuStrip.Font = new Font("Segoe UI", 10f);
        _dockMenuStrip.Items.Add(DockMenuItem("←  left", "left"));
        _dockMenuStrip.Items.Add(DockMenuItem("→  right", "right"));
        _dockMenuStrip.Items.Add(DockMenuItem("↑  Top", "top"));
        _dockMenuStrip.Items.Add(DockMenuItem("↓  Bottom", "bottom"));

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
        BuildSummaryPane();

        _exam.Controls.Add(_summary);
        _exam.Controls.Add(_tasks);
        _exam.Controls.Add(_helpPane);
        _exam.Controls.Add(_navGrip);
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
        _helpPane.Padding = new Padding(8, 6, 8, 6);
        _helpPane.Visible = false;

        _promptCard.Dock = DockStyle.Top;
        _promptCard.Height = 112;
        _promptCard.BackColor = Color.White;
        _promptCard.Padding = new Padding(1);
        _promptCard.Paint += PaintCardBorder;

        var promptHead = new Panel
        {
            Dock = DockStyle.Top,
            Height = 26,
            BackColor = Color.FromArgb(232, 244, 252),
            Padding = new Padding(8, 0, 8, 0),
        };
        _promptTitle.Text = "Đề bài";
        _promptTitle.Dock = DockStyle.Fill;
        _promptTitle.TextAlign = ContentAlignment.MiddleLeft;
        _promptTitle.ForeColor = Ui.Text;
        _promptTitle.UseMnemonic = false;
        promptHead.Controls.Add(_promptTitle);

        _promptBody.Dock = DockStyle.Fill;
        _promptBody.Multiline = true;
        _promptBody.ReadOnly = true;
        _promptBody.WordWrap = true;
        _promptBody.ScrollBars = ScrollBars.Vertical;
        _promptBody.BorderStyle = BorderStyle.None;
        _promptBody.TabStop = false;
        _promptBody.BackColor = Color.White;
        _promptBody.ForeColor = Ui.Text;
        _promptBody.Margin = new Padding(0);
        var promptWrap = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(8, 4, 8, 6),
        };
        promptWrap.Controls.Add(_promptBody);
        _promptCard.Controls.Add(promptWrap);
        _promptCard.Controls.Add(promptHead);

        var helpCard = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(1),
        };
        helpCard.Paint += PaintCardBorder;

        _helpHeader.Dock = DockStyle.Top;
        _helpHeader.Height = 32;
        _helpHeader.BackColor = Color.White;
        _helpHeader.Padding = new Padding(8, 2, 6, 2);
        _helpTitle.Text = "Hướng dẫn";
        _helpTitle.Dock = DockStyle.Fill;
        _helpTitle.TextAlign = ContentAlignment.MiddleLeft;
        _helpTitle.ForeColor = Ui.Text;
        _helpTitle.UseMnemonic = false;
        _aaSmaller.Dock = DockStyle.Right;
        _aaBigger.Dock = DockStyle.Right;
        _aaSmaller.Margin = new Padding(0, 0, 4, 0);
        _aaBigger.Click += (_, _) => NudgeHelpScale(1);
        _aaSmaller.Click += (_, _) => NudgeHelpScale(-1);
        _helpHeader.Controls.Add(_helpTitle);
        _helpHeader.Controls.Add(_aaSmaller);
        _helpHeader.Controls.Add(_aaBigger);

        _helpFooter.Dock = DockStyle.Bottom;
        _helpFooter.Height = 0;
        _helpFooter.Visible = false;

        _helpBody.Dock = DockStyle.Fill;
        _helpBody.BorderStyle = BorderStyle.None;
        _helpBody.ReadOnly = true;
        _helpBody.DetectUrls = false;
        _helpBody.TabStop = false;
        _helpBody.BackColor = Color.White;
        _helpBody.ForeColor = Ui.Text;
        _helpBody.ScrollBars = RichTextBoxScrollBars.Vertical;
        _helpBody.WordWrap = true;
        _helpBody.HideSelection = true;
        _helpBody.ShortcutsEnabled = false;
        _helpBody.Cursor = Cursors.Default;
        _helpBody.Margin = new Padding(0);
        _helpBody.Padding = new Padding(0);
        var bodyWrap = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(8, 4, 8, 8),
        };
        bodyWrap.Controls.Add(_helpBody);
        helpCard.Controls.Add(bodyWrap);
        helpCard.Controls.Add(_helpHeader);

        var gap = new Panel
        {
            Dock = DockStyle.Top,
            Height = 6,
            BackColor = Color.FromArgb(245, 247, 249),
        };
        _helpPane.Controls.Add(helpCard);
        _helpPane.Controls.Add(gap);
        _helpPane.Controls.Add(_promptCard);
        ApplyHelpFonts();
    }

    static void PaintCardBorder(object? sender, PaintEventArgs e)
    {
        if (sender is not Control box)
        {
            return;
        }

        using var pen = new Pen(Color.FromArgb(196, 205, 213));
        e.Graphics.DrawRectangle(pen, 0, 0, box.Width - 1, box.Height - 1);
    }

    void BuildSummaryPane()
    {
        _summary.Dock = DockStyle.Fill;
        _summary.BackColor = Color.White;
        _summary.Padding = new Padding(12, 10, 12, 10);
        _summary.Visible = false;

        _summaryGo.Text = "Chuyển tới vị trí câu hỏi";
        _summaryGo.Dock = DockStyle.Top;
        _summaryGo.Height = 40;
        _summaryGo.AutoSize = false;
        _summaryGo.BackColor = Color.FromArgb(0, 99, 177);
        _summaryGo.ForeColor = Color.White;
        _summaryGo.Click += (_, _) => JumpSelectedSummary();
        Ui.DockTips.SetToolTip(_summaryGo, "Đóng danh sách và mở câu hỏi đang chọn trên thanh bài thi");

        _summaryCancel.BackColor = Ui.DockBlue;
        _summarySave.BackColor = Ui.DockBlue;
        _summaryFinish.BackColor = Ui.DockBlue;
        _summaryCheck.BackColor = Ui.Success;
        _summaryCancel.Click += (_, _) => ShowSummary(false);
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
        Ui.DockTips.SetToolTip(_summarySave, "Lưu bài, chưa nộp");
        Ui.DockTips.SetToolTip(_summaryFinish, "Nộp bài và kết thúc");
        Ui.DockTips.SetToolTip(_summaryCheck, "Chấm lại tệp Word đang mở");
        Ui.DockTips.SetToolTip(_summaryCancel, "Đóng danh sách, giữ nguyên câu hiện tại");

        _summaryActions.Dock = DockStyle.Bottom;
        _summaryActions.Height = 40;
        _summaryActions.ColumnCount = 4;
        _summaryActions.RowCount = 1;
        _summaryActions.BackColor = Color.White;
        foreach (var btn in new[] { _summaryCancel, _summaryCheck, _summarySave, _summaryFinish })
        {
            btn.Dock = DockStyle.Fill;
            btn.AutoSize = false;
            btn.Height = 36;
            btn.Margin = new Padding(0, 0, 6, 0);
        }

        _summaryFinish.Margin = new Padding(0);
        _summaryActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        _summaryActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        _summaryActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        _summaryActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        _summaryActions.Controls.Add(_summaryCancel, 0, 0);
        _summaryActions.Controls.Add(_summaryCheck, 1, 0);
        _summaryActions.Controls.Add(_summarySave, 2, 0);
        _summaryActions.Controls.Add(_summaryFinish, 3, 0);

        _summaryFooter.Dock = DockStyle.Bottom;
        _summaryFooter.Height = 96;
        _summaryFooter.BackColor = Color.White;
        _summaryFooter.Padding = new Padding(0, 8, 0, 0);
        _summaryFooter.Controls.Add(_summaryGo);
        _summaryFooter.Controls.Add(_summaryActions);

        _summaryList.Dock = DockStyle.Fill;
        _summaryList.View = System.Windows.Forms.View.Details;
        _summaryList.FullRowSelect = true;
        _summaryList.HideSelection = false;
        _summaryList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _summaryList.BorderStyle = BorderStyle.None;
        _summaryList.Columns.Add("#", 32);
        _summaryList.Columns.Add("Kỹ năng", 220);
        _summaryList.Columns.Add("Kết quả", 108);
        _summaryList.DoubleClick += (_, _) => JumpSelectedSummary();
        _summaryList.SelectedIndexChanged += (_, _) => RenderSummaryDetail();
        _summaryListPane.Dock = DockStyle.Top;
        _summaryListPane.Height = 220;
        _summaryListPane.Padding = new Padding(0, 0, 0, 8);
        _summaryListPane.Controls.Add(_summaryList);

        _summaryDetail.Dock = DockStyle.Fill;
        _summaryDetail.BackColor = Ui.PageBg;
        _summaryDetail.Padding = new Padding(10, 8, 10, 8);
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
        _detailHintBtn.Dock = DockStyle.Top;
        _detailHintBtn.Height = 36;
        _detailHintBtn.BackColor = Ui.DockTeal;
        _detailHintBtn.Click += (_, _) => ToggleDetailHint();
        Ui.DockTips.SetToolTip(_detailHintBtn, "Hiện các bước Ribbon > Tab > Command");
        _detailHintBox.Dock = DockStyle.Top;
        _detailHintBox.Height = 0;
        _detailHintBox.ReadOnly = true;
        _detailHintBox.BorderStyle = BorderStyle.None;
        _detailHintBox.BackColor = Color.White;
        _detailHintBox.ForeColor = Ui.Text;
        _detailHintBox.ScrollBars = RichTextBoxScrollBars.Vertical;
        _detailHintBox.WordWrap = true;
        _detailHintBox.DetectUrls = false;
        _detailHintBox.TabStop = false;
        _detailHintBox.Visible = false;
        _detailBlocks.Dock = DockStyle.Fill;
        _detailBlocks.FlowDirection = FlowDirection.TopDown;
        _detailBlocks.WrapContents = false;
        _detailBlocks.AutoScroll = true;
        _detailBlocks.BackColor = Ui.PageBg;
        _detailBlocks.Padding = new Padding(0, 8, 0, 0);
        _detailBlocks.Resize += (_, _) => FitReviewCards();
        _summaryDetail.Controls.Add(_detailBlocks);
        _summaryDetail.Controls.Add(_detailHintBox);
        _summaryDetail.Controls.Add(_detailHintBtn);
        _summaryDetail.Controls.Add(_detailScore);
        _summaryDetail.Controls.Add(_detailStatus);
        _summaryDetail.Controls.Add(_detailHead);

        _summarySplit.Dock = DockStyle.Fill;
        _summarySplit.Controls.Add(_summaryDetail);
        _summarySplit.Controls.Add(_summaryListPane);
        _summary.Resize += (_, _) => LayoutSummaryChrome();

        _summarySearch.PlaceholderText = "Tìm tên hoặc mã kỹ năng";
        _summarySearch.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                FillSummary();
                e.Handled = true;
            }
        };
        var searchHost = Ui.SearchField(_summarySearch, FillSummary);
        searchHost.Dock = DockStyle.Fill;
        _summarySearchRow.Dock = DockStyle.Top;
        _summarySearchRow.Height = 40;
        _summarySearchRow.BackColor = Color.White;
        _summarySearchRow.Padding = new Padding(0, 0, 0, 6);
        _summarySearchRow.Controls.Add(searchHost);

        _summaryFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        _summaryFilter.Dock = DockStyle.Fill;
        _summaryFilter.Font = Ui.BodyFont;
        _summaryFilter.Items.AddRange(["Tất cả", "Đã đạt", "Chưa đạt", "Chưa xác minh", "Chưa chấm"]);
        _summaryFilter.SelectedIndex = 0;
        _summaryFilter.SelectedIndexChanged += (_, _) => FillSummary();
        _summaryFilterRow.Dock = DockStyle.Top;
        _summaryFilterRow.Height = 34;
        _summaryFilterRow.BackColor = Color.White;
        _summaryFilterRow.Padding = new Padding(0, 0, 0, 6);
        _summaryFilterRow.Controls.Add(_summaryFilter);

        var meta = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Color.White };
        _summaryBar.Dock = DockStyle.Bottom;
        _summaryBar.Height = 10;
        _summaryBar.Style = ProgressBarStyle.Continuous;
        _summaryStats.Dock = DockStyle.Fill;
        _summaryStats.ForeColor = Ui.Muted;
        _summaryStats.TextAlign = ContentAlignment.MiddleLeft;
        _summaryStats.UseMnemonic = false;
        meta.Controls.Add(_summaryStats);
        meta.Controls.Add(_summaryBar);

        _summaryTitle.Dock = DockStyle.Top;
        _summaryTitle.Height = 36;
        _summaryTitle.Font = Ui.HeadFont;
        _summaryTitle.TextAlign = ContentAlignment.MiddleLeft;
        _summaryTitle.ForeColor = Ui.Text;
        _summaryTitle.UseMnemonic = false;

        _summary.Controls.Add(_summarySplit);
        _summary.Controls.Add(_summaryFooter);
        _summary.Controls.Add(_summaryFilterRow);
        _summary.Controls.Add(_summarySearchRow);
        _summary.Controls.Add(meta);
        _summary.Controls.Add(_summaryTitle);
    }

    void LayoutSummaryChrome()
    {
        var vertical = _docking || _summary.ClientSize.Width < 740;
        var testing = ExamSession.Mode == "testing";
        _summaryCheck.Visible = !testing;
        _summaryActions.ColumnCount = testing ? 3 : 4;
        while (_summaryActions.ColumnStyles.Count < 4)
        {
            _summaryActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        }

        if (testing)
        {
            _summaryActions.ColumnStyles[0].Width = 34;
            _summaryActions.ColumnStyles[1].Width = 0;
            _summaryActions.ColumnStyles[2].Width = 33;
            _summaryActions.ColumnStyles[3].Width = 33;
        }
        else
        {
            _summaryActions.ColumnStyles[0].Width = 25;
            _summaryActions.ColumnStyles[1].Width = 25;
            _summaryActions.ColumnStyles[2].Width = 25;
            _summaryActions.ColumnStyles[3].Width = 25;
        }

        _summaryFooter.Height = 96;
        if (vertical)
        {
            _summary.Padding = new Padding(8, 8, 8, 8);
            _summaryListPane.Dock = DockStyle.Top;
            _summaryListPane.Padding = new Padding(0, 0, 0, 8);
            var body = Math.Max(160, _summary.ClientSize.Height - 250);
            _summaryListPane.Height = Math.Clamp(body * 38 / 100, 120, 240);
            _summaryTitle.TextAlign = ContentAlignment.MiddleLeft;
        }
        else
        {
            _summary.Padding = new Padding(16, 12, 16, 12);
            _summaryListPane.Dock = DockStyle.Left;
            _summaryListPane.Padding = new Padding(0, 0, 12, 0);
            _summaryListPane.Width = Math.Clamp(_summary.ClientSize.Width * 42 / 100, 280, 480);
            _summaryTitle.TextAlign = ContentAlignment.MiddleCenter;
        }

        if (_summaryList.Columns.Count >= 3)
        {
            _summaryList.Columns[0].Width = 32;
            _summaryList.Columns[2].Width = 108;
            _summaryList.Columns[1].Width = Math.Max(100, _summaryListPane.ClientSize.Width - 32 - 108 - 24);
        }

        FitReviewCards();
    }

    void FitReviewCards()
    {
        var w = Math.Max(120, _detailBlocks.ClientSize.Width - 8);
        foreach (Control child in _detailBlocks.Controls)
        {
            if (Equals(child.Tag, "review"))
            {
                child.Width = w;
            }
        }
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

    void ShowSummary(bool open)
    {
        _summaryOpen = open;
        if (open)
        {
            _summaryTitle.Text = (ExamSession.ProjectTitle ?? "Danh sách kỹ năng") + " — Tổng hợp";
            _summarySearch.Text = "";
            _summaryCheck.Visible = ExamSession.Mode != "testing";
            _detailHintBtn.Visible = ExamSession.Mode != "testing";
            _hintExpanded = false;
            FillSummary();
        }

        if (_docking)
        {
            if (open)
            {
                var work = CurrentWork();
                var side = _state is "left" or "right" ? _state : "right";
                var w = Math.Min(420, Math.Max(340, work.W - LayoutMath.MinWord));
                var dock = side == "left"
                    ? new Rect(work.X, work.Y, w, work.H)
                    : new Rect(work.Right - w, work.Y, w, work.H);
                dock = LayoutMath.PinToWork(dock, work, side);
                var word = LayoutMath.WordBeside(work, dock, side);
                FitOverlay(dock.X, dock.Y, dock.W, dock.H);
                WordWindow.ApplySoon(word, _app);
                ApplyExamChrome();
                LayoutSummaryChrome();
            }
            else
            {
                ApplyDock(waitForWord: true);
            }
        }
        else
        {
            ApplyExamChrome();
            LayoutSummaryChrome();
        }
    }

    void FillSummary()
    {
        var q = (_summarySearch.Text ?? "").Trim();
        var filter = _summaryFilter.SelectedItem as string ?? "Tất cả";
        var rows = ExamSession.LastCheck;
        _summaryList.BeginUpdate();
        _summaryList.Items.Clear();
        var pass = 0;
        var fail = 0;
        var pending = 0;
        var ungraded = 0;
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
                _ => true,
            };
            if (!wanted)
            {
                continue;
            }

            var label = SkillReview.Label(status);
            var row = new ListViewItem([(i + 1).ToString(), name, label])
            {
                Tag = i,
            };
            row.ForeColor = SkillReview.ColorOf(status);
            if (i == _taskIndex)
            {
                row.Selected = true;
            }

            _summaryList.Items.Add(row);
        }

        _summaryList.EndUpdate();
        var total = _tasks.Items.Count;
        _summaryBar.Maximum = Math.Max(1, total);
        _summaryBar.Value = SkillReview.HideScores ? 0 : Math.Min(_summaryBar.Maximum, pass);
        _summaryStats.Text = SkillReview.HideScores
            ? "Chế độ thi: ẩn Đạt / Chưa đạt đến khi nộp bài."
            : $"Tiến độ: {pass}/{total} đạt  ·  {fail} chưa đạt  ·  {pending} chưa xác minh  ·  {ungraded} chưa chấm";
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

    void RenderSummaryDetail()
    {
        if (_summaryList.SelectedItems.Count == 0)
        {
            _detailHead.Text = "Chọn một kỹ năng trong danh sách.";
            _detailStatus.Text = "";
            _detailScore.Text = "";
            SetDetailBlocks([new ReviewBlock("Kết quả chấm", "Bấm một dòng bên trên để xem lỗi, thao tác đúng và gợi ý.", "result")]);
            _detailHintBtn.Visible = false;
            _detailHintBox.Visible = false;
            _detailHintBox.Height = 0;
            return;
        }

        var i = _summaryList.SelectedItems[0].Tag is int idx ? idx : 0;
        i = Math.Clamp(i, 0, Math.Max(0, _tasks.Items.Count - 1));
        var id = _tasks.Items.Count > 0 ? _tasks.Items[i].Tag as string : "";
        var name = _tasks.Items.Count > 0 ? _tasks.Items[i].Text : "";
        var item = ExamSession.Rubric?.Criteria?.FirstOrDefault(c => c.Id == id);
        if (item == null && ExamSession.Rubric?.Criteria is { Count: > 0 } list && i < list.Count)
        {
            item = list[i];
        }

        var hit = SkillReview.Find(ExamSession.LastCheck, id, i);
        _detailHead.Text = $"{i + 1}. {name}";
        _detailStatus.Text = SkillReview.Headline(hit.Status);
        _detailStatus.ForeColor = SkillReview.ColorOf(hit.Status);
        _detailScore.Text = string.IsNullOrWhiteSpace(id) ? "" : id + (hit.Possible > 0 && !SkillReview.HideScores ? $"  ·  {hit.Earned:0}/{hit.Possible:0} điểm" : "");
            SetDetailBlocks(SkillReview.AnalysisBlocks(hit, item));
        var steps = SkillReview.HintSteps(item);
        _detailHintBtn.Visible = ExamSession.Mode != "testing" && steps.Count > 0;
        _detailHintBtn.Text = _hintExpanded ? "Ẩn gợi ý" : "Xem gợi ý";
        ApplyDetailHint(steps);
    }

    void ToggleDetailHint()
    {
        _hintExpanded = !_hintExpanded;
        RenderSummaryDetail();
    }

    void ApplyDetailHint(IReadOnlyList<string> steps)
    {
        if (!_hintExpanded || steps.Count == 0)
        {
            _detailHintBox.Visible = false;
            _detailHintBox.Height = 0;
            return;
        }

        try
        {
            _detailHintBox.Rtf = Ui.HelpStepsRtf(steps, 11f);
        }
        catch
        {
            _detailHintBox.Text = string.Join("\n", steps.Select((s, n) => $"{n + 1}. {s}"));
        }

        _detailHintBox.Visible = true;
        _detailHintBox.Height = Math.Min(160, 28 + steps.Count * 28);
    }

    void SetDetailBlocks(IReadOnlyList<ReviewBlock> blocks)
    {
        _detailBlocks.SuspendLayout();
        _detailBlocks.Controls.Clear();
        foreach (var block in blocks)
        {
            _detailBlocks.Controls.Add(Ui.ReviewCard(block.Title, block.Body, block.Tone));
        }

        _detailBlocks.ResumeLayout();
        FitReviewCards();
    }

    void JumpSelectedSummary()
    {
        if (_summaryList.SelectedItems.Count == 0)
        {
            ShowSummary(false);
            return;
        }

        var i = _summaryList.SelectedItems[0].Tag is int idx ? idx : 0;
        if (_tasks.Items.Count > 0)
        {
            i = Math.Clamp(i, 0, _tasks.Items.Count - 1);
            _tasks.SelectedIndices.Clear();
            _tasks.Items[i].Selected = true;
            _tasks.EnsureVisible(i);
            _taskIndex = i;
            RenderHelp();
        }

        ShowSummary(false);
    }

    void ApplyHelpFonts()
    {
        var promptPt = _helpScale switch { 2 => 16f, 1 => 14f, _ => 12.5f };
        var bodyPt = _helpScale switch { 2 => 14f, 1 => 12.5f, _ => 11.5f };
        var titlePt = _helpScale switch { 2 => 14f, 1 => 12.5f, _ => 11.5f };
        _promptTitle.Font = new Font("Segoe UI", titlePt, FontStyle.Bold);
        _promptBody.Font = new Font("Segoe UI", promptPt, FontStyle.Bold);
        _helpTitle.Font = new Font("Segoe UI", titlePt, FontStyle.Bold);
        _helpBody.Font = new Font("Segoe UI", bodyPt);
        _aaSmaller.Enabled = _helpScale > 0;
        _aaBigger.Enabled = _helpScale < 2;
    }

    void NudgeHelpScale(int delta)
    {
        _helpScale = Math.Clamp(_helpScale + delta, 0, 2);
        ApplyHelpFonts();
        RenderHelp();
    }

    void RenderHelp()
    {
        var bodyPt = _helpScale switch { 2 => 14f, 1 => 12.5f, _ => 11.5f };
        var criteria = ExamSession.Rubric?.Criteria;
        if (criteria is not { Count: > 0 })
        {
            _promptBody.Text = ExamSession.ProjectTitle ?? "Bài MOS";
            SetHelpBody(["Làm đúng yêu cầu trên đề trong Microsoft Office đã cài trên máy."], bodyPt);
            return;
        }

        _taskIndex = Math.Clamp(_taskIndex, 0, criteria.Count - 1);
        var item = criteria[_taskIndex];
        var prompt = string.IsNullOrWhiteSpace(item.Prompt) ? item.Id : item.Prompt;
        _promptBody.Text = Ui.StripMarks(prompt);
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
                item.BackColor = state == _state ? Ui.DockBlue : Color.White;
                item.ForeColor = state == _state ? Color.White : Ui.Text;
            }
        }

        Ui.SetIconActive(_dockPin, _pinned);
        _dockPin.BackColor = _pinned ? Ui.DockBlue : Color.FromArgb(148, 163, 184);
        Ui.DockTips.SetToolTip(_dockHint, HelpOpen ? "Ẩn hướng dẫn" : "Hiện hướng dẫn");
        Ui.DockTips.SetToolTip(_dockTasks, "Chấm bài đang làm và hiện danh sách nhiệm vụ");
        Ui.DockTips.SetToolTip(_dockSave, "Lưu và thoát bài");
        Ui.DockTips.SetToolTip(_dockShare, "Bỏ qua chấm, sang nhiệm vụ sau");
        Ui.DockTips.SetToolTip(_dockBack, "Nhiệm vụ trước, không chấm");
        Ui.DockTips.SetToolTip(_dockNext, "Sang nhiệm vụ sau");
        Ui.DockTips.SetToolTip(_dockPos, "Gắn thanh bài thi sang vị trí khác");
        Ui.DockTips.SetToolTip(_dockMenu, "Menu tùy chọn thêm");
    }

    void ApplyExamChrome()
    {
        var showSummary = _summaryOpen;
        var showTasks = !showSummary && (!_compact || !_docking);
        var showHelp = !showSummary && HelpOpen;
        _exam.Padding = showTasks || (showSummary && !_docking) ? new Padding(12, 8, 12, 0) : Padding.Empty;
        _exam.BackColor = showTasks || showSummary ? Color.White : Color.FromArgb(245, 247, 249);
        _summary.Visible = showSummary;
        _dockChrome.Visible = !showSummary;
        _navGrip.Visible = !showSummary && _docking && _compact;
        if (_docking && _compact && !showSummary)
        {
            OrientNav();
            _dockChrome.Padding = new Padding(_nav.ChromePad);
        }
        _helpPane.Visible = showHelp;
        if (showHelp && _compact && _docking)
        {
            _helpPane.Dock = DockStyle.Fill;
            _helpPane.Padding = new Padding(8, 6, 8, 6);
            _promptCard.Height = 112;
            _helpHeader.Height = 32;
            _helpFooter.Visible = false;
            _helpFooter.Height = 0;
        }
        else
        {
            _helpPane.Dock = DockStyle.Top;
            _helpPane.Height = Math.Max(_nav.HelpH, 280);
            _helpPane.Padding = new Padding(8, 6, 8, 6);
            _promptCard.Height = 120;
            _helpHeader.Height = 32;
            _helpFooter.Visible = false;
            _helpFooter.Height = 0;
        }
        ApplyHelpFonts();
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
        _ = RefreshHomeDashboard();
    }

    async Task RefreshHomeDashboard()
    {
        _dash.ShowLoading();
        try
        {
            var attempts = await ExamHub.ListAttemptsAsync();
            IReadOnlyDictionary<string, MosProgress> progress;
            try
            {
                progress = await ExamHub.ListProgramProgressAsync();
            }
            catch
            {
                progress = new Dictionary<string, MosProgress>();
            }

            _dash.Bind(attempts, progress, ExamSession.DisplayName);
        }
        catch (Exception ex)
        {
            _dash.ShowError(ex.Message);
        }
    }

    async Task ResumeOpenAttempt(MosAttempt attempt)
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

        var sets = ExamHub.GroupAttempts(rows);
        var filtered = running ? sets.Open.ToList() : sets.Submitted.ToList();
        list.Controls.Clear();
        if (filtered.Count == 0)
        {
            list.Controls.Add(new Label
            {
                Text = running
                    ? "Không có bài đang làm dở."
                    : "Bạn chưa hoàn thành bài thi nào. Các bài thi đã nộp sẽ hiển thị ở đây.",
                AutoSize = true,
                ForeColor = Ui.Muted,
                MaximumSize = new Size(420, 0),
                Margin = new Padding(12),
            });
            return;
        }

        if (running && sets.ArchivedOpen > 0)
        {
            list.Controls.Add(new Label
            {
                Text = "Đã gom " + sets.ArchivedOpen + " lần mở cũ của cùng đề / quá 21 ngày.",
                AutoSize = true,
                ForeColor = Ui.Muted,
                Margin = new Padding(12, 8, 12, 8),
            });
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
            : $"{Ui.ModeLabel(attempt.Mode)} · {attempt.StartedAt} · {attempt.ScoreLabel} đã xác minh";
        Button? go = null;
        if (resume)
        {
            go = Ui.PrimaryBtn("Tiếp tục", 120);
            go.BackColor = Ui.Success;
            go.Click += async (_, _) => await ResumeOpenAttempt(attempt);
        }

        return Ui.ListCard(attempt.Title, detail, go);
    }

    async Task ConfirmStart(MosProject project, string mode)
    {
        var modeText = mode == "testing"
            ? "Thi: ẩn điểm và hướng dẫn cho đến khi nộp bài."
            : "Luyện tập: hiện hướng dẫn từng bước, nút AAA đổi cỡ chữ, Kiểm tra nhiệm vụ, và tổng hợp phân tích Đạt / Chưa đạt từng kỹ năng.";
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
        if (_demoOnStart && ExamSession.Mode != "testing")
        {
            BeginInvoke(async () => await RunActionDemo());
        }
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
        _summaryOpen = false;
        ExamSession.LastCheck = [];
        ActionEvidence.Begin(ExamSession.AttemptId);
        WordActionProbe.Reset();
        _taskIndex = 0;
        _examStatus.Text = train
            ? "Bóng đèn: hướng dẫn. Danh sách: tổng hợp nhiệm vụ. Đĩa: lưu và thoát."
            : "Danh sách nhiệm vụ. Đĩa: lưu và thoát. Nộp bài trong menu hoặc Nộp bài.";
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

        MinimumSize = new Size(w, h);
        MaximumSize = new Size(w, h);
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

        if (_summaryOpen)
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
        var (dock, _) = LayoutMath.Compute(work, _state, _compact, thickness: _navThickness);
        if (_compact && HelpOpen)
        {
            dock = LayoutMath.GrowForHelp(dock, work, _state);
        }

        dock = LayoutMath.PinToWork(dock, work, _state);
        return (dock, LayoutMath.WordBeside(work, dock, _state));
    }

    const uint SwpNoactivate = 0x0010;
    const uint SwpNozorder = 0x0004;
    const uint SwpFramechanged = 0x0020;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
}
