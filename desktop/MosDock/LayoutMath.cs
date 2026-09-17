namespace MosDock;

public readonly record struct Rect(int X, int Y, int W, int H)
{
    public int Right => X + W;
    public int Bottom => Y + H;
}

public static class LayoutMath
{
    public const int IconBarH = 48;
    public const int IconBarW = 360;
    public const int ExpandedSideW = 340;
    public const int MinWord = 400;

    public static (Rect Dock, Rect Word) Compute(Rect work, string state, bool compact = false)
    {
        state = (state ?? "bottom").ToLowerInvariant();
        compact = compact || state == "minimized";
        Rect dock;
        Rect word;
        if (state == "left")
        {
            if (compact)
            {
                dock = new Rect(work.X, work.Bottom - IconBarH, IconBarW, IconBarH);
                word = new Rect(work.X, work.Y, work.W, work.H - IconBarH);
            }
            else
            {
                dock = new Rect(work.X, work.Y, ExpandedSideW, work.H);
                word = new Rect(dock.Right, work.Y, work.W - ExpandedSideW, work.H);
            }
        }
        else if (state == "right")
        {
            if (compact)
            {
                dock = new Rect(work.Right - IconBarW, work.Bottom - IconBarH, IconBarW, IconBarH);
                word = new Rect(work.X, work.Y, work.W, work.H - IconBarH);
            }
            else
            {
                dock = new Rect(work.Right - ExpandedSideW, work.Y, ExpandedSideW, work.H);
                word = new Rect(work.X, work.Y, work.W - ExpandedSideW, work.H);
            }
        }
        else if (state == "minimized")
        {
            dock = new Rect(work.X, work.Bottom - IconBarH, work.W, IconBarH);
            word = new Rect(work.X, work.Y, work.W, work.H - IconBarH);
        }
        else
        {
            var dockH = compact ? IconBarH : Math.Max(200, (int)(work.H * 0.22));
            dock = new Rect(work.X, work.Bottom - dockH, work.W, dockH);
            word = new Rect(work.X, work.Y, work.W, work.H - dockH);
        }

        return (dock, ClampWord(word, work));
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
