using System.Diagnostics;

namespace MosDock;

sealed class LoginForm : Form
{
    readonly TextBox _user = new();
    readonly TextBox _pass = new();
    readonly Ui.SoftField _userField = new();
    readonly Ui.SoftField _passField = new();
    readonly Label _userHint = new();
    readonly Label _passHint = new();
    readonly Ui.AlertBar _alert = new();
    readonly Label _title = new();
    readonly Label _tag = new();
    readonly Label _hint = new();
    readonly Panel _brand = new();
    readonly Panel _logoHost = new();
    readonly Panel _account;
    readonly Panel _password;
    readonly Button _submit;
    readonly Button _eye = new();
    readonly Button _lang = new();
    readonly ContextMenuStrip _langMenu = new();
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
        ClientSize = new Size(460, 700);
        DoubleBuffered = true;
        Ui.ApplyWindowIcon(this);

        _stack.Dock = DockStyle.Fill;
        _stack.FlowDirection = FlowDirection.TopDown;
        _stack.WrapContents = false;
        _stack.AutoScroll = true;
        _stack.BackColor = Color.White;
        _stack.Padding = new Padding(36, 8, 36, 16);

        _user.BorderStyle = BorderStyle.None;
        _user.Font = new Font("Segoe UI", 11f);
        _user.TextChanged += (_, _) => ClearFieldError(_userField, _userHint);
        _pass.BorderStyle = BorderStyle.None;
        _pass.Font = new Font("Segoe UI", 11f);
        _pass.UseSystemPasswordChar = true;
        _pass.TextChanged += (_, _) => ClearFieldError(_passField, _passHint);

        _submit = Ui.SignInBtn("Đăng nhập");
        _submit.Margin = new Padding(0, 16, 0, 8);
        _submit.Click += async (_, _) => await DoLogin();
        AcceptButton = _submit;
        Ui.RoundControl(_submit, 8);

        _account = Labeled("Tài khoản", UserShell(), _userHint);
        _password = Labeled("Mật khẩu", PasswordShell(), _passHint);
        _alert.Margin = new Padding(0, 0, 0, 12);

        _stack.Controls.Add(BuildBrand());
        _stack.Controls.Add(_alert);
        _stack.Controls.Add(_account);
        _stack.Controls.Add(_password);
        _stack.Controls.Add(_submit);
        _stack.Controls.Add(BuildHint());
        _stack.Controls.Add(Footer());

        Controls.Add(_stack);
        Controls.Add(LangChrome());
        _stack.Resize += (_, _) => FitLayout();
        Shown += (_, _) => FitLayout();
        TryLoadLastUser();
    }

    Panel LangChrome()
    {
        var top = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            BackColor = Color.White,
        };
        _lang.Size = new Size(36, 32);
        _lang.Dock = DockStyle.Right;
        _lang.FlatStyle = FlatStyle.Flat;
        _lang.BackColor = Color.White;
        _lang.Cursor = Cursors.Hand;
        _lang.AccessibleName = "Ngôn ngữ";
        _lang.FlatAppearance.BorderSize = 0;
        _lang.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 247, 249);
        _lang.Paint += (_, e) => Ui.PaintGlobe(e.Graphics, _lang.ClientRectangle, Ui.Muted);
        _langMenu.Items.Add(new ToolStripMenuItem("Tiếng Việt") { Checked = true });
        _lang.Click += (_, _) => _langMenu.Show(_lang, new Point(0, _lang.Height));
        Ui.DockTips.SetToolTip(_lang, "Tiếng Việt");
        top.Controls.Add(_lang);
        return top;
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
        _alert.FitWidth(inner);

        FitLabeled(_account, inner, _userHint);
        FitLabeled(_password, inner, _passHint);

        _submit.Width = inner;
        _submit.Height = 48;
        _hint.Height = Math.Max(22, Ui.MeasureH(_hint.Text, _hint.Font, inner) + 8);
    }

    static void FitLabeled(Panel wrap, int inner, Label hint)
    {
        Label? caption = null;
        foreach (Control child in wrap.Controls)
        {
            if (child is Label label && !ReferenceEquals(label, hint))
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

        var hintH = hint.Visible && !string.IsNullOrWhiteSpace(hint.Text)
            ? Math.Max(18, Ui.MeasureH(hint.Text, hint.Font, inner) + 4)
            : 0;
        hint.Height = hintH;
        wrap.Height = captionH + 50 + hintH + 8;
    }

    Panel BuildBrand()
    {
        _brand.Margin = new Padding(0, 0, 0, 12);
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

    static Panel Labeled(string caption, Control field, Label hint)
    {
        var wrap = new Panel { Margin = new Padding(0, 4, 0, 6) };
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
        hint.AutoSize = false;
        hint.Dock = DockStyle.Top;
        hint.Font = Ui.SmallFont;
        hint.ForeColor = Ui.Danger;
        hint.UseMnemonic = false;
        hint.Visible = false;
        hint.Height = 0;
        hint.Padding = new Padding(2, 2, 0, 0);
        field.Dock = DockStyle.Top;
        field.Height = 50;
        wrap.Controls.Add(hint);
        wrap.Controls.Add(field);
        wrap.Controls.Add(label);
        return wrap;
    }

    Ui.SoftField UserShell()
    {
        var pad = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(12, 11, 12, 10) };
        _user.Dock = DockStyle.Fill;
        _user.BackColor = Color.White;
        pad.Controls.Add(_user);
        _userField.Controls.Add(pad);
        _userField.Bind(_user);
        return _userField;
    }

    Ui.SoftField PasswordShell()
    {
        _eye.Text = "";
        _eye.AutoSize = false;
        _eye.Dock = DockStyle.Right;
        _eye.Width = 36;
        _eye.FlatStyle = FlatStyle.Flat;
        _eye.BackColor = Color.White;
        _eye.Cursor = Cursors.Hand;
        _eye.UseMnemonic = false;
        _eye.AccessibleName = "Hiện mật khẩu";
        _eye.FlatAppearance.BorderSize = 0;
        _eye.Paint += (_, e) => Ui.PaintEye(e.Graphics, _eye.ClientRectangle, Ui.Muted, _showPass);
        _eye.Click += (_, _) =>
        {
            _showPass = !_showPass;
            _pass.UseSystemPasswordChar = !_showPass;
            _eye.AccessibleName = _showPass ? "Ẩn mật khẩu" : "Hiện mật khẩu";
            _eye.Invalidate();
        };
        Ui.DockTips.SetToolTip(_eye, "Hiện hoặc ẩn mật khẩu");
        var pad = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(12, 11, 4, 10) };
        _pass.Dock = DockStyle.Fill;
        _pass.BackColor = Color.White;
        pad.Controls.Add(_pass);
        _passField.Controls.Add(pad);
        _passField.Controls.Add(_eye);
        _passField.Bind(_pass);
        return _passField;
    }

    Label BuildHint()
    {
        _hint.Text = "Bài MOS mở trên Office đã cài trên máy — không dùng Office Online.";
        _hint.Font = Ui.SmallFont;
        _hint.ForeColor = Ui.Muted;
        _hint.AutoSize = false;
        _hint.TextAlign = ContentAlignment.TopCenter;
        _hint.Margin = new Padding(4, 10, 4, 6);
        _hint.UseMnemonic = false;
        return _hint;
    }

    static Panel Footer()
    {
        var wrap = new Panel { Height = 40, Margin = new Padding(0, 8, 0, 4) };
        var help = Link("Trợ giúp", "https://mos.gds.edu.vn", primary: true);
        var down = Link("Tải MOS-KulKul", "https://mos.gds.edu.vn/cai-dat", primary: false);
        help.Dock = DockStyle.Left;
        down.Dock = DockStyle.Right;
        wrap.Controls.Add(down);
        wrap.Controls.Add(help);
        return wrap;
    }

    static LinkLabel Link(string text, string url, bool primary)
    {
        var link = new LinkLabel
        {
            Text = text,
            AutoSize = true,
            Font = primary ? new Font("Segoe UI", 10f, FontStyle.Bold) : Ui.SmallFont,
            LinkColor = primary ? Ui.Primary : Ui.Muted,
            ActiveLinkColor = Ui.PrimaryDark,
            VisitedLinkColor = primary ? Ui.Primary : Ui.Muted,
            LinkBehavior = primary ? LinkBehavior.AlwaysUnderline : LinkBehavior.NeverUnderline,
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

    static void ClearFieldError(Ui.SoftField field, Label hint)
    {
        field.SetError(false);
        hint.Text = "";
        hint.Visible = false;
        hint.Height = 0;
    }

    void SetFieldError(Ui.SoftField field, Label hint, string message)
    {
        field.SetError(true);
        hint.Text = message;
        hint.Visible = true;
    }

    async Task DoLogin()
    {
        _alert.Clear();
        ClearFieldError(_userField, _userHint);
        ClearFieldError(_passField, _passHint);

        var emptyUser = string.IsNullOrWhiteSpace(_user.Text);
        var emptyPass = string.IsNullOrWhiteSpace(_pass.Text);
        if (emptyUser)
        {
            SetFieldError(_userField, _userHint, "Vui lòng nhập tài khoản để tiếp tục");
        }

        if (emptyPass)
        {
            SetFieldError(_passField, _passHint, "Vui lòng nhập mật khẩu để tiếp tục");
        }

        if (emptyUser || emptyPass)
        {
            FitLayout();
            return;
        }

        _submit.Enabled = false;
        try
        {
            var (ok, kind, err, name, _, _) = await Portal.LoginAsync(_user.Text.Trim(), _pass.Text, "word");
            if (!ok)
            {
                var tone = kind == "status" ? "warn" : "danger";
                _alert.ShowMessage(err ?? Portal.AuthWrong, tone);
                FitLayout();
                return;
            }

            DisplayName = name;
            TrySaveLastUser(_user.Text.Trim());
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception)
        {
            _alert.ShowMessage(Portal.StatusBusy, "warn");
            FitLayout();
        }
        finally
        {
            _submit.Enabled = true;
        }
    }
}
