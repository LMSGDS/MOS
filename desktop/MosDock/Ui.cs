namespace MosDock;

/// <summary>
/// Canvas LMS (Instructure) tokens: nav #394B58, primary #0374B5, page #F5F5F5, text #2D3B45.
/// Layout uses Dock stacking and measured text — never overlapping Location for titles.
/// </summary>
static class Ui
{
    public static readonly Color Nav = Color.FromArgb(57, 75, 88);
    public static readonly Color NavDark = Color.FromArgb(43, 57, 67);
    public static readonly Color Primary = Color.FromArgb(3, 116, 181);
    public static readonly Color PrimaryDark = Color.FromArgb(2, 94, 146);
    public static readonly Color PageBg = Color.FromArgb(245, 245, 245);
    public static readonly Color Card = Color.White;
    public static readonly Color Text = Color.FromArgb(45, 59, 69);
    public static readonly Color Muted = Color.FromArgb(107, 119, 128);
    public static readonly Color Line = Color.FromArgb(199, 205, 209);
    public static readonly Color Success = Color.FromArgb(3, 137, 61);
    public static readonly Color Danger = Color.FromArgb(238, 6, 18);
    public static readonly Color Word = Color.FromArgb(43, 87, 154);
    public static readonly Color Excel = Color.FromArgb(33, 115, 70);
    public static readonly Color Ppt = Color.FromArgb(183, 71, 42);
    public static readonly Color Warning = Color.FromArgb(189, 107, 0);

    public static Color Navy => Nav;
    public static Color Blue => Primary;
    public static Color Teal => Success;
    public static Color Orange => Warning;

    public static Font TitleFont => new("Segoe UI", 22f, FontStyle.Bold);
    public static Font HeadFont => new("Segoe UI", 13f, FontStyle.Bold);
    public static Font BodyFont => new("Segoe UI", 10f);
    public static Font SmallFont => new("Segoe UI", 9f);
    public static Font NavFont => new("Segoe UI", 13f, FontStyle.Bold);
    public static Font BtnFont => new("Segoe UI", 10f, FontStyle.Bold);

    static readonly TextFormatFlags WrapFlags =
        TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl;

    public static int MeasureH(string text, Font font, int width)
    {
        width = Math.Max(24, width);
        return TextRenderer.MeasureText(text ?? "", font, new Size(width, int.MaxValue), WrapFlags).Height;
    }

    public static int MeasureW(string text, Font font)
    {
        return TextRenderer.MeasureText(text ?? "", font, new Size(int.MaxValue, 0), TextFormatFlags.SingleLine).Width;
    }

    public static void BindWrap(Label label, int extra = 8)
    {
        void Fit(object? _, EventArgs e)
        {
            var parent = label.Parent;
            if (parent == null)
            {
                return;
            }

            var w = parent.ClientSize.Width - label.Margin.Horizontal - label.Padding.Horizontal;
            if (w < 24)
            {
                return;
            }

            var h = MeasureH(label.Text, label.Font, w) + label.Padding.Vertical + extra;
            if (label.Height != h)
            {
                label.Height = h;
            }
        }

        label.AutoSize = false;
        label.UseMnemonic = false;
        label.ParentChanged += (_, _) =>
        {
            if (label.Parent == null)
            {
                return;
            }

            label.Parent.Resize -= Fit;
            label.Parent.Resize += Fit;
            label.HandleCreated -= Fit;
            label.HandleCreated += Fit;
            Fit(null, EventArgs.Empty);
        };
        if (label.Parent != null)
        {
            label.Parent.Resize -= Fit;
            label.Parent.Resize += Fit;
            Fit(null, EventArgs.Empty);
        }
    }

    public static Label Wrap(string text, Font font, Color color, int extra = 10)
    {
        var label = new Label
        {
            Text = text,
            Font = font,
            ForeColor = color,
            AutoSize = false,
            Dock = DockStyle.Top,
            UseMnemonic = false,
        };
        BindWrap(label, extra);
        return label;
    }

    public static Button PrimaryBtn(string text, int minWidth = 128)
    {
        var w = Math.Max(minWidth, MeasureW(text, BtnFont) + 36);
        var btn = new Button
        {
            Text = text,
            AutoSize = false,
            Size = new Size(w, 38),
            FlatStyle = FlatStyle.Flat,
            BackColor = Primary,
            ForeColor = Color.White,
            Font = BtnFont,
            Cursor = Cursors.Hand,
            UseMnemonic = false,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = PrimaryDark;
        return btn;
    }

    public static Button NavBtn(string text, int minWidth = 120)
    {
        var btn = PrimaryBtn(text, minWidth);
        btn.BackColor = NavDark;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(33, 44, 52);
        btn.Height = 36;
        return btn;
    }

    public static Button GhostBtn(string text, int minWidth = 120)
    {
        var w = Math.Max(minWidth, MeasureW(text, SmallFont) + 28);
        var btn = new Button
        {
            Text = text,
            AutoSize = false,
            Size = new Size(w, 32),
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

    public static Panel StackPage(string title, string subtitle, Control body)
    {
        var page = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = PageBg,
        };
        body.Dock = DockStyle.Fill;
        var lead = Wrap(subtitle, BodyFont, Muted, 16);
        var head = Wrap(title, TitleFont, Text, 8);
        page.Controls.Add(body);
        page.Controls.Add(lead);
        page.Controls.Add(head);
        return page;
    }

    public static Panel Tile(string title, string lead, Color accent, Action onClick)
    {
        const int innerW = 244;
        var titleH = MeasureH(title, HeadFont, innerW);
        var leadH = MeasureH(lead, BodyFont, innerW);
        var cardH = 8 + 16 + titleH + 8 + leadH + 18;

        var shell = new Panel
        {
            Size = new Size(280, cardH),
            BackColor = Line,
            Padding = new Padding(1),
            Margin = new Padding(0, 0, 16, 16),
            Cursor = Cursors.Hand,
        };
        var card = new Panel { Dock = DockStyle.Fill, BackColor = Card, Cursor = Cursors.Hand };
        var bar = new Panel { Dock = DockStyle.Top, Height = 6, BackColor = accent, Cursor = Cursors.Hand };
        var inner = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Card,
            Padding = new Padding(18, 14, 18, 14),
            Cursor = Cursors.Hand,
        };
        var h = new Label
        {
            Text = title,
            Font = HeadFont,
            ForeColor = Text,
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = titleH + 8,
            UseMnemonic = false,
            Cursor = Cursors.Hand,
        };
        var p = new Label
        {
            Text = lead,
            Font = BodyFont,
            ForeColor = Muted,
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = leadH + 4,
            UseMnemonic = false,
            Cursor = Cursors.Hand,
        };
        inner.Controls.Add(p);
        inner.Controls.Add(h);
        card.Controls.Add(inner);
        card.Controls.Add(bar);
        shell.Controls.Add(card);

        void Click(object? _, EventArgs e) => onClick();
        foreach (Control c in new Control[] { shell, card, bar, inner, h, p })
        {
            c.Click += Click;
            c.Cursor = Cursors.Hand;
        }

        shell.MouseEnter += (_, _) => card.BackColor = inner.BackColor = Color.FromArgb(250, 252, 253);
        shell.MouseLeave += (_, _) => card.BackColor = inner.BackColor = Card;
        return shell;
    }

    public static Panel ListCard(string title, string detail, Control? action = null)
    {
        var card = new Panel
        {
            Width = 720,
            Height = 88,
            BackColor = Line,
            Padding = new Padding(1),
            Margin = new Padding(0, 0, 0, 10),
            Tag = "card",
        };
        var inner = new Panel { Dock = DockStyle.Fill, BackColor = Card, Padding = new Padding(16, 12, 16, 12) };
        if (action is not null)
        {
            var side = new Panel
            {
                Dock = DockStyle.Right,
                Width = Math.Max(136, action.Width + 8),
                BackColor = Card,
                Padding = new Padding(8, 0, 0, 0),
            };
            action.Location = new Point(8, 4);
            side.Controls.Add(action);
            inner.Controls.Add(side);
        }

        var copy = new Panel { Dock = DockStyle.Fill, BackColor = Card };
        var d = new Label
        {
            Text = detail,
            Font = SmallFont,
            ForeColor = Muted,
            AutoSize = false,
            Dock = DockStyle.Top,
            UseMnemonic = false,
        };
        var t = new Label
        {
            Text = title,
            Font = HeadFont,
            ForeColor = Text,
            AutoSize = false,
            Dock = DockStyle.Top,
            UseMnemonic = false,
        };
        BindWrap(d, 4);
        BindWrap(t, 6);
        copy.Controls.Add(d);
        copy.Controls.Add(t);
        inner.Controls.Add(copy);
        card.Controls.Add(inner);

        void Fit(object? _, EventArgs e)
        {
            var actionW = action is null ? 0 : Math.Max(136, action.Width + 24);
            var tw = Math.Max(160, card.ClientSize.Width - 36 - actionW);
            var th = MeasureH(title, HeadFont, tw) + MeasureH(detail, SmallFont, tw) + 36;
            var ah = action is null ? 0 : action.Height + 28;
            var next = Math.Max(72, Math.Max(th, ah));
            if (card.Height != next)
            {
                card.Height = next;
            }
        }

        card.Resize += Fit;
        Fit(null, EventArgs.Empty);
        return card;
    }

    public static void FitCards(FlowLayoutPanel list)
    {
        var w = Math.Max(360, list.ClientSize.Width - 28);
        foreach (Control child in list.Controls)
        {
            if (Equals(child.Tag, "card"))
            {
                child.Width = w;
            }
        }
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
