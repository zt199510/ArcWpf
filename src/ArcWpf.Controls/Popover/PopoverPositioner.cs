using System.Windows;
namespace ArcWpf.Controls;

internal readonly record struct PopoverPlacement(Rect Bounds, PopoverSide Side);

internal static class PopoverPositioner
{
    // All arguments are in one common screen-DIP coordinate system, including negative monitor origins.
    public static PopoverPlacement Calculate(Rect anchor, Size desired, Rect workArea, PopoverSide side,
        PopoverAlignment alignment, double offset, double padding, bool rtl)
    {
        double insetX = Math.Min(padding, workArea.Width / 2);
        double insetY = Math.Min(padding, workArea.Height / 2);
        var work = new Rect(workArea.X + insetX, workArea.Y + insetY,
            Math.Max(0, workArea.Width - 2 * insetX), Math.Max(0, workArea.Height - 2 * insetY));
        double Available(PopoverSide s) => Math.Max(0, s switch
        {
            PopoverSide.Top => anchor.Top - offset - work.Top,
            PopoverSide.Bottom => work.Bottom - anchor.Bottom - offset,
            PopoverSide.Left => anchor.Left - offset - work.Left,
            _ => work.Right - anchor.Right - offset
        });
        PopoverSide opposite = side switch { PopoverSide.Top => PopoverSide.Bottom, PopoverSide.Bottom => PopoverSide.Top,
            PopoverSide.Left => PopoverSide.Right, _ => PopoverSide.Left };
        double needed = side is PopoverSide.Top or PopoverSide.Bottom ? desired.Height : desired.Width;
        if (Available(side) < needed && Available(opposite) > Available(side)) { side = opposite; }
        bool vertical = side is PopoverSide.Top or PopoverSide.Bottom;
        double width = Math.Min(desired.Width, work.Width);
        double height = Math.Min(desired.Height, work.Height);
        double available = Available(side);
        // If the anchor consumes the whole work area, overlap it rather than make content entirely inaccessible.
        if (available > 0)
        {
            if (vertical) { height = Math.Min(height, available); } else { width = Math.Min(width, available); }
        }
        var horizontalAlign = rtl ? alignment switch { PopoverAlignment.Start => PopoverAlignment.End,
            PopoverAlignment.End => PopoverAlignment.Start, _ => alignment } : alignment;
        static double Align(double start, double end, double length, PopoverAlignment a) =>
            a switch { PopoverAlignment.Start => start, PopoverAlignment.End => end - length, _ => (start + end - length) / 2 };
        double x = vertical ? Align(anchor.Left, anchor.Right, width, horizontalAlign) :
            side == PopoverSide.Left ? anchor.Left - offset - width : anchor.Right + offset;
        double y = !vertical ? Align(anchor.Top, anchor.Bottom, height, alignment) :
            side == PopoverSide.Top ? anchor.Top - offset - height : anchor.Bottom + offset;
        x = Math.Clamp(x, work.Left, Math.Max(work.Left, work.Right - width));
        y = Math.Clamp(y, work.Top, Math.Max(work.Top, work.Bottom - height));
        return new(new Rect(x, y, width, height), side);
    }
}

internal enum PopoverPhase { Closed, Opening, Open, Closing }

// Tokens invalidate pending close/open completions, including synchronous event-handler reentrancy.
internal sealed class PopoverLifecycle
{
    public PopoverPhase Phase { get; private set; }
    public int Version { get; private set; }
    public bool BeginOpen()
    {
        bool newCycle = Phase == PopoverPhase.Closed;
        ++Version;
        Phase = PopoverPhase.Opening;
        return newCycle;
    }
    public int BeginClose() { ++Version; if (Phase != PopoverPhase.Closed) { Phase = PopoverPhase.Closing; } return Version; }
    public bool CompleteOpen(int version)
    {
        if (version != Version || Phase != PopoverPhase.Opening) { return false; }
        Phase = PopoverPhase.Open;
        return true;
    }
    public bool CompleteClose(int version)
    {
        if (version != Version || Phase != PopoverPhase.Closing) { return false; }
        Phase = PopoverPhase.Closed;
        return true;
    }
}
