using System.Diagnostics;

namespace MosDock;

sealed class LoginForm : Form
{
    readonly TextBox _user = new();
    readonly TextBox _pass = new();
    readonly Label _error = new();
    readonly Label _chosen = new();
    readonly Button _submit = new();
    readonly List<Button> _tiles = [];
    string _app;

    public string SelectedApp => _app;
    public string Mode { get; private set; } = "training";
    public string DisplayName { get; private set; } = "";

    public LoginForm(string? initialApp)
    {
        _app = OfficeApp.Resolve(initialApp).Id;
        Text = "MOS-KulKul";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(640, 580);
        BackColor = Color.FromArgb(244, 248, 252);
        Font = new Font("Segoe UI", 11f);

        var title = new Label
        {
            Text = "MOS-KulKul",
            Font = new Font("Segoe UI", 22f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(28, 20),
        };
        var lead = new Label
        {
            Text = "Chọn Microsoft Word, Excel hoặc PowerPoint rồi đăng nhập trên máy — không dùng Office Online.",
            AutoSize = false,
            Size = new Size(580, 48),
            Location = new Point(28, 62),
            ForeColor = Color.FromArgb(71, 85, 105),
        };
        Controls.Add(title);
        Controls.Add(lead);

        AddTile("Word", "word", Color.FromArgb(43, 87, 154), 28);
        AddTile("Excel", "excel", Color.FromArgb(33, 115, 70), 226);
        AddTile("PowerPoint", "powerpoint", Color.FromArgb(210, 71, 38), 424);
        RefreshTiles();

        _chosen.AutoSize = true;
        _chosen.Location = new Point(28, 168);
        _chosen.ForeColor = Color.FromArgb(15, 23, 42);
        Controls.Add(_chosen);

        AddField("Tài khoản", _user, 198);
        _pass.UseSystemPasswordChar = true;
        AddField("Mật khẩu", _pass, 268);

        var modeBox = new GroupBox
        {
            Text = "Chế độ",
            Location = new Point(28, 338),
            Size = new Size(580, 52),
            ForeColor = Color.FromArgb(15, 23, 42),
        };
        var train = new RadioButton
        {
            Text = "Luyện tập (Training)",
            Location = new Point(16, 20),
            AutoSize = true,
            Checked = true,
        };
        var test = new RadioButton
        {
            Text = "Thi (Testing)",
            Location = new Point(280, 20),
            AutoSize = true,
        };
        train.CheckedChanged += (_, _) => { if (train.Checked) Mode = "training"; };
        test.CheckedChanged += (_, _) => { if (test.Checked) Mode = "testing"; };
        modeBox.Controls.Add(train);
        modeBox.Controls.Add(test);
        Controls.Add(modeBox);

        _error.AutoSize = false;
        _error.Size = new Size(580, 28);
        _error.Location = new Point(28, 398);
        _error.ForeColor = Color.FromArgb(153, 27, 27);
        Controls.Add(_error);

        _submit.Text = "Đăng nhập";
        _submit.Size = new Size(580, 44);
        _submit.Location = new Point(28, 430);
        _submit.FlatStyle = FlatStyle.Flat;
        _submit.BackColor = Color.FromArgb(0, 142, 226);
        _submit.ForeColor = Color.White;
        _submit.FlatAppearance.BorderSize = 0;
        _submit.Click += async (_, _) => await DoLogin();
        AcceptButton = _submit;
        Controls.Add(_submit);

        var hint = new LinkLabel
        {
            Text = "Cần cài đặt MOS-KulKul? https://mos.gds.edu.vn/cai-dat",
            AutoSize = true,
            Location = new Point(28, 486),
            LinkColor = Color.FromArgb(0, 107, 176),
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
        UpdateChosen();
    }

    void AddTile(string label, string id, Color color, int x)
    {
        var btn = new Button
        {
            Text = "  " + label,
            Tag = id,
            Location = new Point(x, 114),
            Size = new Size(188, 44),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = color,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        btn.FlatAppearance.BorderColor = color;
        btn.Click += (_, _) =>
        {
            _app = id;
            RefreshTiles();
            UpdateChosen();
        };
        _tiles.Add(btn);
        Controls.Add(btn);
    }

    void RefreshTiles()
    {
        foreach (var btn in _tiles)
        {
            var id = (string)btn.Tag!;
            var on = id == _app;
            btn.FlatAppearance.BorderSize = on ? 3 : 1;
            btn.BackColor = on ? Color.FromArgb(232, 245, 253) : Color.White;
        }
    }

    void UpdateChosen()
    {
        var name = _app switch
        {
            "excel" => "Microsoft Excel",
            "powerpoint" => "Microsoft PowerPoint",
            _ => "Microsoft Word",
        };
        _chosen.Text = "Chương trình đã chọn: " + name;
    }

    void AddField(string caption, TextBox box, int y)
    {
        var label = new Label
        {
            Text = caption,
            AutoSize = true,
            Location = new Point(28, y),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
        };
        box.Location = new Point(28, y + 22);
        box.Size = new Size(580, 32);
        Controls.Add(label);
        Controls.Add(box);
    }

    async Task DoLogin()
    {
        _error.Text = "";
        _submit.Enabled = false;
        try
        {
            var (ok, err, name, app, _) = await Portal.LoginAsync(_user.Text.Trim(), _pass.Text, _app);
            if (!ok)
            {
                _error.Text = err ?? "Không đăng nhập được.";
                return;
            }

            _app = app;
            DisplayName = name;
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
