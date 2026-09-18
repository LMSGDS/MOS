namespace MosDock;

public readonly record struct Rect(int X, int Y, int W, int H)
{
    public int Right => X + W;
    public int Bottom => Y + H;
}

/// <summary>Kích thước thanh Navigation đã nhân tỉ lệ từ working area máy.</summary>
public readonly record struct NavMetrics(
    int ClusterW,
    int ClusterH,
    int Margin,
    int HelpW,
    int HelpH,
    int ExpandedSideW,
    int ExpandedEdgeH,
    int SummaryW,
    int SummaryH,
    int Icon,
    int IconGap,
    int ChromePad,
    float Fit);

public static class LayoutMath
{
    public const int RefWorkW = 1920;
    public const int RefWorkH = 1040;
    public const int BarHPct = 65;
    public const int BarWPct = 38;
    public const int BarHMin = 52;
    public const int BarHMax = 72;
    public const int BarWMin = 52;
    public const int BarWMax = 72;
    public const int HelpHPct = 90;
    public const int HelpWPct = 90;
    public const int ClusterW = 72;
    public const int ClusterH = 68;
    public const int ClusterMargin = 0;
    public const int HelpW = 173;
    public const int HelpH = 94;
    public const int SummaryW = 1020;
    public const int SummaryH = 680;
    public const int ExpandedSideW = 340;
    public const int MinWord = 400;
    public const int HubMinW = 960;
    public const int HubMinH = 600;
    public const int OverlayMinW = 48;
    public const int OverlayMinH = 48;
    public const int RefIcon = 32;
    public const int RefIconGap = 2;
    public const int RefChromePad = 4;
    public const int OverlayCapPct = 16;

    public static Rect FromScreen(System.Drawing.Rectangle wa) =>
        new(wa.X, wa.Y, wa.Width, wa.Height);

    public static int Px(int logical, float scale) =>
        Math.Max(1, (int)Math.Round(logical * Math.Max(0.5f, scale)));

    public static float Fit(Rect work)
    {
        var w = Math.Max(1, work.W);
        var h = Math.Max(1, work.H);
        var fit = Math.Min(w / (float)RefWorkW, h / (float)RefWorkH);
        return Math.Clamp(fit, 0.55f, 2.4f);
    }

    public static int Scale(int reference, float fit, int min, int max) =>
        Math.Clamp((int)Math.Round(reference * fit), min, Math.Max(min, max));

    public static NavMetrics Measure(Rect work)
    {
        var fit = Fit(work);
        var barH = Math.Clamp((int)Math.Round(work.H * BarHPct / 1000.0), BarHMin, BarHMax);
        var barW = Math.Clamp((int)Math.Round(work.W * BarWPct / 1000.0), BarWMin, BarWMax);
        const int pad = 4;
        const int gap = 2;
        var icon = Math.Clamp(Math.Min(barH, barW) - 2 * pad, 24, 40);
        var capW = Math.Max(barW, work.W * OverlayCapPct / 100);
        var capH = Math.Max(barH, work.H * OverlayCapPct / 100);
        var helpW = Math.Max(0, Math.Min((int)Math.Round(work.W * HelpWPct / 1000.0), capW - barW));
        var helpH = Math.Max(0, Math.Min((int)Math.Round(work.H * HelpHPct / 1000.0), capH - barH));
        var side = Scale(ExpandedSideW, fit, 220, Math.Max(220, work.W / 4));
        var edge = Math.Max(barH + helpH, work.H * OverlayCapPct / 100);
        var summaryW = Math.Min(Scale(SummaryW, fit, 480, work.W - 40), Math.Max(480, work.W - 40));
        var summaryH = Math.Min(Scale(SummaryH, fit, 360, work.H - 40), Math.Max(360, work.H - 40));
        return new NavMetrics(
            barW, barH, 0,
            helpW, helpH,
            side, edge,
            summaryW, summaryH,
            icon, gap, pad,
            fit);
    }

    public static bool Horizontal(string state)
    {
        state = (state ?? "bottom").ToLowerInvariant();
        return state is not "left" and not "right";
    }

    public static int ThicknessOf(Rect dock, string state) =>
        Horizontal(state) ? dock.H : dock.W;

    public static int ClampThickness(Rect work, string state, int thickness)
    {
        var min = Horizontal(state) ? Math.Max(OverlayMinH, BarHMin) : Math.Max(OverlayMinW, BarWMin);
        var span = Horizontal(state) ? work.H : work.W;
        var cap = span * OverlayCapPct / 100;
        var max = Math.Max(min, Math.Min(cap, span - MinWord));
        return Math.Clamp(thickness, min, max);
    }

    public static Rect WithThickness(Rect dock, Rect work, string state, int thickness)
    {
        thickness = ClampThickness(work, state, thickness);
        return Horizontal(state)
            ? PinToWork(new Rect(dock.X, dock.Y, dock.W, thickness), work, state)
            : PinToWork(new Rect(dock.X, dock.Y, thickness, dock.H), work, state);
    }

    public static (int W, int H) SizeFor(Rect work, string state, bool compact = true, int? thickness = null)
    {
        var nav = Measure(work);
        state = (state ?? "bottom").ToLowerInvariant();
        compact = compact || state == "minimized";
        if (state is "left" or "right")
        {
            var w = compact ? nav.ClusterW : nav.ExpandedSideW;
            if (compact && thickness is int t)
            {
                w = ClampThickness(work, state, t);
            }

            return (w, work.H);
        }

        var h = compact ? nav.ClusterH : nav.ExpandedEdgeH;
        if (compact && thickness is int custom)
        {
            h = ClampThickness(work, state, custom);
        }

        return (work.W, h);
    }

    public static (Rect Dock, Rect Word) Compute(Rect work, string state, bool compact = false, float scale = 1f, int? thickness = null)
    {
        state = (state ?? "bottom").ToLowerInvariant();
        compact = compact || state == "minimized";
        var nav = Measure(ScaleWork(work, scale));
        var side = compact ? nav.ClusterW : nav.ExpandedSideW;
        var edge = compact ? nav.ClusterH : nav.ExpandedEdgeH;
        if (compact && thickness is int custom)
        {
            var t = ClampThickness(work, state, custom);
            if (state is "left" or "right")
            {
                side = t;
            }
            else
            {
                edge = t;
            }
        }
        Rect dock;
        if (state == "left")
        {
            dock = new Rect(work.X, work.Y, side, work.H);
        }
        else if (state == "right")
        {
            dock = new Rect(work.Right - side, work.Y, side, work.H);
        }
        else if (state == "top")
        {
            dock = new Rect(work.X, work.Y, work.W, edge);
        }
        else
        {
            dock = new Rect(work.X, work.Bottom - edge, work.W, edge);
        }

        dock = PinToWork(dock, work, state);
        return (dock, WordBeside(work, dock, state));
    }

    /// <summary>
    /// Top/bottom luôn bung 100% chiều ngang working area; left/right bung 100% chiều dọc.
    /// Cụm icon giữa màn (Place cũ) bị kéo sát cạnh và kéo hết cạnh dài.
    /// </summary>
    public static Rect PinToWork(Rect dock, Rect work, string state)
    {
        state = (state ?? "bottom").ToLowerInvariant();
        if (state is "left" or "right")
        {
            var w = Math.Clamp(Math.Max(OverlayMinW, dock.W), OverlayMinW, Math.Max(OverlayMinW, work.W));
            var x = state == "left" ? work.X : work.Right - w;
            return new Rect(x, work.Y, w, work.H);
        }

        var h = Math.Clamp(Math.Max(OverlayMinH, dock.H), OverlayMinH, Math.Max(OverlayMinH, work.H));
        var y = state == "top" ? work.Y : work.Bottom - h;
        return new Rect(work.X, y, work.W, h);
    }

    /// <summary>Word occupies the leftover working area; Navigation never covers the document.</summary>
    public static Rect WordBeside(Rect work, Rect dock, string state)
    {
        state = (state ?? "bottom").ToLowerInvariant();
        Rect word = state switch
        {
            "left" => new Rect(dock.Right, work.Y, work.Right - dock.Right, work.H),
            "right" => new Rect(work.X, work.Y, dock.X - work.X, work.H),
            "top" => new Rect(work.X, dock.Bottom, work.W, work.Bottom - dock.Bottom),
            _ => new Rect(work.X, work.Y, work.W, dock.Y - work.Y),
        };
        return ClampWord(word, work);
    }

    public static Rect Cluster(Rect work, string state, float scale = 1f)
    {
        var nav = Measure(ScaleWork(work, scale));
        return Place(work, state, nav.ClusterW, nav.ClusterH, nav.Margin);
    }

    public static Rect Place(Rect work, string state, int w, int h, int margin = ClusterMargin)
    {
        _ = margin;
        return PinToWork(new Rect(work.X, work.Y, Math.Max(OverlayMinW, w), Math.Max(OverlayMinH, h)), work, state);
    }

    public static Rect GrowForHelp(Rect dock, Rect work, string state, float scale = 1f)
    {
        var nav = Measure(ScaleWork(work, scale));
        state = (state ?? "bottom").ToLowerInvariant();
        if (state is "left" or "right")
        {
            var cap = Math.Max(nav.ClusterW, work.W * OverlayCapPct / 100);
            var w = Math.Min(Math.Max(dock.W, dock.W + nav.HelpW), Math.Min(cap, Math.Max(dock.W, work.W - MinWord)));
            var x = state == "left" ? work.X : work.Right - w;
            return PinToWork(new Rect(x, work.Y, w, work.H), work, state);
        }

        var capH = Math.Max(nav.ClusterH, work.H * OverlayCapPct / 100);
        var h = Math.Min(Math.Max(dock.H, dock.H + nav.HelpH), Math.Min(capH, Math.Max(dock.H, work.H - MinWord)));
        var y = state == "top" ? work.Y : work.Bottom - h;
        return PinToWork(new Rect(work.X, y, work.W, h), work, state);
    }

    static Rect ScaleWork(Rect work, float scale)
    {
        if (scale is > 0.99f and < 1.01f)
        {
            return work;
        }

        var s = Math.Max(0.5f, scale);
        return new Rect(work.X, work.Y, Math.Max(1, (int)Math.Round(work.W * s)), Math.Max(1, (int)Math.Round(work.H * s)));
    }

    static Rect ClampWord(Rect word, Rect work)
    {
        var w = Math.Max(MinWord, Math.Min(word.W, work.W));
        var h = Math.Max(MinWord, Math.Min(word.H, work.H));
        var x = Math.Max(work.X, Math.Min(word.X, work.Right - w));
        var y = Math.Max(work.Y, Math.Min(word.Y, work.Bottom - h));
        return new Rect(x, y, w, h);
    }
}
