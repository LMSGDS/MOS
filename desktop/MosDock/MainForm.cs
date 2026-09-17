using System.Diagnostics;
using System.Net;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace MosDock;

sealed class MainForm : Form
{
    readonly Panel _bar = new();
    readonly WebView2 _web = new();
    readonly System.Windows.Forms.Timer _keepWord = new();
    readonly Label _status = new();
    LocalAgent? _agent;
    string _state;
    string _app;
    bool _compact;
    readonly bool _launchOnStart;
    readonly string? _fileOnStart;

    public MainForm(string? initialState, string? initialApp = null, bool launchOnStart = false, string? fileOnStart = null)
    {
        _state = string.IsNullOrWhiteSpace(initialState) ? "bottom" : initialState.ToLowerInvariant();
        _app = OfficeApp.Resolve(initialApp).Id;
        _launchOnStart = launchOnStart;
        _fileOnStart = fileOnStart;
        Text = "MOS-KulKul";
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(30, 79, 115);

        _bar.Height = 40;
        _bar.Dock = DockStyle.Top;
        _bar.Padding = new Padding(8, 6, 8, 6);

        var title = new Label
        {
            Text = "MOS-KulKul",
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(8, 10),
        };
        _bar.Controls.Add(title);
        AddButton("Thu nhỏ", "minimized", 140);
        AddButton("Đính trái", "left", 240);
        AddButton("Đính phải", "right", 350);
        AddButton("Đính đáy", "bottom", 460);
        AddButton("Đặt cửa sổ", "place", 570, placeOnly: true);
        AddButton("Mở rộng đề", "expand", 680, placeOnly: true);
        AddButton("Tải đề", "exam-open", 790, placeOnly: true);
        AddButton("Nộp bài", "exam-submit", 900, placeOnly: true);

        _status.AutoSize = true;
        _status.ForeColor = Color.FromArgb(200, 230, 255);
        _status.Location = new Point(1010, 12);
        _status.Text = ExamSession.Mode == "testing" ? "Chế độ thi" : "Chế độ luyện tập";
        _bar.Controls.Add(_status);

        _web.Dock = DockStyle.Fill;

        Controls.Add(_web);
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
                _status.Text = "Agent local lỗi: " + ex.Message;
            }

            if (_launchOnStart)
            {
                _compact = true;
                WordWindow.Launch(_app, _fileOnStart);
            }

            ApplyDock(waitForWord: true);
            try
            {
                var dataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MOS",
                    "KulKul",
                    "WebView2");
                Directory.CreateDirectory(dataDir);
                var env = await CoreWebView2Environment.CreateAsync(null, dataDir);
                await _web.EnsureCoreWebView2Async(env);
                SyncWebViewCookies();
                _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                _web.CoreWebView2.WebMessageReceived += (_, e) =>
                {
                    var raw = e.TryGetWebMessageAsString();
                    var launch = raw?.Contains("\"open\"", StringComparison.OrdinalIgnoreCase) == true
                        || raw?.Contains("mos-open", StringComparison.OrdinalIgnoreCase) == true;
                    OnPlaceRequest(
                        ExtractField(raw, "state") ?? _state,
                        ExtractField(raw, "app"),
                        launch,
                        ExtractField(raw, "file"),
                        launch);
                };
                _web.CoreWebView2.NavigationStarting += (_, e) =>
                {
                    if (e.Uri.StartsWith("ms-word:", StringComparison.OrdinalIgnoreCase)
                        || e.Uri.StartsWith("ms-excel:", StringComparison.OrdinalIgnoreCase)
                        || e.Uri.StartsWith("ms-powerpoint:", StringComparison.OrdinalIgnoreCase)
                        || e.Uri.StartsWith("mosdock:", StringComparison.OrdinalIgnoreCase)
                        || e.Uri.StartsWith("mos-kulkul:", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        if (e.Uri.StartsWith("ms-excel:", StringComparison.OrdinalIgnoreCase))
                        {
                            _app = "excel";
                        }
                        else if (e.Uri.StartsWith("ms-powerpoint:", StringComparison.OrdinalIgnoreCase))
                        {
                            _app = "powerpoint";
                        }
                        else if (e.Uri.StartsWith("ms-word:", StringComparison.OrdinalIgnoreCase))
                        {
                            _app = "word";
                        }

                        var launch = !e.Uri.Contains(":place", StringComparison.OrdinalIgnoreCase);
                        if (e.Uri.StartsWith("mosdock:", StringComparison.OrdinalIgnoreCase)
                            || e.Uri.StartsWith("mos-kulkul:", StringComparison.OrdinalIgnoreCase))
                        {
                            var parsed = Protocol.Parse([e.Uri]);
                            if (!string.IsNullOrWhiteSpace(parsed.App))
                            {
                                _app = OfficeApp.Resolve(parsed.App).Id;
                            }

                            if (!string.IsNullOrWhiteSpace(parsed.State))
                            {
                                _state = parsed.State;
                            }

                            launch = parsed.Launch;
                        }

                        if (launch || e.Uri.StartsWith("ms-", StringComparison.OrdinalIgnoreCase))
                        {
                            WordWindow.Launch(_app);
                        }

                        ApplyDock(waitForWord: true);
                    }
                };
                _web.CoreWebView2.Navigate(
                    $"{Portal.Origin}/?che-do=dock&chuong-trinh={Uri.EscapeDataString(_app)}");
            }
            catch (Exception ex)
            {
                _web.Visible = false;
                _status.Text = "Thiếu WebView2 — cài lại MOS-KulKul";
                MessageBox.Show(
                    "MOS-KulKul không tải được WebView2.\n\n"
                    + "Gỡ bản cũ rồi tải Setup.exe mới tại https://mos.gds.edu.vn/cai-dat\n"
                    + "Hoặc cài Microsoft Edge WebView2 Runtime rồi mở lại MOS-KulKul.\n\n"
                    + ex.Message,
                    "MOS-KulKul",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                try
                {
                    Process.Start(new ProcessStartInfo("https://mos.gds.edu.vn/cai-dat") { UseShellExecute = true });
                }
                catch
                {
                    // ignore
                }
            }
        };

        FormClosed += (_, _) => _agent?.Dispose();

        _keepWord.Interval = 700;
        _keepWord.Tick += (_, _) => ApplyWordOnly();
        _keepWord.Start();
    }

    void SyncWebViewCookies()
    {
        if (_web.CoreWebView2 is null)
        {
            return;
        }

        Uri uri;
        try
        {
            uri = new Uri(Portal.Origin + "/");
        }
        catch
        {
            return;
        }

        foreach (Cookie cookie in Portal.Cookies.GetCookies(uri))
        {
            var domain = string.IsNullOrWhiteSpace(cookie.Domain) ? uri.Host : cookie.Domain.TrimStart('.');
            var wv = _web.CoreWebView2.CookieManager.CreateCookie(
                cookie.Name,
                cookie.Value,
                domain,
                string.IsNullOrWhiteSpace(cookie.Path) ? "/" : cookie.Path);
            wv.IsHttpOnly = cookie.HttpOnly;
            wv.IsSecure = cookie.Secure;
            _web.CoreWebView2.CookieManager.AddOrUpdateCookie(wv);
        }
    }

    void AddButton(string text, string state, int x, bool placeOnly = false)
    {
        var btn = new Button
        {
            Text = text,
            Tag = state,
            Location = new Point(x, 6),
            Size = new Size(placeOnly ? 100 : 100, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 142, 226),
            ForeColor = Color.White,
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (_, _) =>
        {
            var tag = (string)btn.Tag!;
            if (tag == "exam-open")
            {
                _ = StartExam();
                return;
            }

            if (tag == "exam-submit")
            {
                _ = SubmitExam();
                return;
            }

            if (tag == "expand")
            {
                _compact = false;
                if (_state == "minimized")
                {
                    _state = "bottom";
                }

                ApplyDock(waitForWord: true);
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

            ApplyDock(waitForWord: true);
        };
        _bar.Controls.Add(btn);
    }

    async Task StartExam()
    {
        _status.Text = "Đang tải đề từ PostgreSQL…";
        var (ok, msg) = await ExamHub.StartProjectAsync(_app);
        _compact = true;
        ApplyDock(waitForWord: true);
        _status.Text = ok ? msg : ("Lỗi đề: " + msg);
    }

    async Task SubmitExam()
    {
        _status.Text = "Đang nộp bài…";
        var (ok, msg) = await ExamHub.SubmitAsync(_app);
        _status.Text = msg;
        if (!ok)
        {
            MessageBox.Show(msg, "MOS-KulKul", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
        }

        if (launch || compact || state == "minimized")
        {
            _compact = true;
        }

        if (launch)
        {
            WordWindow.Launch(_app, file);
        }

        ApplyDock(waitForWord: true);
    }

    static string? ExtractField(string? raw, string field)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var key = $"\"{field}\"";
        var i = raw.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (i < 0)
        {
            return field == "state" ? raw.Trim() : null;
        }

        var colon = raw.IndexOf(':', i + key.Length);
        if (colon < 0)
        {
            return null;
        }

        var q1 = raw.IndexOf('"', colon);
        var q2 = q1 < 0 ? -1 : raw.IndexOf('"', q1 + 1);
        return q1 >= 0 && q2 > q1 ? raw[(q1 + 1)..q2] : null;
    }

    void ApplyDock(bool waitForWord)
    {
        var wa = Screen.FromHandle(IsHandleCreated ? Handle : IntPtr.Zero).WorkingArea;
        var work = new Rect(wa.X, wa.Y, wa.Width, wa.Height);
        var (dock, word) = LayoutMath.Compute(work, _state, _compact);
        Bounds = new Rectangle(dock.X, dock.Y, dock.W, dock.H);
        TopMost = true;
        var controlsOnly = _compact || _state == "minimized";
        _web.Visible = !controlsOnly;
        _bar.Dock = controlsOnly ? DockStyle.Fill : DockStyle.Top;
        if (!controlsOnly)
        {
            _bar.Height = 40;
        }
        _status.Text = $"{OfficeApp.Resolve(_app).Id} → ({word.X},{word.Y}) {word.W}×{word.H}";
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
        if (!Visible)
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
