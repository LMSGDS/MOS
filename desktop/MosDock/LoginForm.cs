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
        BackColor = Color.White;
        Padding = new Padding(32, 28, 32, 24);
        ClientSize = new Size(460, 460);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 8,
            BackColor = Color.White,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = "MOS-KulKul",
            Font = Ui.TitleFont,
            ForeColor = Ui.Navy,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8),
        };
        var lead = new Label
        {
            Text = "Đăng nhập tài khoản nhà trường",
            Font = Ui.BodyFont,
            ForeColor = Ui.Muted,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 22),
        };

        _user.BorderStyle = BorderStyle.FixedSingle;
        _pass.UseSystemPasswordChar = true;
        _pass.BorderStyle = BorderStyle.FixedSingle;
        _user.Dock = DockStyle.Top;
        _pass.Dock = DockStyle.Top;
        _user.Height = 32;
        _pass.Height = 32;

        _error.AutoSize = false;
        _error.Height = 24;
        _error.Dock = DockStyle.Fill;
        _error.ForeColor = Color.FromArgb(153, 27, 27);
        _error.TextAlign = ContentAlignment.MiddleLeft;
        _error.Margin = new Padding(0, 8, 0, 8);

        _submit.Text = "Đăng nhập";
        _submit.Height = 44;
        _submit.Dock = DockStyle.Top;
        _submit.FlatStyle = FlatStyle.Flat;
        _submit.BackColor = Ui.Blue;
        _submit.ForeColor = Color.White;
        _submit.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
        _submit.FlatAppearance.BorderSize = 0;
        _submit.Margin = new Padding(0, 4, 0, 12);
        _submit.Click += async (_, _) => await DoLogin();
        AcceptButton = _submit;

        var hint = new LinkLabel
        {
            Text = "Cài MOS-KulKul trên máy",
            AutoSize = true,
            LinkColor = Ui.Blue,
            Margin = new Padding(0, 4, 0, 0),
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

        root.Controls.Add(title, 0, 0);
        root.Controls.Add(lead, 0, 1);
        root.Controls.Add(Field("Tài khoản", _user), 0, 2);
        root.Controls.Add(Field("Mật khẩu", _pass), 0, 3);
        root.Controls.Add(_error, 0, 4);
        root.Controls.Add(_submit, 0, 6);
        root.Controls.Add(hint, 0, 7);
        Controls.Add(root);
        TryLoadLastUser();
    }

    static Panel Field(string caption, TextBox box)
    {
        var wrap = new Panel
        {
            Height = 64,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 12),
        };
        var label = new Label
        {
            Text = caption,
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Ui.Text,
            Location = new Point(0, 0),
        };
        box.Location = new Point(0, 24);
        box.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        wrap.Controls.Add(label);
        wrap.Controls.Add(box);
        wrap.Resize += (_, _) => box.Width = Math.Max(120, wrap.ClientSize.Width);
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
