using System.Windows.Controls;
using System.Windows.Threading;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class FloatingButtonGroupPage : UserControl
{
    private readonly FloatingButtonGroupItem _comment = new() { Id = "comment", Label = "Comment", Icon = "message-circle", IconOnly = true, Shortcut = "C", IsPressed = false };
    private readonly FloatingButtonGroupItem _share = new() { Id = "share", Label = "Share", Icon = "link-2", ReserveLabels = ["Copied"] };
    private readonly FloatingButtonGroupItem _present = new() { Id = "present", Label = "Present", Icon = "play", ReserveLabels = ["Stop"], IsPressed = false };
    private readonly FloatingButtonGroupItem _lock = new() { Id = "lock", Label = "Lock layer", Icon = "lock", IsPressed = false };
    private readonly FloatingButtonGroupItem _hide = new() { Id = "hide", Label = "Hide layer", Icon = "eye", IsPressed = false };
    private readonly FloatingButtonGroupItem _pin = new() { Id = "pin", Label = "Pin message", Icon = "pin", IsPressed = false };
    private readonly DispatcherTimer _copied = new() { Interval = System.TimeSpan.FromSeconds(1.6) };

    public FloatingButtonGroupPage()
    {
        InitializeComponent();
        Wide.Items =
        [
            new() { Id = "undo", Label = "Undo", Icon = "undo-2", IconOnly = true, Shortcut = "\u2318Z" },
            new() { Id = "redo", Label = "Redo", Icon = "redo-2", IconOnly = true, Shortcut = "\u21E7\u2318Z", IsDisabled = true },
            new() { IsSeparator = true },
            _comment,
            _share,
            new() { IsSeparator = true },
            _present,
        ];
        Rail.Items =
        [
            new() { Id = "in", Label = "Zoom in", Icon = "zoom-in", Shortcut = "+" },
            new() { Id = "out", Label = "Zoom out", Icon = "zoom-out", Shortcut = "-" },
            new() { Id = "fit", Label = "Zoom to fit", Icon = "scan", IsDisabled = true },
            new() { IsSeparator = true },
            _lock,
            _hide,
        ];
        Selection.Items =
        [
            new() { Id = "copy", Label = "Copy text", Icon = "copy" },
            _pin,
            new() { Id = "archive", Label = "Archive", Icon = "archive" },
            new() { IsSeparator = true },
            new() { Id = "delete", Label = "Delete", Icon = "trash-2", IsDisabled = true },
        ];
        _copied.Tick += (_, _) => { _copied.Stop(); _share.Label = "Share"; _share.Icon = "link-2"; };
        Unloaded += (_, _) => _copied.Stop();
    }

    private void OnWide(object? sender, string id)
    {
        if (id == "comment") { _comment.IsPressed = _comment.IsPressed != true; }
        else if (id == "share") { _share.Label = "Copied"; _share.Icon = "check"; _copied.Stop(); _copied.Start(); }
        else if (id == "present")
        {
            bool on = _present.IsPressed != true;
            _present.IsPressed = on;
            _present.Label = on ? "Stop" : "Present";
            _present.Icon = on ? "square" : "play";
        }
    }

    private void OnRail(object? sender, string id)
    {
        if (id == "lock") { _lock.IsPressed = _lock.IsPressed != true; }
        else if (id == "hide") { _hide.IsPressed = _hide.IsPressed != true; }
    }

    private void OnSelection(object? sender, string id) { if (id == "pin") { _pin.IsPressed = _pin.IsPressed != true; } }
}
