using Microsoft.Web.WebView2.WinForms;

namespace MosDock;

sealed class MainForm : Form
{
    readonly Panel _bar = new();
    readonly WebView2 _web = new();
    readonly System.Windows.Forms.Timer _keepWord = new();
    string _state = "bottom";
    const string Portal = "https://mos.gds.edu.vn/dang-nhap?che-do=dock";

    public MainForm()
    {
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

        _web.Dock = DockStyle.Fill;

        Controls.Add(_web);
        Controls.Add(_bar);

        Load += async (_, _) =>
        {
            ApplyDock();
            await _web.EnsureCoreWebView2Async();
            _web.CoreWebView2.Navigate(Portal);
        };

        _keepWord.Interval = 700;
        _keepWord.Tick += (_, _) => ApplyWordOnly();
        _keepWord.Start();
    }

    void AddButton(string text, string state, int x)
    {
        var btn = new Button
        {
            Text = text,
            Tag = state,
            Location = new Point(x, 6),
            Size = new Size(100, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 142, 226),
            ForeColor = Color.White,
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (_, _) =>
        {
            _state = (string)btn.Tag!;
            ApplyDock();
        };
        _bar.Controls.Add(btn);
    }

    void ApplyDock()
    {
        var wa = Screen.FromHandle(Handle).WorkingArea;
        var work = new Rect(wa.X, wa.Y, wa.Width, wa.Height);
        var (dock, word) = LayoutMath.Compute(work, _state);
        Bounds = new Rectangle(dock.X, dock.Y, dock.W, dock.H);
        TopMost = true;
        WordWindow.Apply(word);
        _web.Visible = _state != "minimized";
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
        WordWindow.Apply(word);
        TopMost = true;
    }
}
