namespace MosDock;

public readonly record struct Rect(int X, int Y, int W, int H)
{
    public int Right => X + W;
    public int Bottom => Y + H;
}

public static class LayoutMath
{
    public const double BottomRatio = 0.28;
    public const double SideRatio = 0.30;
    public const int ControlsH = 96;
    public const int ControlsW = 248;
    public const int MinWord = 400;

    public static (Rect Dock, Rect Word) Compute(Rect work, string state, bool compact = false)
    {
        state = (state ?? "bottom").ToLowerInvariant();
        compact = compact || state == "minimized";
        Rect dock;
        Rect word;
        if (state == "left")
        {
            var dockW = compact ? ControlsW : Math.Max(280, (int)(work.W * SideRatio));
            dock = new Rect(work.X, work.Y, dockW, work.H);
            word = new Rect(dock.Right, work.Y, work.W - dockW, work.H);
        }
        else if (state == "right")
        {
            var dockW = compact ? ControlsW : Math.Max(280, (int)(work.W * SideRatio));
            dock = new Rect(work.Right - dockW, work.Y, dockW, work.H);
            word = new Rect(work.X, work.Y, work.W - dockW, work.H);
        }
        else if (state == "minimized")
        {
            dock = new Rect(work.X, work.Bottom - ControlsH, work.W, ControlsH);
            word = new Rect(work.X, work.Y, work.W, work.H - ControlsH);
        }
        else
        {
            var dockH = compact ? ControlsH : Math.Max(180, (int)(work.H * BottomRatio));
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
