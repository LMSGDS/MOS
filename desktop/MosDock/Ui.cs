namespace MosDock;

static class Ui
{
    public static readonly Color Navy = Color.FromArgb(15, 76, 129);
    public static readonly Color Page = Color.FromArgb(244, 247, 251);
    public static readonly Color Card = Color.White;
    public static readonly Color Text = Color.FromArgb(15, 23, 42);
    public static readonly Color Muted = Color.FromArgb(71, 85, 105);
    public static readonly Color Blue = Color.FromArgb(0, 120, 212);
    public static readonly Color Teal = Color.FromArgb(15, 118, 110);
    public static readonly Color Orange = Color.FromArgb(194, 92, 36);
    public static readonly Color Word = Color.FromArgb(43, 87, 154);
    public static readonly Color Excel = Color.FromArgb(33, 115, 70);
    public static readonly Color Ppt = Color.FromArgb(183, 71, 42);
    public static readonly Color Line = Color.FromArgb(226, 232, 240);

    public static Font TitleFont => new("Segoe UI", 22f, FontStyle.Bold);
    public static Font HeadFont => new("Segoe UI", 14f, FontStyle.Bold);
    public static Font BodyFont => new("Segoe UI", 10.5f);
    public static Font SmallFont => new("Segoe UI", 9f);

    public static Button Primary(string text, Color color, int w = 220, int h = 40)
    {
        var btn = new Button
        {
            Text = text,
            Size = new Size(w, h),
            FlatStyle = FlatStyle.Flat,
            BackColor = color,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    public static Button Ghost(string text, int w = 120, int h = 32)
    {
        var btn = new Button
        {
            Text = text,
            Size = new Size(w, h),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Navy,
            Font = SmallFont,
            Cursor = Cursors.Hand,
        };
        btn.FlatAppearance.BorderColor = Line;
        return btn;
    }

    public static Panel Tile(string title, string lead, Color accent, Action onClick)
    {
        var card = new Panel
        {
            Size = new Size(280, 168),
            BackColor = Card,
            Margin = new Padding(12),
            Cursor = Cursors.Hand,
            Padding = new Padding(20),
        };
        var bar = new Panel { BackColor = accent, Size = new Size(48, 6), Location = new Point(20, 20) };
        var h = new Label
        {
            Text = title,
            Font = HeadFont,
            ForeColor = Text,
            AutoSize = false,
            Location = new Point(20, 40),
            Size = new Size(240, 32),
        };
        var p = new Label
        {
            Text = lead,
            Font = BodyFont,
            ForeColor = Muted,
            AutoSize = false,
            Location = new Point(20, 78),
            Size = new Size(240, 64),
        };
        card.Controls.Add(bar);
        card.Controls.Add(h);
        card.Controls.Add(p);
        void click(object? _, EventArgs e) => onClick();
        card.Click += click;
        foreach (Control c in card.Controls)
        {
            c.Cursor = Cursors.Hand;
            c.Click += click;
        }

        return card;
    }

    public static string AppName(string id) => id switch
    {
        "excel" => "Microsoft Excel",
        "powerpoint" => "Microsoft PowerPoint",
        _ => "Microsoft Word",
    };

    public static Color AppColor(string id) => id switch
    {
        "excel" => Excel,
        "powerpoint" => Ppt,
        _ => Word,
    };

    public static string ModeLabel(string mode) =>
        mode == "testing" ? "Thi" : "Luyện tập";
}
