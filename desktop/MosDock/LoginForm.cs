using System.Diagnostics;

namespace MosDock;

sealed class LoginForm : Form
{
    readonly TextBox _user = new();
    readonly TextBox _pass = new();
    readonly Label _error = new();
    readonly Button _submit;
    readonly Panel _card = new();

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
        BackColor = Ui.PageBg;
        ClientSize = new Size(520, 600);

        var nav = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = Ui.Nav,
            Padding = new Padding(24, 0, 24, 0),
        };
        nav.Controls.Add(new Label
        {
            Text = "MOS-KulKul",
            ForeColor = Color.White,
            Font = Ui.NavFont,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false,
        });

        var host = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Ui.PageBg,
        };

        _card.BackColor = Ui.Line;
        _card.Padding = new Padding(1);
        _card.Size = new Size(420, 460);

        var inner = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Ui.Card,
            Padding = new Padding(28, 24, 28, 20),
        };

        _user.BorderStyle = BorderStyle.FixedSingle;
        _pass.UseSystemPasswordChar = true;
        _pass.BorderStyle = BorderStyle.FixedSingle;

        _error.AutoSize = false;
        _error.ForeColor = Ui.Danger;
        _error.UseMnemonic = false;
        _error.Dock = DockStyle.Top;
        _error.Height = 8;
        Ui.BindWrap(_error, 4);

        _submit = Ui.PrimaryBtn("Đăng nhập", 200);
        _submit.Dock = DockStyle.Top;
        _submit.Height = 42;
        _submit.Margin = new Padding(0, 8, 0, 0);
        _submit.Click += async (_, _) => await DoLogin();
        AcceptButton = _submit;

        var hint = new LinkLabel
        {
            Text = "Cài MOS-KulKul trên máy",
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 28,
            LinkColor = Ui.Primary,
            ActiveLinkColor = Ui.PrimaryDark,
            UseMnemonic = false,
        };
        hint.LinkClicked += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://mos.gds.edu.vn/cai-dat") { UseShellExecute = true });
            }
            catch
            {
                // ignore
            }
        };

        var userField = Field("Tài khoản", _user);
        var passField = Field("Mật khẩu", _pass);
        userField.Dock = DockStyle.Top;
        passField.Dock = DockStyle.Top;

        inner.Controls.Add(hint);
        inner.Controls.Add(_submit);
        inner.Controls.Add(_error);
        inner.Controls.Add(passField);
        inner.Controls.Add(userField);
        inner.Controls.Add(Ui.Wrap(
            "Dùng tài khoản nhà trường. Bài MOS mở trên Microsoft Office đã cài trên máy — không dùng Office Online.",
            Ui.BodyFont,
            Ui.Muted,
            14));
        inner.Controls.Add(Ui.Wrap("Đăng nhập", Ui.TitleFont, Ui.Text, 8));

        _card.Controls.Add(inner);
        host.Controls.Add(_card);
        host.Resize += (_, _) => PlaceCard(host);
        PlaceCard(host);

        Controls.Add(host);
        Controls.Add(nav);
        TryLoadLastUser();
    }

    void PlaceCard(Panel host)
    {
        var w = Math.Min(420, Math.Max(320, host.ClientSize.Width - 40));
        _card.Width = w;
        _card.Height = Math.Min(500, Math.Max(420, host.ClientSize.Height - 40));
        _card.Left = Math.Max(12, (host.ClientSize.Width - _card.Width) / 2);
        _card.Top = Math.Max(16, (host.ClientSize.Height - _card.Height) / 3);
        var boxW = Math.Max(200, _card.ClientSize.Width - 58);
        _user.Width = boxW;
        _pass.Width = boxW;
        _submit.Width = boxW;
    }

    static Panel Field(string caption, TextBox box)
    {
        var wrap = new Panel
        {
            Height = 72,
            Margin = new Padding(0, 0, 0, 4),
        };
        var label = new Label
        {
            Text = caption,
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 22,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Ui.Text,
            UseMnemonic = false,
        };
        box.Dock = DockStyle.Top;
        box.Height = 36;
        wrap.Controls.Add(box);
        wrap.Controls.Add(label);
        return wrap;
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
