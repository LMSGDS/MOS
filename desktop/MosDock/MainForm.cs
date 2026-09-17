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

    const string Portal = "https://mos.gds.edu.vn/dang-nhap?che-do=dock";

    public MainForm(string? initialState, string? initialApp = null)
    {
        _state = string.IsNullOrWhiteSpace(initialState) ? "bottom" : initialState.ToLowerInvariant();
        _app = OfficeApp.Resolve(initialApp).Id;
        Text = "MOS Dock";
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
            Text = "MOS · GMetrix",
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

        _status.AutoSize = true;
        _status.ForeColor = Color.FromArgb(200, 230, 255);
        _status.Location = new Point(690, 12);
        _status.Text = "Office sẽ nhảy vào ô còn lại sau khi mở";
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

            ApplyDock(waitForWord: true);
            await _web.EnsureCoreWebView2Async();
            _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            _web.CoreWebView2.WebMessageReceived += (_, e) =>
            {
                var raw = e.TryGetWebMessageAsString();
                OnPlaceRequest(ExtractField(raw, "state") ?? _state, ExtractField(raw, "app"));
            };
            _web.CoreWebView2.NavigationStarting += (_, e) =>
            {
                if (e.Uri.StartsWith("ms-word:", StringComparison.OrdinalIgnoreCase)
                    || e.Uri.StartsWith("ms-excel:", StringComparison.OrdinalIgnoreCase)
                    || e.Uri.StartsWith("ms-powerpoint:", StringComparison.OrdinalIgnoreCase)
                    || e.Uri.StartsWith("mosdock:", StringComparison.OrdinalIgnoreCase))
                {
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

                    ApplyDock(waitForWord: true);
                }
            };
            _web.CoreWebView2.Navigate(Portal);
        };

        FormClosed += (_, _) => _agent?.Dispose();

        _keepWord.Interval = 700;
        _keepWord.Tick += (_, _) => ApplyWordOnly();
        _keepWord.Start();
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
            if (!placeOnly)
            {
                _state = (string)btn.Tag!;
            }

            ApplyDock(waitForWord: true);
        };
        _bar.Controls.Add(btn);
    }

    void OnPlaceRequest(string state, string? app = null)
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
        var (dock, word) = LayoutMath.Compute(work, _state);
        Bounds = new Rectangle(dock.X, dock.Y, dock.W, dock.H);
        TopMost = true;
        _web.Visible = _state != "minimized";
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
        var (_, word) = LayoutMath.Compute(work, _state);
        WordWindow.Apply(word, _app);
        TopMost = true;
    }
}
