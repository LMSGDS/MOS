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
    readonly Button _checkBtn = new();
    readonly Button _submitBtn = new();
    readonly Button _saveBtn = new();
    readonly Button _compactBtn = new();
    readonly System.Windows.Forms.Timer _keepWord = new();
    LocalAgent? _agent;
    string _state = "left";
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
        ClientSize = new Size(980, 640);
        MinimumSize = new Size(820, 520);
        BackColor = Ui.Page;
        Font = Ui.BodyFont;

        BuildHeader();
        BuildHome();
        BuildCatalog();
        BuildListPage(_resume, _resumeList, "Bài đang làm dở — chọn một bài để tiếp tục trên Office máy.");
        BuildListPage(_done, _doneList, "Bài đã nộp. Điểm hiển thị phần đã xác minh; Find/Go To có thể còn chưa xác minh.");
        BuildExam();

        _body.Dock = DockStyle.Fill;
        _body.BackColor = Ui.Page;
        _body.Padding = new Padding(28, 16, 28, 24);
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
        _header.Height = 56;
        _header.BackColor = Ui.Navy;

        _back.Text = "← Trang chủ";
        _back.Size = new Size(120, 28);
        _back.Location = new Point(16, 14);
        _back.FlatStyle = FlatStyle.Flat;
        _back.BackColor = Color.FromArgb(11, 58, 99);
        _back.ForeColor = Color.White;
        _back.FlatAppearance.BorderSize = 0;
        _back.Visible = false;
        _back.Click += (_, _) => ShowHome();

        _crumb.Text = "MOS-KulKul";
        _crumb.ForeColor = Color.White;
        _crumb.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
        _crumb.AutoSize = true;
        _crumb.Location = new Point(150, 16);

        _user.AutoSize = true;
        _user.ForeColor = Color.FromArgb(191, 219, 254);
        _user.Location = new Point(620, 18);
        _user.Text = ExamSession.DisplayName;

        _signOut.Text = "Đăng xuất";
        _signOut.Size = new Size(100, 28);
        _signOut.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _signOut.FlatStyle = FlatStyle.Flat;
        _signOut.BackColor = Color.FromArgb(11, 58, 99);
        _signOut.ForeColor = Color.White;
        _signOut.FlatAppearance.BorderSize = 0;
        _signOut.Click += (_, _) =>
        {
            SignOutRequested = true;
            Close();
        };

        _header.Resize += (_, _) =>
        {
            _signOut.Location = new Point(_header.Width - 116, 14);
            _user.Location = new Point(Math.Max(300, _signOut.Left - 12 - _user.PreferredSize.Width), 18);
        };
        _header.Controls.Add(_back);
        _header.Controls.Add(_crumb);
        _header.Controls.Add(_user);
        _header.Controls.Add(_signOut);
    }

    void BuildHome()
    {
        _home.Dock = DockStyle.Fill;
        _home.BackColor = Ui.Page;
        var intro = new Label
        {
            Text = "Trang chủ",
            Font = Ui.TitleFont,
            ForeColor = Ui.Text,
            AutoSize = true,
            Location = new Point(8, 8),
        };
        var lead = new Label
        {
            Text = "Chọn một ô. Đề MOS mở trên Microsoft Office đã cài trên máy — không dùng Office Online.",
            Font = Ui.BodyFont,
            ForeColor = Ui.Muted,
            AutoSize = false,
            Location = new Point(8, 52),
            Size = new Size(860, 28),
        };
        var tiles = new FlowLayoutPanel
        {
            Location = new Point(0, 96),
            Size = new Size(900, 400),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Ui.Page,
        };
        tiles.Controls.Add(Ui.Tile("Bài mới", "Chọn Word, Excel hoặc PowerPoint, rồi Luyện tập hoặc Thi.", Ui.Blue, ShowCatalog));
        tiles.Controls.Add(Ui.Tile("Tiếp tục bài", "Mở bài đang làm dở, không tạo lần làm mới.", Ui.Teal, () => _ = ShowResume()));
        tiles.Controls.Add(Ui.Tile("Bài đã nộp", "Xem điểm đã xác minh và bài đã gửi lên máy chủ.", Ui.Orange, () => _ = ShowCompleted()));
        _home.Controls.Add(intro);
        _home.Controls.Add(lead);
        _home.Controls.Add(tiles);
        _home.Resize += (_, _) => tiles.Width = Math.Max(300, _home.ClientSize.Width - 8);
    }

    void BuildCatalog()
    {
        _catalog.Dock = DockStyle.Fill;
        _catalog.BackColor = Ui.Page;
        var intro = new Label
        {
            Text = "Bài mới",
            Font = Ui.TitleFont,
            ForeColor = Ui.Text,
            AutoSize = true,
            Location = new Point(8, 8),
        };
        var lead = new Label
        {
            Text = "1. Chọn chương trình   2. Chọn đề   3. Luyện tập hoặc Thi",
            Font = Ui.BodyFont,
            ForeColor = Ui.Muted,
            AutoSize = true,
            Location = new Point(8, 52),
        };
        _products.Location = new Point(0, 88);
        _products.Size = new Size(900, 188);
        _products.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _products.BackColor = Ui.Page;
        _products.Controls.Add(Ui.Tile("Word", "Microsoft Word trên máy", Ui.Word, () => _ = LoadCatalog("word")));
        _products.Controls.Add(Ui.Tile("Excel", "Microsoft Excel trên máy", Ui.Excel, () => _ = LoadCatalog("excel")));
        _products.Controls.Add(Ui.Tile("PowerPoint", "Microsoft PowerPoint trên máy", Ui.Ppt, () => _ = LoadCatalog("powerpoint")));

        _tests.Location = new Point(0, 284);
        _tests.Size = new Size(900, 260);
        _tests.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _tests.AutoScroll = true;
        _tests.BackColor = Ui.Page;
        _catalog.Controls.Add(intro);
        _catalog.Controls.Add(lead);
        _catalog.Controls.Add(_products);
        _catalog.Controls.Add(_tests);
        _catalog.Resize += (_, _) =>
        {
            _products.Width = Math.Max(300, _catalog.ClientSize.Width - 8);
            _tests.Width = _products.Width;
            _tests.Height = Math.Max(120, _catalog.ClientSize.Height - _tests.Top - 8);
        };
    }

    void BuildListPage(Panel page, FlowLayoutPanel list, string lead)
    {
        page.Dock = DockStyle.Fill;
        page.BackColor = Ui.Page;
        list.Dock = DockStyle.Fill;
        list.AutoScroll = true;
        list.BackColor = Ui.Page;
        list.Padding = new Padding(0, 8, 0, 8);
        page.Controls.Add(list);
        page.Controls.Add(new Label
        {
            Text = lead,
            Font = Ui.BodyFont,
            ForeColor = Ui.Muted,
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 36,
            Padding = new Padding(8, 8, 8, 0),
        });
    }

    void BuildExam()
    {
        _exam.Dock = DockStyle.Fill;
        _exam.BackColor = Color.FromArgb(248, 250, 252);
        _exam.Padding = new Padding(16);
        _exam.Visible = false;

        _examTitle.Dock = DockStyle.Top;
        _examTitle.Height = 48;
        _examTitle.Font = Ui.HeadFont;
        _examTitle.ForeColor = Ui.Text;

        _examMeta.Dock = DockStyle.Top;
        _examMeta.Height = 24;
        _examMeta.ForeColor = Ui.Muted;

        _tasks.Dock = DockStyle.Fill;
        _tasks.View = System.Windows.Forms.View.Details;
        _tasks.FullRowSelect = true;
        _tasks.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _tasks.Columns.Add("Nhiệm vụ", 240);
        _tasks.Columns.Add("Kết quả", 90);
        _tasks.BorderStyle = BorderStyle.FixedSingle;
        _tasks.BackColor = Color.White;

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 148,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0),
        };
        _examStatus.AutoSize = false;
        _examStatus.Size = new Size(200, 48);
        _examStatus.ForeColor = Ui.Navy;
        _checkBtn.Text = "Kiểm tra nhiệm vụ";
        StyleExamBtn(_checkBtn, Color.FromArgb(124, 58, 237));
        _checkBtn.Click += async (_, _) => await CheckTasks();
        _submitBtn.Text = "Nộp bài";
        StyleExamBtn(_submitBtn, Ui.Teal);
        _submitBtn.Click += async (_, _) => await SubmitExam();
        _saveBtn.Text = "Lưu và về trang chủ";
        StyleExamBtn(_saveBtn, Ui.Navy);
        _saveBtn.Click += (_, _) => SaveAndHome();
        _compactBtn.Text = "Thu gọn khung";
        StyleExamBtn(_compactBtn, Ui.Blue);
        _compactBtn.Click += (_, _) =>
        {
            _compact = !_compact;
            _compactBtn.Text = _compact ? "Hiện nhiệm vụ" : "Thu gọn khung";
            EnterDock(_compact);
        };
        actions.Controls.Add(_examStatus);
        actions.Controls.Add(_checkBtn);
        actions.Controls.Add(_submitBtn);
        actions.Controls.Add(_saveBtn);
        actions.Controls.Add(_compactBtn);

        _exam.Controls.Add(_tasks);
        _exam.Controls.Add(actions);
        _exam.Controls.Add(_examMeta);
        _exam.Controls.Add(_examTitle);
    }

    static void StyleExamBtn(Button btn, Color color)
    {
        btn.Size = new Size(200, 32);
        btn.Margin = new Padding(0, 0, 0, 6);
        btn.FlatStyle = FlatStyle.Flat;
        btn.BackColor = color;
        btn.ForeColor = Color.White;
        btn.FlatAppearance.BorderSize = 0;
        btn.TextAlign = ContentAlignment.MiddleLeft;
        btn.Padding = new Padding(10, 0, 0, 0);
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
        _crumb.Text = title;
        _crumb.Location = new Point(_back.Visible ? 150 : 20, 16);
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
        var card = new Panel
        {
            Width = 860,
            Height = 92,
            BackColor = Color.White,
            Margin = new Padding(8, 6, 8, 6),
            Padding = new Padding(16),
        };
        card.Controls.Add(new Label
        {
            Text = project.Title,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Ui.Text,
            AutoSize = false,
            Location = new Point(16, 12),
            Size = new Size(520, 24),
        });
        var mins = Math.Max(1, project.TimeLimitSec / 60);
        card.Controls.Add(new Label
        {
            Text = (string.IsNullOrWhiteSpace(project.Skill) ? Ui.AppName(project.Program) : project.Skill) + " · " + mins + " phút",
            ForeColor = Ui.Muted,
            AutoSize = true,
            Location = new Point(16, 40),
        });
        var train = Ui.Primary("Luyện tập", Ui.Blue, 120, 36);
        train.Location = new Point(560, 28);
        train.Click += async (_, _) => await ConfirmStart(project, "training");
        var test = Ui.Primary("Thi", Ui.Orange, 100, 36);
        test.Location = new Point(692, 28);
        test.Click += async (_, _) => await ConfirmStart(project, "testing");
        card.Controls.Add(train);
        card.Controls.Add(test);
        card.Resize += (_, _) =>
        {
            card.Width = Math.Max(480, _tests.ClientSize.Width - 36);
            test.Left = card.Width - 120;
            train.Left = test.Left - 132;
        };
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
    }

    Panel AttemptCard(MosAttempt attempt, bool resume)
    {
        var card = new Panel
        {
            Width = 860,
            Height = 80,
            BackColor = Color.White,
            Margin = new Padding(8, 6, 8, 6),
        };
        card.Controls.Add(new Label
        {
            Text = attempt.Title,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            Location = new Point(16, 12),
            AutoSize = true,
        });
        var detail = resume
            ? $"{Ui.AppName(attempt.Program)} · {Ui.ModeLabel(attempt.Mode)} · {attempt.StartedAt}"
            : $"{Ui.ModeLabel(attempt.Mode)} · {attempt.StartedAt} · {(attempt.Score is { } s ? $"{s}/{attempt.MaxScore} đã xác minh" : "chưa có điểm")}";
        card.Controls.Add(new Label
        {
            Text = detail,
            ForeColor = Ui.Muted,
            Location = new Point(16, 42),
            AutoSize = true,
        });
        if (resume)
        {
            var go = Ui.Primary("Tiếp tục", Ui.Teal, 120, 36);
            go.Location = new Point(700, 22);
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
                EnterDock(compact: false);
            };
            card.Controls.Add(go);
        }

        return card;
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
        EnterDock(compact: false);
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
                "pass" => Ui.Teal,
                "fail" => Color.FromArgb(153, 27, 27),
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
        _tasks.Visible = !_compact;
        _examTitle.Visible = !_compact;
        _examMeta.Visible = !_compact;
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
