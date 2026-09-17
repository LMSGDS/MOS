namespace MosDock;

/// <summary>
/// Màu và kiểu gần với Canvas LMS (Instructure): nav #394B58, primary #0374B5, nền #F5F5F5.
/// </summary>
static class Ui
{
    public static readonly Color Nav = Color.FromArgb(57, 75, 88);
    public static readonly Color NavDark = Color.FromArgb(43, 57, 67);
    public static readonly Color Primary = Color.FromArgb(3, 116, 181);
    public static readonly Color PrimaryDark = Color.FromArgb(2, 94, 146);
    public static readonly Color Page = Color.FromArgb(245, 245, 245);
    public static readonly Color Card = Color.White;
    public static readonly Color Text = Color.FromArgb(45, 59, 69);
    public static readonly Color Muted = Color.FromArgb(107, 119, 128);
    public static readonly Color Line = Color.FromArgb(199, 205, 209);
    public static readonly Color Success = Color.FromArgb(3, 137, 61);
    public static readonly Color Danger = Color.FromArgb(238, 6, 18);
    public static readonly Color Word = Color.FromArgb(43, 87, 154);
    public static readonly Color Excel = Color.FromArgb(33, 115, 70);
    public static readonly Color Ppt = Color.FromArgb(183, 71, 42);

    // aliases used by older call sites
    public static Color Navy => Nav;
    public static Color Blue => Primary;
    public static Color Teal => Success;
    public static Color Orange => Color.FromArgb(189, 107, 0);

    public static Font TitleFont => new("Segoe UI", 20f, FontStyle.Bold);
    public static Font HeadFont => new("Segoe UI", 13f, FontStyle.Bold);
    public static Font BodyFont => new("Segoe UI", 10f);
    public static Font SmallFont => new("Segoe UI", 9f);

    public static Label Title(string text) => new()
    {
        Text = text,
        Font = TitleFont,
        ForeColor = Text,
        AutoSize = true,
        Margin = new Padding(0, 0, 0, 6),
        UseMnemonic = false,
    };

    public static Label Subtitle(string text, int maxWidth = 680) => new()
    {
        Text = text,
        Font = BodyFont,
        ForeColor = Muted,
        AutoSize = true,
        MaximumSize = new Size(maxWidth, 0),
        Margin = new Padding(0, 0, 0, 16),
        UseMnemonic = false,
    };

    public static Button PrimaryBtn(string text, int minWidth = 128)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(minWidth, 36),
            Padding = new Padding(14, 6, 14, 6),
            FlatStyle = FlatStyle.Flat,
            BackColor = Primary,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            UseMnemonic = false,
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    public static Button SecondaryBtn(string text, int minWidth = 128)
    {
        var btn = PrimaryBtn(text, minWidth);
        btn.BackColor = NavDark;
        return btn;
    }

    public static Button DangerBtn(string text, int minWidth = 110)
    {
        var btn = PrimaryBtn(text, minWidth);
        btn.BackColor = NavDark;
        return btn;
    }

    public static Button Primary(string text, Color color, int w = 220, int h = 40)
    {
        var btn = PrimaryBtn(text, w);
        btn.BackColor = color;
        btn.MinimumSize = new Size(w, h);
        btn.AutoSize = false;
        btn.Size = new Size(w, h);
        return btn;
    }

    public static Button Ghost(string text, int w = 120, int h = 32)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(w, h),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Primary,
            Font = SmallFont,
            Cursor = Cursors.Hand,
            UseMnemonic = false,
        };
        btn.FlatAppearance.BorderColor = Line;
        return btn;
    }

    public static TableLayoutPanel Page(string title, string subtitle, Control body)
    {
        var page = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Page,
            Padding = new Padding(8, 4, 8, 8),
        };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.Controls.Add(Title(title), 0, 0);
        page.Controls.Add(Subtitle(subtitle), 0, 1);
        body.Dock = DockStyle.Fill;
        page.Controls.Add(body, 0, 2);
        return page;
    }

    public static Panel Tile(string title, string lead, Color accent, Action onClick)
    {
        var card = new Panel
        {
            Width = 300,
            Height = 188,
            BackColor = Card,
            Margin = new Padding(0, 0, 16, 16),
            Cursor = Cursors.Hand,
            Padding = new Padding(0),
        };
        var accentBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 6,
            BackColor = accent,
        };
        var inner = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(18, 14, 18, 14),
            BackColor = Card,
        };
        inner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        inner.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var h = new Label
        {
            Text = title,
            Font = HeadFont,
            ForeColor = Text,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8),
            UseMnemonic = false,
        };
        var p = new Label
        {
            Text = lead,
            Font = BodyFont,
            ForeColor = Muted,
            AutoSize = true,
            MaximumSize = new Size(254, 0),
            UseMnemonic = false,
        };
        inner.Controls.Add(h, 0, 0);
        inner.Controls.Add(p, 0, 1);
        card.Controls.Add(inner);
        card.Controls.Add(accentBar);
        void click(object? _, EventArgs e) => onClick();
        card.Click += click;
        inner.Click += click;
        h.Click += click;
        p.Click += click;
        accentBar.Click += click;
        h.Cursor = p.Cursor = inner.Cursor = Cursors.Hand;
        return card;
    }

    public static Panel ListCard(string title, string detail, Control? action = null)
    {
        var card = new TableLayoutPanel
        {
            Width = 840,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = action is null ? 1 : 2,
            RowCount = 2,
            BackColor = Card,
            Padding = new Padding(16, 12, 16, 12),
            Margin = new Padding(0, 0, 0, 10),
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        if (action is not null)
        {
            card.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            action.Anchor = AnchorStyles.Right;
            action.Margin = new Padding(8, 4, 0, 4);
            card.Controls.Add(action, 1, 0);
            card.SetRowSpan(action, 2);
        }

        card.Controls.Add(new Label
        {
            Text = title,
            Font = HeadFont,
            ForeColor = Text,
            AutoSize = true,
            MaximumSize = new Size(560, 0),
            UseMnemonic = false,
            Margin = new Padding(0, 0, 12, 4),
        }, 0, 0);
        card.Controls.Add(new Label
        {
            Text = detail,
            Font = SmallFont,
            ForeColor = Muted,
            AutoSize = true,
            MaximumSize = new Size(560, 0),
            UseMnemonic = false,
            Margin = new Padding(0, 0, 12, 0),
        }, 0, 1);
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
