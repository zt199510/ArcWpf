using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;

// The registry demo: three editable workspace shortcuts that know about each other, a Key style switch, a line that reports which shortcut
// just ran when it is pressed inside the panel, and the cheatsheet of every group.
public partial class ShortcutRecorderPage : UserControl
{
    private sealed class Entry(string id, string label, string group, string? shortcut, bool editable)
    {
        public string Id { get; } = id;
        public string Label { get; } = label;
        public string Group { get; } = group;
        public string? Initial { get; } = shortcut;
        public string? Shortcut { get; set; } = shortcut;
        public bool Editable { get; } = editable;
    }

    private readonly List<Entry> _entries =
    [
        new("command", "Open command menu", "Workspace", "mod+k", true),
        new("issue", "Create issue", "Workspace", "alt+n", true),
        new("sidebar", "Toggle sidebar", "Workspace", "mod+.", true),
        new("inbox", "Go to inbox", "Navigation", "alt+1", false),
        new("projects", "Go to projects", "Navigation", "alt+2", false),
        new("search", "Search everything", "Navigation", "mod+shift+f", false),
        new("bold", "Bold", "Editing", "mod+b", false),
        new("link", "Insert link", "Editing", "mod+shift+l", false),
        new("save", "Save draft", "Editing", "mod+s", false),
    ];
    private readonly Dictionary<string, ShortcutRecorder> _recorders = [];
    private string? _ran;

    public ShortcutRecorderPage()
    {
        InitializeComponent();
        foreach (var entry in _entries.Where(entry => entry.Editable))
        {
            var recorder = new ShortcutRecorder { Label = entry.Label, Shortcut = entry.Shortcut, ResetValue = entry.Initial, Margin = new Thickness(0, 0, 0, 16) };
            recorder.ShortcutChanged += (_, e) =>
            {
                entry.Shortcut = string.IsNullOrEmpty(e.Shortcut) ? null : e.Shortcut;
                if (e.Replaced is { } replaced) foreach (var other in _entries.Where(other => other.Label == replaced.Label)) other.Shortcut = null;
                Sync(false);
            };
            _recorders[entry.Id] = recorder;
            Recorders.Children.Add(recorder);
        }
        Sync(false);
    }

    private ShortcutPlatform Platform => KeyStyle?.SelectedIndex == 0 ? ShortcutPlatform.Mac : ShortcutPlatform.Other;

    private void KeyStyleChanged(object sender, SelectionChangedEventArgs e) { if (Sheet is not null) Sync(false); }

    private void Sync(bool ranChanged)
    {
        foreach (var entry in _entries.Where(entry => entry.Editable))
        {
            var recorder = _recorders[entry.Id];
            recorder.Platform = Platform;
            recorder.Shortcut = entry.Shortcut;
            recorder.Bindings = _entries.Where(other => other.Id != entry.Id && other.Shortcut is not null).Select(other => new ShortcutBinding(other.Shortcut!, other.Label)).ToList();
        }
        Sheet.Platform = Platform;
        Sheet.Groups = new[] { "Workspace", "Navigation", "Editing" }
            .Select(group => new ShortcutListGroup(group, _entries.Where(entry => entry.Group == group && entry.Shortcut is not null).Select(entry => new ShortcutListItem(entry.Label, entry.Shortcut!)).ToList()))
            .ToList();
        RenderRan(ranChanged);
    }

    // Keys that a focused recorder handled never reach here, as with the source's defaultPrevented check.
    private void DemoKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled) return;
        var key = Shortcuts.RealKey(e);
        var hit = _entries.FirstOrDefault(entry => Shortcuts.Matches(key, Keyboard.Modifiers, entry.Shortcut, Platform));
        if (hit is null) return;
        e.Handled = true;
        _ran = $"{hit.Label} ({Shortcuts.Format(hit.Shortcut, Platform).Text})";
        RenderRan(true);
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);
        if (!Demo.IsKeyboardFocusWithin && Demo.IsMouseOver) Demo.Focus();
    }

    // The line swaps in place (popLayout): the new copy rises 6 DIP out of a subtle blur; the snappy spring is approximated with an exponential ease-out
    // because the motion helpers are internal to the controls assembly.
    private void RenderRan(bool animate)
    {
        var line = new WrapPanel { VerticalAlignment = VerticalAlignment.Center, RenderTransform = new TranslateTransform() };
        var command = _entries[0];
        if (_ran is not null) line.Children.Add(new TextBlock { Text = $"Ran {_ran}", VerticalAlignment = VerticalAlignment.Center });
        else if (command.Shortcut is not null)
        {
            line.Children.Add(new TextBlock { Text = "Press ", VerticalAlignment = VerticalAlignment.Center });
            line.Children.Add(new ShortcutKeys { Shortcut = command.Shortcut, Platform = Platform, Size = ControlSize.Small, Margin = new Thickness(0, 2, 4, 2) });
            line.Children.Add(new TextBlock { Text = "here to open the command menu", VerticalAlignment = VerticalAlignment.Center });
        }
        else line.Children.Add(new TextBlock { Text = "Record a shortcut, then press it here", VerticalAlignment = VerticalAlignment.Center });
        if (_ran is null) line.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.TextMuted");
        Ran.Content = line;
        if (!animate || ArcMotion.GetLevel(this) == ArcMotionLevel.Off) return;
        bool full = ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        line.Opacity = 0;
        line.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromSeconds(full ? .26 : .12)));
        if (!full) return;
        var move = (TranslateTransform)line.RenderTransform;
        move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(6, 0, TimeSpan.FromSeconds(.52)) { EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 7 } });
        var blur = new BlurEffect { Radius = 2 };
        line.Effect = blur;
        blur.BeginAnimation(BlurEffect.RadiusProperty, new DoubleAnimation(0, TimeSpan.FromSeconds(.26)));
    }
}
