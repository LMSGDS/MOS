namespace MosDock;

/// <summary>
/// MOS-KulKul trên PC: đề từ PostgreSQL, mở Word/Excel/PowerPoint trên máy, dock TopMost.
/// Không nhúng website, không dùng Office Online.
/// </summary>
sealed class MainForm : Form
{
    readonly FlowLayoutPanel _bar = new();
    readonly Panel _exam = new();
    readonly ComboBox _projects = new();
    readonly TextBox _steps = new();
    readonly Label _meta = new();
    readonly Label _score = new();
    readonly System.Windows.Forms.Timer _keepWord = new();
    readonly Button _openBtn = new();
    readonly Button _submitBtn = new();
    readonly Button _checkBtn = new();
    readonly ListBox _results = new();
    LocalAgent? _agent;
    string _state;
    string _app;
    bool _compact;
    bool _docking;
    Rectangle? _savedWorkspace;
    readonly bool _launchOnStart;
    readonly string? _fileOnStart;
    IReadOnlyList<MosProject> _items = [];

    public MainForm(string? initialState, string? initialApp = null, bool launchOnStart = false, string? fileOnStart = null)
    {
        _state = string.IsNullOrWhiteSpace(initialState) ? "bottom" : initialState.ToLowerInvariant();
        _app = OfficeApp.Resolve(initialApp).Id;
        _launchOnStart = launchOnStart;
        _fileOnStart = fileOnStart;
        Text = "MOS-KulKul";
        FormBorderStyle = FormBorderStyle.Sizable;
        TopMost = false;
        ShowInTaskbar = true;
        MinimizeBox = true;
        MaximizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(980, 640);
        MinimumSize = new Size(720, 420);
        BackColor = Color.FromArgb(30, 79, 115);
        Font = new Font("Segoe UI", 10f);

        _bar.Height = 44;
        _bar.Dock = DockStyle.Top;
        _bar.WrapContents = false;
        _bar.Padding = new Padding(6, 6, 6, 4);
        _bar.BackColor = Color.FromArgb(30, 79, 115);

        _bar.Controls.Add(MakeLabel("MOS-KulKul  " + (ExamSession.DisplayName ?? "")));
        AddAppButton("Word", "word", Color.FromArgb(43, 87, 154));
        AddAppButton("Excel", "excel", Color.FromArgb(33, 115, 70));
        AddAppButton("PPT", "powerpoint", Color.FromArgb(210, 71, 38));
        AddDockButton("Thu nhỏ", "minimized");
        AddDockButton("Trái", "left");
        AddDockButton("Phải", "right");
        AddDockButton("Đáy", "bottom");
        AddDockButton("Đặt cửa sổ", "place", placeOnly: true);
        AddDockButton("Mở rộng đề", "expand", placeOnly: true);
        AddDockButton("Cửa sổ đề", "workspace", placeOnly: true);
        var checkBar = new Button
        {
            Text = "Kiểm tra nhiệm vụ",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(124, 58, 237),
            ForeColor = Color.White,
            Margin = new Padding(8, 2, 2, 2),
        };
        checkBar.FlatAppearance.BorderSize = 0;
        checkBar.Click += async (_, _) => await CheckTasks();
        _bar.Controls.Add(checkBar);

        _exam.Dock = DockStyle.Fill;
        _exam.BackColor = Color.FromArgb(244, 248, 252);
        _exam.Padding = new Padding(12, 8, 12, 8);

        _meta.AutoSize = false;
        _meta.Dock = DockStyle.Top;
        _meta.Height = 22;
        _meta.ForeColor = Color.FromArgb(15, 23, 42);
        _meta.Text = ModeText();

        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 36,
            WrapContents = false,
            BackColor = Color.FromArgb(244, 248, 252),
        };
        var pickLbl = new Label
        {
            Text = "Đề MOS",
            AutoSize = true,
            Margin = new Padding(0, 8, 8, 0),
            ForeColor = Color.FromArgb(15, 23, 42),
        };
        _projects.DropDownStyle = ComboBoxStyle.DropDownList;
        _projects.Width = 280;
        _projects.Margin = new Padding(0, 4, 8, 0);
        _projects.SelectedIndexChanged += (_, _) => ShowSelected();
        StyleAction(_openBtn, "Mở đề trên máy", Color.FromArgb(0, 142, 226));
        _openBtn.Click += async (_, _) => await StartExam();
        StyleAction(_submitBtn, "Nộp bài", Color.FromArgb(15, 118, 110));
        _submitBtn.Click += async (_, _) => await SubmitExam();
        StyleAction(_checkBtn, "Kiểm tra nhiệm vụ", Color.FromArgb(124, 58, 237));
        _checkBtn.Click += async (_, _) => await CheckTasks();
        row.Controls.Add(pickLbl);
        row.Controls.Add(_projects);
        row.Controls.Add(_openBtn);
        row.Controls.Add(_checkBtn);
        row.Controls.Add(_submitBtn);

        _steps.Multiline = true;
        _steps.ReadOnly = true;
        _steps.Dock = DockStyle.Fill;
        _steps.BorderStyle = BorderStyle.FixedSingle;
        _steps.BackColor = Color.White;
        _steps.ScrollBars = ScrollBars.Vertical;

        _results.Dock = DockStyle.Bottom;
        _results.Height = 160;
        _results.IntegralHeight = false;
        _results.Font = new Font("Consolas", 9f);
        _results.HorizontalScrollbar = true;

        _score.AutoSize = false;
        _score.Dock = DockStyle.Bottom;
        _score.Height = 24;
        _score.ForeColor = Color.FromArgb(0, 107, 176);
        _score.Text = "Chưa mở đề.";

        _exam.Controls.Add(_steps);
        _exam.Controls.Add(_results);
        _exam.Controls.Add(_score);
        _exam.Controls.Add(row);
        _exam.Controls.Add(_meta);

        Controls.Add(_exam);
        Controls.Add(_bar);

        Load += async (_, _) =>
        {
            Protocol.Register();
            try
            {
                _agent = new LocalAgent(this, OnPlaceRequest);
            }
            catch (Exception ex)
            {
                _score.Text = "Agent local lỗi: " + ex.Message;
            }

            if (_launchOnStart)
            {
                WordWindow.Launch(_app, _fileOnStart);
                EnterDock(compact: true);
            }
            else
            {
                ShowWorkspace();
            }

            await LoadProjects();
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

    static string ModeText() =>
        (ExamSession.Mode == "testing" ? "Chế độ thi" : "Chế độ luyện tập")
        + " · Office trên máy, không dùng Office Online";

    Label MakeLabel(string text) => new()
    {
        Text = text,
        ForeColor = Color.White,
        AutoSize = true,
        Margin = new Padding(4, 8, 12, 0),
        Font = new Font("Segoe UI", 10f, FontStyle.Bold),
    };

    static void StyleAction(Button btn, string text, Color color)
    {
        btn.Text = text;
        btn.AutoSize = true;
        btn.FlatStyle = FlatStyle.Flat;
        btn.BackColor = color;
        btn.ForeColor = Color.White;
        btn.FlatAppearance.BorderSize = 0;
        btn.Margin = new Padding(0, 2, 8, 0);
        btn.Padding = new Padding(10, 4, 10, 4);
    }

    void AddAppButton(string text, string id, Color color)
    {
        var btn = new Button
        {
            Text = text,
            Tag = id,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = id == _app ? color : Color.FromArgb(15, 50, 80),
            ForeColor = Color.White,
            Margin = new Padding(2, 2, 2, 2),
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += async (_, _) =>
        {
            _app = id;
            foreach (Control c in _bar.Controls)
            {
                if (c is Button b && b.Tag is string tag && tag is "word" or "excel" or "powerpoint")
                {
                    var on = tag == _app;
                    b.BackColor = on
                        ? (tag == "excel" ? Color.FromArgb(33, 115, 70)
                            : tag == "powerpoint" ? Color.FromArgb(210, 71, 38)
                            : Color.FromArgb(43, 87, 154))
                        : Color.FromArgb(15, 50, 80);
                }
            }

            await LoadProjects();
        };
        _bar.Controls.Add(btn);
    }

    void AddDockButton(string text, string state, bool placeOnly = false)
    {
        var btn = new Button
        {
            Text = text,
            Tag = state,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 142, 226),
            ForeColor = Color.White,
            Margin = new Padding(2, 2, 2, 2),
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (_, _) =>
        {
            var tag = (string)btn.Tag!;
            if (tag == "workspace")
            {
                ShowWorkspace();
                return;
            }

            if (tag == "expand")
            {
                _compact = false;
                if (_state == "minimized")
                {
                    _state = "bottom";
                }

                EnterDock(compact: false);
                return;
            }

            if (tag == "minimized" || tag == "place")
            {
                _compact = true;
            }

            if (!placeOnly)
            {
                _state = tag;
            }

            EnterDock(compact: _compact);
            return;
        };
        _bar.Controls.Add(btn);
    }

    async Task LoadProjects()
    {
        _meta.Text = OfficeApp.Resolve(_app).Id.ToUpperInvariant() + " · " + ModeText();
        try
        {
            _items = await ExamHub.ListProjectsAsync(_app);
        }
        catch (Exception ex)
        {
            _items = [];
            _score.Text = "Không tải được đề: " + ex.Message;
        }

        _projects.Items.Clear();
        foreach (var p in _items)
        {
            _projects.Items.Add(p.Title);
        }

        if (_projects.Items.Count > 0)
        {
            _projects.SelectedIndex = 0;
        }

        ShowSelected();
    }

    void ShowSelected()
    {
        if (_projects.SelectedIndex < 0 || _projects.SelectedIndex >= _items.Count)
        {
            _steps.Text = "Chưa có đề cho chương trình này.";
            return;
        }

        var p = _items[_projects.SelectedIndex];

        var mins = Math.Max(1, p.TimeLimitSec / 60);
        _checkBtn.Visible = ExamSession.Mode != "testing";
        _steps.Text =
            p.Title + Environment.NewLine
            + "Kỹ năng: " + (string.IsNullOrWhiteSpace(p.Skill) ? "—" : p.Skill)
            + " · Thời gian: " + mins + " phút"
            + (string.IsNullOrWhiteSpace(p.RubricVersion) ? "" : " · Rubric " + p.RubricVersion)
            + Environment.NewLine + Environment.NewLine
            + "Hướng dẫn:" + Environment.NewLine
            + string.Join(Environment.NewLine, p.Steps.Select((s, i) => $"{i + 1}. {s}"));
    }

    async Task StartExam()
    {
        var id = _projects.SelectedIndex >= 0 && _projects.SelectedIndex < _items.Count
            ? _items[_projects.SelectedIndex].Id
            : null;
        _score.Text = "Đang tải đề và mở Office trên máy…";
        var (ok, msg) = await ExamHub.StartProjectAsync(_app, id);
        _score.Text = ok ? "Đã mở: " + msg : "Lỗi đề: " + msg;
        if (ok)
        {
            EnterDock(compact: true);
        }
        else
        {
            MessageBox.Show(_score.Text, "MOS-KulKul", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    async Task CheckTasks()
    {
        if (ExamSession.Mode == "testing")
        {
            _score.Text = "Chế độ thi ẩn điểm luyện tập. Nộp bài khi xong.";
            return;
        }

        ShowWorkspace();
        _score.Text = "Đang lưu tài liệu bài thi và chấm nhiệm vụ…";
        var (ok, summary, criteria) = await ExamHub.CheckTasksAsync(_app);
        _score.Text = summary;
        _results.Items.Clear();
        foreach (var c in criteria)
        {
            var mark = c.Status switch
            {
                "pass" => "ĐẠT",
                "fail" => "CHƯA ĐẠT",
                "unverified" => "CHƯA XN",
                "error" => "LỖI",
                _ => c.Status.ToUpperInvariant(),
            };
            _results.Items.Add($"{c.Id} [{mark}] {c.Earned}/{c.Possible} — {c.Message}");
        }

        if (!ok)
        {
            MessageBox.Show(summary, "MOS-KulKul", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    async Task SubmitExam()
    {
        _score.Text = "Đang lưu và nộp bài…";
        var (ok, msg) = await ExamHub.SubmitAsync(_app);
        _score.Text = msg;
        if (!ok)
        {
            MessageBox.Show(msg, "MOS-KulKul", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        else
        {
            ShowWorkspace();
        }
    }

    void OnPlaceRequest(string state, string? app = null, bool launch = false, string? file = null, bool compact = false)
    {
        state = (state ?? "bottom").ToLowerInvariant();
        if (state is "left" or "right" or "minimized" or "bottom")
        {
            _state = state;
        }

        if (!string.IsNullOrWhiteSpace(app))
        {
            _app = OfficeApp.Resolve(app).Id;
            _ = LoadProjects();
        }

        if (launch || compact || state == "minimized")
        {
            _compact = true;
        }

        if (launch)
        {
            WordWindow.Launch(_app, file);
        }

        EnterDock(compact: _compact);
    }

    void ShowWorkspace()
    {
        var switching = _docking;
        _docking = false;
        _compact = false;
        _keepWord.Stop();
        if (switching)
        {
            Hide();
        }

        TopMost = false;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = true;
        MaximizeBox = true;
        ControlBox = true;
        Text = "MOS-KulKul";
        _exam.Visible = true;
        _bar.Dock = DockStyle.Top;
        _bar.Height = 44;
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
        var controlsOnly = _compact || _state == "minimized";
        _exam.Visible = !controlsOnly;
        _bar.Dock = controlsOnly ? DockStyle.Fill : DockStyle.Top;
        if (!controlsOnly)
        {
            _bar.Height = 44;
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
