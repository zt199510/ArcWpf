using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public sealed class UndoToastEntry
{
    public string Key { get; init; } = string.Empty;
    public object? Item { get; init; }
    public string Kind { get; init; } = "delete";
    public int OriginalIndex { get; init; }
}

/// <summary>A kind of removal: Label is used in failure copy ("Archive"), Done in the toast ("archived").</summary>
public sealed record UndoToastAction(string Label, string Done);

public enum UndoToastPlacement { Container, Viewport }

/// <summary>
/// Independent WPF implementation of Arc's Undo toast, from its public documentation and the displayed geometry of its public preview. A
/// list whose rows fold away with a second chance: a countdown toast merges repeat removals, and Undo or Ctrl+Z reopens every row in place.
/// </summary>
[TemplatePart(Name = "PART_Toast", Type = typeof(Border))]
[TemplatePart(Name = "PART_Layer", Type = typeof(Panel))]
[TemplatePart(Name = "PART_Lead", Type = typeof(Grid))]
[TemplatePart(Name = "PART_Message", Type = typeof(Grid))]
[TemplatePart(Name = "PART_Undo", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "PART_Kbd", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Empty", Type = typeof(FrameworkElement))]
public class UndoToast : ItemsControl
{
    public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(nameof(Duration), typeof(int), typeof(UndoToast), new FrameworkPropertyMetadata(6000, null, (_, v) => Math.Max(250, (int)v)));
    public static readonly DependencyProperty ToastPlacementProperty = DependencyProperty.Register(nameof(ToastPlacement), typeof(UndoToastPlacement), typeof(UndoToast), new FrameworkPropertyMetadata(UndoToastPlacement.Container, (d, _) => ((UndoToast)d).PlaceToast()));
    public static readonly DependencyProperty PauseOnHoverProperty = DependencyProperty.Register(nameof(PauseOnHover), typeof(bool), typeof(UndoToast), new FrameworkPropertyMetadata(true, (d, _) => ((UndoToast)d).UpdatePause()));
    public static readonly DependencyProperty UndoShortcutProperty = DependencyProperty.Register(nameof(UndoShortcut), typeof(bool), typeof(UndoToast), new FrameworkPropertyMetadata(true, (d, _) => ((UndoToast)d).UpdateUndoButton()));
    public static readonly DependencyProperty ToastTextProperty = DependencyProperty.Register(nameof(ToastText), typeof(string), typeof(UndoToast), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty UndoAvailableProperty = DependencyProperty.Register(nameof(UndoAvailable), typeof(bool), typeof(UndoToast), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(UndoToast), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty NounSingularProperty = DependencyProperty.Register(nameof(NounSingular), typeof(string), typeof(UndoToast), new FrameworkPropertyMetadata("item"));
    public static readonly DependencyProperty NounPluralProperty = DependencyProperty.Register(nameof(NounPlural), typeof(string), typeof(UndoToast), new FrameworkPropertyMetadata("items"));
    public static readonly DependencyProperty EmptyProperty = DependencyProperty.Register(nameof(Empty), typeof(object), typeof(UndoToast), new FrameworkPropertyMetadata(null));
    private static readonly DependencyProperty RingProgressProperty = DependencyProperty.Register("RingProgress", typeof(double), typeof(UndoToast), new PropertyMetadata(1d, (d, e) => ((UndoToast)d).ApplyRing((double)e.NewValue)));
    public int Duration { get => (int)GetValue(DurationProperty); set => SetValue(DurationProperty, value); }
    public UndoToastPlacement ToastPlacement { get => (UndoToastPlacement)GetValue(ToastPlacementProperty); set => SetValue(ToastPlacementProperty, value); }
    public bool PauseOnHover { get => (bool)GetValue(PauseOnHoverProperty); set => SetValue(PauseOnHoverProperty, value); }
    public bool UndoShortcut { get => (bool)GetValue(UndoShortcutProperty); set => SetValue(UndoShortcutProperty, value); }
    public string ToastText { get => (string)GetValue(ToastTextProperty); private set => SetValue(ToastTextProperty, value); }
    public bool UndoAvailable { get => (bool)GetValue(UndoAvailableProperty); private set => SetValue(UndoAvailableProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string NounSingular { get => (string)GetValue(NounSingularProperty); set => SetValue(NounSingularProperty, value); }
    public string NounPlural { get => (string)GetValue(NounPluralProperty); set => SetValue(NounPluralProperty, value); }
    public object? Empty { get => GetValue(EmptyProperty); set => SetValue(EmptyProperty, value); }
    /// <summary>A stable key for each item. Rows are tracked by key, so a restored item keeps its identity.</summary>
    public Func<object, string>? KeySelector { get; set; }
    /// <summary>The kinds of removal. The first key is the default kind.</summary>
    public IDictionary<string, UndoToastAction> Actions { get; } = new Dictionary<string, UndoToastAction> { ["archive"] = new("Archive", "archived"), ["delete"] = new("Delete", "deleted") };
    /// <summary>Single key shortcuts on a focused row, mapped to a removal kind.</summary>
    public IDictionary<Key, string> RowShortcuts { get; } = new Dictionary<Key, string>();
    /// <summary>Called on undo. A failing task keeps the rows removed, says so and restarts the countdown.</summary>
    public Func<IReadOnlyList<UndoToastEntry>, Task>? OnUndo { get; set; }
    /// <summary>Called when the undo window closes. A failing task opens the rows again and says so.</summary>
    public Func<IReadOnlyList<UndoToastEntry>, Task>? OnCommit { get; set; }
    public ObservableCollection<UndoToastEntry> Pending { get; } = new();
    public event EventHandler<IReadOnlyList<UndoToastEntry>>? ItemsRemoved;
    public event EventHandler<IReadOnlyList<UndoToastEntry>>? ItemsRestored;
    public event EventHandler<IReadOnlyList<UndoToastEntry>>? ItemsCommitted;

    private const double ToastWidth = 372, ToastInset = 12, RestoredHold = 2200, FailedHold = 3200, StaggerItem = .035;
    private Border? _toast;
    private Panel? _layer;
    private Grid? _lead, _message;
    private System.Windows.Controls.Button? _undo;
    private FrameworkElement? _kbd, _empty;
    private Popup? _popup;
    private readonly TranslateTransform _toastMove = new();
    private readonly ScaleTransform _toastScale = new(1, 1);
    private readonly Ellipse _ringTrack = new() { StrokeThickness = 2 }, _ringFill = new() { StrokeThickness = 2, StrokeDashCap = PenLineCap.Round, RenderTransformOrigin = new Point(.5, .5), RenderTransform = new RotateTransform(-90) };
    private readonly UndoRollNumber _seconds = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Travel = 9.6 };
    private Grid? _countdown;
    private UndoRollNumber? _count;
    private string _shape = string.Empty;
    private AnimationClock? _clock;
    private bool _paused, _toastOpen;
    private int _batch;
    private DispatcherTimer? _closeTimer;
    private Window? _window;

    static UndoToast() => DefaultStyleKeyProperty.OverrideMetadata(typeof(UndoToast), new FrameworkPropertyMetadata(typeof(UndoToast)));
    public UndoToast()
    {
        TextElement.SetFontSize(_seconds, 12);
        TextElement.SetFontWeight(_seconds, FontWeights.Medium);
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
        IsVisibleChanged += (_, _) => UpdatePause();
        PreviewKeyDown += OnWindowKeyDown;
        ((INotifyCollectionChanged)Items).CollectionChanged += (_, _) => UpdateEmpty();
    }

    protected override bool IsItemItsOwnContainerOverride(object item) => item is UndoToastContainer;
    protected override DependencyObject GetContainerForItemOverride() => new UndoToastContainer();

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (_undo is not null) _undo.Click -= UndoClick;
        _toast = GetTemplateChild("PART_Toast") as Border;
        _layer = GetTemplateChild("PART_Layer") as Panel;
        _lead = GetTemplateChild("PART_Lead") as Grid;
        _message = GetTemplateChild("PART_Message") as Grid;
        _undo = GetTemplateChild("PART_Undo") as System.Windows.Controls.Button;
        _kbd = GetTemplateChild("PART_Kbd") as FrameworkElement;
        _empty = GetTemplateChild("PART_Empty") as FrameworkElement;
        if (_undo is not null) _undo.Click += UndoClick;
        if (_toast is not null)
        {
            var transform = new TransformGroup();
            transform.Children.Add(_toastScale);
            transform.Children.Add(_toastMove);
            _toast.RenderTransform = transform;
            _toast.RenderTransformOrigin = new Point(.5, 1);
            _toast.MouseEnter += (_, _) => UpdatePause();
            _toast.MouseLeave += (_, _) => UpdatePause();
            _toast.IsKeyboardFocusWithinChanged += (_, _) => UpdatePause();
            _toast.KeyDown += OnToastKeyDown;
        }
        _ringTrack.SetResourceReference(Shape.StrokeProperty, "ArcWpf.Brush.Border");
        _ringFill.SetResourceReference(Shape.StrokeProperty, "ArcWpf.Brush.Accent");
        _countdown = new Grid { Width = 24, Height = 24 };
        _ringTrack.Margin = _ringFill.Margin = new Thickness(1);
        DetachFromParent(_ringTrack);
        DetachFromParent(_ringFill);
        DetachFromParent(_seconds);
        _countdown.Children.Add(_ringTrack);
        _countdown.Children.Add(_ringFill);
        _countdown.Children.Add(_seconds);
        SizeChanged += (_, _) => FitToast();
        FitToast();
        PlaceToast();
        UpdateUndoButton();
        UpdateEmpty();
    }

    private static void DetachFromParent(FrameworkElement element)
    {
        if (element.Parent is Panel parent) parent.Children.Remove(element);
    }

    // The toast is at most 372 wide and keeps 12 from the component edges.
    private void FitToast()
    {
        if (_toast is null) return;
        _toast.Width = ToastPlacement == UndoToastPlacement.Viewport || ActualWidth <= 0 ? ToastWidth : Math.Min(ToastWidth, Math.Max(0, ActualWidth - ToastInset * 2));
    }

    private void PlaceToast()
    {
        if (_toast is null || _layer is null) return;
        bool viewport = ToastPlacement == UndoToastPlacement.Viewport;
        if (viewport && _toast.Parent == _layer)
        {
            _layer.Children.Remove(_toast);
            // The popup leaves room around the toast for its floating shadow.
            _popup = new Popup { AllowsTransparency = true, Placement = PlacementMode.Custom, StaysOpen = true, Child = new Border { Padding = new Thickness(56), Child = _toast } };
            _popup.CustomPopupPlacementCallback = (popup, target, _) => [new CustomPopupPlacement(new Point((target.Width - popup.Width) / 2, target.Height - popup.Height + 56 - ToastInset), PopupPrimaryAxis.None)];
        }
        else if (!viewport && _popup is not null)
        {
            ((Border)_popup.Child).Child = null;
            _popup.IsOpen = false;
            _popup = null;
            _layer.Children.Add(_toast);
        }
        FitToast();
    }

    private void Attach()
    {
        _window = Window.GetWindow(this);
        if (_window is not null) _window.PreviewKeyDown += OnWindowKeyDown;
        UpdatePause();
    }

    private void Detach()
    {
        if (_window is not null) _window.PreviewKeyDown -= OnWindowKeyDown;
        _window = null;
        if (_popup is not null) _popup.IsOpen = false;
        UpdatePause();
    }

    private string KeyOf(object item) => KeySelector?.Invoke(item) ?? item.ToString() ?? string.Empty;
    private object? ItemOf(string key)
    {
        foreach (var item in Items) if (KeyOf(item) == key) return item;
        return null;
    }
    private UndoToastContainer? ContainerOf(object? item) => item is null ? null : ItemContainerGenerator.ContainerFromItem(item) as UndoToastContainer;
    private IEnumerable<UndoToastContainer> Containers() => Enumerable.Range(0, Items.Count).Select(i => ItemContainerGenerator.ContainerFromIndex(i)).OfType<UndoToastContainer>();
    private string DefaultKind => Actions.Keys.FirstOrDefault() ?? "delete";
    private bool Motion => IsLoaded && IsVisible && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;

    /// <summary>Removes the row with this key. It folds away and stays in the items until the batch is committed.</summary>
    public void Remove(string key, string? kind = null)
    {
        if (Pending.Any(e => e.Key == key)) return;
        var item = ItemOf(key);
        var entry = new UndoToastEntry { Key = key, Item = item, Kind = kind ?? DefaultKind, OriginalIndex = item is null ? Pending.Count : Items.IndexOf(item) };
        HandOffFocus(ContainerOf(item));
        ContainerOf(item)?.Fold(Motion);
        Pending.Add(entry);
        UndoAvailable = true;
        _batch++;
        var snapshot = Pending.ToArray();
        ShowCounting(snapshot);
        ItemsRemoved?.Invoke(this, snapshot);
        Announcer.Say(this, $"{ToastText}. Press Control Z or the Undo button to undo.");
        UpdateEmpty();
    }

    internal void RemoveRow(DependencyObject source, string? kind)
    {
        var container = source as UndoToastContainer ?? ContainerFromElementSafe(source);
        if (container is null) return;
        var item = ItemContainerGenerator.ItemFromContainer(container);
        if (item != DependencyProperty.UnsetValue) Remove(KeyOf(item), kind);
    }

    private static UndoToastContainer? ContainerFromElementSafe(DependencyObject source)
    {
        for (var node = source; node is not null; node = VisualTreeHelper.GetParent(node))
            if (node is UndoToastContainer container) return container;
        return null;
    }

    internal bool TryShortcut(UndoToastRow row, Key key)
    {
        if (!RowShortcuts.TryGetValue(key, out var kind)) return false;
        RemoveRow(row, kind);
        return true;
    }

    // A removed row hands focus to the same control on the next row, or the previous one; an empty list hands it to Undo.
    private void HandOffFocus(UndoToastContainer? container)
    {
        if (container is null || Keyboard.FocusedElement is not DependencyObject focused || !container.IsAncestorOf(focused)) return;
        var open = Containers().Where(c => c.IsOpen).ToList();
        int index = open.IndexOf(container);
        var target = index + 1 < open.Count ? open[index + 1] : index > 0 ? open[index - 1] : null;
        if (target is null) { _undo?.Focus(); return; }
        var from = container.FindRow();
        int action = from is null || focused is not UndoToastRowAction current ? -1 : from.ActionsInOrder().ToList().IndexOf(current);
        var to = target.FindRow();
        if (to is null) return;
        if (action >= 0 && to.ActionsInOrder().ElementAtOrDefault(action) is { } same) same.Focus();
        else to.Focus();
    }

    private void UndoClick(object sender, RoutedEventArgs e) => Undo();

    public async void Undo()
    {
        if (Pending.Count == 0) return;
        var entries = Pending.ToArray();
        int batch = ++_batch;
        bool fromToast = _toast?.IsKeyboardFocusWithin == true;
        StopClock();
        if (OnUndo?.Invoke(entries) is { IsCompletedSuccessfully: false } task)
        {
            ShowLead(LeadSpinner());
            SetMessage(null, "Restoring");
            SetUndoVisible(false);
            try { await task; }
            catch
            {
                if (batch != _batch) return;
                ShowRetry(entries);
                return;
            }
            if (batch != _batch) return;
        }
        Pending.Clear();
        UndoAvailable = false;
        ToastText = Message(entries.Length, "restored");
        ShowLead(LeadGlyph(Lucide.Check, "SwapSuccess"));
        SetMessageCounted(entries.Length, "restored");
        SetUndoVisible(false);
        ReopenRows(entries, fromToast);
        Announcer.Say(this, ToastText);
        CloseAfter(RestoredHold);
        ItemsRestored?.Invoke(this, entries);
        UpdateEmpty();
    }

    private void ShowRetry(UndoToastEntry[] entries)
    {
        ToastText = "Couldn't undo, try again";
        ShowLead(LeadGlyph(Lucide.CircleAlert, "SwapDanger"));
        SetMessage(null, ToastText);
        SetUndoVisible(true);
        Announcer.Say(this, ToastText);
        StartClock();
    }

    /// <summary>Closes the undo window now and commits the batch.</summary>
    public async void Commit()
    {
        if (Pending.Count == 0) return;
        var entries = Pending.ToArray();
        _batch++;
        StopClock();
        Pending.Clear();
        UndoAvailable = false;
        ToastText = string.Empty;
        HideToast();
        if (OnCommit?.Invoke(entries) is { } task)
        {
            try { await task; }
            catch
            {
                var action = Actions.TryGetValue(entries[0].Kind, out var known) ? known.Label : entries[0].Kind;
                ToastText = $"Couldn't {action.ToLowerInvariant()} {(entries.Length == 1 ? NounSingular : NounPlural)}";
                ShowToast();
                ShowLead(LeadGlyph(Lucide.CircleAlert, "SwapDanger"));
                SetMessage(null, ToastText);
                SetUndoVisible(false);
                ReopenRows(entries, false);
                Announcer.Say(this, ToastText);
                CloseAfter(FailedHold);
                UpdateEmpty();
                return;
            }
        }
        RemoveFromSource(entries);
        ItemsCommitted?.Invoke(this, entries);
        UpdateEmpty();
    }

    private void RemoveFromSource(UndoToastEntry[] entries)
    {
        if (ItemsSource is IList { IsReadOnly: false, IsFixedSize: false } list)
        {
            foreach (var entry in entries) if (entry.Item is not null) list.Remove(entry.Item);
        }
        else if (ItemsSource is null)
        {
            foreach (var entry in entries) if (entry.Item is not null) Items.Remove(entry.Item);
        }
    }

    private void ReopenRows(UndoToastEntry[] entries, bool moveFocus)
    {
        bool motion = Motion;
        var ordered = entries.OrderBy(e => e.OriginalIndex).ToArray();
        for (int i = 0; i < ordered.Length; i++) ContainerOf(ordered[i].Item)?.Open(motion, i * StaggerItem);
        if (moveFocus) ContainerOf(ordered[0].Item)?.FindRow()?.Focus();
    }

    private string Message(int count, string verb)
    {
        string noun = count == 1 ? NounSingular : NounPlural;
        return count == 1 ? $"{char.ToUpperInvariant(noun[0])}{noun[1..]} {verb}" : $"{count} {noun} {verb}";
    }

    private void ShowCounting(UndoToastEntry[] entries)
    {
        var kinds = entries.GroupBy(e => e.Kind).ToArray();
        string Done(string kind) => Actions.TryGetValue(kind, out var action) ? action.Done : kind;
        bool wasOpen = _toastOpen && _closeTimer is null;
        ShowToast();
        ShowLead(_countdown);
        SetUndoVisible(true);
        if (kinds.Length > 1)
        {
            ToastText = string.Join(", ", kinds.Select(g => $"{g.Count()} {Done(g.Key)}"));
            SetMessage(null, ToastText);
        }
        else
        {
            ToastText = Message(entries.Length, Done(kinds[0].Key));
            SetMessageCounted(entries.Length, Done(kinds[0].Key));
        }
        if (wasOpen) Nudge();
        StartClock();
    }

    private void SetMessageCounted(int count, string verb)
    {
        if (count == 1) { SetMessage(null, Message(1, verb)); return; }
        SetMessage(count, $"{NounPlural} {verb}");
    }

    // The message crossfades vertically when its shape changes; a new count only rolls its digits.
    private void SetMessage(int? count, string text)
    {
        if (_message is null) return;
        string shape = (count.HasValue ? "#" : string.Empty) + text;
        bool motion = Motion;
        if (shape == _shape && count.HasValue && _count is not null)
        {
            _count.Set(count.Value, motion);
            return;
        }
        var previous = _message.Children.OfType<FrameworkElement>().LastOrDefault();
        _shape = shape;
        _count = count.HasValue ? new UndoRollNumber { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0), Travel = 14 } : null;
        var label = new TextBlock { Text = text, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        var row = new DockPanel { LastChildFill = true };
        if (_count is not null)
        {
            _count.Set(count!.Value, false);
            DockPanel.SetDock(_count, Dock.Left);
            row.Children.Add(_count);
        }
        row.Children.Add(label);
        _message.Children.Add(row);
        if (previous is not null)
        {
            if (motion) Tween.Present(previous, false, -6, 0, Tween.Fast, ArcEasing.Standard, done: () => _message?.Children.Remove(previous));
            else _message.Children.Remove(previous);
        }
        if (motion && previous is not null) Tween.Present(row, true, 6, 0, Tween.Standard, ArcEasing.Enter);
    }

    private void ShowLead(FrameworkElement? face)
    {
        if (_lead is null || face is null) return;
        if (face.Parent == _lead && _lead.Children.Count == 1) return;
        foreach (var old in _lead.Children.OfType<FrameworkElement>().ToArray())
        {
            if (ReferenceEquals(old, face)) continue;
            _lead.Children.Remove(old);
        }
        if (face.Parent != _lead) _lead.Children.Add(face);
        if (Motion)
        {
            face.Opacity = 0;
            Tween.To(face, OpacityProperty, 1, Tween.Standard, ArcEasing.Enter);
        }
    }

    private FrameworkElement LeadGlyph(string data, string brush)
    {
        var host = new Grid { Width = 24, Height = 24, Children = { Lucide.Glyph(data, 16) } };
        host.SetResourceReference(TextElement.ForegroundProperty, $"ArcWpf.Brush.{brush}");
        return host;
    }

    private FrameworkElement LeadSpinner()
    {
        var spinner = RingSpinner.Create(Foreground, ArcMotion.GetLevel(this) != ArcMotionLevel.Off);
        return new Grid { Width = 24, Height = 24, Children = { spinner } };
    }

    private void SetUndoVisible(bool visible)
    {
        if (_undo is not null) _undo.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateUndoButton()
    {
        if (_kbd is not null) _kbd.Visibility = UndoShortcut ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateEmpty()
    {
        if (_empty is null) return;
        bool show = Empty is not null && !Containers().Any(c => c.IsOpen) && Pending.Count == Items.Count;
        if (show == (_empty.Visibility == Visibility.Visible)) return;
        _empty.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (show && Motion) Tween.Present(_empty, true, 6, 0, Tween.Standard, ArcEasing.Enter);
    }

    private void ShowToast()
    {
        _closeTimer?.Stop();
        _closeTimer = null;
        if (_toast is null || _toastOpen) { _toastOpen = true; return; }
        _toastOpen = true;
        _toast.Visibility = Visibility.Visible;
        if (_popup is not null)
        {
            _popup.PlacementTarget = Window.GetWindow(this);
            _popup.IsOpen = true;
        }
        var level = ArcMotion.GetLevel(this);
        if (!IsLoaded || level == ArcMotionLevel.Off)
        {
            Tween.Set(_toast, OpacityProperty, 1);
            Tween.Set(_toastMove, TranslateTransform.YProperty, 0);
            Tween.Set(_toastScale, ScaleTransform.ScaleXProperty, 1);
            Tween.Set(_toastScale, ScaleTransform.ScaleYProperty, 1);
            return;
        }
        Tween.Set(_toast, OpacityProperty, 0);
        Tween.To(_toast, OpacityProperty, 1, Tween.Standard, ArcEasing.Enter);
        if (level == ArcMotionLevel.Reduced) return;
        Tween.Set(_toastMove, TranslateTransform.YProperty, 18);
        Tween.Set(_toastScale, ScaleTransform.ScaleXProperty, .96);
        Tween.Set(_toastScale, ScaleTransform.ScaleYProperty, .96);
        Tween.Spring(_toastMove, TranslateTransform.YProperty, 0, Tween.Morph);
        Tween.Spring(_toastScale, ScaleTransform.ScaleXProperty, 1, Tween.Morph);
        Tween.Spring(_toastScale, ScaleTransform.ScaleYProperty, 1, Tween.Morph);
    }

    private void HideToast()
    {
        _closeTimer?.Stop();
        _closeTimer = null;
        if (_toast is null || !_toastOpen) { _toastOpen = false; return; }
        _toastOpen = false;
        void Done()
        {
            if (_toastOpen || _toast is null) return;
            _toast.Visibility = Visibility.Collapsed;
            if (_popup is not null) _popup.IsOpen = false;
            _message?.Children.Clear();
            _shape = string.Empty;
            _count = null;
        }
        if (!Motion) { Done(); return; }
        Tween.To(_toast, OpacityProperty, 0, Tween.Exit, ArcEasing.Standard, done: Done);
        Tween.To(_toastMove, TranslateTransform.YProperty, 12, Tween.Exit, ArcEasing.Standard);
    }

    private void CloseAfter(double milliseconds)
    {
        _closeTimer?.Stop();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); if (ReferenceEquals(_closeTimer, timer)) HideToast(); };
        _closeTimer = timer;
        timer.Start();
    }

    // A new removal gives the toast a small velocity nudge on its scale.
    private void Nudge()
    {
        if (!Motion) return;
        Tween.Set(_toastScale, ScaleTransform.ScaleXProperty, 1.018);
        Tween.Set(_toastScale, ScaleTransform.ScaleYProperty, 1.018);
        Tween.Spring(_toastScale, ScaleTransform.ScaleXProperty, 1, Tween.Snappy);
        Tween.Spring(_toastScale, ScaleTransform.ScaleYProperty, 1, Tween.Snappy);
    }

    // One value drained by a linear animation; hover, focus and a hidden window pause that same clock.
    private void StartClock()
    {
        StopClock();
        int batch = _batch;
        var animation = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(Duration)) { FillBehavior = FillBehavior.Stop };
        var clock = animation.CreateClock();
        clock.Completed += (_, _) => { if (ReferenceEquals(_clock, clock) && batch == _batch) Commit(); };
        _clock = clock;
        _paused = false;
        ApplyAnimationClock(RingProgressProperty, clock);
        ApplyRing(1);
        UpdatePause();
    }

    private void StopClock()
    {
        _clock = null;
        BeginAnimation(RingProgressProperty, null);
    }

    private void UpdatePause()
    {
        if (_clock?.Controller is not { } controller) return;
        bool pause = !IsVisible || (PauseOnHover && _toast is { } toast && (toast.IsMouseOver || toast.IsKeyboardFocusWithin));
        if (pause == _paused) return;
        _paused = pause;
        if (pause) controller.Pause();
        else controller.Resume();
    }

    private void ApplyRing(double progress)
    {
        double units = Math.Max(0, progress) * (2 * Math.PI * 10) / _ringFill.StrokeThickness;
        _ringFill.StrokeDashArray = [units, 1000];
        _ringFill.Visibility = progress <= 0 ? Visibility.Hidden : Visibility.Visible;
        _seconds.Set((int)Math.Ceiling(Math.Max(0, progress) * Duration / 1000), Motion);
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Z || Keyboard.Modifiers != ModifierKeys.Control || !UndoShortcut || !UndoAvailable) return;
        // Inside a text field the field's own undo wins.
        if (Keyboard.FocusedElement is TextBoxBase or PasswordBox) return;
        Undo();
        e.Handled = true;
    }

    private void OnToastKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || !UndoAvailable) return;
        Commit();
        e.Handled = true;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new UndoToastPeer(this);
    private sealed class UndoToastPeer(UndoToast owner) : ItemsControlAutomationPeer(owner)
    {
        protected override ItemAutomationPeer CreateItemAutomationPeer(object item) => new UndoToastItemPeer(item, this);
        protected override string GetNameCore() => string.IsNullOrEmpty(owner.Label) ? "Undo toast" : owner.Label;
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;
    }
    private sealed class UndoToastItemPeer(object item, ItemsControlAutomationPeer owner) : ItemAutomationPeer(item, owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;
        protected override string GetClassNameCore() => nameof(UndoToastContainer);
    }
}

/// <summary>Reports its child's full height scaled by the fold factor, so the rows below glide up to fill the gap.</summary>
public sealed class UndoToastFold : Decorator
{
    public static readonly DependencyProperty FactorProperty = DependencyProperty.Register(nameof(Factor), typeof(double), typeof(UndoToastFold), new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public double Factor { get => (double)GetValue(FactorProperty); set => SetValue(FactorProperty, value); }
    public UndoToastFold() => ClipToBounds = true;
    protected override Size MeasureOverride(Size constraint)
    {
        if (Child is null) return new Size();
        Child.Measure(new Size(constraint.Width, double.PositiveInfinity));
        return new Size(Child.DesiredSize.Width, Child.DesiredSize.Height * Math.Clamp(Factor, 0, 1));
    }
    protected override Size ArrangeOverride(Size arrangeSize)
    {
        Child?.Arrange(new Rect(0, 0, arrangeSize.Width, Child.DesiredSize.Height));
        return arrangeSize;
    }
}

[TemplatePart(Name = "PART_Fold", Type = typeof(UndoToastFold))]
[TemplatePart(Name = "PART_Content", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Glow", Type = typeof(FrameworkElement))]
public class UndoToastContainer : ContentControl
{
    private const double SlipX = 28;
    private UndoToastFold? _fold;
    private FrameworkElement? _content, _glow;
    private readonly TranslateTransform _slip = new();
    private readonly ScaleTransform _scale = new(1, 1);
    internal bool IsOpen { get; private set; } = true;
    private int _run;

    static UndoToastContainer()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(UndoToastContainer), new FrameworkPropertyMetadata(typeof(UndoToastContainer)));
        FocusableProperty.OverrideMetadata(typeof(UndoToastContainer), new FrameworkPropertyMetadata(false));
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _fold = GetTemplateChild("PART_Fold") as UndoToastFold;
        _content = GetTemplateChild("PART_Content") as FrameworkElement;
        _glow = GetTemplateChild("PART_Glow") as FrameworkElement;
        if (_content is not null)
        {
            var transform = new TransformGroup();
            transform.Children.Add(_scale);
            transform.Children.Add(_slip);
            _content.RenderTransform = transform;
            _content.RenderTransformOrigin = new Point(.5, .5);
        }
    }

    internal UndoToastRow? FindRow() => Find<UndoToastRow>(this);
    private static T? Find<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) return match;
            if (Find<T>(child) is { } nested) return nested;
        }
        return null;
    }

    // A folded row is inert: it leaves the tab order at once and the accessibility tree when it is done folding.
    internal void Fold(bool motion)
    {
        IsOpen = false;
        IsEnabled = false;
        int run = ++_run;
        if (_fold is null || _content is null || !motion)
        {
            Snap(0, 0, 1);
            Visibility = Visibility.Collapsed;
            return;
        }
        Tween.To(_content, OpacityProperty, 0, Tween.Exit, ArcEasing.Standard);
        Tween.To(_slip, TranslateTransform.XProperty, SlipX, Tween.Exit, ArcEasing.Standard);
        Tween.To(_scale, ScaleTransform.ScaleXProperty, .98, Tween.Exit, ArcEasing.Standard);
        Tween.To(_scale, ScaleTransform.ScaleYProperty, .98, Tween.Exit, ArcEasing.Standard);
        Tween.Spring(_fold, UndoToastFold.FactorProperty, 0, Tween.Smooth, done: () => { if (run == _run) Visibility = Visibility.Collapsed; });
    }

    // Rows reopen top to bottom; the content slides back after the height starts to open, with a faint accent glow where it returned.
    internal void Open(bool motion, double delay)
    {
        IsOpen = true;
        IsEnabled = true;
        Visibility = Visibility.Visible;
        ++_run;
        if (_fold is null || _content is null || !motion)
        {
            Snap(1, 1, 0);
            return;
        }
        Hold(_fold, UndoToastFold.FactorProperty);
        Hold(_content, OpacityProperty);
        Hold(_slip, TranslateTransform.XProperty);
        Tween.Spring(_fold, UndoToastFold.FactorProperty, 1, Tween.Smooth, delay);
        Tween.To(_content, OpacityProperty, 1, Tween.Standard, ArcEasing.Enter, delay + .06);
        Tween.Spring(_slip, TranslateTransform.XProperty, 0, Tween.Snappy, delay + .06);
        Tween.Spring(_scale, ScaleTransform.ScaleXProperty, 1, Tween.Snappy, delay + .06);
        Tween.Spring(_scale, ScaleTransform.ScaleYProperty, 1, Tween.Snappy, delay + .06);
        if (_glow is null) return;
        // undo-toast.module.css: 1.8s after a .12s delay, rising to full at 18%, each leg on the standard curve.
        var flash = new DoubleAnimationUsingKeyFrames { BeginTime = TimeSpan.FromSeconds(delay + .12), Duration = TimeSpan.FromSeconds(1.8) };
        flash.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(0)));
        flash.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromPercent(.18), ArcEasing.Standard));
        flash.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1), ArcEasing.Standard));
        _glow.BeginAnimation(OpacityProperty, flash);
    }

    // During a delayed start the base value shows, so pin it to where the row is now.
    private static void Hold(IAnimatable target, DependencyProperty property)
        => Tween.Set(target, property, (double)((DependencyObject)target).GetValue(property));

    private void Snap(double factor, double opacity, double glow)
    {
        if (_fold is not null) Tween.Set(_fold, UndoToastFold.FactorProperty, factor);
        if (_content is not null) Tween.Set(_content, OpacityProperty, opacity);
        if (_glow is not null) Tween.Set(_glow, OpacityProperty, 0);
        Tween.Set(_slip, TranslateTransform.XProperty, factor > 0 ? 0 : SlipX);
        Tween.Set(_scale, ScaleTransform.ScaleXProperty, 1);
        Tween.Set(_scale, ScaleTransform.ScaleYProperty, 1);
    }
}

/// <summary>A ready made row: leading media, title and description, and metadata that gives way to the action buttons on hover and focus without reflowing.</summary>
[TemplatePart(Name = "PART_Surface", Type = typeof(Border))]
[TemplatePart(Name = "PART_Meta", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Actions", Type = typeof(FrameworkElement))]
public class UndoToastRow : Control
{
    public static readonly DependencyProperty LeadingProperty = DependencyProperty.Register(nameof(Leading), typeof(object), typeof(UndoToastRow));
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(object), typeof(UndoToastRow));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(object), typeof(UndoToastRow));
    public static readonly DependencyProperty MetaProperty = DependencyProperty.Register(nameof(Meta), typeof(object), typeof(UndoToastRow));
    public static readonly DependencyProperty ActionsProperty = DependencyProperty.Register(nameof(Actions), typeof(object), typeof(UndoToastRow));
    public static readonly DependencyProperty UnreadProperty = DependencyProperty.Register(nameof(Unread), typeof(bool), typeof(UndoToastRow));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(UndoToastRow), new FrameworkPropertyMetadata(string.Empty, (d, e) => AutomationProperties.SetName(d, (string)e.NewValue)));
    private static readonly DependencyProperty ThemeProbeProperty = DependencyProperty.Register("ThemeProbe", typeof(object), typeof(UndoToastRow), new PropertyMetadata(null, (d, _) => ((UndoToastRow)d).UpdateHover(false)));
    public object? Leading { get => GetValue(LeadingProperty); set => SetValue(LeadingProperty, value); }
    public object? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public object? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public object? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }
    public object? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }
    public bool Unread { get => (bool)GetValue(UnreadProperty); set => SetValue(UnreadProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    private Border? _surface;
    private FrameworkElement? _meta, _actions;
    private readonly SolidColorBrush _fill = new(Colors.Transparent);
    private readonly TranslateTransform _actionsMove = new(6, 0);

    static UndoToastRow() => DefaultStyleKeyProperty.OverrideMetadata(typeof(UndoToastRow), new FrameworkPropertyMetadata(typeof(UndoToastRow)));
    public UndoToastRow() => SetResourceReference(ThemeProbeProperty, "ArcWpf.Brush.SurfaceMuted");

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _surface = GetTemplateChild("PART_Surface") as Border;
        _meta = GetTemplateChild("PART_Meta") as FrameworkElement;
        _actions = GetTemplateChild("PART_Actions") as FrameworkElement;
        if (_surface is not null) _surface.Background = _fill;
        if (_actions is not null) _actions.RenderTransform = _actionsMove;
        UpdateHover(false);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsMouseOverProperty || e.Property == IsKeyboardFocusWithinProperty) UpdateHover(IsLoaded);
    }

    private void UpdateHover(bool animate)
    {
        bool shown = IsMouseOver || IsKeyboardFocusWithin;
        var muted = Tween.Resolve(this, "ArcWpf.Brush.SurfaceMuted");
        Tween.Color(_fill, shown ? muted : Color.FromArgb(0, muted.R, muted.G, muted.B), Tween.Fast, animate && ArcMotion.GetLevel(this) != ArcMotionLevel.Off);
        if (_meta is null || _actions is null) return;
        _actions.IsHitTestVisible = shown;
        if (animate && ArcMotion.GetLevel(this) == ArcMotionLevel.Full)
        {
            Tween.To(_meta, OpacityProperty, shown ? 0 : 1, Tween.Fast, ArcEasing.Standard);
            Tween.To(_actions, OpacityProperty, shown ? 1 : 0, Tween.Fast, ArcEasing.Standard);
            Tween.To(_actionsMove, TranslateTransform.XProperty, shown ? 0 : 6, Tween.Fast, ArcEasing.Standard);
            return;
        }
        Tween.Set(_meta, OpacityProperty, shown ? 0 : 1);
        Tween.Set(_actions, OpacityProperty, shown ? 1 : 0);
        Tween.Set(_actionsMove, TranslateTransform.XProperty, shown ? 0 : 6);
    }

    internal IEnumerable<UndoToastRowAction> ActionsInOrder()
    {
        if (Actions is UndoToastRowAction single) return [single];
        return Actions is Panel panel ? panel.Children.OfType<UndoToastRowAction>() : [];
    }

    private UndoToast? Owner
    {
        get
        {
            for (DependencyObject? node = this; node is not null; node = VisualTreeHelper.GetParent(node))
                if (node is UndoToast toast) return toast;
            return null;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !IsKeyboardFocused || Keyboard.Modifiers != ModifierKeys.None) return;
        if (Owner?.TryShortcut(this, e.Key) == true) e.Handled = true;
    }
}

/// <summary>A round icon button for a row action, named by its label. Its click removes the row and does not bubble to it.</summary>
public class UndoToastRowAction : ButtonBase
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(string), typeof(UndoToastRowAction), new FrameworkPropertyMetadata(null, (d, e) => ((UndoToastRowAction)d).SetCurrentValue(ContentProperty, e.NewValue is string data ? Lucide.Glyph(data, 16) : null)));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(UndoToastRowAction), new FrameworkPropertyMetadata(string.Empty, (d, e) => { AutomationProperties.SetName(d, (string)e.NewValue); ((UndoToastRowAction)d).ToolTip = e.NewValue; }));
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(string), typeof(UndoToastRowAction), new FrameworkPropertyMetadata(null));
    /// <summary>Path data on the lucide 24-unit grid, drawn at 16 DIP.</summary>
    public string? Icon { get => (string?)GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <summary>The removal kind this action performs; the default kind when unset.</summary>
    public string? Kind { get => (string?)GetValue(KindProperty); set => SetValue(KindProperty, value); }

    static UndoToastRowAction() => DefaultStyleKeyProperty.OverrideMetadata(typeof(UndoToastRowAction), new FrameworkPropertyMetadata(typeof(UndoToastRowAction)));

    protected override void OnClick()
    {
        base.OnClick();
        for (DependencyObject? node = this; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is not UndoToast toast) continue;
            toast.RemoveRow(this, Kind);
            return;
        }
    }
}

/// <summary>A number whose changed digits roll: the old digit lifts away and the new one rises from below.</summary>
internal sealed class UndoRollNumber : StackPanel
{
    internal double Travel { get; set; } = 10;
    private readonly List<Grid> _slots = [];
    private string _value = string.Empty;

    public UndoRollNumber() => Orientation = Orientation.Horizontal;

    public void Set(int value, bool animate)
    {
        string text = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (text == _value) return;
        while (_slots.Count < text.Length)
        {
            var slot = new Grid();
            _slots.Add(slot);
            Children.Add(slot);
        }
        while (_slots.Count > text.Length)
        {
            Children.Remove(_slots[^1]);
            _slots.RemoveAt(_slots.Count - 1);
        }
        for (int i = 0; i < text.Length; i++)
        {
            var slot = _slots[i];
            var digit = text[i].ToString();
            var current = slot.Children.OfType<TextBlock>().LastOrDefault();
            if (current?.Text == digit) continue;
            var next = new TextBlock { Text = digit, RenderTransform = new TranslateTransform() };
            slot.Children.Add(next);
            if (current is null) continue;
            if (!animate)
            {
                slot.Children.Remove(current);
                continue;
            }
            Tween.Present(next, true, Travel, 0, Tween.Snappy, ArcEasing.Enter);
            Tween.Present(current, false, -Travel, 0, Tween.Snappy, ArcEasing.Enter, done: () => slot.Children.Remove(current));
        }
        _value = text;
    }
}
