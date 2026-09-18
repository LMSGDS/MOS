namespace MosDock;

public readonly record struct Rect(int X, int Y, int W, int H)
{
    public int Right => X + W;
    public int Bottom => Y + H;
}

public static class LayoutMath
{
    public const int ClusterW = 260;
    public const int ClusterH = 108;
    public const int ClusterMargin = 8;
    public const int HelpW = 300;
    public const int HelpH = 248;
    public const int SummaryW = 780;
    public const int SummaryH = 560;
    public const int ExpandedSideW = 340;
    public const int MinWord = 400;
    public const int HubMinW = 960;
    public const int HubMinH = 600;
    public const int OverlayMinW = 80;
    public const int OverlayMinH = 48;

    public static int Px(int logical, float scale) =>
        Math.Max(1, (int)Math.Round(logical * Math.Max(0.5f, scale)));

    public static (Rect Dock, Rect Word) Compute(Rect work, string state, bool compact = false, float scale = 1f)
    {
        state = (state ?? "bottom").ToLowerInvariant();
        compact = compact || state == "minimized";
        if (compact)
        {
            return (Cluster(work, state, scale), work);
        }

        Rect dock;
        Rect word;
        var side = Px(ExpandedSideW, scale);
        if (state == "left")
        {
            dock = new Rect(work.X, work.Y, side, work.H);
            word = new Rect(dock.Right, work.Y, work.W - side, work.H);
        }
        else if (state == "right")
        {
            dock = new Rect(work.Right - side, work.Y, side, work.H);
            word = new Rect(work.X, work.Y, work.W - side, work.H);
        }
        else if (state == "top")
        {
            var dockH = Math.Max(Px(200, scale), (int)(work.H * 0.22));
            dock = new Rect(work.X, work.Y, work.W, dockH);
            word = new Rect(work.X, dock.Bottom, work.W, work.H - dockH);
        }
        else
        {
            var dockH = Math.Max(Px(200, scale), (int)(work.H * 0.22));
            dock = new Rect(work.X, work.Bottom - dockH, work.W, dockH);
            word = new Rect(work.X, work.Y, work.W, work.H - dockH);
        }

        return (dock, ClampWord(word, work));
    }

    public static Rect Cluster(Rect work, string state, float scale = 1f)
    {
        return Place(work, state, Px(ClusterW, scale), Px(ClusterH, scale));
    }

    public static Rect Place(Rect work, string state, int w, int h)
    {
        var m = ClusterMargin;
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
        var helpW = Px(HelpW, scale);
        var helpH = Px(HelpH, scale);
        var w = Math.Max(dock.W, helpW);
        var h = dock.H + helpH;
        w = Math.Min(w, Cap(work.W, w, Px(ClusterW, scale)));
        h = Math.Min(h, Cap(work.H, h, dock.H + Px(160, scale)));
        var x = dock.X - (w - dock.W) / 2;
        var y = state is "top" ? dock.Y : dock.Y - (h - dock.H);
        if (x < work.X)
        {
            x = work.X + ClusterMargin;
        }

        if (x + w > work.Right)
        {
            x = work.Right - w - ClusterMargin;
        }

        if (y < work.Y)
        {
            y = work.Y + ClusterMargin;
        }

        if (y + h > work.Bottom)
        {
            y = work.Bottom - h - ClusterMargin;
        }

        return new Rect(x, y, w, h);
    }

    static int Cap(int workSpan, int wanted, int min) =>
        Math.Max(min, Math.Min(wanted, workSpan * 2 / 5));

    static Rect ClampWord(Rect word, Rect work)
    {
        var w = Math.Max(MinWord, Math.Min(word.W, work.W));
        var h = Math.Max(MinWord, Math.Min(word.H, work.H));
        var x = Math.Max(work.X, Math.Min(word.X, work.Right - w));
        var y = Math.Max(work.Y, Math.Min(word.Y, work.Bottom - h));
        return new Rect(x, y, w, h);
    }
}
