using System.Diagnostics;

namespace MosDock;

sealed class LoginForm : Form
{
    readonly TextBox _user = new();
    readonly TextBox _pass = new();
    readonly ComboBox _lang = new();
    readonly Label _error = new();
    readonly Button _submit;
    readonly Button _eye = new();
    readonly FlowLayoutPanel _stack = new();
    bool _showPass;

    public string DisplayName { get; private set; } = "";

    public LoginForm()
    {
        Text = "MOS-KulKul";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        Font = Ui.BodyFont;
        BackColor = Color.White;
        ClientSize = new Size(440, 680);

        _stack.Dock = DockStyle.Fill;
        _stack.FlowDirection = FlowDirection.TopDown;
        _stack.WrapContents = false;
        _stack.AutoScroll = true;
        _stack.BackColor = Color.White;
        _stack.Padding = new Padding(40, 36, 40, 20);

        _user.BorderStyle = BorderStyle.None;
        _user.Font = new Font("Segoe UI", 11f);
        _pass.BorderStyle = BorderStyle.None;
        _pass.Font = new Font("Segoe UI", 11f);
        _pass.UseSystemPasswordChar = true;

        _lang.DropDownStyle = ComboBoxStyle.DropDownList;
        _lang.FlatStyle = FlatStyle.Flat;
        _lang.Font = new Font("Segoe UI", 11f);
        _lang.Items.Add("Tiếng Việt");
        _lang.SelectedIndex = 0;

        _error.AutoSize = false;
        _error.ForeColor = Ui.Danger;
        _error.UseMnemonic = false;
        _error.Height = 8;
        _error.Margin = new Padding(0, 4, 0, 4);
        Ui.BindWrap(_error, 4);

        _submit = Ui.SignInBtn("Đăng nhập");
        _submit.Margin = new Padding(0, 16, 0, 8);
        _submit.Click += async (_, _) => await DoLogin();
        AcceptButton = _submit;
        Ui.RoundControl(_submit, 8);

        _stack.Controls.Add(Brand());
        _stack.Controls.Add(Labeled("Tài khoản", InputShell(_user)));
        _stack.Controls.Add(Labeled("Mật khẩu", PasswordShell()));
        _stack.Controls.Add(LangShell());
        _stack.Controls.Add(_error);
        _stack.Controls.Add(_submit);
        _stack.Controls.Add(Hint());
        _stack.Controls.Add(Footer());

        Controls.Add(_stack);
        _stack.Resize += (_, _) => FitWidths();
        Shown += (_, _) => FitWidths();
        TryLoadLastUser();
    }

    void FitWidths()
    {
        var w = Math.Max(240, _stack.ClientSize.Width - _stack.Padding.Horizontal);
        foreach (Control child in _stack.Controls)
        {
            child.Width = w;
        }

        _submit.Width = w;
        _submit.Height = 48;
    }

    static Panel Brand()
    {
        var wrap = new Panel { Height = 92, Margin = new Padding(0, 0, 0, 20) };
        var mark = new Label
        {
            Text = "MOS-KulKul",
            Font = new Font("Segoe UI", 22f, FontStyle.Bold),
            ForeColor = Ui.Nav,
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 40,
            TextAlign = ContentAlignment.MiddleCenter,
            UseMnemonic = false,
        };
        var tag = new Label
        {
            Text = "Hệ thống luyện thi MOS",
            Font = Ui.BodyFont,
            ForeColor = Ui.Muted,
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.TopCenter,
            UseMnemonic = false,
        };
        wrap.Controls.Add(tag);
        wrap.Controls.Add(mark);
        return wrap;
    }

    static Panel Labeled(string caption, Control field)
    {
        var wrap = new Panel { Height = 86, Margin = new Padding(0, 0, 0, 6) };
        var label = new Label
        {
            Text = caption,
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 24,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Ui.Text,
            UseMnemonic = false,
        };
        field.Dock = DockStyle.Top;
        field.Height = 44;
        wrap.Controls.Add(field);
        wrap.Controls.Add(label);
        return wrap;
    }

    Panel InputShell(TextBox box)
    {
        var shell = new Panel { Height = 44, BackColor = Color.White, Padding = new Padding(1) };
        var pad = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(12, 10, 12, 8) };
        box.Dock = DockStyle.Fill;
        box.BackColor = Color.White;
        pad.Controls.Add(box);
        shell.Controls.Add(pad);
        PaintBorder(shell, box);
        return shell;
    }

    Panel PasswordShell()
    {
        var shell = new Panel { Height = 44, BackColor = Color.White, Padding = new Padding(1) };
        _eye.Text = "Hiện";
        _eye.AutoSize = false;
        _eye.Dock = DockStyle.Right;
        _eye.Width = 52;
        _eye.FlatStyle = FlatStyle.Flat;
        _eye.BackColor = Color.White;
        _eye.ForeColor = Ui.Muted;
        _eye.Font = Ui.SmallFont;
        _eye.Cursor = Cursors.Hand;
        _eye.UseMnemonic = false;
        _eye.FlatAppearance.BorderSize = 0;
        _eye.Click += (_, _) =>
        {
            _showPass = !_showPass;
            _pass.UseSystemPasswordChar = !_showPass;
            _eye.Text = _showPass ? "Ẩn" : "Hiện";
        };
        var pad = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(12, 10, 4, 8) };
        _pass.Dock = DockStyle.Fill;
        _pass.BackColor = Color.White;
        pad.Controls.Add(_pass);
        shell.Controls.Add(pad);
        shell.Controls.Add(_eye);
        PaintBorder(shell, _pass);
        return shell;
    }

    Panel LangShell()
    {
        var wrap = new Panel { Height = 52, Margin = new Padding(0, 8, 0, 4) };
        var shell = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(1) };
        _lang.Dock = DockStyle.Fill;
        _lang.BackColor = Color.White;
        _lang.ForeColor = Ui.Text;
        shell.Controls.Add(_lang);
        PaintBorder(shell, _lang);
        wrap.Controls.Add(shell);
        return wrap;
    }

    static void PaintBorder(Panel shell, Control focus)
    {
        shell.Paint += (_, e) =>
        {
            var color = focus.Focused ? Ui.Primary : Ui.Line;
            using var pen = new Pen(color, 1.5f);
            var r = new Rectangle(0, 0, shell.Width - 1, shell.Height - 1);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var path = Ui.RoundedRect(r, 8);
            e.Graphics.DrawPath(pen, path);
        };
        focus.GotFocus += (_, _) => shell.Invalidate();
        focus.LostFocus += (_, _) => shell.Invalidate();
    }

    static Label Hint()
    {
        var hint = new Label
        {
            Text = "Tài khoản nhà trường. Bài MOS mở trên Office đã cài trên máy — không dùng Office Online.",
            Font = Ui.SmallFont,
            ForeColor = Ui.Muted,
            AutoSize = false,
            Height = 48,
            TextAlign = ContentAlignment.TopCenter,
            Margin = new Padding(0, 8, 0, 8),
            UseMnemonic = false,
        };
        Ui.BindWrap(hint, 8);
        return hint;
    }

    static Panel Footer()
    {
        var wrap = new Panel { Height = 36, Margin = new Padding(0, 12, 0, 0) };
        var help = Link("Trợ giúp", "https://mos.gds.edu.vn");
        var down = Link("Tải MOS-KulKul", "https://mos.gds.edu.vn/cai-dat");
        help.Dock = DockStyle.Left;
        down.Dock = DockStyle.Right;
        wrap.Controls.Add(help);
        wrap.Controls.Add(down);
        return wrap;
    }

    static LinkLabel Link(string text, string url)
    {
        var link = new LinkLabel
        {
            Text = text,
            AutoSize = true,
            LinkColor = Ui.Primary,
            ActiveLinkColor = Ui.PrimaryDark,
            VisitedLinkColor = Ui.Primary,
            UseMnemonic = false,
            Margin = new Padding(0, 8, 0, 0),
        };
        link.LinkClicked += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
                // ignore
            }
        };
        return link;
    }

    static string LastUserPath => Path.Combine(ExamSession.DataDir, "last-user.txt");

    void TryLoadLastUser()
    {
        try
        {
            if (File.Exists(LastUserPath))
            {
                _user.Text = File.ReadAllText(LastUserPath).Trim();
            }
        }
        catch
        {
            // ignore
        }
    }

    static void TrySaveLastUser(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(ExamSession.DataDir);
            File.WriteAllText(LastUserPath, username);
        }
        catch
        {
            // ignore
        }
    }

    async Task DoLogin()
    {
        _error.Text = "";
        _submit.Enabled = false;
        try
        {
            var (ok, err, name, _, _) = await Portal.LoginAsync(_user.Text.Trim(), _pass.Text, "word");
            if (!ok)
            {
                _error.Text = err ?? "Tên đăng nhập hoặc mật khẩu không đúng.";
                return;
            }

            DisplayName = name;
            TrySaveLastUser(_user.Text.Trim());
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _error.Text = ex.Message;
        }
        finally
        {
            _submit.Enabled = true;
        }
    }
}
