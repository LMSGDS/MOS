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
        ClientSize = new Size(440, 390);
        BackColor = Color.White;
        Font = Ui.BodyFont;

        Controls.Add(new Label
        {
            Text = "MOS-KulKul",
            Font = Ui.TitleFont,
            AutoSize = true,
            Location = new Point(36, 28),
            ForeColor = Ui.Navy,
        });
        Controls.Add(new Label
        {
            Text = "Đăng nhập bằng tài khoản nhà trường.\nBài MOS mở trên Word, Excel hoặc PowerPoint đã cài trên máy.",
            AutoSize = false,
            Size = new Size(368, 48),
            Location = new Point(36, 72),
            ForeColor = Ui.Muted,
        });

        AddField("Tài khoản", _user, 132);
        _pass.UseSystemPasswordChar = true;
        AddField("Mật khẩu", _pass, 204);

        _error.AutoSize = false;
        _error.Size = new Size(368, 24);
        _error.Location = new Point(36, 276);
        _error.ForeColor = Color.FromArgb(153, 27, 27);
        Controls.Add(_error);

        _submit.Text = "Đăng nhập";
        _submit.Size = new Size(368, 44);
        _submit.Location = new Point(36, 304);
        _submit.FlatStyle = FlatStyle.Flat;
        _submit.BackColor = Ui.Blue;
        _submit.ForeColor = Color.White;
        _submit.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
        _submit.FlatAppearance.BorderSize = 0;
        _submit.Click += async (_, _) => await DoLogin();
        AcceptButton = _submit;
        Controls.Add(_submit);

        var hint = new LinkLabel
        {
            Text = "Cài MOS-KulKul trên máy",
            AutoSize = true,
            Location = new Point(36, 356),
            LinkColor = Ui.Blue,
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
        Controls.Add(hint);
        TryLoadLastUser();
    }

    void AddField(string caption, TextBox box, int y)
    {
        Controls.Add(new Label
        {
            Text = caption,
            AutoSize = true,
            Location = new Point(36, y),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Ui.Text,
        });
        box.Location = new Point(36, y + 22);
        box.Size = new Size(368, 28);
        box.BorderStyle = BorderStyle.FixedSingle;
        Controls.Add(box);
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
