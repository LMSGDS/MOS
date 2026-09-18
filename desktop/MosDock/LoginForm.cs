using System.Diagnostics;

namespace MosDock;

sealed class LoginForm : Form
{
    readonly TextBox _user = new();
    readonly TextBox _pass = new();
    readonly ComboBox _lang = new();
    readonly Label _error = new();
    readonly Label _title = new();
    readonly Label _tag = new();
    readonly Label _hint = new();
    readonly Panel _brand = new();
    readonly Panel _logoHost = new();
    readonly Panel _account;
    readonly Panel _password;
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
        ClientSize = new Size(460, 780);
        Ui.ApplyWindowIcon(this);

        _stack.Dock = DockStyle.Fill;
        _stack.FlowDirection = FlowDirection.TopDown;
        _stack.WrapContents = false;
        _stack.AutoScroll = true;
        _stack.BackColor = Color.White;
        _stack.Padding = new Padding(36, 28, 36, 16);

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
        _error.Margin = new Padding(0, 6, 0, 6);
        _error.TextAlign = ContentAlignment.TopCenter;

        _submit = Ui.SignInBtn("Đăng nhập");
        _submit.Margin = new Padding(0, 18, 0, 10);
        _submit.Click += async (_, _) => await DoLogin();
        AcceptButton = _submit;
        Ui.RoundControl(_submit, 8);

        _account = Labeled("Tài khoản", InputShell(_user));
        _password = Labeled("Mật khẩu", PasswordShell());

        _stack.Controls.Add(BuildBrand());
        _stack.Controls.Add(_account);
        _stack.Controls.Add(_password);
        _stack.Controls.Add(LangShell());
        _stack.Controls.Add(_error);
        _stack.Controls.Add(_submit);
        _stack.Controls.Add(BuildHint());
        _stack.Controls.Add(Footer());

        Controls.Add(_stack);
        _stack.Resize += (_, _) => FitLayout();
        Shown += (_, _) => FitLayout();
        TryLoadLastUser();
    }

    void FitLayout()
    {
        var inner = Math.Max(240, _stack.ClientSize.Width - _stack.Padding.Horizontal);
        foreach (Control child in _stack.Controls)
        {
            child.Width = inner;
        }

        _logoHost.Height = 88;
        _title.Height = Math.Max(40, Ui.MeasureH(_title.Text, _title.Font, inner) + 12);
        _tag.Height = Math.Max(44, Ui.MeasureH(_tag.Text, _tag.Font, inner) + 14);
        _brand.Height = _logoHost.Height + _title.Height + _tag.Height + 4;

        FitLabeled(_account, inner);
        FitLabeled(_password, inner);

        _eye.Width = Math.Max(76, Ui.MeasureW("Hiện", _eye.Font) + 24);
        _error.Height = string.IsNullOrWhiteSpace(_error.Text)
            ? 8
            : Math.Max(24, Ui.MeasureH(_error.Text, _error.Font, inner) + 8);
        _submit.Width = inner;
        _submit.Height = 48;
        _hint.Height = Math.Max(64, Ui.MeasureH(_hint.Text, _hint.Font, inner) + 18);
    }

    static void FitLabeled(Panel wrap, int inner)
    {
        Label? caption = null;
        foreach (Control child in wrap.Controls)
        {
            if (child is Label label)
            {
                caption = label;
                break;
            }
        }

        var captionH = 28;
        if (caption != null)
        {
            captionH = Math.Max(28, Ui.MeasureH(caption.Text, caption.Font, inner) + 10);
            caption.Height = captionH;
        }

        wrap.Height = captionH + 48 + 10;
    }

    Panel BuildBrand()
    {
        _brand.Margin = new Padding(0, 0, 0, 18);
        _logoHost.Dock = DockStyle.Top;
        _logoHost.Height = 88;
        var logo = new PictureBox
        {
            Size = new Size(72, 72),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = Ui.BrandMark(72),
        };
        _logoHost.Resize += (_, _) =>
        {
            logo.Left = Math.Max(0, (_logoHost.Width - logo.Width) / 2);
            logo.Top = 8;
        };
        _logoHost.Controls.Add(logo);

        _title.Text = "MOS-KulKul";
        _title.Font = new Font("Segoe UI", 20f, FontStyle.Bold);
        _title.ForeColor = Ui.Nav;
        _title.AutoSize = false;
        _title.Dock = DockStyle.Top;
        _title.TextAlign = ContentAlignment.MiddleCenter;
        _title.UseMnemonic = false;
        _title.Padding = new Padding(0, 4, 0, 4);

        _tag.Text = "Hệ thống luyện thi MOS" + Environment.NewLine + "Word · Excel · PowerPoint";
        _tag.Font = Ui.BodyFont;
        _tag.ForeColor = Ui.Muted;
        _tag.AutoSize = false;
        _tag.Dock = DockStyle.Top;
        _tag.TextAlign = ContentAlignment.TopCenter;
        _tag.UseMnemonic = false;
        _tag.Padding = new Padding(8, 4, 8, 6);

        _brand.Controls.Add(_tag);
        _brand.Controls.Add(_title);
        _brand.Controls.Add(_logoHost);
        return _brand;
    }

    static Panel Labeled(string caption, Control field)
    {
        var wrap = new Panel { Margin = new Padding(0, 4, 0, 10) };
        var label = new Label
        {
            Text = caption,
            AutoSize = false,
            Dock = DockStyle.Top,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Ui.Text,
            UseMnemonic = false,
            Padding = new Padding(0, 2, 0, 4),
            TextAlign = ContentAlignment.BottomLeft,
        };
        field.Dock = DockStyle.Top;
        field.Height = 48;
        wrap.Controls.Add(field);
        wrap.Controls.Add(label);
        return wrap;
    }

    Panel InputShell(TextBox box)
    {
        var shell = new Panel { Height = 48, BackColor = Color.White, Padding = new Padding(1) };
        var pad = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(12, 12, 12, 10) };
        box.Dock = DockStyle.Fill;
        box.BackColor = Color.White;
        pad.Controls.Add(box);
        shell.Controls.Add(pad);
        PaintBorder(shell, box);
        return shell;
    }

    Panel PasswordShell()
    {
        var shell = new Panel { Height = 48, BackColor = Color.White, Padding = new Padding(1) };
        _eye.Text = "Hiện";
        _eye.AutoSize = false;
        _eye.Dock = DockStyle.Right;
        _eye.Width = 76;
        _eye.FlatStyle = FlatStyle.Flat;
        _eye.BackColor = Color.White;
        _eye.ForeColor = Ui.Muted;
        _eye.Font = Ui.SmallFont;
        _eye.Cursor = Cursors.Hand;
        _eye.UseMnemonic = false;
        _eye.TextAlign = ContentAlignment.MiddleCenter;
        _eye.FlatAppearance.BorderSize = 0;
        _eye.Click += (_, _) =>
        {
            _showPass = !_showPass;
            _pass.UseSystemPasswordChar = !_showPass;
            _eye.Text = _showPass ? "Ẩn" : "Hiện";
        };
        var pad = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(12, 12, 8, 10) };
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
        var wrap = new Panel { Height = 56, Margin = new Padding(0, 8, 0, 6) };
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

    Label BuildHint()
    {
        _hint.Text = "Tài khoản nhà trường." + Environment.NewLine
            + "Bài MOS mở trên Office đã cài trên máy." + Environment.NewLine
            + "Không dùng Office Online.";
        _hint.Font = Ui.SmallFont;
        _hint.ForeColor = Ui.Muted;
        _hint.AutoSize = false;
        _hint.TextAlign = ContentAlignment.TopCenter;
        _hint.Margin = new Padding(4, 12, 4, 8);
        _hint.Padding = new Padding(4, 4, 4, 4);
        _hint.UseMnemonic = false;
        return _hint;
    }

    static Panel Footer()
    {
        var wrap = new Panel { Height = 40, Margin = new Padding(0, 8, 0, 4) };
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
            Padding = new Padding(0, 6, 0, 0),
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
                FitLayout();
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
            FitLayout();
        }
        finally
        {
            _submit.Enabled = true;
        }
    }
}
