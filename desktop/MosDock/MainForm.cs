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
    readonly Button _navHome = Ui.IconBtn(NavIcon.Home, "Lưu và về trang chủ");
    readonly Button _navLeft = Ui.IconBtn(NavIcon.Left, "Trái");
    readonly Button _navRight = Ui.IconBtn(NavIcon.Right, "Phải");
    readonly Button _navBottom = Ui.IconBtn(NavIcon.Bottom, "Đáy");
    readonly Button _navToggle = Ui.IconBtn(NavIcon.Expand, "Hiện nhiệm vụ");
    readonly Button _checkBtn = Ui.IconBtn(NavIcon.Check, "Kiểm tra nhiệm vụ");
    readonly Button _submitBtn = Ui.IconBtn(NavIcon.Submit, "Nộp bài");
    readonly ToolTip _navTips = new() { ShowAlways = true };
    readonly FlowLayoutPanel _navIcons = new();
    readonly System.Windows.Forms.Timer _keepWord = new();
    LocalAgent? _agent;
    string _state = "bottom";
    string _app = "word";
    bool _compact;
    bool _docking;
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
        _exam.Padding = new Padding(8, 4, 8, 8);
        _exam.Visible = false;

        _navIcons.Dock = DockStyle.Top;
        _navIcons.Height = LayoutMath.IconBarH;
        _navIcons.WrapContents = false;
        _navIcons.FlowDirection = FlowDirection.LeftToRight;
        _navIcons.BackColor = Ui.Nav;
        _navIcons.Padding = new Padding(4, 2, 4, 2);
        _navIcons.Controls.Add(new Label
        {
            Text = "MOS",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(6, 8, 8, 0),
            UseMnemonic = false,
        });
        _navHome.Click += (_, _) => SaveAndHome();
        _navLeft.Click += (_, _) => MoveDock("left");
        _navRight.Click += (_, _) => MoveDock("right");
        _navBottom.Click += (_, _) => MoveDock("bottom");
        _navToggle.Click += (_, _) =>
        {
            _compact = !_compact;
            EnterDock(_compact);
        };
        _checkBtn.Click += async (_, _) => await CheckTasks();
        _submitBtn.Click += async (_, _) => await SubmitExam();
        _navIcons.Controls.Add(_navHome);
        _navIcons.Controls.Add(_navLeft);
        _navIcons.Controls.Add(_navRight);
        _navIcons.Controls.Add(_navBottom);
        _navIcons.Controls.Add(_navToggle);
        _navIcons.Controls.Add(_checkBtn);
        _navIcons.Controls.Add(_submitBtn);

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

        _exam.Controls.Add(_tasks);
        _exam.Controls.Add(_examStatus);
        _exam.Controls.Add(_examMeta);
        _exam.Controls.Add(_examTitle);
        _exam.Controls.Add(_navIcons);
    }

    void MoveDock(string state)
    {
        _state = state;
        if (!_docking)
        {
            EnterDock(_compact);
            return;
        }

        ApplyDock(waitForWord: true);
        HighlightDockIcons();
    }

    void HighlightDockIcons()
    {
        Ui.SetIconActive(_navLeft, _state == "left");
        Ui.SetIconActive(_navRight, _state == "right");
        Ui.SetIconActive(_navBottom, _state is "bottom" or "minimized");
        _navToggle.Tag = _compact ? NavIcon.Expand : NavIcon.Collapse;
        _navTips.SetToolTip(_navToggle, _compact ? "Hiện nhiệm vụ" : "Thu gọn thanh");
        _navToggle.Invalidate();
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
            ? "Thi: ẩn điểm và gợi ý cho đến khi nộp bài."
            : "Luyện tập: có Kiểm tra nhiệm vụ sau mỗi bước.";
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
            + (ExamSession.Mode == "testing" ? " · điểm ẩn đến khi nộp" : " · có kiểm tra nhiệm vụ");
        _checkBtn.Visible = ExamSession.Mode != "testing";
        _examStatus.Text = "Làm bài trên cửa sổ Office bên cạnh. Khung này giữ danh sách nhiệm vụ.";
        _tasks.Items.Clear();
        var lines = ExamSession.Rubric?.Criteria is { Count: > 0 } criteria
            ? criteria.Select(c => (c.Id, string.IsNullOrWhiteSpace(c.Prompt) ? c.Id : c.Prompt)).ToList()
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
        if (state is "left" or "right" or "minimized" or "bottom")
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
        TopMost = true;
        if (!_keepWord.Enabled)
        {
            _keepWord.Start();
        }

        ApplyDock(waitForWord: true);
        if (switching)
        {
            Show();
            TopMost = true;
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
        Bounds = new Rectangle(dock.X, dock.Y, dock.W, dock.H);
        TopMost = true;
        _exam.Padding = _compact ? Padding.Empty : new Padding(12, 8, 12, 12);
        _exam.BackColor = _compact ? Ui.Nav : Color.White;
        _navIcons.Visible = true;
        _tasks.Visible = !_compact;
        _examTitle.Visible = !_compact;
        _examMeta.Visible = !_compact;
        _examStatus.Visible = !_compact;
        HighlightDockIcons();
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
