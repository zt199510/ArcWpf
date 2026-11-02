using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public sealed class UndoToastEntry
{
    public string Key { get; init; } = string.Empty;
    public object? Item { get; init; }
    public string Kind { get; init; } = "delete";
    public int OriginalIndex { get; init; }
}

/// <summary>Arc UndoToast 的 WPF 适配：保留移除、倒计时、撤销和提交批次语义。</summary>
public class UndoToast : ContentControl
{
    public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(nameof(Duration), typeof(int), typeof(UndoToast), new FrameworkPropertyMetadata(6000, null, (_, v) => Math.Max(250, (int)v)));
    public static readonly DependencyProperty ToastPlacementProperty = DependencyProperty.Register(nameof(ToastPlacement), typeof(UndoToastPlacement), typeof(UndoToast), new FrameworkPropertyMetadata(UndoToastPlacement.Container));
    public static readonly DependencyProperty PauseOnHoverProperty = DependencyProperty.Register(nameof(PauseOnHover), typeof(bool), typeof(UndoToast), new FrameworkPropertyMetadata(true));
    public static readonly DependencyProperty ToastTextProperty = DependencyProperty.Register(nameof(ToastText), typeof(string), typeof(UndoToast), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty UndoAvailableProperty = DependencyProperty.Register(nameof(UndoAvailable), typeof(bool), typeof(UndoToast), new FrameworkPropertyMetadata(false));
    public int Duration { get => (int)GetValue(DurationProperty); set => SetValue(DurationProperty, value); }
    public UndoToastPlacement ToastPlacement { get => (UndoToastPlacement)GetValue(ToastPlacementProperty); set => SetValue(ToastPlacementProperty, value); }
    public bool PauseOnHover { get => (bool)GetValue(PauseOnHoverProperty); set => SetValue(PauseOnHoverProperty, value); }
    public string ToastText { get => (string)GetValue(ToastTextProperty); private set => SetValue(ToastTextProperty, value); }
    public bool UndoAvailable { get => (bool)GetValue(UndoAvailableProperty); private set => SetValue(UndoAvailableProperty, value); }
    public ObservableCollection<UndoToastEntry> Pending { get; } = new();
    public event EventHandler<IReadOnlyList<UndoToastEntry>>? ItemsRemoved;
    public event EventHandler<IReadOnlyList<UndoToastEntry>>? ItemsRestored;
    public event EventHandler<IReadOnlyList<UndoToastEntry>>? ItemsCommitted;
    private DispatcherTimer? _timer; private DateTime _started; private int _remaining; private bool _paused; private StackPanel? _panel; private TextBlock? _toastLabel; private System.Windows.Controls.Button? _undoButton;
    static UndoToast() => DefaultStyleKeyProperty.OverrideMetadata(typeof(UndoToast), new FrameworkPropertyMetadata(typeof(UndoToast)));
    public UndoToast() { Loaded += (_, _) => BuildVisual(); Unloaded += (_, _) => StopTimer(); MouseEnter += (_, _) => { if (PauseOnHover) _paused = true; }; MouseLeave += (_, _) => _paused = false; PreviewKeyDown += OnPreviewKeyDown; }
    public void Remove(string key, string kind = "delete")
    {
        if (Pending.Any(e => e.Key == key)) return;
        var entry = new UndoToastEntry { Key = key, Kind = kind, OriginalIndex = Pending.Count };
        Pending.Add(entry); ToastText = Pending.Count == 1 ? $"{kind}d 1 item" : $"{kind}d {Pending.Count} items"; UndoAvailable = true; ItemsRemoved?.Invoke(this, Pending.ToArray()); StartTimer(); RefreshVisual();
    }
    public void Undo()
    {
        if (Pending.Count == 0) return; var restored = Pending.ToArray(); Pending.Clear(); StopTimer(); UndoAvailable = false; ToastText = $"Restored {restored.Length} item{(restored.Length == 1 ? "" : "s")}"; ItemsRestored?.Invoke(this, restored); RefreshVisual();
    }
    public void Commit()
    {
        if (Pending.Count == 0) return; var committed = Pending.ToArray(); Pending.Clear(); StopTimer(); UndoAvailable = false; ToastText = string.Empty; ItemsCommitted?.Invoke(this, committed); RefreshVisual();
    }
    private void StartTimer() { StopTimer(); _remaining = Duration; _started = DateTime.UtcNow; _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) }; _timer.Tick += TimerTick; _timer.Start(); }
    private void TimerTick(object? sender, EventArgs e) { if (_paused) { _started = DateTime.UtcNow; return; } if ((DateTime.UtcNow - _started).TotalMilliseconds >= _remaining) Commit(); }
    private void StopTimer() { if (_timer is null) return; _timer.Stop(); _timer.Tick -= TimerTick; _timer = null; }
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e) { if (e.Key == Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && UndoAvailable && !(Keyboard.FocusedElement is TextBox)) { Undo(); e.Handled = true; } }
    private void BuildVisual() { if (_panel is not null) return; _panel = new StackPanel(); _toastLabel = new TextBlock(); _undoButton = new System.Windows.Controls.Button { Content = "Undo", MinHeight = 28, Padding = new Thickness(10, 0, 10, 0) }; _undoButton.Click += (_, _) => Undo(); var toast = new Border { Background = TryFindResource("ArcWpf.Brush.SurfaceRaised") as System.Windows.Media.Brush, BorderBrush = TryFindResource("ArcWpf.Brush.Border") as System.Windows.Media.Brush, BorderThickness = new Thickness(1), Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { _toastLabel, _undoButton } }, Padding = new Thickness(12), CornerRadius = new CornerRadius(12) }; _panel.Children.Add(new ContentPresenter { Content = ItemsPresenter() }); _panel.Children.Add(toast); SetCurrentValue(ContentProperty, _panel); RefreshVisual(); }
    private UIElement ItemsPresenter() => new ItemsControl { ItemsSource = Pending, ItemTemplate = new DataTemplate { VisualTree = new FrameworkElementFactory(typeof(TextBlock)) } };
    private void RefreshVisual() { if (_toastLabel is null || _undoButton is null) return; _toastLabel.Text = ToastText; _undoButton.Visibility = UndoAvailable ? Visibility.Visible : Visibility.Collapsed; }
    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeerImpl(this);
    private sealed class FrameworkElementAutomationPeerImpl(UndoToast owner) : FrameworkElementAutomationPeer(owner) { protected override string GetNameCore() => "Undo toast"; protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.StatusBar; }
}
public enum UndoToastPlacement { Container, Viewport }
