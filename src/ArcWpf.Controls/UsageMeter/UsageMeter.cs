using System.Globalization;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Automation;

namespace ArcWpf.Controls;

public sealed class UsageMeterSegment : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _label = string.Empty;
    private double _value;
    public string Id { get => _id; set => Set(ref _id, value); }
    public string Label { get => _label; set => Set(ref _label, value); }
    public double Value
    {
        get => _value;
        set
        {
            if (!double.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            Set(ref _value, value);
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public enum UsageMeterStatus { Normal, Near, Over }

public partial class UsageMeter : Control
{
    public static readonly DependencyProperty UsedProperty = DependencyProperty.Register(nameof(Used), typeof(double), typeof(UsageMeter), new FrameworkPropertyMetadata(0d, OnVisualChanged), value => value is double number && double.IsFinite(number) && number >= 0);
    public static readonly DependencyProperty LimitProperty = DependencyProperty.Register(nameof(Limit), typeof(double), typeof(UsageMeter), new FrameworkPropertyMetadata(100d, OnVisualChanged), value => value is double number && double.IsFinite(number) && number >= 0);
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(UsageMeter), new PropertyMetadata("Usage", OnVisualChanged));
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(nameof(Unit), typeof(string), typeof(UsageMeter), new PropertyMetadata(string.Empty, OnVisualChanged));
    public static readonly DependencyProperty WarningThresholdProperty = DependencyProperty.Register(nameof(WarningThreshold), typeof(double), typeof(UsageMeter), new PropertyMetadata(.9d, OnVisualChanged), value => value is double number && double.IsFinite(number) && number is >= 0 and <= 1);
    public static readonly DependencyProperty DecimalsProperty = DependencyProperty.Register(nameof(Decimals), typeof(int), typeof(UsageMeter), new PropertyMetadata(1, OnVisualChanged), value => value is int number && number is >= 0 and <= 3);
    public static readonly DependencyProperty FreeLabelProperty = DependencyProperty.Register(nameof(FreeLabel), typeof(string), typeof(UsageMeter), new PropertyMetadata("Free", OnVisualChanged));
    public static readonly DependencyProperty OverLabelProperty = DependencyProperty.Register(nameof(OverLabel), typeof(string), typeof(UsageMeter), new PropertyMetadata("Over limit", OnVisualChanged));
    private static readonly DependencyPropertyKey PercentagePropertyKey = DependencyProperty.RegisterReadOnly(nameof(Percentage), typeof(double), typeof(UsageMeter), new FrameworkPropertyMetadata(0d));
    public static readonly DependencyProperty PercentageProperty = PercentagePropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey IsWarningPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsWarning), typeof(bool), typeof(UsageMeter), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsWarningProperty = IsWarningPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey TotalPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Total), typeof(double), typeof(UsageMeter), new PropertyMetadata(0d));
    public static readonly DependencyProperty TotalProperty = TotalPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey FreePropertyKey = DependencyProperty.RegisterReadOnly(nameof(Free), typeof(double), typeof(UsageMeter), new PropertyMetadata(0d));
    public static readonly DependencyProperty FreeProperty = FreePropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey OverPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Over), typeof(double), typeof(UsageMeter), new PropertyMetadata(0d));
    public static readonly DependencyProperty OverProperty = OverPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey StatusPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Status), typeof(UsageMeterStatus), typeof(UsageMeter), new PropertyMetadata(UsageMeterStatus.Normal));
    public static readonly DependencyProperty StatusProperty = StatusPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey StatusTextPropertyKey = DependencyProperty.RegisterReadOnly(nameof(StatusText), typeof(string), typeof(UsageMeter), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty StatusTextProperty = StatusTextPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey CaptionPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Caption), typeof(string), typeof(UsageMeter), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty CaptionProperty = CaptionPropertyKey.DependencyProperty;
    private Border? _fill;
    private ScaleTransform? _scale;
    private readonly OwnedMotion _motion = new();
    private TextBlock? _value;
    private Grid? _numberHost;
    private OdometerNumber? _ticker;
    private readonly HashSet<UsageMeterSegment> _observed = [];
    private Border? _track;
    private Canvas? _segmentsCanvas;
    private Border? _overTint;
    private Rectangle? _overHatch;
    private Border? _limitMarker;
    private WrapPanel? _legend;
    private readonly List<(UsageMeterSegment Segment, Border Visual)> _segmentVisuals = [];
    private readonly List<LegendEntry> _legendEntries = [];
    private string? _hoveredId, _focusedId, _pinnedId;
    private int _rovingIndex;
    private sealed record LegendEntry(string Id, ToggleButton Button, TextBlock Label, TextBlock Value, OdometerNumber Ticker, TextBlock Unit, Border Swatch);
    public double Used { get => (double)GetValue(UsedProperty); set => SetValue(UsedProperty, value); }
    public double Limit { get => (double)GetValue(LimitProperty); set => SetValue(LimitProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public double WarningThreshold { get => (double)GetValue(WarningThresholdProperty); set => SetValue(WarningThresholdProperty, value); }
    public int Decimals { get => (int)GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }
    public string FreeLabel { get => (string)GetValue(FreeLabelProperty); set => SetValue(FreeLabelProperty, value); }
    public string OverLabel { get => (string)GetValue(OverLabelProperty); set => SetValue(OverLabelProperty, value); }
    public ObservableCollection<UsageMeterSegment> Segments { get; } = [];
    public double Percentage => (double)GetValue(PercentageProperty);
    public bool IsWarning => (bool)GetValue(IsWarningProperty);
    public double Total => (double)GetValue(TotalProperty);
    public double Free => (double)GetValue(FreeProperty);
    public double Over => (double)GetValue(OverProperty);
    public UsageMeterStatus Status => (UsageMeterStatus)GetValue(StatusProperty);
    public string StatusText => (string)GetValue(StatusTextProperty);
    public string Caption => (string)GetValue(CaptionProperty);
    static UsageMeter() { DefaultStyleKeyProperty.OverrideMetadata(typeof(UsageMeter), new FrameworkPropertyMetadata(typeof(UsageMeter))); }
    public UsageMeter()
    {
        Segments.CollectionChanged += SegmentsChanged;
        Loaded += (_, _) => StartRevealTracking();
        Unloaded += (_, _) => { _motion.Stop(); StopNumberMotion(); StopBadgeMotion(); StopRevealTracking(); };
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }
    private void SegmentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var segment in _observed.Except(Segments).ToArray())
        {
            segment.PropertyChanged -= SegmentChanged;
            _observed.Remove(segment);
        }
        foreach (var segment in Segments)
            if (_observed.Add(segment)) segment.PropertyChanged += SegmentChanged;
        RebuildSegments();
        RebuildLegend();
        Refresh();
        StartRevealTracking();
    }
    private void SegmentChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UsageMeterSegment.Id)) { RebuildSegments(); RebuildLegend(); }
        Refresh();
    }
    public override void OnApplyTemplate()
    {
        _motion.Stop();
        _ticker?.Stop();
        StopBadgeMotion();
        if (_track is not null) _track.SizeChanged -= TrackSizeChanged;
        if (_segmentsCanvas is not null)
        {
            _segmentsCanvas.MouseMove -= TrackMouseMove;
            _segmentsCanvas.MouseLeave -= TrackMouseLeave;
            _segmentsCanvas.TouchUp -= TrackTouchUp;
        }
        ClearLegend();
        base.OnApplyTemplate();
        _fill = GetTemplateChild("PART_Fill") as Border;
        _value = GetTemplateChild("PART_Value") as TextBlock;
        _badge = GetTemplateChild("PART_StatusBadge") as Border;
        _badgeText = GetTemplateChild("PART_StatusBadgeText") as TextBlock;
        _badgeWidthMotion.Attach(_badge);
        _numberHost = GetTemplateChild("PART_NumberHost") as Grid;
        _ticker = null;
        if (_numberHost is not null)
        {
            _ticker = new OdometerNumber { FontSize = 28, FontWeight = FontWeights.Medium, RevealOnLoad = false };
            _ticker.SetResourceReference(ForegroundProperty, "ArcWpf.Brush.Foreground");
            _ticker.SetResourceReference(FontFamilyProperty, "ArcWpf.Font.Body");
            _numberHost.Children.Add(_ticker);
        }
        if (_value is not null && _ticker is not null) _value.Visibility = Visibility.Collapsed;
        _track = GetTemplateChild("PART_Track") as Border;
        _segmentsCanvas = GetTemplateChild("PART_Segments") as Canvas;
        _overTint = GetTemplateChild("PART_OverTint") as Border;
        _overHatch = GetTemplateChild("PART_OverHatch") as Rectangle;
        _limitMarker = GetTemplateChild("PART_LimitMarker") as Border;
        _legend = GetTemplateChild("PART_Legend") as WrapPanel;
        _segmentVisuals.Clear();
        if (_track is not null) _track.SizeChanged += TrackSizeChanged;
        if (_segmentsCanvas is not null)
        {
            _segmentsCanvas.MouseMove += TrackMouseMove;
            _segmentsCanvas.MouseLeave += TrackMouseLeave;
            _segmentsCanvas.TouchUp += TrackTouchUp;
        }
        _scale = _fill is null ? null : new ScaleTransform(0, 1);
        if (_fill is not null) _fill.RenderTransform = _scale;
        RebuildSegments();
        RebuildLegend();
        Refresh(false);
        if (_revealed) RevealNumbers(); else StartRevealTracking();
    }
    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((UsageMeter)d).Refresh();
    private void Refresh() => Refresh(true);
    private void Refresh(bool animate)
    {
        double total = Segments.Count > 0 ? Segments.Sum(segment => segment.Value) : Used;
        if (!double.IsFinite(total)) throw new InvalidOperationException("Total usage exceeds the supported numeric range.");
        double free = Math.Max(0, Limit - total);
        double over = Math.Max(0, total - Limit);
        double percent = Limit > 0 ? Math.Clamp(total / Limit, 0, 1) : 0;
        var status = total > Limit ? UsageMeterStatus.Over : total >= Limit * WarningThreshold ? UsageMeterStatus.Near : UsageMeterStatus.Normal;
        SetValue(TotalPropertyKey, total);
        SetValue(FreePropertyKey, free);
        SetValue(OverPropertyKey, over);
        var previousStatus = Status;
        SetValue(StatusPropertyKey, status);
        SetValue(PercentagePropertyKey, percent);
        SetValue(IsWarningPropertyKey, status == UsageMeterStatus.Near);
        string statusText = status == UsageMeterStatus.Over ? $"{Format(over)} over" : status == UsageMeterStatus.Near ? "Almost full" : $"{Format(free)} free";
        SetValue(StatusTextPropertyKey, statusText);
        UpdateBadge(statusText, animate);
        if (previousStatus != status && IsLoaded)
            UIElementAutomationPeer.FromElement(this)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        SetValue(CaptionPropertyKey, $"{Unit} of {Format(Limit)} used".Trim());
        if (_fill is not null) _fill.Visibility = Segments.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_segmentsCanvas is not null) _segmentsCanvas.Visibility = Segments.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (_scale is not null)
        {
            var level = ArcMotion.GetLevel(this);
            bool moving = animate && IsLoaded && IsVisible && level != ArcMotionLevel.Off;
            _motion.Double(_scale, ScaleTransform.ScaleXProperty, percent, level == ArcMotionLevel.Full ? .35 : .12, moving);
        }
        if (_value is not null) _value.Text = Format(total);
        UpdateTrack(animate);
        UpdateLegend();
    }
    private string Format(double value) => $"{Math.Round(value, Decimals).ToString($"F{Decimals}", CultureInfo.CurrentCulture)} {Unit}".Trim();
    private void RebuildSegments()
    {
        if (_segmentsCanvas is null) return;
        _motion.Stop();
        foreach (var (_, visual) in _segmentVisuals) _segmentsCanvas.Children.Remove(visual);
        _segmentVisuals.Clear();
        for (int index = 0; index < Segments.Count; index++)
        {
            var visual = new Border { Height = 14, Width = 0, IsHitTestVisible = false };
            visual.SetResourceReference(Border.BackgroundProperty, $"ArcWpf.Brush.UsageSegment{Math.Min(index, 3)}");
            Canvas.SetLeft(visual, 0);
            _segmentsCanvas.Children.Insert(index, visual);
            _segmentVisuals.Add((Segments[index], visual));
        }
    }
    private void TrackSizeChanged(object sender, SizeChangedEventArgs e) => UpdateTrack(false);
    private void UpdateTrack(bool animate)
    {
        if (_segmentsCanvas is null || Segments.Count == 0) return;
        if (_segmentVisuals.Count != Segments.Count || _segmentVisuals.Where((entry, index) => !ReferenceEquals(entry.Segment, Segments[index])).Any())
            RebuildSegments();
        double width = _segmentsCanvas.ActualWidth > 0 ? _segmentsCanvas.ActualWidth : _track?.ActualWidth ?? 0;
        if (width <= 0) return;
        double span = Math.Max(Math.Max(Total, Limit), 1e-6);
        double cumulative = 0;
        bool moving = animate && IsLoaded && IsVisible && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        if (!_revealed && ArcMotion.GetLevel(this) == ArcMotionLevel.Full)
        {
            foreach (var (_, visual) in _segmentVisuals)
            {
                _motion.Double(visual, Canvas.LeftProperty, 0, 0, false);
                _motion.Double(visual, WidthProperty, 0, 0, false);
            }
            if (_overTint is not null) _motion.Double(_overTint, OpacityProperty, 0, 0, false);
            if (_overHatch is not null) _motion.Double(_overHatch, OpacityProperty, 0, 0, false);
            if (_limitMarker is not null) _motion.Double(_limitMarker, OpacityProperty, 0, 0, false);
            return;
        }
        for (int index = 0; index < _segmentVisuals.Count; index++)
        {
            var (segment, visual) = _segmentVisuals[index];
            double left = cumulative / span * width;
            double size = segment.Value / span * width;
            double remaining = Math.Max(0, width - left - size);
            double visibleSize = Math.Max(0, size - Math.Min(2, Math.Min(size, remaining)));
            double delay = moving && _revealing ? index * .07 : 0;
            _motion.Double(visual, Canvas.LeftProperty, left, .42, moving, spring: true, delay: delay);
            _motion.Double(visual, WidthProperty, visibleSize, .42, moving, spring: true, delay: delay);
            cumulative += segment.Value;
        }
        double limitX = Math.Clamp(Limit / span * width, 0, width);
        double overWidth = Math.Max(0, width - limitX);
        if (_overTint is not null)
        {
            _overTint.Visibility = Visibility.Visible;
            Canvas.SetLeft(_overTint, limitX);
            _overTint.Width = overWidth;
            _overTint.IsHitTestVisible = false;
            _motion.Double(_overTint, OpacityProperty, Status == UsageMeterStatus.Over ? .45 : 0, .16, moving, delay: _revealing ? Segments.Count * .07 : 0);
        }
        if (_overHatch is not null)
        {
            _overHatch.Visibility = Visibility.Visible;
            Canvas.SetLeft(_overHatch, limitX);
            _overHatch.Width = overWidth;
            _overHatch.IsHitTestVisible = false;
            _motion.Double(_overHatch, OpacityProperty, Status == UsageMeterStatus.Over ? 1 : 0, .16, moving, delay: _revealing ? Segments.Count * .07 : 0);
        }
        if (_limitMarker is not null)
        {
            _limitMarker.Visibility = Visibility.Visible;
            Canvas.SetLeft(_limitMarker, Math.Max(0, limitX - 1));
            _limitMarker.IsHitTestVisible = false;
            _motion.Double(_limitMarker, OpacityProperty, Status == UsageMeterStatus.Over ? 1 : 0, .16, moving, delay: _revealing ? Segments.Count * .07 : 0);
        }
    }
    private void ClearLegend()
    {
        foreach (var entry in _legendEntries)
        {
            entry.Button.Click -= LegendClicked;
            entry.Button.MouseEnter -= LegendMouseEnter;
            entry.Button.MouseLeave -= LegendMouseLeave;
            entry.Button.GotKeyboardFocus -= LegendFocused;
            entry.Button.LostKeyboardFocus -= LegendBlurred;
            entry.Button.PreviewKeyDown -= LegendKeyDown;
            entry.Ticker.Stop();
        }
        _legendEntries.Clear();
        _legend?.Children.Clear();
    }
    private void StopNumberMotion()
    {
        _ticker?.Stop();
        foreach (var entry in _legendEntries) entry.Ticker.Stop();
    }
    private void RebuildLegend()
    {
        ClearLegend();
        if (_legend is null || Segments.Count == 0) return;
        for (int index = 0; index <= Segments.Count; index++)
        {
            bool remainder = index == Segments.Count;
            string id = remainder ? "free" : Segments[index].Id;
            var swatch = new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 0, 8, 0) };
            swatch.SetResourceReference(Border.BackgroundProperty, remainder ? "ArcWpf.Brush.UsageTrack" : $"ArcWpf.Brush.UsageSegment{Math.Min(index, 3)}");
            var label = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 130, FontSize = 14 };
            label.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextSecondary");
            var heading = new StackPanel { Orientation = Orientation.Horizontal };
            heading.Children.Add(swatch);
            heading.Children.Add(label);
            var value = new TextBlock { Visibility = Visibility.Collapsed };
            var ticker = new OdometerNumber { FontSize = 16, FontWeight = FontWeights.Medium, RevealOnLoad = false };
            ticker.SetResourceReference(ForegroundProperty, "ArcWpf.Brush.Foreground");
            ticker.SetResourceReference(FontFamilyProperty, "ArcWpf.Font.Body");
            var unit = new TextBlock { FontSize = 14, Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Bottom };
            unit.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextSecondary");
            var amount = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(18, 2, 0, 0) };
            amount.Children.Add(ticker);
            amount.Children.Add(unit);
            var content = new StackPanel();
            content.Children.Add(heading);
            content.Children.Add(value);
            content.Children.Add(amount);
            var button = new ToggleButton { Tag = id, Content = content, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 2, 2) };
            button.Style = TryFindResource("ArcWpf.UsageMeter.LegendButton") as Style;
            button.Click += LegendClicked;
            button.MouseEnter += LegendMouseEnter;
            button.MouseLeave += LegendMouseLeave;
            button.GotKeyboardFocus += LegendFocused;
            button.LostKeyboardFocus += LegendBlurred;
            button.PreviewKeyDown += LegendKeyDown;
            _legend.Children.Add(button);
            _legendEntries.Add(new LegendEntry(id, button, label, value, ticker, unit, swatch));
        }
        if (_rovingIndex >= _legendEntries.Count) _rovingIndex = 0;
        if (_pinnedId is not null && !_legendEntries.Any(entry => entry.Id == _pinnedId)) _pinnedId = null;
        UpdateLegend();
        if (_revealed && IsLoaded) RevealNumbers();
    }
    private void UpdateLegend()
    {
        string? active = _hoveredId ?? _focusedId ?? _pinnedId;
        if (active is not null && !_legendEntries.Any(entry => entry.Id == active)) active = null;
        for (int index = 0; index < _legendEntries.Count; index++)
        {
            var entry = _legendEntries[index];
            bool remainder = index == Segments.Count;
            double amount = remainder ? Status == UsageMeterStatus.Over ? Over : Free : Segments[index].Value;
            entry.Label.Text = remainder ? Status == UsageMeterStatus.Over ? OverLabel : FreeLabel : Segments[index].Label;
            entry.Value.Text = Format(amount);
            entry.Ticker.Set(amount, Math.Round(amount, Decimals).ToString($"F{Decimals}", CultureInfo.CurrentCulture), true);
            entry.Unit.Text = Unit;
            entry.Button.IsChecked = entry.Id == _pinnedId;
            entry.Button.Opacity = active is null || entry.Id == active ? 1 : .45;
            entry.Button.IsTabStop = index == _rovingIndex;
            entry.Swatch.SetResourceReference(Border.BackgroundProperty, remainder && Status == UsageMeterStatus.Over ? "ArcWpf.Brush.Danger" : remainder ? "ArcWpf.Brush.UsageTrack" : $"ArcWpf.Brush.UsageSegment{Math.Min(index, 3)}");
            AutomationProperties.SetName(entry.Button, $"{entry.Label.Text}, {entry.Value.Text}");
        }
        foreach (var (segment, visual) in _segmentVisuals)
            visual.Opacity = active is null || segment.Id == active ? 1 : .28;
        if (_value is null) return;
        double displayValue = Total;
        if (active is null) { _value.Text = Format(Total); SetValue(CaptionPropertyKey, $"{Unit} of {Format(Limit)} used".Trim()); }
        else if (active == "free")
        {
            displayValue = Status == UsageMeterStatus.Over ? Over : Free;
            _value.Text = Format(displayValue);
            SetValue(CaptionPropertyKey, $"{Unit} {(Status == UsageMeterStatus.Over ? "over the limit" : "free")}".Trim());
        }
        else if (Segments.FirstOrDefault(segment => segment.Id == active) is { } segment)
        {
            displayValue = segment.Value;
            _value.Text = Format(segment.Value);
            double share = Limit > 0 ? Math.Round(segment.Value / Limit * 100) : 0;
            SetValue(CaptionPropertyKey, $"{Unit} in {segment.Label} · {share}% of plan".Trim());
        }
        _ticker?.Set(displayValue, _value.Text, true);
    }
    private void LegendClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button) return;
        string id = (string)button.Tag;
        _rovingIndex = _legendEntries.FindIndex(entry => ReferenceEquals(entry.Button, button));
        _pinnedId = _pinnedId == id ? null : id;
        UpdateLegend();
    }
    private void LegendMouseEnter(object sender, MouseEventArgs e) { _hoveredId = (string)((ToggleButton)sender).Tag; UpdateLegend(); }
    private void LegendMouseLeave(object sender, MouseEventArgs e) { _hoveredId = null; UpdateLegend(); }
    private void LegendFocused(object sender, KeyboardFocusChangedEventArgs e) { _focusedId = (string)((ToggleButton)sender).Tag; UpdateLegend(); }
    private void LegendBlurred(object sender, KeyboardFocusChangedEventArgs e) { _focusedId = null; UpdateLegend(); }
    private void LegendKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_pinnedId is null) return;
            _pinnedId = null;
            UpdateLegend();
            e.Handled = true;
            return;
        }
        int next = e.Key switch
        {
            Key.Left or Key.Up => _rovingIndex - 1,
            Key.Right or Key.Down => _rovingIndex + 1,
            Key.Home => 0,
            Key.End => _legendEntries.Count - 1,
            _ => -1
        };
        if (next < -1 || _legendEntries.Count == 0) return;
        if (e.Key is not (Key.Left or Key.Up or Key.Right or Key.Down or Key.Home or Key.End)) return;
        _rovingIndex = (next + _legendEntries.Count) % _legendEntries.Count;
        _legendEntries[_rovingIndex].Button.Focus();
        UpdateLegend();
        e.Handled = true;
    }
    private void TrackMouseMove(object sender, MouseEventArgs e)
    {
        if (_segmentsCanvas is null) return;
        string? id = SegmentAt(e.GetPosition(_segmentsCanvas).X);
        if (_hoveredId == id) return;
        _hoveredId = id;
        UpdateLegend();
    }
    private void TrackMouseLeave(object sender, MouseEventArgs e) { _hoveredId = null; UpdateLegend(); }
    private void TrackTouchUp(object? sender, TouchEventArgs e)
    {
        if (_segmentsCanvas is null) return;
        string? id = SegmentAt(e.GetTouchPoint(_segmentsCanvas).Position.X);
        _pinnedId = _pinnedId == id ? null : id;
        UpdateLegend();
        e.Handled = true;
    }
    private string? SegmentAt(double x)
    {
        if (_segmentsCanvas is null || _segmentsCanvas.ActualWidth <= 0) return null;
        double at = x / _segmentsCanvas.ActualWidth * Math.Max(Total, Limit);
        if (at > Limit && Status == UsageMeterStatus.Over) return "free";
        double end = 0;
        foreach (var segment in Segments) { end += segment.Value; if (at <= end) return segment.Id; }
        return "free";
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ArcMotion.LevelProperty && _motion is not null) { _motion.Stop(); StopNumberMotion(); StopBadgeMotion(); Refresh(false); StartRevealTracking(); }
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new UsageMeterPeer(this);
    private sealed class UsageMeterPeer(UsageMeter owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetNameCore() => owner.Label;
        protected override string GetItemStatusCore() => owner.StatusText;
        protected override string GetHelpTextCore() => $"{owner.Format(owner.Total)} of {owner.Format(owner.Limit)} used. " +
            string.Join(", ", owner.Segments.Select(segment => $"{segment.Label} {owner.Format(segment.Value)}"));
    }
}
