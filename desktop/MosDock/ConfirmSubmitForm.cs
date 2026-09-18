namespace MosDock;

/// <summary>
/// Failsafe before Nộp bài: complete vs leftover tasks, never a red panic button.
/// </summary>
sealed class ConfirmSubmitForm : Form
{
    public ConfirmSubmitForm(IReadOnlyList<int> leftover)
    {
        var open = leftover.Count;
        Text = "Xác nhận nộp bài thi";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = Ui.BodyFont;
        BackColor = Color.White;
        ClientSize = new Size(460, open > 0 ? 268 : 228);
        Ui.ApplyWindowIcon(this);

        var head = new Label
        {
            Text = "Xác nhận nộp bài thi",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = Ui.Text,
            Dock = DockStyle.Top,
            Height = 40,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false,
        };

        var mark = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.White };
        mark.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var box = new Rectangle(0, 4, 28, 28);
            using var fill = new SolidBrush(open > 0 ? Ui.Warning : Ui.Success);
            g.FillEllipse(fill, box);
            using var pen = new Pen(Color.White, 2f);
            if (open > 0)
            {
                g.DrawLine(pen, 14, 11, 14, 22);
                g.FillEllipse(Brushes.White, 12, 25, 4, 4);
            }
            else
            {
                g.DrawLines(pen, new[] { new Point(8, 18), new Point(13, 23), new Point(22, 12) });
            }
        };

        var body = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Font = Ui.BodyFont,
            ForeColor = Ui.Text,
            UseMnemonic = false,
            Text = open == 0
                ? "Bạn đã hoàn thành toàn bộ nhiệm vụ. Bạn có muốn nộp bài và xem kết quả ngay không?"
                : "Bạn còn " + open + " nhiệm vụ chưa hoàn thành (" + FormatLeftover(leftover) + "). Nếu nộp bài bây giờ, các câu này sẽ bị tính là 0 điểm.",
        };

        var back = Ui.OutlineBtn("Quay lại làm tiếp", Ui.Primary, 160);
        var go = Ui.PrimaryBtn("Nộp bài ngay", 140);
        go.BackColor = Ui.Orange;
        go.FlatAppearance.MouseOverBackColor = Color.FromArgb(160, 90, 0);
        back.DialogResult = DialogResult.Cancel;
        go.DialogResult = DialogResult.OK;
        CancelButton = back;
        AcceptButton = open > 0 ? back : go;

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            FlowDirection = open > 0 ? FlowDirection.LeftToRight : FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.White,
            Padding = new Padding(0, 8, 0, 0),
        };
        if (open > 0)
        {
            back.Margin = new Padding(0, 0, 10, 0);
            actions.Controls.Add(back);
            actions.Controls.Add(go);
        }
        else
        {
            go.Margin = new Padding(10, 0, 0, 0);
            actions.Controls.Add(go);
            actions.Controls.Add(back);
        }

        var inner = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 12, 20, 16) };
        inner.Controls.Add(body);
        inner.Controls.Add(mark);
        inner.Controls.Add(head);
        inner.Controls.Add(actions);
        Controls.Add(inner);
    }

    static string FormatLeftover(IReadOnlyList<int> leftover)
    {
        var shown = leftover.Take(6).Select(n => "Câu " + n).ToArray();
        var extra = leftover.Count - shown.Length;
        return extra > 0
            ? string.Join(", ", shown) + " và " + extra + " câu khác"
            : string.Join(", ", shown);
    }
}

sealed class PostSubmitForm : Form
{
    public PostSubmitForm(string summary)
    {
        Text = "Đã nộp bài";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = Ui.BodyFont;
        BackColor = Color.White;
        ClientSize = new Size(440, 220);
        Ui.ApplyWindowIcon(this);

        var head = new Label
        {
            Text = "Đã nộp bài",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = Ui.Text,
            Dock = DockStyle.Top,
            Height = 36,
            UseMnemonic = false,
        };
        var body = new Label
        {
            Text = summary + Environment.NewLine + Environment.NewLine + "Chọn Làm lại bài này hoặc Quay về Trang chủ.",
            AutoSize = false,
            Dock = DockStyle.Fill,
            ForeColor = Ui.Text,
            UseMnemonic = false,
        };
        var retry = Ui.OutlineBtn("Làm lại bài này", Ui.Primary, 160);
        var home = Ui.PrimaryBtn("Quay về Trang chủ", 180);
        retry.DialogResult = DialogResult.Retry;
        home.DialogResult = DialogResult.OK;
        AcceptButton = home;
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 8, 0, 0),
        };
        retry.Margin = new Padding(0, 0, 10, 0);
        actions.Controls.Add(retry);
        actions.Controls.Add(home);
        var inner = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 12, 20, 16) };
        inner.Controls.Add(body);
        inner.Controls.Add(head);
        inner.Controls.Add(actions);
        Controls.Add(inner);
    }
}
