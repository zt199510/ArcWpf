using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;

namespace ArcWpf.Controls;

public class ShortcutRecorder : Control
{
    public static readonly DependencyProperty ShortcutProperty = DependencyProperty.Register(nameof(Shortcut), typeof(string), typeof(ShortcutRecorder), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualChanged));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ShortcutRecorder), new PropertyMetadata("Shortcut"));
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(ShortcutRecorder), new PropertyMetadata("Record shortcut"));
    private static readonly DependencyPropertyKey IsRecordingPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsRecording), typeof(bool), typeof(ShortcutRecorder), new FrameworkPropertyMetadata(false, OnVisualChanged));
    public static readonly DependencyProperty IsRecordingProperty = IsRecordingPropertyKey.DependencyProperty;
    public static readonly RoutedEvent ShortcutChangedEvent = EventManager.RegisterRoutedEvent(nameof(ShortcutChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ShortcutRecorder));
    private System.Windows.Controls.Button? _button; private TextBlock? _text;
    public string Shortcut { get => (string)GetValue(ShortcutProperty); set => SetValue(ShortcutProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public bool IsRecording => (bool)GetValue(IsRecordingProperty);
    public event RoutedEventHandler ShortcutChanged { add => AddHandler(ShortcutChangedEvent, value); remove => RemoveHandler(ShortcutChangedEvent, value); }
    static ShortcutRecorder() { DefaultStyleKeyProperty.OverrideMetadata(typeof(ShortcutRecorder), new FrameworkPropertyMetadata(typeof(ShortcutRecorder))); }
    public override void OnApplyTemplate() { ReleaseTemplate(); base.OnApplyTemplate(); _button = GetTemplateChild("PART_Button") as System.Windows.Controls.Button; _text = GetTemplateChild("PART_Text") as TextBlock; if (_button is not null) { _button.Click += ButtonClick; _button.PreviewKeyDown += ButtonKeyDown; _button.LostKeyboardFocus += (_, _) => StopRecording(); } Refresh(); }
    private void ReleaseTemplate() { if (_button is not null) { _button.Click -= ButtonClick; _button.PreviewKeyDown -= ButtonKeyDown; } _button = null; _text = null; }
    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ShortcutRecorder)d).Refresh();
    private void Refresh() { if (_text is not null) _text.Text = IsRecording ? "Press keys…" : string.IsNullOrEmpty(Shortcut) ? Placeholder : Shortcut; }
    private void ButtonClick(object? sender, RoutedEventArgs e) { SetValue(IsRecordingPropertyKey, true); Refresh(); _button?.Focus(); e.Handled = true; }
    private void ButtonKeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsRecording) return; e.Handled = true; if (e.Key == Key.Escape) { StopRecording(); return; } if (e.Key == Key.Back) { SetCurrentValue(ShortcutProperty, string.Empty); StopRecording(); return; }
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        string text = FormatShortcut(e.Key, Keyboard.Modifiers); SetCurrentValue(ShortcutProperty, text); SetValue(IsRecordingPropertyKey, false); Refresh(); RaiseEvent(new RoutedEventArgs(ShortcutChangedEvent, this));
    }
    private void StopRecording() { if (!IsRecording) return; SetValue(IsRecordingPropertyKey, false); Refresh(); }
    private static string FormatShortcut(Key key, ModifierKeys modifiers)
    {
        var parts = new List<string>(); if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl"); if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt"); if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift"); if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win"); parts.Add(key switch { Key.Space => "Space", Key.Add => "+", Key.Subtract => "-", _ => key.ToString() }); return string.Join("+", parts);
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new ShortcutRecorderPeer(this);
    private sealed class ShortcutRecorderPeer(ShortcutRecorder owner) : FrameworkElementAutomationPeer(owner) { protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button; protected override string GetNameCore() => owner.Label; }
}
