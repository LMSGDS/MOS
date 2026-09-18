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
    public const int ClusterW = 200;
    public const int ClusterH = 80;
    public const int ClusterMargin = 6;
    public const int HelpW = 240;
    public const int HelpH = 132;
    public const int SummaryW = 1020;
    public const int SummaryH = 680;
    public const int ExpandedSideW = 340;
    public const int MinWord = 400;
    public const int HubMinW = 960;
    public const int HubMinH = 600;
    public const int OverlayMinW = 80;
    public const int OverlayMinH = 48;
    public const int RefIcon = 32;
    public const int RefIconGap = 2;
    public const int RefChromePad = 3;
    public const int OverlayCapPct = 22;

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
        var icon = Scale(RefIcon, fit, 26, 40);
        var gap = Scale(RefIconGap, fit, 1, 4);
        var pad = Scale(RefChromePad, fit, 2, 6);
        var capW = Math.Max(160, work.W * OverlayCapPct / 100);
        var capH = Math.Max(72, work.H * OverlayCapPct / 100);
        var clusterW = Math.Min(
            240,
            Math.Max(Scale(ClusterW, fit, 160, Math.Min(capW, 240)), 5 * (icon + 2 * gap) + 2 * pad));
        var clusterH = Math.Min(
            100,
            Math.Max(Scale(ClusterH, fit, 64, Math.Min(capH, 100)), 2 * (icon + 2 * gap) + 2 * pad));
        var helpW = Scale(HelpW, fit, clusterW, Math.Min(capW, 260));
        var helpH = Scale(HelpH, fit, 88, Math.Min(capH, 148));
        var margin = Scale(ClusterMargin, fit, 4, 10);
        var side = Scale(ExpandedSideW, fit, 220, Math.Max(220, work.W / 3));
        var edge = Math.Max(Scale(200, fit, 140, 420), (int)(work.H * 0.22));
        var summaryW = Math.Min(Scale(SummaryW, fit, 480, work.W - 40), Math.Max(480, work.W - 40));
        var summaryH = Math.Min(Scale(SummaryH, fit, 360, work.H - 40), Math.Max(360, work.H - 40));
        return new NavMetrics(
            clusterW, clusterH, margin,
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

    public static (int W, int H) SizeFor(Rect work, string state, bool compact = true)
    {
        var nav = Measure(work);
        state = (state ?? "bottom").ToLowerInvariant();
        compact = compact || state == "minimized";
        if (state is "left" or "right")
        {
            return (compact ? nav.ClusterW : nav.ExpandedSideW, work.H);
        }

        return (work.W, compact ? nav.ClusterH : nav.ExpandedEdgeH);
    }

    public static (Rect Dock, Rect Word) Compute(Rect work, string state, bool compact = false, float scale = 1f)
    {
        state = (state ?? "bottom").ToLowerInvariant();
        compact = compact || state == "minimized";
        var nav = Measure(ScaleWork(work, scale));
        var side = compact ? nav.ClusterW : nav.ExpandedSideW;
        var edge = compact ? nav.ClusterH : nav.ExpandedEdgeH;
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

        return (dock, WordBeside(work, dock, state));
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
        var m = Math.Max(4, margin);
        state = (state ?? "bottom").ToLowerInvariant();
        w = Math.Max(OverlayMinW, w);
        h = Math.Max(OverlayMinH, h);
        return state switch
        {
            "left" => new Rect(work.X + m, work.Y + Math.Max(m, (work.H - h) / 2), w, h),
            "right" => new Rect(work.Right - w - m, work.Y + Math.Max(m, (work.H - h) / 2), w, h),
            "top" => new Rect(work.X + Math.Max(m, (work.W - w) / 2), work.Y + m, w, h),
            _ => new Rect(work.X + Math.Max(m, (work.W - w) / 2), work.Bottom - h - m, w, h),
        };
    }

    public static Rect GrowForHelp(Rect dock, Rect work, string state, float scale = 1f)
    {
        var nav = Measure(ScaleWork(work, scale));
        state = (state ?? "bottom").ToLowerInvariant();
        if (state is "left" or "right")
        {
            var w = Math.Min(Cap(work.W, dock.W + nav.HelpW, dock.W), Math.Max(dock.W, work.W - MinWord));
            var x = state == "left" ? work.X : work.Right - w;
            return new Rect(x, work.Y, w, work.H);
        }

        var h = Math.Min(Cap(work.H, dock.H + nav.HelpH, dock.H), Math.Max(dock.H, work.H - MinWord));
        var y = state == "top" ? work.Y : work.Bottom - h;
        return new Rect(work.X, y, work.W, h);
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

    static int Cap(int workSpan, int wanted, int min) =>
        Math.Max(min, Math.Min(wanted, workSpan * OverlayCapPct / 100));

    static Rect ClampWord(Rect word, Rect work)
    {
        var w = Math.Max(MinWord, Math.Min(word.W, work.W));
        var h = Math.Max(MinWord, Math.Min(word.H, work.H));
        var x = Math.Max(work.X, Math.Min(word.X, work.Right - w));
        var y = Math.Max(work.Y, Math.Min(word.Y, work.Bottom - h));
        return new Rect(x, y, w, h);
    }
}
