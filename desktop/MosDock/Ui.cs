namespace MosDock;

/// <summary>
/// Canvas LMS tokens with a warm-minimalist hub: nav #394B58, primary #0374B5,
/// page linen #F7F3EE, text #2D3B45. Layout uses Dock stacking — never overlapping titles.
/// </summary>
static class Ui
{
    public static readonly Color Nav = Color.FromArgb(57, 75, 88);
    public static readonly Color NavDark = Color.FromArgb(43, 57, 67);
    public static readonly Color Primary = Color.FromArgb(3, 116, 181);
    public static readonly Color PrimaryDark = Color.FromArgb(2, 94, 146);
    public static readonly Color PageBg = Color.FromArgb(247, 243, 238);
    public static readonly Color WarmShadow = Color.FromArgb(226, 214, 200);
    public static readonly Color BannerBg = Color.FromArgb(227, 240, 248);
    public static readonly Color ReviewResult = Color.FromArgb(232, 242, 250);
    public static readonly Color ReviewYours = Color.FromArgb(255, 246, 230);
    public static readonly Color ReviewCorrect = Color.FromArgb(230, 245, 233);
    public static readonly Color ReviewPitfall = Color.FromArgb(255, 235, 230);
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
    public static readonly Color SignIn = Color.FromArgb(11, 37, 69);
    public static readonly Color SignInHover = Color.FromArgb(8, 28, 54);
    public static readonly Color DockBlue = Color.FromArgb(3, 116, 181);
    public static readonly Color DockTeal = Color.FromArgb(56, 142, 168);
    public static readonly Color DockGreen = Color.FromArgb(46, 139, 87);
    public static readonly Color DockQuiet = Color.FromArgb(120, 136, 156);
    public static readonly Color DockHint = Color.FromArgb(201, 148, 36);
    /// <summary>UDL Tab focus ring: 2px solid #005fb8, offset 2px.</summary>
    public static readonly Color FocusRing = Color.FromArgb(0, 95, 184);
    /// <summary>Canvas LMS ic-box: 16px pad, 8px radius painted only — never a clipping Region.</summary>
    public const int CardPad = 16;
    public const int CardRadius = 8;

    /// <summary>
    /// Form error SOP: radius 8, idle Line, focus Primary + soft shadow,
    /// empty-field Danger border + SmallFont microcopy under the field,
    /// auth/status errors use AlertBar above the first field (never under the password).
    /// </summary>
    public const int FormFieldRadius = 8;

    public static Color Navy => Nav;
    public static Color Blue => Primary;
    public static Color Teal => Success;
    public static Color Orange => Warning;

    public static void ApplyWindowIcon(Form form)
    {
        try
        {
            var ico = Path.Combine(AppContext.BaseDirectory, "Assets", "kulkul.ico");
            if (!File.Exists(ico))
            {
                ico = Path.Combine(AppContext.BaseDirectory, "kulkul.ico");
            }

            if (File.Exists(ico))
            {
                form.Icon = new Icon(ico);
                return;
            }

            var exe = Application.ExecutablePath;
            if (!string.IsNullOrWhiteSpace(exe) && File.Exists(exe))
            {
                form.Icon = Icon.ExtractAssociatedIcon(exe);
            }
        }
        catch
        {
            // default WinForms icon
        }
    }

    public static Image? BrandMark(int size = 72)
    {
        try
        {
            var png = Path.Combine(AppContext.BaseDirectory, "Assets", "kulkul.png");
            if (!File.Exists(png))
            {
                png = Path.Combine(AppContext.BaseDirectory, "kulkul.png");
            }

            if (!File.Exists(png))
            {
                return PaintBrand(size);
            }

            using var src = Image.FromFile(png);
            return new Bitmap(src, new Size(size, size));
        }
        catch
        {
            return PaintBrand(size);
        }
    }

    public static Image PaintBrand(int size = 72)
    {
        size = Math.Max(24, size);
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);
        var rect = new Rectangle(0, 0, size - 1, size - 1);
        using (var bg = new SolidBrush(SignIn))
        {
            g.FillRounded(rect, size / 5, bg);
        }

        var cx = size * 0.50f;
        var cy = size * 0.50f;
        var len = size * 0.64f;
        var thick = size * 0.30f;
        DrawBlade(g, cx + size * 0.04f, cy - size * 0.06f, len, thick, -48f, Word);
        DrawBlade(g, cx - size * 0.06f, cy + size * 0.10f, len * 0.78f, thick * 0.88f, 205f, Excel);
        DrawBlade(g, cx + size * 0.08f, cy + size * 0.12f, len * 0.80f, thick * 0.88f, 22f, Ppt);
        DrawSpark(g, cx, cy, size * 0.09f);
        return bmp;
    }

    static void DrawBlade(Graphics g, float cx, float cy, float len, float thick, float angle, Color color)
    {
        var state = g.Save();
        g.TranslateTransform(cx, cy);
        g.RotateTransform(angle);
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddEllipse(-len * 0.18f, -thick / 2f, len, thick);
        using var fill = new SolidBrush(color);
        g.FillPath(fill, path);
        using var hi = new SolidBrush(Color.FromArgb(40, Color.White));
        g.FillEllipse(hi, -len * 0.05f, -thick * 0.28f, len * 0.55f, thick * 0.42f);
        g.Restore(state);
    }

    static void DrawSpark(Graphics g, float cx, float cy, float r)
    {
        using var fill = new SolidBrush(Color.White);
        var pts = new PointF[8];
        for (var i = 0; i < 8; i++)
        {
            var a = (float)(Math.PI / 2 + i * Math.PI / 4);
            var rad = i % 2 == 0 ? r : r * 0.38f;
            pts[i] = new PointF(cx + rad * (float)Math.Cos(a), cy - rad * (float)Math.Sin(a));
        }

        g.FillPolygon(fill, pts);
    }

    static void FillRounded(this Graphics g, Rectangle rect, int radius, Brush brush)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }

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

    public static void BindWrap(Label label, int extra = 8, int maxHeight = 0)
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
            if (maxHeight > 0)
            {
                h = Math.Min(h, maxHeight);
            }
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
        AttachFocusRing(btn);
        return btn;
    }

    public static Button OutlineBtn(string text, Color accent, int minWidth = 128)
    {
        var btn = PrimaryBtn(text, minWidth);
        btn.BackColor = Card;
        btn.ForeColor = accent;
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.BorderColor = accent;
        btn.FlatAppearance.MouseOverBackColor = Blend(accent, Card, 28);
        btn.FlatAppearance.MouseDownBackColor = Blend(accent, Card, 46);
        return btn;
    }

    public static Color Blend(Color tint, Color onto, int amount)
    {
        var a = Math.Clamp(amount, 0, 255);
        return Color.FromArgb(
            (tint.R * a + onto.R * (255 - a)) / 255,
            (tint.G * a + onto.G * (255 - a)) / 255,
            (tint.B * a + onto.B * (255 - a)) / 255);
    }

    public static Button NavBtn(string text, int minWidth = 120)
    {
        var btn = PrimaryBtn(text, minWidth);
        btn.BackColor = NavDark;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(33, 44, 52);
        btn.Height = 36;
        return btn;
    }

    public static Button SignInBtn(string text)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = false,
            Height = 48,
            FlatStyle = FlatStyle.Flat,
            BackColor = SignIn,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            UseMnemonic = false,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = SignInHover;
        AttachFocusRing(btn);
        return btn;
    }

    public static void RoundControl(Control control, int radius)
    {
        void Apply(object? _, EventArgs e)
        {
            if (control.Width < 4 || control.Height < 4)
            {
                return;
            }

            using var path = RoundedRect(new Rectangle(0, 0, control.Width, control.Height), radius);
            var next = new Region(path);
            var prev = control.Region;
            control.Region = next;
            prev?.Dispose();
        }

        control.Resize += Apply;
        Apply(null, EventArgs.Empty);
    }

    public static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var d = Math.Max(2, radius * 2);
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
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
        AttachFocusRing(btn);
        return btn;
    }

    public static Panel SoftCard(out Panel inner, int radius = CardRadius)
    {
        _ = radius;
        var shell = new Panel
        {
            BackColor = Line,
            Padding = new Padding(1),
            Margin = new Padding(0, 0, 12, 12),
        };
        inner = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Card,
            Padding = new Padding(CardPad),
        };
        shell.Controls.Add(inner);
        return shell;
    }

    public sealed class SoftField : Panel
    {
        Control? _focus;
        bool _error;
        bool _focused;

        public SoftField()
        {
            Height = 50;
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Color.White;
            Padding = new Padding(4, 3, 4, 4);
        }

        public void Bind(Control focus)
        {
            _focus = focus;
            focus.GotFocus += (_, _) =>
            {
                _focused = true;
                Invalidate();
            };
            focus.LostFocus += (_, _) =>
            {
                _focused = false;
                Invalidate();
            };
        }

        public void SetError(bool error)
        {
            _error = error;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var box = new Rectangle(3, 2, Math.Max(8, Width - 7), Math.Max(8, Height - 6));
            using var path = RoundedRect(box, FormFieldRadius);
            if (_focused && !_error)
            {
                using var shadow = new SolidBrush(Color.FromArgb(40, Primary));
                var sh = box;
                sh.Offset(0, 2);
                using var sp = RoundedRect(sh, FormFieldRadius);
                g.FillPath(shadow, sp);
            }

            using (var fill = new SolidBrush(Card))
            {
                g.FillPath(fill, path);
            }

            var border = _error ? Danger : _focused ? Primary : Line;
            using var pen = new Pen(border, _focused || _error ? 1.8f : 1.2f);
            g.DrawPath(pen, path);
        }
    }

    public sealed class AlertBar : Panel
    {
        readonly Label _msg = new();
        readonly Panel _icon = new();
        string _tone = "danger";

        public AlertBar()
        {
            Visible = false;
            Height = 0;
            Padding = new Padding(10, 8, 12, 8);
            _icon.Dock = DockStyle.Left;
            _icon.Width = 28;
            _icon.Paint += (_, e) => PaintInfoMark(e.Graphics, _icon.ClientRectangle, AccentOf(_tone));
            _msg.Dock = DockStyle.Fill;
            _msg.Font = SmallFont;
            _msg.ForeColor = Ui.Text;
            _msg.UseMnemonic = false;
            Controls.Add(_msg);
            Controls.Add(_icon);
            RoundControl(this, 10);
        }

        public void ShowMessage(string text, string tone)
        {
            _tone = tone;
            _msg.Text = text;
            Visible = true;
            ApplyTone();
            Invalidate();
            _icon.Invalidate();
        }

        public void Clear()
        {
            _msg.Text = "";
            Visible = false;
            Height = 0;
        }

        public int FitWidth(int inner)
        {
            if (!Visible || string.IsNullOrWhiteSpace(_msg.Text))
            {
                Height = 0;
                return 0;
            }

            Height = Math.Max(52, MeasureH(_msg.Text, SmallFont, Math.Max(120, inner - 56)) + 22);
            return Height;
        }

        void ApplyTone()
        {
            var fill = _tone switch
            {
                "warn" => ReviewYours,
                "info" => BannerBg,
                _ => ReviewPitfall,
            };
            BackColor = fill;
            _icon.BackColor = fill;
            _msg.BackColor = fill;
        }

        static Color AccentOf(string tone) => tone switch
        {
            "warn" => Warning,
            "info" => Primary,
            _ => Danger,
        };
    }

    public static void PaintEye(Graphics g, Rectangle r, Color color, bool open)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var cx = r.X + r.Width / 2f;
        var cy = r.Y + r.Height / 2f;
        using var pen = new Pen(color, 1.6f);
        pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
        pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
        g.DrawBezier(pen, cx - 9, cy, cx - 4, cy - 6, cx + 4, cy - 6, cx + 9, cy);
        g.DrawBezier(pen, cx - 9, cy, cx - 4, cy + 6, cx + 4, cy + 6, cx + 9, cy);
        if (open)
        {
            g.DrawEllipse(pen, cx - 3, cy - 3, 6, 6);
        }
        else
        {
            g.DrawLine(pen, cx - 8, cy + 7, cx + 8, cy - 7);
        }
    }

    public static void PaintGlobe(Graphics g, Rectangle r, Color color)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var box = new Rectangle(r.X + (r.Width - 16) / 2, r.Y + (r.Height - 16) / 2, 16, 16);
        using var pen = new Pen(color, 1.5f);
        g.DrawEllipse(pen, box);
        g.DrawEllipse(pen, box.X + 4, box.Y, 8, 16);
        g.DrawLine(pen, box.X, box.Y + 8, box.Right, box.Y + 8);
        g.DrawLine(pen, box.X + 2, box.Y + 4, box.Right - 2, box.Y + 4);
        g.DrawLine(pen, box.X + 2, box.Y + 12, box.Right - 2, box.Y + 12);
    }

    public static Panel InfoBanner(string text)
    {
        var bar = new Panel
        {
            Height = 44,
            MinimumSize = new Size(0, 40),
            BackColor = BannerBg,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(10, 8, 12, 8),
        };
        RoundControl(bar, 10);
        var icon = new Panel
        {
            Dock = DockStyle.Left,
            Width = 28,
            BackColor = BannerBg,
        };
        icon.Paint += (_, e) => PaintInfoMark(e.Graphics, icon.ClientRectangle, Primary);
        var msg = new Label
        {
            Text = text,
            Font = SmallFont,
            ForeColor = Text,
            Dock = DockStyle.Top,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = false,
            UseMnemonic = false,
        };
        BindWrap(msg, 4);
        void FitBar(object? _, EventArgs e)
        {
            var h = Math.Max(40, msg.Height + bar.Padding.Vertical);
            if (bar.Height != h)
            {
                bar.Height = h;
            }
        }

        bar.Resize += FitBar;
        msg.SizeChanged += FitBar;
        bar.Controls.Add(msg);
        bar.Controls.Add(icon);
        return bar;
    }

    public static void PaintInfoMark(Graphics g, Rectangle r, Color color)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var size = Math.Min(r.Width, r.Height) - 8;
        var box = new Rectangle(r.X + (r.Width - size) / 2, r.Y + (r.Height - size) / 2, size, size);
        using var fill = new SolidBrush(color);
        g.FillEllipse(fill, box);
        using var pen = new Pen(Color.White, 1.6f);
        var cx = box.X + box.Width / 2;
        g.DrawLine(pen, cx, box.Y + 8, cx, box.Bottom - 5);
        g.FillEllipse(Brushes.White, cx - 1, box.Y + 4, 3, 3);
    }

    public static void PaintAppGlyph(Graphics g, Rectangle r, Color color, string glyph)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        using var fill = new SolidBrush(color);
        using var path = RoundedRect(r, 10);
        g.FillPath(fill, path);
        TextRenderer.DrawText(
            g,
            glyph,
            new Font("Segoe UI", 14f, FontStyle.Bold),
            r,
            Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    public static Label SectionLabel(string text)
    {
        return new Label
        {
            Text = text,
            Font = HeadFont,
            ForeColor = Text,
            AutoSize = false,
            Height = 28,
            Margin = new Padding(0, 4, 0, 8),
            UseMnemonic = false,
        };
    }

    public static Button TextLink(string text, Action onClick)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = Primary,
            Font = SmallFont,
            Cursor = Cursors.Hand,
            UseMnemonic = false,
            Padding = Padding.Empty,
            Margin = new Padding(0),
            Height = 24,
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(235, 244, 250);
        btn.Click += (_, _) => onClick();
        AttachFocusRing(btn);
        return btn;
    }

    public static Panel AppLaunchTile(string title, string lead, Color accent, string glyph, Action onClick)
    {
        var shell = SoftCard(out var inner);
        shell.Margin = new Padding(0, 0, 12, 0);
        inner.Cursor = Cursors.Hand;
        inner.Padding = new Padding(CardPad);
        var bar = new Panel { Dock = DockStyle.Top, Height = 4, BackColor = accent, Cursor = Cursors.Hand };
        var glyphBox = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            Cursor = Cursors.Hand,
            BackColor = Card,
        };
        glyphBox.Paint += (_, e) => PaintAppGlyph(e.Graphics, new Rectangle(0, 2, 32, 32), accent, glyph);
        var h = new Label
        {
            Text = title,
            Font = HeadFont,
            ForeColor = Text,
            AutoSize = false,
            Dock = DockStyle.Top,
            UseMnemonic = false,
            Cursor = Cursors.Hand,
        };
        var p = new Label
        {
            Text = lead,
            Font = SmallFont,
            ForeColor = Muted,
            AutoSize = false,
            Dock = DockStyle.Top,
            UseMnemonic = false,
            Cursor = Cursors.Hand,
        };
        var copy = new TileCopy { Title = h, Lead = p };
        shell.Tag = copy;
        void FitTile(object? _, EventArgs e)
        {
            var w = Math.Max(80, inner.ClientSize.Width - inner.Padding.Horizontal);
            h.Height = Math.Max(24, MeasureH(title, HeadFont, w) + 8);
            p.Height = Math.Max(20, MeasureH(lead, SmallFont, w) + 8);
        }

        inner.Resize += FitTile;
        FitTile(null, EventArgs.Empty);
        inner.Controls.Add(p);
        inner.Controls.Add(h);
        inner.Controls.Add(glyphBox);
        inner.Controls.Add(bar);

        void Click(object? _, EventArgs e) => onClick();
        foreach (Control c in new Control[] { shell, inner, bar, glyphBox, h, p })
        {
            c.Click += Click;
            c.Cursor = Cursors.Hand;
        }

        inner.MouseEnter += (_, _) => inner.BackColor = Color.FromArgb(252, 249, 245);
        inner.MouseLeave += (_, _) => inner.BackColor = Card;
        return shell;
    }

    public sealed class TileCopy
    {
        public Label Title = null!;
        public Label Lead = null!;

        public int HeightFor(int width)
        {
            var inner = Math.Max(80, width - 2 - CardPad * 2);
            return 4 + 40 + Math.Max(24, MeasureH(Title.Text, HeadFont, inner) + 8)
                + Math.Max(20, MeasureH(Lead.Text, SmallFont, inner) + 8)
                + CardPad * 2 + 8;
        }
    }

    public static Panel MiniScoreRow(string title, string score, Color accent, Action? onClick)
    {
        var row = new Panel
        {
            Height = 44,
            Dock = DockStyle.Top,
            BackColor = Card,
            Padding = new Padding(0, 4, 0, 4),
            Cursor = onClick is null ? Cursors.Default : Cursors.Hand,
        };
        var mark = new Panel { Dock = DockStyle.Left, Width = 6, BackColor = accent };
        var pts = new Label
        {
            Text = score,
            Font = HeadFont,
            ForeColor = Text,
            Dock = DockStyle.Right,
            Width = 88,
            TextAlign = ContentAlignment.MiddleRight,
            UseMnemonic = false,
        };
        var name = new Label
        {
            Text = title,
            Font = BodyFont,
            ForeColor = Text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            UseMnemonic = false,
        };
        row.Controls.Add(name);
        row.Controls.Add(pts);
        row.Controls.Add(mark);
        if (onClick is not null)
        {
            void Click(object? _, EventArgs e) => onClick();
            foreach (Control c in new Control[] { row, name, pts, mark })
            {
                c.Click += Click;
                c.Cursor = Cursors.Hand;
            }
        }

        return row;
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

    public static Panel ProgramTile(string program, string title, Color accent, string glyph, Action onClick)
    {
        var shell = SoftCard(out var inner);
        shell.Dock = DockStyle.Fill;
        shell.Margin = new Padding(0, 0, 10, 10);
        inner.Cursor = Cursors.Hand;
        inner.Padding = new Padding(16, 12, 16, 12);
        var bar = new Panel { Dock = DockStyle.Top, Height = 5, BackColor = accent, Cursor = Cursors.Hand };
        var row = new Panel { Dock = DockStyle.Fill, BackColor = Card, Cursor = Cursors.Hand };
        var glyphBox = new Panel
        {
            Size = new Size(44, 44),
            Dock = DockStyle.Left,
            Width = 48,
            Cursor = Cursors.Hand,
            BackColor = Card,
        };
        glyphBox.Paint += (_, e) =>
        {
            var wash = glyphBox.BackColor;
            e.Graphics.Clear(wash);
            PaintAppGlyph(e.Graphics, new Rectangle(0, 4, 36, 36), accent, glyph);
        };
        var h = new Label
        {
            Text = title,
            Font = HeadFont,
            ForeColor = Text,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false,
            Cursor = Cursors.Hand,
            Padding = new Padding(10, 0, 0, 0),
        };
        row.Controls.Add(h);
        row.Controls.Add(glyphBox);
        inner.Controls.Add(row);
        inner.Controls.Add(bar);

        var state = new ProgramTileState
        {
            Id = program,
            Accent = accent,
            Inner = inner,
            Bar = bar,
            Title = h,
            Glyph = glyphBox,
            Row = row,
        };
        shell.Tag = state;

        void Click(object? _, EventArgs e) => onClick();
        foreach (Control c in new Control[] { shell, inner, bar, row, glyphBox, h })
        {
            c.Click += Click;
            c.Cursor = Cursors.Hand;
            c.MouseEnter += (_, _) =>
            {
                state.Hover = true;
                PaintProgramTile(state);
            };
            c.MouseLeave += (_, _) =>
            {
                state.Hover = inner.RectangleToScreen(inner.ClientRectangle).Contains(Cursor.Position);
                PaintProgramTile(state);
            };
        }

        PaintProgramTile(state);
        return shell;
    }

    public static void MarkProgramTiles(Control host, string active)
    {
        foreach (Control child in host.Controls)
        {
            if (child.Tag is ProgramTileState state)
            {
                state.Active = string.Equals(state.Id, active, StringComparison.OrdinalIgnoreCase);
                PaintProgramTile(state);
            }
        }
    }

    static void PaintProgramTile(ProgramTileState state)
    {
        var wash = state.Active
            ? Blend(state.Accent, Card, 22)
            : state.Hover
                ? Color.FromArgb(252, 249, 245)
                : Card;
        state.Inner.BackColor = wash;
        state.Row.BackColor = wash;
        state.Glyph.BackColor = wash;
        state.Title.BackColor = wash;
        state.Title.ForeColor = state.Active ? Text : Muted;
        state.Bar.Height = state.Active ? 7 : 4;
        state.Glyph.Invalidate();
    }

    sealed class ProgramTileState
    {
        public string Id = "";
        public Color Accent;
        public Panel Inner = null!;
        public Panel Bar = null!;
        public Label Title = null!;
        public Panel Glyph = null!;
        public Panel Row = null!;
        public bool Active;
        public bool Hover;
    }

    public static Panel Tile(string title, string lead, Color accent, Action onClick)
    {
        const int innerW = 244;
        var titleH = MeasureH(title, HeadFont, innerW);
        var leadH = MeasureH(lead, BodyFont, innerW);
        var cardH = 8 + 16 + titleH + 8 + leadH + 18;

        var shell = new Panel
        {
            Size = new Size(280, cardH + 3),
            BackColor = WarmShadow,
            Padding = new Padding(0, 0, 1, 3),
            Margin = new Padding(0, 0, 16, 16),
            Cursor = Cursors.Hand,
        };
        var card = new Panel { Dock = DockStyle.Fill, BackColor = Card, Cursor = Cursors.Hand };
        RoundControl(shell, 14);
        RoundControl(card, 12);
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
            Height = 104,
            BackColor = Line,
            Padding = new Padding(1),
            Margin = new Padding(0, 0, 0, 14),
            Tag = "card",
        };
        var inner = new Panel { Dock = DockStyle.Fill, BackColor = Card, Padding = new Padding(CardPad, CardPad, CardPad, CardPad) };
        if (action is not null)
        {
            var side = new Panel
            {
                Dock = DockStyle.Right,
                Width = Math.Max(148, action.Width + 16),
                BackColor = Card,
                Padding = new Padding(16, 2, 4, 2),
            };
            action.Location = new Point(16, 4);
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
        copy.Controls.Add(d);
        copy.Controls.Add(t);
        inner.Controls.Add(copy);
        card.Controls.Add(inner);

        void Fit(object? _, EventArgs e)
        {
            var actionW = action is null ? 0 : Math.Max(148, action.Width + 40);
            var tw = Math.Max(160, card.ClientSize.Width - CardPad * 2 - 4 - actionW);
            t.Height = Math.Max(24, MeasureH(title, HeadFont, tw) + 10);
            d.Height = Math.Max(20, MeasureH(detail, SmallFont, tw) + 8);
            var th = t.Height + d.Height + CardPad * 2 + 12;
            var ah = action is null ? 0 : action.Height + CardPad * 2;
            var next = Math.Max(88, Math.Max(th, ah));
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
        var w = Math.Max(LayoutMath.DashCardMin, list.ClientSize.Width - 28);
        foreach (Control child in list.Controls)
        {
            if (Equals(child.Tag, "card"))
            {
                child.Width = w;
            }
        }
    }

    /// <summary>auto-fit minmax: columns from real width so PowerPoint wraps instead of vanishing.</summary>
    public static void FitWrapRow(FlowLayoutPanel row, int minW, int height, int gap = -1)
    {
        if (gap < 0)
        {
            gap = LayoutMath.DashGap;
        }

        var inner = Math.Max(minW, row.ClientSize.Width);
        var n = row.Controls.Count;
        var cardW = LayoutMath.AutoFitCardWidth(inner, minW, Math.Max(1, n), gap);
        var cols = Math.Min(Math.Max(1, n), LayoutMath.AutoFitColumns(inner, minW, gap));
        while (cols > 1 && cols * cardW + (cols - 1) * gap > inner)
        {
            cols--;
        }

        var heights = new int[Math.Max(1, n)];
        for (var i = 0; i < n; i++)
        {
            var child = row.Controls[i];
            child.Dock = DockStyle.None;
            child.Width = cardW;
            var lastInRow = cols <= 1 || (i % cols) == cols - 1 || i == n - 1;
            child.Margin = new Padding(0, 0, lastInRow ? 0 : gap, gap);
            var contentH = child.Tag is TileCopy copy ? copy.HeightFor(cardW) : height;
            heights[i] = Math.Max(height, contentH);
        }

        var lines = n == 0 ? 1 : Math.Max(1, (n + cols - 1) / cols);
        var lineH = new int[lines];
        for (var i = 0; i < n; i++)
        {
            var r = cols <= 0 ? 0 : i / cols;
            lineH[r] = Math.Max(lineH[r], heights[i]);
        }

        for (var i = 0; i < n; i++)
        {
            var r = cols <= 0 ? 0 : i / cols;
            row.Controls[i].Height = lineH[r];
        }

        var total = 0;
        foreach (var h in lineH)
        {
            total += h + gap;
        }

        row.Height = Math.Max(height + gap, total);
    }

    public static void AttachFocusRing(Control control)
    {
        control.GotFocus += (_, _) => control.Invalidate();
        control.LostFocus += (_, _) => control.Invalidate();
        control.Paint += (_, e) =>
        {
            if (!control.ContainsFocus && !control.Focused)
            {
                return;
            }

            if (!control.Focused)
            {
                return;
            }

            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var box = control.ClientRectangle;
            box.Inflate(-2, -2);
            if (box.Width < 6 || box.Height < 6)
            {
                return;
            }

            using var pen = new Pen(FocusRing, 2f);
            g.DrawRectangle(pen, box.X, box.Y, box.Width - 1, box.Height - 1);
        };
    }

    /// <summary>0.3s ease: opacity analog via 10px rise so tab switches do not snap.</summary>
    public static void PlayReveal(Control host)
    {
        if (host is null || host.IsDisposed)
        {
            return;
        }

        const int shift = 10;
        const int ms = 300;
        var startPad = host.Padding;
        host.Padding = new Padding(startPad.Left, startPad.Top + shift, startPad.Right, startPad.Bottom);
        var start = Environment.TickCount;
        var timer = new System.Windows.Forms.Timer { Interval = 16 };
        timer.Tick += (_, _) =>
        {
            if (host.IsDisposed)
            {
                timer.Stop();
                timer.Dispose();
                return;
            }

            var t = Math.Clamp((Environment.TickCount - start) / (double)ms, 0, 1);
            var ease = 1 - Math.Pow(1 - t, 3);
            host.Padding = new Padding(
                startPad.Left,
                startPad.Top + (int)Math.Round(shift * (1 - ease)),
                startPad.Right,
                startPad.Bottom);
            if (t >= 1)
            {
                host.Padding = startPad;
                timer.Stop();
                timer.Dispose();
            }
        };
        timer.Start();
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
        mode == "testing" ? "Testing" : "Training";

    /// <summary>Labeled GMetrix Test Runner control — not an icon square.</summary>
    public static Button RunnerBtn(string text, Color fill, int minWidth = 108)
    {
        var w = Math.Max(minWidth, MeasureW(text, SmallFont) + 20);
        var btn = new Button
        {
            Text = text,
            AutoSize = false,
            Size = new Size(w, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = fill,
            ForeColor = Color.White,
            Font = SmallFont,
            Cursor = Cursors.Hand,
            UseMnemonic = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(3, 4, 3, 4),
            AccessibleName = text,
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(fill);
        btn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(fill);
        DockTips.SetToolTip(btn, text);
        AttachFocusRing(btn);
        return btn;
    }

    public static Button DockSquare(NavIcon icon, string tip, Color fill)
    {
        var btn = new Button
        {
            Size = new Size(40, 40),
            FlatStyle = FlatStyle.Flat,
            BackColor = fill,
            ForeColor = Color.White,
            Margin = new Padding(3, 3, 3, 3),
            Tag = icon,
            Cursor = Cursors.Hand,
            UseMnemonic = false,
            Text = "",
            AccessibleName = tip,
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(fill);
        btn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(fill);
        DockTips.SetToolTip(btn, tip);
        btn.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var kind = btn.Tag is NavIcon n ? n : icon;
            DrawNavIcon(e.Graphics, btn.ClientRectangle, kind, Color.White, btn.BackColor);
        };
        AttachFocusRing(btn);
        RoundControl(btn, 6);
        return btn;
    }

    static ToolTip? _dockTips;
    static readonly Font TipFont = new("Segoe UI", 9f, FontStyle.Bold);
    static readonly Font AaaBig = new("Segoe UI", 13f, FontStyle.Bold);
    static readonly Font AaaMid = new("Segoe UI", 10f, FontStyle.Bold);
    static readonly Font AaaSm = new("Segoe UI", 8f, FontStyle.Bold);

    public static ToolTip DockTips => _dockTips ??= CreateDarkTip();

    public static ToolTip CreateDarkTip()
    {
        var tip = new ToolTip
        {
            ShowAlways = true,
            OwnerDraw = true,
            UseAnimation = false,
            UseFading = false,
            InitialDelay = 120,
            AutoPopDelay = 5000,
            ReshowDelay = 80,
            BackColor = Color.FromArgb(20, 20, 20),
            ForeColor = Color.White,
        };
        tip.Popup += (_, e) =>
        {
            e.ToolTipSize = new Size(Math.Max(88, e.ToolTipSize.Width + 28), Math.Max(32, e.ToolTipSize.Height + 8));
        };
        tip.Draw += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var box = new Rectangle(0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
            using var path = RoundedRect(box, 8);
            using var bg = new SolidBrush(Color.FromArgb(20, 20, 20));
            e.Graphics.FillPath(bg, path);
            TextRenderer.DrawText(
                e.Graphics,
                e.ToolTipText,
                TipFont,
                box,
                Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        };
        return tip;
    }

    public static Button AaSizeButton(string text, string tip)
    {
        var btn = new Button
        {
            Size = new Size(40, 26),
            FlatStyle = FlatStyle.Flat,
            BackColor = DockBlue,
            ForeColor = Color.White,
            Text = text,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            UseMnemonic = false,
            AccessibleName = tip,
            Margin = new Padding(4, 0, 0, 0),
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 99, 177);
        DockTips.SetToolTip(btn, tip);
        AttachFocusRing(btn);
        RoundControl(btn, 6);
        return btn;
    }

    public static Button AaaButton()
    {
        var btn = new Button
        {
            Size = new Size(54, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = DockBlue,
            ForeColor = Color.White,
            Text = "",
            Cursor = Cursors.Hand,
            UseMnemonic = false,
            AccessibleName = "Cỡ chữ hướng dẫn",
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 99, 177);
        DockTips.SetToolTip(btn, "Cỡ chữ hướng dẫn");
        btn.Paint += (_, e) => PaintAaa(e.Graphics, btn.ClientRectangle);
        RoundControl(btn, 6);
        return btn;
    }

    public static void PaintAaa(Graphics g, Rectangle r)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        using var brush = new SolidBrush(Color.White);
        g.DrawString("A", AaaBig, brush, r.X + 3, r.Y + 3);
        g.DrawString("A", AaaMid, brush, r.X + 20, r.Y + 10);
        g.DrawString("A", AaaSm, brush, r.X + 33, r.Y + 16);
    }

    public static string StripMarks(string? text) =>
        (text ?? "").Replace("**", "", StringComparison.Ordinal);

    public static string MarkedDocumentRtf(string text, float bodyPt)
    {
        var fs = Math.Max(16, (int)Math.Round(bodyPt * 2));
        var sb = new System.Text.StringBuilder();
        sb.Append(@"{\rtf1\ansi\deff0\viewkind4\uc1{\fonttbl{\f0\fnil\fcharset0 Segoe UI;}}");
        var first = true;
        foreach (var line in (text ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            if (!first)
            {
                sb.Append(@"\par ");
            }

            first = false;
            var heading = line.StartsWith("**", StringComparison.Ordinal) && line.EndsWith("**", StringComparison.Ordinal) && line.Length > 4 && !line[2..^2].Contains("**", StringComparison.Ordinal);
            sb.Append(@"\pard\widctlpar\ql\sa80\li0\ri80\f0\fs").Append(heading ? fs + 2 : fs).Append(' ');
            AppendMarkedRtf(sb, line);
        }

        sb.Append('}');
        return sb.ToString();
    }

    public static ImageList CreateStatusImages()
    {
        var list = new ImageList
        {
            ColorDepth = ColorDepth.Depth32Bit,
            ImageSize = new Size(16, 16),
        };
        list.Images.Add("none", StatusIcon(Muted, "none"));
        list.Images.Add("pass", StatusIcon(Success, "pass"));
        list.Images.Add("fail", StatusIcon(Danger, "fail"));
        list.Images.Add("warn", StatusIcon(Warning, "warn"));
        return list;
    }

    static Bitmap StatusIcon(Color color, string kind)
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        using var brush = new SolidBrush(color);
        using var pen = new Pen(Color.White, 1.8f);
        pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
        pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
        if (kind == "none")
        {
            using var outline = new Pen(color, 1.5f);
            g.DrawEllipse(outline, 3, 3, 10, 10);
            return bmp;
        }

        g.FillEllipse(brush, 1, 1, 14, 14);
        if (kind == "pass")
        {
            g.DrawLines(pen, new[] { new Point(4, 8), new Point(7, 11), new Point(12, 5) });
        }
        else if (kind == "fail")
        {
            g.DrawLine(pen, 5, 5, 11, 11);
            g.DrawLine(pen, 11, 5, 5, 11);
        }
        else
        {
            g.FillRectangle(Brushes.White, 7, 4, 2, 5);
            g.FillRectangle(Brushes.White, 7, 11, 2, 2);
        }

        return bmp;
    }

    public static string HelpStepsRtf(IReadOnlyList<string> steps, float bodyPt)
    {
        var fs = Math.Max(16, (int)Math.Round(bodyPt * 2));
        var sb = new System.Text.StringBuilder();
        sb.Append(@"{\rtf1\ansi\deff0\viewkind4\uc1{\fonttbl{\f0\fnil\fcharset0 Segoe UI;}}");
        sb.Append(@"\pard\ql\sl0\slmult1\sa240\sb40\li0\ri120\cf0\f0\fs").Append(fs).Append(' ');
        for (var i = 0; i < steps.Count; i++)
        {
            sb.Append(i + 1).Append(". ");
            AppendMarkedRtf(sb, steps[i] ?? "");
            sb.Append(@"\par ");
        }

        sb.Append('}');
        return sb.ToString();
    }

    static void AppendMarkedRtf(System.Text.StringBuilder sb, string text)
    {
        var parts = text.Split("**");
        for (var i = 0; i < parts.Length; i++)
        {
            if (i % 2 == 1)
            {
                sb.Append(@"\b ");
            }

            foreach (var ch in parts[i])
            {
                if (ch is '\\' or '{' or '}')
                {
                    sb.Append('\\').Append(ch);
                }
                else if (ch > 127)
                {
                    sb.Append(@"\u").Append((int)ch).Append('?');
                }
                else
                {
                    sb.Append(ch);
                }
            }

            if (i % 2 == 1)
            {
                sb.Append(@"\b0 ");
            }
        }
    }

    public static Panel SearchField(TextBox box, Action onFind)
    {
        var host = new Panel
        {
            Height = 36,
            BackColor = Line,
            Padding = new Padding(1),
        };
        var inner = new Panel { Dock = DockStyle.Fill, BackColor = Card };
        var find = new Button
        {
            Dock = DockStyle.Right,
            Width = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Card,
            Cursor = Cursors.Hand,
            Text = "",
            TabStop = false,
            AccessibleName = "Tìm",
            UseMnemonic = false,
        };
        find.FlatAppearance.BorderSize = 0;
        find.FlatAppearance.MouseOverBackColor = Color.FromArgb(236, 244, 250);
        find.Paint += (_, e) => PaintSearchMark(e.Graphics, find.ClientRectangle, Muted);
        find.Click += (_, _) => onFind();
        DockTips.SetToolTip(find, "Tìm kỹ năng");
        box.BorderStyle = BorderStyle.None;
        box.Dock = DockStyle.Fill;
        box.Font = BodyFont;
        inner.Controls.Add(box);
        inner.Controls.Add(find);
        host.Controls.Add(inner);
        RoundControl(host, 8);
        return host;
    }

    public static void PaintSearchMark(Graphics g, Rectangle r, Color color)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var cx = r.X + r.Width / 2 - 1;
        var cy = r.Y + r.Height / 2 - 1;
        using var pen = new Pen(color, 1.7f);
        pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
        pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
        g.DrawEllipse(pen, cx - 6, cy - 7, 11, 11);
        g.DrawLine(pen, cx + 3, cy + 3, cx + 8, cy + 8);
    }

    public static Panel EmptyHint(string title, string lead)
    {
        var box = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Card,
            MinimumSize = new Size(160, 150),
            Padding = new Padding(8, 12, 8, 8),
        };
        var icon = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Card };
        icon.Paint += (_, e) => PaintEmptyDoc(e.Graphics, icon.ClientRectangle);
        var head = new Label
        {
            Text = title,
            Font = HeadFont,
            ForeColor = Text,
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.TopCenter,
            UseMnemonic = false,
        };
        var body = new Label
        {
            Text = lead,
            Font = SmallFont,
            ForeColor = Muted,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopCenter,
            UseMnemonic = false,
        };
        BindWrap(body, 4);
        box.Controls.Add(body);
        box.Controls.Add(head);
        box.Controls.Add(icon);
        return box;
    }

    public static void PaintEmptyDoc(Graphics g, Rectangle r)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var w = 28;
        var h = 34;
        var x = r.X + (r.Width - w) / 2;
        var y = r.Y + Math.Max(4, (r.Height - h) / 2);
        using var fill = new SolidBrush(Color.FromArgb(36, Text));
        using var pen = new Pen(Color.FromArgb(70, Text), 1.4f);
        g.FillRectangle(fill, x, y, w, h);
        g.DrawRectangle(pen, x, y, w, h);
        g.DrawLine(pen, x + 6, y + 10, x + w - 6, y + 10);
        g.DrawLine(pen, x + 6, y + 16, x + w - 6, y + 16);
        g.DrawLine(pen, x + 6, y + 22, x + w - 10, y + 22);
    }

    public sealed class PercentTrack : Panel
    {
        int? _pct;
        string _caption = "—";

        public PercentTrack()
        {
            Height = 22;
            DoubleBuffered = true;
            BackColor = Card;
        }

        public void Set(int? pct, string caption)
        {
            _pct = pct is { } n ? Math.Clamp(n, 0, 100) : null;
            _caption = caption;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var labelW = 72;
            var track = new Rectangle(0, Height / 2 - 5, Math.Max(20, Width - labelW - 4), 10);
            using (var bg = new SolidBrush(Color.FromArgb(232, 226, 218)))
            {
                g.FillRectangle(bg, track);
            }

            if (_pct is { } n && n > 0)
            {
                var fillW = Math.Max(4, track.Width * n / 100);
                using var fill = new SolidBrush(Success);
                g.FillRectangle(fill, track.X, track.Y, fillW, track.Height);
            }

            TextRenderer.DrawText(
                g,
                _caption,
                SmallFont,
                new Rectangle(track.Right + 4, 0, labelW, Height),
                Muted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }

    public static Color ReviewFill(string tone) => tone switch
    {
        "correct" => ReviewCorrect,
        "pitfall" => ReviewPitfall,
        "yours" => ReviewYours,
        "result" => ReviewResult,
        _ => Card,
    };

    public static Color ReviewAccent(string tone) => tone switch
    {
        "correct" => Success,
        "pitfall" => Danger,
        "yours" => Warning,
        "result" => Primary,
        _ => Line,
    };

    public static Panel ReviewCard(string title, string body, string tone)
    {
        var fill = ReviewFill(tone);
        var accent = ReviewAccent(tone);
        var shell = new Panel
        {
            BackColor = fill,
            Margin = new Padding(0, 0, 0, 8),
            Padding = Padding.Empty,
            Tag = "review",
        };
        var bar = new Panel { Dock = DockStyle.Left, Width = 5, BackColor = accent };
        var inner = new Panel { Dock = DockStyle.Fill, BackColor = fill, Padding = new Padding(10, 8, 10, 8) };
        var head = new Label
        {
            Text = title,
            Font = HeadFont,
            ForeColor = Text,
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 22,
            UseMnemonic = false,
        };
        var box = new RichTextBox
        {
            Dock = DockStyle.Top,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = fill,
            ForeColor = Text,
            ScrollBars = RichTextBoxScrollBars.None,
            WordWrap = true,
            DetectUrls = false,
            TabStop = false,
            Font = BodyFont,
        };
        try
        {
            box.Rtf = MarkedDocumentRtf(body, 10.5f);
        }
        catch
        {
            box.Text = StripMarks(body);
        }

        void Fit(object? _, EventArgs e)
        {
            var w = Math.Max(80, inner.ClientSize.Width - inner.Padding.Horizontal);
            var h = MeasureH(StripMarks(body), BodyFont, w) + 12;
            if (box.Height != h)
            {
                box.Height = h;
            }

            var next = inner.Padding.Vertical + head.Height + box.Height + 4;
            if (shell.Height != next)
            {
                shell.Height = Math.Max(56, next);
            }
        }

        inner.Controls.Add(box);
        inner.Controls.Add(head);
        shell.Controls.Add(inner);
        shell.Controls.Add(bar);
        RoundControl(shell, 8);
        inner.Resize += Fit;
        shell.Resize += Fit;
        Fit(null, EventArgs.Empty);
        return shell;
    }

    public static FlowLayoutPanel DockChip()
    {
        var chip = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            BackColor = Color.White,
            Padding = new Padding(4, 4, 4, 4),
            Margin = new Padding(0, 0, 0, 2),
        };
        RoundControl(chip, 10);
        return chip;
    }

    public static Button IconBtn(NavIcon icon, string tip)
    {
        var btn = DockSquare(icon, tip, DockBlue);
        btn.Size = new Size(36, 32);
        return btn;
    }

    public static void SetIconActive(Button btn, bool on)
    {
        btn.BackColor = on ? Primary : NavDark;
        btn.Invalidate();
    }

    static void DrawNavIcon(Graphics g, Rectangle r, NavIcon icon, Color color, Color cutout)
    {
        using var pen = new Pen(color, 1.6f);
        using var brush = new SolidBrush(color);
        var cx = r.X + r.Width / 2;
        var cy = r.Y + r.Height / 2;
        var frame = new Rectangle(cx - 10, cy - 8, 20, 16);
        switch (icon)
        {
            case NavIcon.Home:
                g.FillPolygon(brush, new[] { new Point(cx, cy - 8), new Point(cx - 8, cy + 1), new Point(cx + 8, cy + 1) });
                g.FillRectangle(brush, cx - 5, cy, 10, 8);
                break;
            case NavIcon.Dock:
                g.DrawRectangle(pen, new Rectangle(cx - 9, cy - 8, 12, 10));
                g.FillRectangle(brush, new Rectangle(cx - 3, cy - 4, 12, 10));
                g.FillPolygon(brush, new[] { new Point(cx + 10, cy + 8), new Point(cx + 14, cy + 4), new Point(cx + 6, cy + 4) });
                break;
            case NavIcon.Left:
                g.DrawRectangle(pen, frame);
                g.FillRectangle(brush, new Rectangle(frame.X + 1, frame.Y + 1, 7, frame.Height - 1));
                break;
            case NavIcon.Right:
                g.DrawRectangle(pen, frame);
                g.FillRectangle(brush, new Rectangle(frame.Right - 8, frame.Y + 1, 7, frame.Height - 1));
                break;
            case NavIcon.Top:
                g.DrawRectangle(pen, frame);
                g.FillRectangle(brush, new Rectangle(frame.X + 1, frame.Y + 1, frame.Width - 1, 5));
                break;
            case NavIcon.Bottom:
                g.DrawRectangle(pen, frame);
                g.FillRectangle(brush, new Rectangle(frame.X + 1, frame.Bottom - 6, frame.Width - 1, 5));
                break;
            case NavIcon.Save:
                g.FillRectangle(brush, new Rectangle(cx - 8, cy - 8, 16, 16));
                using (var hole = new SolidBrush(cutout))
                {
                    g.FillRectangle(hole, new Rectangle(cx - 4, cy - 6, 8, 5));
                    g.FillRectangle(hole, new Rectangle(cx - 5, cy + 2, 10, 5));
                }
                break;
            case NavIcon.Tasks:
                g.DrawRectangle(pen, new Rectangle(cx - 8, cy - 8, 16, 16));
                g.DrawLine(pen, cx - 5, cy - 3, cx + 5, cy - 3);
                g.DrawLine(pen, cx - 5, cy + 1, cx + 5, cy + 1);
                g.DrawLine(pen, cx - 5, cy + 5, cx + 5, cy + 5);
                break;
            case NavIcon.Refresh:
                g.DrawArc(pen, cx - 7, cy - 7, 14, 14, 40, 260);
                g.FillPolygon(brush, new[] { new Point(cx + 6, cy - 8), new Point(cx + 11, cy - 2), new Point(cx + 2, cy - 2) });
                break;
            case NavIcon.Pin:
                g.FillEllipse(brush, cx - 3, cy - 8, 6, 6);
                g.FillRectangle(brush, cx - 2, cy - 3, 4, 8);
                g.DrawLine(pen, cx, cy + 5, cx, cy + 9);
                break;
            case NavIcon.Menu:
                g.DrawLine(pen, cx - 8, cy - 5, cx + 8, cy - 5);
                g.DrawLine(pen, cx - 8, cy, cx + 8, cy);
                g.DrawLine(pen, cx - 8, cy + 5, cx + 8, cy + 5);
                break;
            case NavIcon.Settings:
                g.DrawEllipse(pen, cx - 4, cy - 4, 8, 8);
                for (var i = 0; i < 6; i++)
                {
                    var a = i * Math.PI / 3.0;
                    g.DrawLine(
                        pen,
                        cx + (float)Math.Cos(a) * 5,
                        cy + (float)Math.Sin(a) * 5,
                        cx + (float)Math.Cos(a) * 9,
                        cy + (float)Math.Sin(a) * 9);
                }

                break;
            case NavIcon.Help:
                g.DrawEllipse(pen, cx - 8, cy - 8, 16, 16);
                TextRenderer.DrawText(
                    g,
                    "?",
                    new Font("Segoe UI", 10f, FontStyle.Bold),
                    new Rectangle(cx - 8, cy - 9, 17, 18),
                    color,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                break;
            case NavIcon.Hint:
                g.FillEllipse(brush, cx - 6, cy - 8, 12, 12);
                g.FillRectangle(brush, cx - 3, cy + 3, 6, 3);
                g.DrawLine(pen, cx - 3, cy + 8, cx + 3, cy + 8);
                break;
            case NavIcon.Share:
                g.DrawLines(pen, new[] { new Point(cx - 6, cy + 4), new Point(cx + 2, cy - 4), new Point(cx + 2, cy + 1) });
                g.DrawLine(pen, cx + 2, cy - 4, cx + 8, cy - 4);
                break;
            case NavIcon.Back:
                g.DrawLines(pen, new[] { new Point(cx + 4, cy - 7), new Point(cx - 6, cy), new Point(cx + 4, cy + 7) });
                break;
            case NavIcon.Next:
                g.DrawLines(pen, new[] { new Point(cx - 4, cy - 7), new Point(cx + 6, cy), new Point(cx - 4, cy + 7) });
                break;
            case NavIcon.Expand:
                g.DrawRectangle(pen, frame);
                g.DrawLine(pen, frame.X + 4, cy, frame.Right - 4, cy);
                g.DrawLine(pen, cx, frame.Y + 3, cx, frame.Bottom - 3);
                break;
            case NavIcon.Collapse:
                g.DrawRectangle(pen, frame);
                g.FillRectangle(brush, new Rectangle(frame.X + 1, frame.Bottom - 6, frame.Width - 1, 5));
                break;
            case NavIcon.Check:
                g.DrawLines(pen, new[] { new Point(cx - 6, cy), new Point(cx - 1, cy + 5), new Point(cx + 7, cy - 6) });
                break;
            case NavIcon.Submit:
                g.DrawLine(pen, cx, cy + 6, cx, cy - 6);
                g.DrawLines(pen, new[] { new Point(cx - 5, cy - 1), new Point(cx, cy - 6), new Point(cx + 5, cy - 1) });
                g.DrawLine(pen, cx - 7, cy + 7, cx + 7, cy + 7);
                break;
        }
    }
}

enum NavIcon
{
    Home,
    Dock,
    Left,
    Right,
    Top,
    Bottom,
    Save,
    Tasks,
    Refresh,
    Pin,
    Menu,
    Help,
    Hint,
    Share,
    Back,
    Next,
    Expand,
    Collapse,
    Check,
    Submit,
    Settings,
}
