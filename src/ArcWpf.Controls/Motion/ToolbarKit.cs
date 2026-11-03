using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcWpf.Controls;

// Shared by FloatingButtonGroup and ExpandingButtonGroup: a toolbar button that can be aria-disabled and still see the pointer.
internal class ToolbarButton(double radius, double borderThickness = 0) : ArcChromeButton(radius, borderThickness)
{
    private bool _inert;

    /// <summary>aria-disabled: dimmed and inert, but it keeps its hover, tooltip and place in the row, which IsEnabled would take away.</summary>
    public bool IsInert
    {
        get => _inert;
        set { _inert = value; Cursor = value ? System.Windows.Input.Cursors.No : System.Windows.Input.Cursors.Hand; Refresh(IsLoaded); }
    }

    public event Action<bool>? PressedChanged;

    protected override void OnIsPressedChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnIsPressedChanged(e);
        PressedChanged?.Invoke((bool)e.NewValue);
    }

    protected override void OnClick() { if (!_inert) { base.OnClick(); } }

    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    private sealed class Peer(ToolbarButton owner) : ButtonAutomationPeer(owner)
    {
        protected override bool IsEnabledCore() => !owner._inert && base.IsEnabledCore();
    }
}

internal static class ToolbarKeys
{
    /// <summary>The next stop for a toolbar key, wrapping and skipping unusable entries; -1 when the key does not move.</summary>
    public static int Next(IReadOnlyList<bool> usable, int index, System.Windows.Input.Key key, bool vertical)
    {
        var forward = vertical ? System.Windows.Input.Key.Down : System.Windows.Input.Key.Right;
        var backward = vertical ? System.Windows.Input.Key.Up : System.Windows.Input.Key.Left;
        if (key == System.Windows.Input.Key.Home) { for (int i = 0; i < usable.Count; i++) { if (usable[i]) { return i; } } return -1; }
        if (key == System.Windows.Input.Key.End) { for (int i = usable.Count - 1; i >= 0; i--) { if (usable[i]) { return i; } } return -1; }
        if (key != forward && key != backward) { return -1; }
        int step = key == forward ? 1 : -1;
        for (int offset = 1; offset <= usable.Count; offset++)
        {
            int candidate = ((index + step * offset) % usable.Count + usable.Count) % usable.Count;
            if (usable[candidate]) { return candidate; }
        }
        return -1;
    }
}

/// <summary>A shadow-casting plate: effects rasterize their element, so a raised surface keeps its text on a separate layer.</summary>
internal static class FloatingShadow
{
    public static Border Plate(double radius)
    {
        var plate = new Border { CornerRadius = new CornerRadius(radius), IsHitTestVisible = false, Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, BlurRadius = 48, ShadowDepth = 20, Direction = 270, Opacity = .105 } };
        plate.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceRaised");
        return plate;
    }
}
