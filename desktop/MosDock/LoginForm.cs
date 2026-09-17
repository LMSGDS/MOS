using System.Diagnostics;

namespace MosDock;

sealed class LoginForm : Form
{
    readonly TextBox _user = new();
    readonly TextBox _pass = new();
    readonly Label _error = new();
    readonly Button _submit = new();

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
        BackColor = Ui.Page;
        ClientSize = new Size(480, 520);

        var nav = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = Ui.Nav,
        };
        nav.Controls.Add(new Label
        {
            Text = "MOS-KulKul",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(24, 15),
            UseMnemonic = false,
        });

        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            BackColor = Color.White,
            Padding = new Padding(32, 28, 32, 24),
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 6; i++)
        {
            card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _user.BorderStyle = BorderStyle.FixedSingle;
        _pass.UseSystemPasswordChar = true;
        _pass.BorderStyle = BorderStyle.FixedSingle;
        _user.Dock = DockStyle.Top;
        _pass.Dock = DockStyle.Top;
        _user.Height = 34;
        _pass.Height = 34;

        _error.AutoSize = true;
        _error.MaximumSize = new Size(400, 0);
        _error.ForeColor = Ui.Danger;
        _error.Margin = new Padding(0, 8, 0, 8);
        _error.UseMnemonic = false;

        _submit = Ui.PrimaryBtn("Đăng nhập", 160);
        _submit.Dock = DockStyle.Top;
        _submit.Height = 42;
        _submit.AutoSize = false;
        _submit.Margin = new Padding(0, 8, 0, 16);
        _submit.Click += async (_, _) => await DoLogin();
        AcceptButton = _submit;

        var hint = new LinkLabel
        {
            Text = "Cài MOS-KulKul trên máy",
            AutoSize = true,
            LinkColor = Ui.Primary,
            Margin = new Padding(0, 4, 0, 0),
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

        card.Controls.Add(Ui.Title("Đăng nhập"), 0, 0);
        card.Controls.Add(Ui.Subtitle("Dùng tài khoản nhà trường. Bài MOS mở trên Office đã cài trên máy.", 400), 0, 1);
        card.Controls.Add(Field("Tài khoản", _user), 0, 2);
        card.Controls.Add(Field("Mật khẩu", _pass), 0, 3);
        card.Controls.Add(_error, 0, 4);
        card.Controls.Add(_submit, 0, 5);
        card.Controls.Add(hint, 0, 6);

        Controls.Add(card);
        Controls.Add(nav);
        TryLoadLastUser();
    }

    static Panel Field(string caption, TextBox box)
    {
        var wrap = new Panel
        {
            Height = 70,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 8),
        };
        var label = new Label
        {
            Text = caption,
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Ui.Text,
            Dock = DockStyle.Top,
            Height = 22,
            UseMnemonic = false,
        };
        box.Dock = DockStyle.Top;
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
