using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;

namespace ArcWpf.Controls;

public enum ToastStackType { Success, Info, Warning, Error, Loading }
public enum ToastStackPosition { BottomRight, BottomCenter, BottomLeft }

public sealed class ToastStackOptions
{
    public string? Id { get; init; }
    public ToastStackType Type { get; init; } = ToastStackType.Info;
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? ActionText { get; init; }
    public ICommand? ActionCommand { get; init; }
    public int? Duration { get; init; }
}

public sealed class ToastStackPatch
{
    public ToastStackType? Type { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? ActionText { get; init; }
    public ICommand? ActionCommand { get; init; }
    public int? Duration { get; init; }
}

public sealed class ToastStackItem : ContentControl
{
    public static readonly DependencyProperty IdProperty = DependencyProperty.Register(nameof(Id), typeof(string), typeof(ToastStackItem), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty TypeProperty = DependencyProperty.Register(nameof(Type), typeof(ToastStackType), typeof(ToastStackItem), new FrameworkPropertyMetadata(ToastStackType.Info));
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(ToastStackItem), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(ToastStackItem), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty ActionTextProperty = DependencyProperty.Register(nameof(ActionText), typeof(string), typeof(ToastStackItem), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty ActionCommandProperty = DependencyProperty.Register(nameof(ActionCommand), typeof(ICommand), typeof(ToastStackItem), new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(nameof(Duration), typeof(int), typeof(ToastStackItem), new FrameworkPropertyMetadata(5000));
    public static readonly DependencyProperty IsPausedProperty = DependencyProperty.Register(nameof(IsPaused), typeof(bool), typeof(ToastStackItem), new FrameworkPropertyMetadata(false));

    public string Id { get => (string)GetValue(IdProperty); set => SetValue(IdProperty, value); }
    public ToastStackType Type { get => (ToastStackType)GetValue(TypeProperty); set => SetValue(TypeProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public string ActionText { get => (string)GetValue(ActionTextProperty); set => SetValue(ActionTextProperty, value); }
    public ICommand? ActionCommand { get => (ICommand?)GetValue(ActionCommandProperty); set => SetValue(ActionCommandProperty, value); }
    public int Duration { get => (int)GetValue(DurationProperty); set => SetValue(DurationProperty, value); }
    public bool IsPaused { get => (bool)GetValue(IsPausedProperty); set => SetValue(IsPausedProperty, value); }

    public event EventHandler? Closed;
    public event EventHandler? ActionInvoked;
    private DispatcherTimer? _timer;
    private DateTime _started;
    private int _remaining;
    private ButtonBase? _close;
    private ButtonBase? _action;

    static ToastStackItem() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ToastStackItem), new FrameworkPropertyMetadata(typeof(ToastStackItem)));

    public ToastStackItem() { Loaded += (_, _) => StartTimer(); Unloaded += (_, _) => StopTimer(); }

    public void Close()
    {
        StopTimer();
        SetCurrentValue(IsPausedProperty, false);
        Closed?.Invoke(this, EventArgs.Empty);
    }

    internal void SetTimerDuration(int duration)
    {
        SetCurrentValue(DurationProperty, duration);
        _remaining = duration;
        if (IsLoaded) StartTimer();
    }

    private void StartTimer()
    {
        StopTimer();
        _remaining = Duration;
        if (Duration <= 0 || Type == ToastStackType.Loading) return;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _timer.Tick += TimerTick;
        _started = DateTime.UtcNow;
        _timer.Start();
    }
    private void TimerTick(object? sender, EventArgs e)
    {
        if (IsPaused) { _remaining = Math.Max(0, _remaining - (int)(DateTime.UtcNow - _started).TotalMilliseconds); _started = DateTime.UtcNow; return; }
        if ((DateTime.UtcNow - _started).TotalMilliseconds >= _remaining) Close();
    }
    private void StopTimer() { if (_timer is not null) { _timer.Stop(); _timer.Tick -= TimerTick; _timer = null; } }

    public override void OnApplyTemplate()
    {
        ReleaseTemplate(); base.OnApplyTemplate();
        _close = GetTemplateChild("PART_Close") as ButtonBase;
        _action = GetTemplateChild("PART_Action") as ButtonBase;
        if (_close is not null) _close.Click += CloseClick;
        if (_action is not null) _action.Click += ActionClick;
    }
    private void CloseClick(object sender, RoutedEventArgs e) { Close(); e.Handled = true; }
    private void ActionClick(object sender, RoutedEventArgs e)
    {
        if (ActionCommand?.CanExecute(Id) == true) ActionCommand.Execute(Id);
        ActionInvoked?.Invoke(this, EventArgs.Empty);
        Close(); e.Handled = true;
    }
    private void ReleaseTemplate()
    {
        if (_close is not null) _close.Click -= CloseClick;
        if (_action is not null) _action.Click -= ActionClick;
        _close = null; _action = null;
    }
}

public class ToastStack : ItemsControl
{
    public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(nameof(Duration), typeof(int), typeof(ToastStack), new FrameworkPropertyMetadata(5000));
    public static readonly DependencyProperty LimitProperty = DependencyProperty.Register(nameof(Limit), typeof(int), typeof(ToastStack), new FrameworkPropertyMetadata(12, null, CoercePositive));
    public static readonly DependencyProperty VisibleToastsProperty = DependencyProperty.Register(nameof(VisibleToasts), typeof(int), typeof(ToastStack), new FrameworkPropertyMetadata(3, null, CoercePositive));
    public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(nameof(Position), typeof(ToastStackPosition), typeof(ToastStack), new FrameworkPropertyMetadata(ToastStackPosition.BottomRight));
    public static readonly DependencyProperty IsContainedProperty = DependencyProperty.Register(nameof(IsContained), typeof(bool), typeof(ToastStack), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(ToastStack), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty HotkeyProperty = DependencyProperty.Register(nameof(Hotkey), typeof(bool), typeof(ToastStack), new FrameworkPropertyMetadata(true));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ToastStack), new FrameworkPropertyMetadata("Notifications"));

    public int Duration { get => (int)GetValue(DurationProperty); set => SetValue(DurationProperty, value); }
    public int Limit { get => (int)GetValue(LimitProperty); set => SetValue(LimitProperty, value); }
    public int VisibleToasts { get => (int)GetValue(VisibleToastsProperty); set => SetValue(VisibleToastsProperty, value); }
    public ToastStackPosition Position { get => (ToastStackPosition)GetValue(PositionProperty); set => SetValue(PositionProperty, value); }
    public bool IsContained { get => (bool)GetValue(IsContainedProperty); set => SetValue(IsContainedProperty, value); }
    public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public bool Hotkey { get => (bool)GetValue(HotkeyProperty); set => SetValue(HotkeyProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public int Count => Items.Count;

    private Window? _window;
    private KeyEventHandler? _hotkeyHandler;

    static ToastStack() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ToastStack), new FrameworkPropertyMetadata(typeof(ToastStack)));

    public ToastStack()
    {
        MouseEnter += (_, _) => PauseAndExpand(true);
        MouseLeave += (_, _) => PauseAndExpand(false);
        GotKeyboardFocus += (_, _) => PauseAndExpand(true);
        LostKeyboardFocus += (_, _) => { if (!IsMouseOver) PauseAndExpand(false); };
        Loaded += (_, _) => AttachHotkey();
        Unloaded += (_, _) => DetachHotkey();
    }

    public string Show(ToastStackOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        string id = string.IsNullOrWhiteSpace(options.Id) ? $"toast-stack-{Guid.NewGuid():N}" : options.Id!;
        var existing = Find(id);
        if (existing is not null) { Update(id, new ToastStackPatch { Type = options.Type, Title = options.Title, Description = options.Description, ActionText = options.ActionText, ActionCommand = options.ActionCommand, Duration = options.Duration }); return id; }
        var item = new ToastStackItem { Id = id, Type = options.Type, Title = options.Title, Description = options.Description ?? string.Empty, ActionText = options.ActionText ?? string.Empty, ActionCommand = options.ActionCommand };
        item.SetTimerDuration(options.Duration ?? DurationFor(options.Type));
        item.Closed += ItemClosed;
        Items.Insert(0, item);
        while (Items.Count > Limit) RemoveItem((ToastStackItem)Items[Items.Count - 1]);
        InvalidateMeasure(); return id;
    }

    public string Show(string title, string? description = null, ToastStackType type = ToastStackType.Info, int? duration = null) => Show(new ToastStackOptions { Title = title, Description = description, Type = type, Duration = duration });

    public void Update(string id, ToastStackPatch patch)
    {
        var item = Find(id); if (item is null) return;
        if (patch.Type is { } type) item.Type = type;
        if (patch.Title is not null) item.Title = patch.Title;
        if (patch.Description is not null) item.Description = patch.Description;
        if (patch.ActionText is not null) item.ActionText = patch.ActionText;
        if (patch.ActionCommand is not null) item.ActionCommand = patch.ActionCommand;
        if (patch.Duration is { } duration) item.SetTimerDuration(duration); else item.SetTimerDuration(DurationFor(item.Type));
        item.InvalidateMeasure(); InvalidateMeasure();
    }

    public void Dismiss(string? id = null)
    {
        if (id is null) { foreach (var item in Items.OfType<ToastStackItem>().ToArray()) item.Close(); return; }
        Find(id)?.Close();
    }

    private ToastStackItem? Find(string id) => Items.OfType<ToastStackItem>().FirstOrDefault(item => item.Id == id);
    private int DurationFor(ToastStackType type) => type == ToastStackType.Loading ? 0 : type is ToastStackType.Warning or ToastStackType.Error ? (int)(Duration * 1.6) : Duration;
    private void ItemClosed(object? sender, EventArgs e) { if (sender is ToastStackItem item) RemoveItem(item); }
    private void RemoveItem(ToastStackItem item) { item.Closed -= ItemClosed; Items.Remove(item); InvalidateMeasure(); }
    private void PauseAndExpand(bool value) { SetCurrentValue(IsExpandedProperty, value && Items.Count > 0); foreach (var item in Items.OfType<ToastStackItem>()) item.SetCurrentValue(ToastStackItem.IsPausedProperty, value); UpdatePanel(); }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) { base.OnPropertyChanged(e); if (e.Property == IsExpandedProperty || e.Property == VisibleToastsProperty) UpdatePanel(); }
    public override void OnApplyTemplate() { base.OnApplyTemplate(); UpdatePanel(); }
    private void UpdatePanel() { if (FindVisual<ToastStackPanel>(this) is { } panel) { panel.Expanded = IsExpanded; panel.VisibleToasts = VisibleToasts; } }
    private static T? FindVisual<T>(DependencyObject root) where T : DependencyObject { for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); if (child is T found) return found; if (FindVisual<T>(child) is { } nested) return nested; } return null; }
    protected override AutomationPeer OnCreateAutomationPeer() => new ToastStackPeer(this);
    private sealed class ToastStackPeer(ToastStack owner) : FrameworkElementAutomationPeer(owner) { protected override string GetNameCore() => owner.Label; protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List; }
    private static object CoercePositive(DependencyObject d, object value) => Math.Max(1, (int)value);
    private void AttachHotkey()
    {
        if (!Hotkey || (_window = Window.GetWindow(this)) is null) return;
        _hotkeyHandler = (_, e) => { if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Alt && Items.Count > 0) { SetCurrentValue(IsExpandedProperty, true); (ItemContainerGenerator.ContainerFromIndex(0) as FrameworkElement)?.Focus(); e.Handled = true; } };
        _window.PreviewKeyDown += _hotkeyHandler;
    }
    private void DetachHotkey() { if (_window is not null && _hotkeyHandler is not null) _window.PreviewKeyDown -= _hotkeyHandler; _window = null; _hotkeyHandler = null; }
}

public sealed class ToastStackPanel : Panel
{
    public static readonly DependencyProperty ExpandedProperty = DependencyProperty.RegisterAttached(nameof(Expanded), typeof(bool), typeof(ToastStackPanel), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));
    public static readonly DependencyProperty VisibleToastsProperty = DependencyProperty.RegisterAttached(nameof(VisibleToasts), typeof(int), typeof(ToastStackPanel), new FrameworkPropertyMetadata(3, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));
    public bool Expanded { get => (bool)GetValue(ExpandedProperty); set => SetValue(ExpandedProperty, value); }
    public int VisibleToasts { get => (int)GetValue(VisibleToastsProperty); set => SetValue(VisibleToastsProperty, value); }
    protected override Size MeasureOverride(Size availableSize) { double width = 0, height = 0; foreach (UIElement child in InternalChildren) { child.Measure(new Size(availableSize.Width, availableSize.Height)); width = Math.Max(width, child.DesiredSize.Width); height += child.DesiredSize.Height; } if (!Expanded && InternalChildren.Count > 0) height = InternalChildren[0].DesiredSize.Height + 14 * (Math.Min(VisibleToasts, InternalChildren.Count) - 1); return new Size(Math.Min(availableSize.Width, width), height); }
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (InternalChildren.Count == 0) return finalSize;
        int visible = Math.Min(Math.Max(1, VisibleToasts), InternalChildren.Count);
        if (Expanded) { double bottom = finalSize.Height; for (int i = 0; i < InternalChildren.Count; i++) { var child = InternalChildren[i]; var h = child.DesiredSize.Height; bottom -= h; child.Visibility = Visibility.Visible; child.Opacity = i < visible ? 1 : 0; child.IsHitTestVisible = i < visible; child.RenderTransform = new TranslateTransform(); child.Arrange(new Rect(0, bottom, finalSize.Width, h)); bottom -= 12; } }
        else { double h = InternalChildren[0].DesiredSize.Height; for (int i = 0; i < InternalChildren.Count; i++) { var child = InternalChildren[i]; bool show = i < visible; child.Visibility = Visibility.Visible; child.Opacity = show ? 1 : 0; child.IsHitTestVisible = show; child.RenderTransform = new TransformGroup { Children = { new ScaleTransform(1 - .05 * i, 1 - .05 * i), new TranslateTransform(0, -14 * i) } }; child.Arrange(new Rect(0, finalSize.Height - h, finalSize.Width, h)); } }
        return finalSize;
    }
}





