using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public sealed class AnnouncementItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Message { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = string.Empty;
    public DateTimeOffset? CountdownTarget { get; set; }
    public string CountdownLabel { get; set; } = string.Empty;
}

/// <summary>A slim bar for the top of a page. Messages rotate on a ring clock, rise in while the bar springs to their height, and dismissing collapses the height so the page eases up.</summary>
[TemplatePart(Name = "PART_Root", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Bar", Type = typeof(Border))]
[TemplatePart(Name = "PART_Viewport", Type = typeof(Grid))]
[TemplatePart(Name = "PART_Controls", Type = typeof(FrameworkElement))]
public class AnnouncementBar : Control
{
    private const double Rise = .46, HeightSpring = .42, Collapse = .44, Ring = 2 * Math.PI * 8, NarrowWidth = 560, SoftBlur = 6;

    public static readonly DependencyProperty MessagesProperty = DependencyProperty.Register(nameof(Messages), typeof(ObservableCollection<string>), typeof(AnnouncementBar), new PropertyMetadata(null, OnSourceChanged));
    public static readonly DependencyProperty AnnouncementsProperty = DependencyProperty.Register(nameof(Announcements), typeof(ObservableCollection<AnnouncementItem>), typeof(AnnouncementBar), new PropertyMetadata(null, OnSourceChanged));
    public static readonly DependencyProperty IndexProperty = DependencyProperty.Register(nameof(Index), typeof(int), typeof(AnnouncementBar), new PropertyMetadata(0, OnIndexChanged));
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(AnnouncementBar), new PropertyMetadata(true, (d, _) => ((AnnouncementBar)d).UpdateOpen(true)));
    public static readonly DependencyProperty AutoPlayProperty = DependencyProperty.Register(nameof(AutoPlay), typeof(bool), typeof(AnnouncementBar), new PropertyMetadata(true, (d, _) => ((AnnouncementBar)d).UpdateClock()));
    public static readonly DependencyProperty IntervalProperty = DependencyProperty.Register(nameof(Interval), typeof(TimeSpan), typeof(AnnouncementBar), new PropertyMetadata(TimeSpan.FromSeconds(6), (d, _) => ((AnnouncementBar)d).UpdateClock()));
    public static readonly DependencyProperty DismissibleProperty = DependencyProperty.Register(nameof(Dismissible), typeof(bool), typeof(AnnouncementBar), new PropertyMetadata(true, (d, _) => ((AnnouncementBar)d).UpdateControls()));
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(nameof(Tone), typeof(string), typeof(AnnouncementBar), new PropertyMetadata("neutral"));
    public static readonly DependencyProperty ControlsProperty = DependencyProperty.Register(nameof(Controls), typeof(bool), typeof(AnnouncementBar), new PropertyMetadata(false, (d, _) => ((AnnouncementBar)d).UpdateControls()));
    public static readonly DependencyProperty IsPausedProperty = DependencyProperty.Register(nameof(IsPaused), typeof(bool), typeof(AnnouncementBar), new PropertyMetadata(false, (d, _) => ((AnnouncementBar)d).UpdateClock()));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(AnnouncementBar), new PropertyMetadata("Announcements", (d, e) => AutomationProperties.SetName(d, (string)e.NewValue)));
    public static readonly DependencyProperty MutedBrushProperty = DependencyProperty.Register(nameof(MutedBrush), typeof(Brush), typeof(AnnouncementBar));
    public static readonly DependencyProperty IconHoverBrushProperty = DependencyProperty.Register(nameof(IconHoverBrush), typeof(Brush), typeof(AnnouncementBar));
    public static readonly DependencyProperty IconHoverForegroundProperty = DependencyProperty.Register(nameof(IconHoverForeground), typeof(Brush), typeof(AnnouncementBar));
    private static readonly DependencyProperty ProgressProperty = DependencyProperty.Register("Progress", typeof(double), typeof(AnnouncementBar), new PropertyMetadata(0d, (d, _) => ((AnnouncementBar)d).UpdateRing()));

    public ObservableCollection<string> Messages { get => (ObservableCollection<string>)GetValue(MessagesProperty); set => SetValue(MessagesProperty, value); }
    public ObservableCollection<AnnouncementItem> Announcements { get => (ObservableCollection<AnnouncementItem>)GetValue(AnnouncementsProperty); set => SetValue(AnnouncementsProperty, value); }
    public int Index { get => (int)GetValue(IndexProperty); set => SetValue(IndexProperty, value); }
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public bool AutoPlay { get => (bool)GetValue(AutoPlayProperty); set => SetValue(AutoPlayProperty, value); }
    public TimeSpan Interval { get => (TimeSpan)GetValue(IntervalProperty); set => SetValue(IntervalProperty, value); }
    public bool Dismissible { get => (bool)GetValue(DismissibleProperty); set => SetValue(DismissibleProperty, value); }
    public string Tone { get => (string)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
    public bool Controls { get => (bool)GetValue(ControlsProperty); set => SetValue(ControlsProperty, value); }
    public bool IsPaused { get => (bool)GetValue(IsPausedProperty); set => SetValue(IsPausedProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public Brush MutedBrush { get => (Brush)GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }
    public Brush IconHoverBrush { get => (Brush)GetValue(IconHoverBrushProperty); set => SetValue(IconHoverBrushProperty, value); }
    public Brush IconHoverForeground { get => (Brush)GetValue(IconHoverForegroundProperty); set => SetValue(IconHoverForegroundProperty, value); }

    public event EventHandler<AnnouncementItem>? ActionInvoked;
    public event EventHandler<AnnouncementItem>? CountdownEnded;

    private FrameworkElement? _root, _controls;
    private Border? _bar;
    private Grid? _viewport;
    private ColumnDefinition? _side;
    private ButtonBase? _previous, _pause, _next, _close;
    private Ellipse? _ringFill;
    private Decorator? _pauseIcon;
    private AnnouncementFace? _face;
    private INotifyCollectionChanged? _source;
    private int _direction = 1;
    private readonly SpringDriver _heightDriver;
    private readonly ControlSpring _height;
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _rotating;

    static AnnouncementBar() => DefaultStyleKeyProperty.OverrideMetadata(typeof(AnnouncementBar), new FrameworkPropertyMetadata(typeof(AnnouncementBar)));

    public AnnouncementBar()
    {
        _heightDriver = new SpringDriver(() => { if (_viewport is not null) _viewport.Height = Math.Max(0, _height!.Position); });
        _height = _heightDriver.Add(0, HeightSpring);
        Messages = [];
        Announcements = [];
        AutomationProperties.SetName(this, Label);
        _tick.Tick += (_, _) => _face?.Tick(ArcMotion.GetLevel(this));
        Loaded += (_, _) => { _tick.Start(); ShowCurrent(false); UpdateClock(); };
        Unloaded += (_, _) => { _tick.Stop(); StopClock(); _heightDriver.Stop(); };
        SizeChanged += (_, e) => { if (e.WidthChanged) { UpdateNarrow(); Reflow(); } };
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _root = GetTemplateChild("PART_Root") as FrameworkElement;
        _bar = GetTemplateChild("PART_Bar") as Border;
        _viewport = GetTemplateChild("PART_Viewport") as Grid;
        _controls = GetTemplateChild("PART_Controls") as FrameworkElement;
        _side = GetTemplateChild("PART_Side") as ColumnDefinition;
        _ringFill = GetTemplateChild("PART_RingFill") as Ellipse;
        _pauseIcon = GetTemplateChild("PART_PauseIcon") as Decorator;
        _previous = Wire("PART_Previous", Previous);
        _next = Wire("PART_Next", Next);
        _pause = Wire("PART_Pause", () => IsPaused = !IsPaused);
        _close = Wire("PART_Close", Close);
        if (GetTemplateChild("PART_PreviousIcon") is Decorator up) up.Child = Lucide.Glyph(Lucide.ChevronUp, 16);
        if (GetTemplateChild("PART_NextIcon") is Decorator down) down.Child = Lucide.Glyph(Lucide.ChevronDown, 16);
        if (GetTemplateChild("PART_CloseIcon") is Decorator x) x.Child = Lucide.Glyph(Lucide.X, 16);
        _face = null;
        _viewport?.Children.Clear();
        AttachSource();
        UpdateControls();
        UpdateNarrow();
        UpdateRing();
        ShowCurrent(false);
        UpdateOpen(false);
    }

    private ButtonBase? Wire(string name, Action action)
    {
        if (GetTemplateChild(name) is not ButtonBase button) return null;
        button.Click += (_, _) => action();
        return button;
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((AnnouncementBar)d).AttachSource();

    private void AttachSource()
    {
        if (_source is not null) _source.CollectionChanged -= OnItemsChanged;
        _source = Announcements is { Count: > 0 } ? Announcements : Messages;
        if (_source is not null) _source.CollectionChanged += OnItemsChanged;
        if (IsInitialized) ShowCurrent(false);
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) { if (!ReferenceEquals(sender, Announcements) && Announcements is { Count: > 0 }) return; AttachSource(); UpdateControls(); UpdateClock(); }

    private int Count => Announcements is { Count: > 0 } ? Announcements.Count : Messages?.Count ?? 0;

    private AnnouncementItem? Current
    {
        get
        {
            int total = Count;
            if (total == 0) return null;
            int at = (Index % total + total) % total;
            return Announcements is { Count: > 0 } ? Announcements[at] : new AnnouncementItem { Id = "message" + at, Message = Messages[at] };
        }
    }

    private static void OnIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var bar = (AnnouncementBar)d;
        bar.StopClock();
        bar.SetValue(ProgressProperty, 0d);
        bar.ShowCurrent(true);
        bar.UpdateClock();
    }

    public void Next() { if (Count > 1) { _direction = 1; Index = (Index + 1) % Count; } }
    public void Previous() { if (Count > 1) { _direction = -1; Index = (Index - 1 + Count) % Count; } }
    public void Close() => IsOpen = false;

    // Next rises from below and leaves upward; previous runs the other way.
    private void ShowCurrent(bool animate)
    {
        if (_viewport is null) return;
        var item = Current;
        if (item is null) { _viewport.Children.Clear(); _face = null; return; }
        var level = ArcMotion.GetLevel(this);
        bool full = animate && IsLoaded && level == ArcMotionLevel.Full;
        bool reduced = animate && IsLoaded && level == ArcMotionLevel.Reduced;
        var old = _face;
        if (old is not null && old.Item.Id == item.Id && old.Item.Message == item.Message) { Reflow(); return; }
        var face = new AnnouncementFace(this, item, position: Index + 1, total: Count);
        _face = face;
        if (old is not null)
        {
            old.IsEnabled = false;
            if (full)
            {
                double y = old.ActualHeight * -.6 * _direction;
                Tween.Spring((TranslateTransform)old.RenderTransform, TranslateTransform.YProperty, y, Rise);
                Tween.To(old, OpacityProperty, 0, Tween.Fast, ArcEasing.Standard, 0, () => _viewport.Children.Remove(old));
                old.Effect = new BlurEffect { Radius = 0 };
                old.Effect.BeginAnimation(BlurEffect.RadiusProperty, new DoubleAnimation(SoftBlur, TimeSpan.FromSeconds(Tween.Fast)) { EasingFunction = ArcEasing.Standard });
            }
            else if (reduced) Tween.To(old, OpacityProperty, 0, Tween.Instant, ArcEasing.Standard, 0, () => _viewport.Children.Remove(old));
            else _viewport.Children.Remove(old);
        }
        _viewport.Children.Add(face);
        double width = _viewport.ActualWidth > 0 ? _viewport.ActualWidth : double.PositiveInfinity;
        if (old is not null) _viewport.Height = _viewport.ActualHeight;
        face.Measure(new Size(width, double.PositiveInfinity));
        double target = Math.Max(32, face.DesiredSize.Height);
        if (full)
        {
            ((TranslateTransform)face.RenderTransform).Y = target * .7 * _direction;
            Tween.Spring((TranslateTransform)face.RenderTransform, TranslateTransform.YProperty, 0, Rise);
            face.Opacity = 0;
            Tween.To(face, OpacityProperty, 1, Tween.Standard, ArcEasing.Enter);
            var blur = new BlurEffect { Radius = SoftBlur };
            face.Effect = blur;
            var clear = new DoubleAnimation(0, TimeSpan.FromSeconds(Tween.Standard)) { EasingFunction = ArcEasing.Enter };
            clear.Completed += (_, _) => { if (ReferenceEquals(face.Effect, blur)) face.Effect = null; };
            blur.BeginAnimation(BlurEffect.RadiusProperty, clear);
            if (_height.Position <= 0) _height.Snap(_viewport.ActualHeight);
            _heightDriver.Animate(_height, target, HeightSpring);
        }
        else
        {
            if (reduced) { face.Opacity = 0; Tween.To(face, OpacityProperty, 1, Tween.Standard, ArcEasing.Standard); }
            _heightDriver.Stop();
            _heightDriver.Snap(_height, target);
            _viewport.Height = target;
        }
    }

    // A new width can rewrap the message, so the bar follows it without animating.
    private void Reflow()
    {
        if (_viewport is null || _face is null || _viewport.ActualWidth <= 0) return;
        _face.Measure(new Size(_viewport.ActualWidth, double.PositiveInfinity));
        double target = Math.Max(32, _face.DesiredSize.Height);
        _heightDriver.Stop();
        _heightDriver.Snap(_height, target);
        _viewport.Height = target;
    }

    internal void RaiseAction(AnnouncementItem item) => ActionInvoked?.Invoke(this, item);
    internal void RaiseCountdownEnded(AnnouncementItem item) => CountdownEnded?.Invoke(this, item);

    // The side column mirrors the controls, so the message stays centered on the page; narrow bars drop it and read from the start edge.
    private void UpdateControls()
    {
        if (_controls is null) return;
        bool navigable = Controls && Count > 1;
        bool rotating = navigable && AutoPlay && ArcMotion.GetLevel(this) != ArcMotionLevel.Off && ArcMotion.GetLevel(this) != ArcMotionLevel.Reduced;
        Show(_previous, navigable);
        Show(_next, navigable);
        Show(_pause, rotating);
        Show(_close, Dismissible);
        UpdateNarrow();
    }

    private static void Show(UIElement? element, bool shown) { if (element is not null) element.Visibility = shown ? Visibility.Visible : Visibility.Collapsed; }

    private int ControlCount => new UIElement?[] { _previous, _pause, _next, _close }.Count(button => button?.Visibility == Visibility.Visible);

    private void UpdateNarrow()
    {
        if (_bar is null || _viewport is null || _side is null) return;
        bool narrow = ActualWidth > 0 && ActualWidth <= NarrowWidth;
        int count = ControlCount;
        _side.Width = new GridLength(narrow ? 0 : count * 32 + Math.Max(count - 1, 0) * 2);
        _bar.Padding = new Thickness(narrow ? 14 : 16, 6, 8, 6);
        double gap = narrow ? 8 : 12;
        _viewport.Margin = new Thickness(gap, 0, gap, 0);
        foreach (var face in _viewport.Children.OfType<AnnouncementFace>()) face.SetNarrow(narrow);
    }

    internal bool IsNarrow => ActualWidth > 0 && ActualWidth <= NarrowWidth;

    protected override void OnMouseEnter(System.Windows.Input.MouseEventArgs e) { base.OnMouseEnter(e); UpdateClock(); }
    protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e) { base.OnMouseLeave(e); UpdateClock(); }
    protected override void OnIsKeyboardFocusWithinChanged(DependencyPropertyChangedEventArgs e) { base.OnIsKeyboardFocusWithinChanged(e); UpdateClock(); }

    // The ring is the clock: its progress runs to one, then advances. Pausing stops it where it is; resuming finishes the rest.
    private void UpdateClock()
    {
        var level = ArcMotion.GetLevel(this);
        bool rotating = AutoPlay && level == ArcMotionLevel.Full && Count > 1;
        bool running = rotating && IsLoaded && IsOpen && !IsPaused && !IsMouseOver && !IsKeyboardFocusWithin;
        if (_pause is not null) AutomationProperties.SetName(_pause, IsPaused ? "Resume announcements" : "Pause announcements");
        if (_pauseIcon is not null) _pauseIcon.Child = Lucide.Glyph(IsPaused ? Lucide.Play : Lucide.Pause, 9, 2.4);
        if (running == _rotating) return;
        _rotating = running;
        if (!running) { StopClock(); return; }
        double from = (double)GetValue(ProgressProperty);
        var duration = Interval <= TimeSpan.Zero ? TimeSpan.FromSeconds(6) : Interval;
        var run = new DoubleAnimation(from, 1, TimeSpan.FromTicks(Math.Max(1, (long)(duration.Ticks * (1 - from)))));
        run.Completed += (_, _) =>
        {
            if (!_rotating) return;
            _rotating = false;
            BeginAnimation(ProgressProperty, null);
            SetValue(ProgressProperty, 0d);
            Next();
        };
        BeginAnimation(ProgressProperty, run);
    }

    private void StopClock()
    {
        _rotating = false;
        double at = (double)GetValue(ProgressProperty);
        BeginAnimation(ProgressProperty, null);
        SetValue(ProgressProperty, at);
    }

    private void UpdateRing()
    {
        if (_ringFill is null) return;
        double units = Ring / _ringFill.StrokeThickness;
        _ringFill.StrokeDashArray = [units, units];
        _ringFill.StrokeDashOffset = units * (1 - (double)GetValue(ProgressProperty));
    }

    // The outer section animates its height to zero on dismiss, so whatever sits below eases up with it.
    private void UpdateOpen(bool animate)
    {
        if (_root is null) { UpdateClock(); return; }
        var level = ArcMotion.GetLevel(this);
        bool motion = animate && IsLoaded;
        bool full = motion && level == ArcMotionLevel.Full;
        if (IsOpen)
        {
            _root.Visibility = Visibility.Visible;
            if (full)
            {
                double from = _root.ActualHeight;
                _root.Measure(new Size(ActualWidth, double.PositiveInfinity));
                _root.BeginAnimation(HeightProperty, null);
                _root.Height = double.NaN;
                _root.Measure(new Size(ActualWidth, double.PositiveInfinity));
                double to = _root.DesiredSize.Height;
                _root.Height = from;
                Tween.Spring(_root, HeightProperty, to, Collapse, 0, () => { _root.BeginAnimation(HeightProperty, null); _root.Height = double.NaN; });
                Tween.To(_root, OpacityProperty, 1, Tween.Standard, ArcEasing.Enter);
            }
            else if (motion) { _root.Height = double.NaN; _root.Opacity = 0; Tween.To(_root, OpacityProperty, 1, Tween.Fast, ArcEasing.Standard); }
            else { Tween.Set(_root, OpacityProperty, 1); _root.BeginAnimation(HeightProperty, null); _root.Height = double.NaN; }
        }
        else if (full)
        {
            Tween.Set(_root, HeightProperty, _root.ActualHeight);
            Tween.Spring(_root, HeightProperty, 0, Collapse, 0, () => _root.Visibility = Visibility.Collapsed);
            Tween.To(_root, OpacityProperty, 0, Tween.Exit, ArcEasing.Standard);
        }
        else if (motion) Tween.To(_root, OpacityProperty, 0, Tween.Fast, ArcEasing.Standard, 0, () => _root.Visibility = Visibility.Collapsed);
        else _root.Visibility = Visibility.Collapsed;
        UpdateClock();
    }
}

/// <summary>Lays the message, countdown and action out as one wrapped, centered run: gap 2 between lines, 12 between items.</summary>
internal sealed class AnnouncementFlow : Panel
{
    public double ColumnGap { get; set; } = 12;
    public bool Centered { get; set; } = true;

    protected override Size MeasureOverride(Size available)
    {
        double width = 0, height = 0, line = 0, lineHeight = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(available);
            var size = child.DesiredSize;
            double next = line == 0 ? size.Width : line + ColumnGap + size.Width;
            if (line > 0 && next > available.Width) { height += lineHeight + 2; width = Math.Max(width, line); line = size.Width; lineHeight = size.Height; continue; }
            line = next;
            lineHeight = Math.Max(lineHeight, size.Height);
        }
        return new Size(Math.Max(width, line), height + lineHeight);
    }

    protected override Size ArrangeOverride(Size final)
    {
        double y = 0;
        var row = new List<UIElement>();
        double line = 0, lineHeight = 0;
        void Flush()
        {
            double x = Centered ? (final.Width - line) / 2 : 0;
            foreach (var item in row)
            {
                item.Arrange(new Rect(x, y, item.DesiredSize.Width, lineHeight));
                x += item.DesiredSize.Width + ColumnGap;
            }
            y += lineHeight + 2;
            row.Clear();
            line = 0;
            lineHeight = 0;
        }
        foreach (UIElement child in InternalChildren)
        {
            var size = child.DesiredSize;
            if (row.Count > 0 && line + ColumnGap + size.Width > final.Width) Flush();
            line = row.Count == 0 ? size.Width : line + ColumnGap + size.Width;
            lineHeight = Math.Max(lineHeight, size.Height);
            row.Add(child);
        }
        if (row.Count > 0) Flush();
        return final;
    }
}

/// <summary>One message: its copy, optional live countdown and call to action.</summary>
internal sealed class AnnouncementFace : Grid
{
    private readonly AnnouncementBar _owner;
    private readonly AnnouncementFlow _flow = new();
    private readonly AnnouncementTime? _time;
    public AnnouncementItem Item { get; }

    public AnnouncementFace(AnnouncementBar owner, AnnouncementItem item, int position, int total)
    {
        _owner = owner;
        Item = item;
        VerticalAlignment = VerticalAlignment.Top;
        MinHeight = 32;
        RenderTransform = new TranslateTransform();
        _flow.Margin = new Thickness(0, 6, 0, 6);
        _flow.VerticalAlignment = VerticalAlignment.Center;
        if (total > 1) AutomationProperties.SetName(this, $"{position} of {total}");
        var text = new TextBlock { Text = item.Message, TextWrapping = TextWrapping.Wrap, FontSize = 14, LineHeight = 19.6, LineStackingStrategy = LineStackingStrategy.BlockLineHeight };
        _flow.Children.Add(text);
        if (item.CountdownTarget is { } target)
        {
            _time = new AnnouncementTime(owner, item, target);
            _flow.Children.Add(_time);
        }
        if (!string.IsNullOrWhiteSpace(item.ActionLabel))
        {
            var action = new Button { Content = item.ActionLabel };
            action.SetResourceReference(StyleProperty, "ArcWpf.AnnouncementBar.Action");
            action.Click += (_, _) => owner.RaiseAction(item);
            _flow.Children.Add(action);
        }
        SetNarrow(owner.IsNarrow);
        Children.Add(_flow);
    }

    public void SetNarrow(bool narrow)
    {
        _flow.Centered = !narrow;
        _flow.ColumnGap = narrow ? 10 : 12;
        _flow.InvalidateMeasure();
    }

    public void Tick(ArcMotionLevel level) => _time?.Update(level);
}

/// <summary>The countdown is one run of text: every cell shares the line, so digits, colons and the day mark sit on the message baseline.</summary>
internal sealed class AnnouncementTime : StackPanel
{
    private readonly AnnouncementBar _owner;
    private readonly AnnouncementItem _item;
    private readonly DateTimeOffset _target;
    private readonly StackPanel _cells = new() { Orientation = Orientation.Horizontal };
    private readonly List<AnnouncementDigit> _digits = [];
    private bool _ended;
    private int _dayDigits = -1;

    public AnnouncementTime(AnnouncementBar owner, AnnouncementItem item, DateTimeOffset target)
    {
        _owner = owner;
        _item = item;
        _target = target;
        Orientation = Orientation.Horizontal;
        if (!string.IsNullOrEmpty(item.CountdownLabel))
        {
            var label = Muted(item.CountdownLabel, FontWeights.Normal);
            label.Margin = new Thickness(0, 0, 6, 0);
            Children.Add(label);
        }
        Children.Add(_cells);
        Update(ArcMotionLevel.Off);
    }

    private TextBlock Muted(string text, FontWeight weight)
    {
        var block = new TextBlock { Text = text, FontWeight = weight, FontSize = 14, LineHeight = 19.6, LineStackingStrategy = LineStackingStrategy.BlockLineHeight };
        block.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding(nameof(AnnouncementBar.MutedBrush)) { Source = _owner });
        return block;
    }

    public void Update(ArcMotionLevel level)
    {
        long total = Math.Max(0, (long)Math.Floor((_target - DateTimeOffset.UtcNow).TotalSeconds));
        long days = total / 86400;
        string day = days > 0 ? days.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
        string clock = $"{total % 86400 / 3600:00}{total % 3600 / 60:00}{total % 60:00}";
        if (day.Length != _dayDigits) Build(day.Length);
        string all = day + clock;
        for (int i = 0; i < all.Length; i++) _digits[i].Set(all[i], level);
        AutomationProperties.SetName(this, $"{(days > 0 ? $"{days} days " : string.Empty)}{total % 86400 / 3600} hours {total % 3600 / 60} minutes");
        if (total == 0 && !_ended) { _ended = true; _owner.RaiseCountdownEnded(_item); }
    }

    private void Build(int dayDigits)
    {
        _dayDigits = dayDigits;
        _cells.Children.Clear();
        _digits.Clear();
        void Digits(int count) { for (int i = 0; i < count; i++) { var digit = new AnnouncementDigit(); _digits.Add(digit); _cells.Children.Add(digit); } }
        if (dayDigits > 0)
        {
            Digits(dayDigits);
            var mark = Muted("d", FontWeights.Normal);
            mark.Margin = new Thickness(1, 0, 5.6, 0);
            _cells.Children.Add(mark);
        }
        for (int part = 0; part < 3; part++)
        {
            if (part > 0)
            {
                var sep = Muted(":", FontWeights.Normal);
                sep.Margin = new Thickness(1, 0, 1, 0);
                _cells.Children.Add(sep);
            }
            Digits(2);
        }
    }
}

/// <summary>One character cell. A changed digit drops in from above while the old one falls away, like a counter winding down.</summary>
internal sealed class AnnouncementDigit : Grid
{
    private TextBlock? _face;
    private char _char;

    public AnnouncementDigit()
    {
        ClipToBounds = true;
        Children.Add(new TextBlock { Text = "0", Visibility = Visibility.Hidden, FontSize = 14, FontWeight = FontWeights.Medium, LineHeight = 19.6, LineStackingStrategy = LineStackingStrategy.BlockLineHeight });
    }

    public void Set(char value, ArcMotionLevel level)
    {
        if (_face is not null && _char == value) return;
        _char = value;
        var next = new TextBlock { Text = value.ToString(), FontSize = 14, FontWeight = FontWeights.Medium, LineHeight = 19.6, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, TextAlignment = TextAlignment.Center, RenderTransform = new TranslateTransform() };
        var old = _face;
        _face = next;
        Children.Add(next);
        if (old is null || level == ArcMotionLevel.Off) { if (old is not null) Children.Remove(old); return; }
        const double Travel = 19.6 * .7;
        if (level == ArcMotionLevel.Reduced)
        {
            next.Opacity = 0;
            Tween.To(next, OpacityProperty, 1, Tween.Instant, null);
            Tween.To(old, OpacityProperty, 0, Tween.Instant, null, 0, () => Children.Remove(old));
            return;
        }
        var move = (TranslateTransform)next.RenderTransform;
        move.Y = -Travel;
        next.Opacity = 0;
        Tween.Spring(move, TranslateTransform.YProperty, 0, .32);
        Tween.To(next, OpacityProperty, 1, Tween.Fast, ArcEasing.Standard);
        Tween.Spring((TranslateTransform)old.RenderTransform, TranslateTransform.YProperty, Travel, .32);
        Tween.To(old, OpacityProperty, 0, Tween.Fast, ArcEasing.Standard, 0, () => Children.Remove(old));
    }
}
