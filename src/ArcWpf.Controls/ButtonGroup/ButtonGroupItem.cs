using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace ArcWpf.Controls;

/// <summary>One segment of a <see cref="ButtonGroup"/>. Content, when set, replaces the visible label with a live value.</summary>
[TemplatePart(Name = "PART_Divider", Type = typeof(Border))]
[TemplatePart(Name = "PART_Content", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Slot", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Sizers", Type = typeof(Panel))]
[TemplatePart(Name = "PART_Phase", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Icon", Type = typeof(ContentPresenter))]
[TemplatePart(Name = "PART_Label", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_Value", Type = typeof(ContentPresenter))]
[TemplatePart(Name = "PART_PreviousPhase", Type = typeof(Image))]
public class ButtonGroupItem : System.Windows.Controls.Button
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ButtonGroupItem), new FrameworkPropertyMetadata(string.Empty, OnPhaseChanged));
    public static readonly DependencyProperty ReserveProperty = DependencyProperty.Register(nameof(Reserve), typeof(IList<string>), typeof(ButtonGroupItem), new FrameworkPropertyMetadata(null, OnReserveChanged));
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(object), typeof(ButtonGroupItem), new FrameworkPropertyMetadata(null, OnPhaseChanged));
    public static readonly DependencyProperty IsIconOnlyProperty = DependencyProperty.Register(nameof(IsIconOnly), typeof(bool), typeof(ButtonGroupItem), new FrameworkPropertyMetadata(false, OnPhaseChanged));

    /// <summary>Visible text and accessible name. Changing it crossfades in place, so "Share" can answer "Copied".</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <summary>Every other label this segment can show. The segment sizes to the widest, so a label change never resizes it.</summary>
    [TypeConverter(typeof(ReserveConverter))]
    public IList<string>? Reserve { get => (IList<string>?)GetValue(ReserveProperty); set => SetValue(ReserveProperty, value); }
    /// <summary>A Geometry is drawn as a 24-unit stroked icon in the segment's foreground; any other object is shown as-is.</summary>
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public bool IsIconOnly { get => (bool)GetValue(IsIconOnlyProperty); set => SetValue(IsIconOnlyProperty, value); }

    private const double PressIn = .12, Fast = .16, Standard = .24, ResetSpring = .58, MorphWindow = .9;
    private static readonly IEasingFunction MorphSpring = ArcEasing.Spring(.42, MorphWindow);

    private Border? _divider;
    private FrameworkElement? _content;
    private FrameworkElement? _slot;
    private Panel? _sizers;
    private FrameworkElement? _phase;
    private ContentPresenter? _icon;
    private TextBlock? _label;
    private ContentPresenter? _value;
    private Image? _previous;
    private readonly ScaleTransform _press = new(1, 1);
    private readonly TranslateTransform _phaseOffset = new();
    private readonly TranslateTransform _previousOffset = new();
    private readonly OwnedMotion _motion = new();
    private readonly OwnedMotion _phaseMotion = new();
    private SegmentLayout _layout = SegmentLayout.Default;
    private string _phaseKey = string.Empty;
    private int _morphGeneration;
    private long _activatedAt = long.MinValue;
    private bool _ownsWidth;

    static ButtonGroupItem() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ButtonGroupItem), new FrameworkPropertyMetadata(typeof(ButtonGroupItem)));

    public ButtonGroupItem()
    {
        Unloaded += (_, _) => StopMotion();
        IsVisibleChanged += (_, _) => { if (!IsVisible) { StopMotion(); } };
        IsEnabledChanged += (_, _) => UpdateDisabledContent();
    }

    internal ButtonGroup? Owner => ItemsControl.ItemsControlFromItemContainer(this) as ButtonGroup;
    internal bool IsCollapsible => Icon is not null && !IsIconOnly && Content is null;
    private bool Collapsed => _layout.Compact && IsCollapsible;
    private ArcMotionLevel Level => IsLoaded && IsVisible ? ArcMotion.GetLevel(this) : ArcMotionLevel.Off;

    public override void OnApplyTemplate()
    {
        StopMotion();
        base.OnApplyTemplate();
        _divider = GetTemplateChild("PART_Divider") as Border;
        _content = GetTemplateChild("PART_Content") as FrameworkElement;
        _slot = GetTemplateChild("PART_Slot") as FrameworkElement;
        _sizers = GetTemplateChild("PART_Sizers") as Panel;
        _phase = GetTemplateChild("PART_Phase") as FrameworkElement;
        _icon = GetTemplateChild("PART_Icon") as ContentPresenter;
        _label = GetTemplateChild("PART_Label") as TextBlock;
        _value = GetTemplateChild("PART_Value") as ContentPresenter;
        _previous = GetTemplateChild("PART_PreviousPhase") as Image;
        if (_content is not null) { _content.RenderTransformOrigin = new Point(.5, .5); _content.RenderTransform = _press; }
        if (_phase is not null) { _phase.RenderTransform = _phaseOffset; }
        if (_previous is not null) { _previous.RenderTransform = _previousOffset; }
        _phaseKey = PhaseKey();
        RefreshPhase();
        ApplyLayout(_layout);
        UpdateDisabledContent();
    }

    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    {
        StopMotion();
        base.OnTemplateChanged(oldTemplate, newTemplate);
    }

    protected override void OnClick()
    {
        _activatedAt = Environment.TickCount64;
        base.OnClick();
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        PhaseChanged();
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsPressedProperty) { UpdatePress(); }
        else if (e.Property == ArcMotion.LevelProperty) { StopMotion(); }
    }

    private static void OnPhaseChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var item = (ButtonGroupItem)d;
        if (e.Property == LabelProperty) { item.SetCurrentValue(AutomationProperties.NameProperty, (string)e.NewValue); }
        item.PhaseChanged();
        if (e.Property == LabelProperty && Environment.TickCount64 - item._activatedAt < 1000 && item.Content is null)
        {
            // A confirmation right after a press ("Copied") is announced once; the quiet revert is not.
            item._activatedAt = long.MinValue;
            item.Owner?.Announce((string)e.NewValue);
        }
        if (e.Property != LabelProperty) { item.Owner?.InvalidateCollapse(); }
    }

    private static void OnReserveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ButtonGroupItem)d).RebuildSizers();

    // Same icon object and same text keep the phase still; only a real change crossfades (content values never do).
    private string PhaseKey()
    {
        string icon = Icon is null ? string.Empty : Icon is string text ? text : RuntimeHelpers.GetHashCode(Icon).ToString(CultureInfo.InvariantCulture);
        return icon + "|" + (Content is not null ? "\u0000content" : IsIconOnly ? string.Empty : Label);
    }

    private void PhaseChanged()
    {
        var key = PhaseKey();
        if (key == _phaseKey || _phase is null) { _phaseKey = key; RefreshPhase(); ApplyLayout(_layout); return; }
        _phaseKey = key;
        var level = Level;
        bool animate = level != ArcMotionLevel.Off && _phase.ActualWidth > 0;
        var snapshot = animate && level == ArcMotionLevel.Full ? Snapshot(_phase) : null;
        double oldWidth = _slot?.ActualWidth ?? 0;
        StopPhaseMotion();
        RefreshPhase();
        ApplyLayout(_layout);
        if (!animate) { return; }
        if (level == ArcMotionLevel.Reduced)
        {
            _phaseMotion.Double(_phase, OpacityProperty, 0, 0, false);
            _phaseMotion.Double(_phase, OpacityProperty, 1, .12, true);
            return;
        }
        // The new label rises in from a soft blur while the old one lifts away.
        if (snapshot is not null && _previous is not null)
        {
            _previous.Source = snapshot;
            _previous.Width = _phase.ActualWidth;
            _previous.Height = _phase.ActualHeight;
            Canvas.SetLeft(_previous, -_phase.ActualWidth / 2);
            Canvas.SetTop(_previous, -_phase.ActualHeight / 2);
            var exitBlur = new BlurEffect { Radius = 0 };
            _previous.Effect = exitBlur;
            _phaseMotion.Double(_previous, OpacityProperty, 1, 0, false);
            _phaseMotion.Double(_previous, OpacityProperty, 0, Fast, true, completed: ClearPrevious);
            _phaseMotion.Double(_previousOffset, TranslateTransform.YProperty, -3, Fast, true);
            _phaseMotion.Double(exitBlur, BlurEffect.RadiusProperty, 4, Fast, true);
        }
        var enterBlur = new BlurEffect { Radius = 4 };
        _phase.Effect = enterBlur;
        _phaseMotion.Double(_phase, OpacityProperty, 0, 0, false);
        _phaseMotion.Double(_phase, OpacityProperty, 1, Standard, true, enter: true);
        _phaseMotion.Double(_phaseOffset, TranslateTransform.YProperty, 4, 0, false);
        _phaseMotion.Double(_phaseOffset, TranslateTransform.YProperty, 0, Standard, true, enter: true);
        // Blur off at rest so the label keeps ClearType.
        _phaseMotion.Double(enterBlur, BlurEffect.RadiusProperty, 0, Standard, true, enter: true, completed: () => { if (_phase?.Effect == enterBlur) { _phase.Effect = null; } });
        MorphWidth(oldWidth);
    }

    // A label nobody reserved springs the slot to its width; reserved labels already share the widest box.
    private void MorphWidth(double oldWidth)
    {
        if (_slot is null || oldWidth <= 0) { return; }
        _slot.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double target = _slot.DesiredSize.Width;
        if (Math.Abs(target - oldWidth) < .5) { return; }
        var slot = _slot;
        int generation = ++_morphGeneration;
        slot.ClipToBounds = true;
        var animation = new DoubleAnimation(oldWidth, target, TimeSpan.FromSeconds(MorphWindow)) { EasingFunction = MorphSpring };
        animation.Completed += (_, _) => { if (generation == _morphGeneration) { ClearMorph(); } };
        slot.BeginAnimation(WidthProperty, animation);
    }

    private void ClearMorph()
    {
        if (_slot is null) { return; }
        _slot.BeginAnimation(WidthProperty, null);
        _slot.ClearValue(WidthProperty);
        _slot.ClipToBounds = false;
    }

    private void ClearPrevious()
    {
        if (_previous is null) { return; }
        _previous.Source = null;
        _previous.Effect = null;
    }

    private void RefreshPhase()
    {
        if (_icon is not null)
        {
            _icon.Content = Icon;
            _icon.Visibility = Icon is null ? Visibility.Collapsed : Visibility.Visible;
        }
        if (_label is not null) { _label.Text = Label; }
        if (_value is not null)
        {
            _value.Content = Content;
            _value.Visibility = Content is null ? Visibility.Collapsed : Visibility.Visible;
        }
        RebuildSizers();
    }

    // Every reserved label sits invisibly in the same cell, so the widest state sets the width.
    private void RebuildSizers()
    {
        if (_sizers is null) { return; }
        _sizers.Children.Clear();
        if (IsIconOnly || Content is not null || Collapsed) { return; }
        var texts = new List<string> { Label };
        if (Reserve is not null) { texts.AddRange(Reserve.Where(text => !texts.Contains(text))); }
        foreach (var text in texts)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            if (Icon is not null) { row.Children.Add(new Border { Width = 16, Height = 16, Margin = new Thickness(0, 0, _layout.Gap, 0) }); }
            row.Children.Add(new TextBlock { Text = text });
            _sizers.Children.Add(row);
        }
    }

    internal void ApplyLayout(SegmentLayout layout)
    {
        bool rebuild = _layout.Compact != layout.Compact || _layout.Gap != layout.Gap;
        _layout = layout;
        SetCurrentValue(HeightProperty, layout.Height);
        SetCurrentValue(MinWidthProperty, layout.Height);
        bool square = IsIconOnly || Collapsed;
        if (square && !layout.Vertical) { SetCurrentValue(WidthProperty, layout.Height); _ownsWidth = true; }
        else if (_ownsWidth) { ClearValue(WidthProperty); _ownsWidth = false; }
        SetCurrentValue(PaddingProperty, square ? new Thickness(0) : new Thickness(layout.PaddingX, 0, layout.PaddingX, 0));
        SetCurrentValue(HorizontalContentAlignmentProperty, layout.Vertical && !IsIconOnly ? HorizontalAlignment.Left : HorizontalAlignment.Center);
        SetCurrentValue(ToolTipProperty, square ? Label : null);
        if (_label is not null) { _label.Visibility = IsIconOnly || Content is not null || Collapsed ? Visibility.Collapsed : Visibility.Visible; }
        if (_icon is not null) { _icon.Margin = _label?.Visibility == Visibility.Visible || Content is not null ? new Thickness(0, 0, layout.Gap, 0) : new Thickness(0); }
        if (_divider is not null)
        {
            _divider.Visibility = layout.IsFirst ? Visibility.Collapsed : Visibility.Visible;
            _divider.SetResourceReference(Border.BackgroundProperty, layout.Solid ? "ArcWpf.Brush.ButtonGroupSolidDivider" : "ArcWpf.Brush.Border");
            if (layout.Vertical)
            {
                _divider.Width = double.NaN; _divider.Height = 1;
                _divider.HorizontalAlignment = HorizontalAlignment.Stretch; _divider.VerticalAlignment = VerticalAlignment.Top;
                _divider.Margin = new Thickness(layout.DividerInset, 0, layout.DividerInset, 0);
            }
            else
            {
                _divider.Width = 1; _divider.Height = double.NaN;
                _divider.HorizontalAlignment = HorizontalAlignment.Left; _divider.VerticalAlignment = VerticalAlignment.Stretch;
                _divider.Margin = new Thickness(0, layout.DividerInset, 0, layout.DividerInset);
            }
        }
        if (rebuild) { RebuildSizers(); }
    }

    // Dividers beside the highlight fade so it reads as one soft shape.
    internal void SetQuiet(bool quiet, bool animate)
    {
        if (_divider is not null) { _motion.Double(_divider, OpacityProperty, quiet ? 0 : 1, Fast, animate && Level != ArcMotionLevel.Off); }
    }

    // The press answers inside the segment: content dips and springs back, the group and its neighbours stay still.
    private void UpdatePress()
    {
        bool pressed = IsPressed && IsEnabled;
        double target = pressed ? (IsIconOnly ? .9 : .97) : 1;
        bool animate = Level == ArcMotionLevel.Full;
        if (!animate) { target = 1; }
        _motion.Double(_press, ScaleTransform.ScaleXProperty, target, pressed ? PressIn : ResetSpring, animate, spring: !pressed);
        _motion.Double(_press, ScaleTransform.ScaleYProperty, target, pressed ? PressIn : ResetSpring, animate, spring: !pressed);
    }

    private void UpdateDisabledContent()
    {
        // A single disabled segment dims its content; a disabled group dims as a whole instead.
        if (_content is not null) { _content.Opacity = !IsEnabled && Owner?.IsEnabled != false ? .4 : 1; }
    }

    private static BitmapSource? Snapshot(FrameworkElement element)
    {
        var dpi = VisualTreeHelper.GetDpi(element);
        int width = (int)Math.Ceiling(element.ActualWidth * dpi.DpiScaleX), height = (int)Math.Ceiling(element.ActualHeight * dpi.DpiScaleY);
        if (width <= 0 || height <= 0 || width > 4096 || height > 4096) { return null; }
        var bitmap = new RenderTargetBitmap(width, height, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(element);
        bitmap.Freeze();
        return bitmap;
    }

    private void StopPhaseMotion()
    {
        _phaseMotion.Stop();
        ++_morphGeneration;
        ClearMorph();
        ClearPrevious();
        if (_previous is not null) { _previous.Opacity = 0; }
        _previousOffset.Y = 0;
        _phaseOffset.Y = 0;
        if (_phase is not null) { _phase.Opacity = 1; _phase.Effect = null; }
    }

    internal void StopMotion()
    {
        StopPhaseMotion();
        _motion.Stop();
        _press.ScaleX = _press.ScaleY = 1;
    }

    private sealed class ReserveConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

        // XAML: Reserve="Share,Copied,Failed".
        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
            value is string text ? text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList() : base.ConvertFrom(context, culture, value);
    }
}

internal readonly record struct SegmentLayout(double Height, double PaddingX, double Gap, double DividerInset, bool Vertical, bool Compact, bool IsFirst, bool Solid)
{
    public static SegmentLayout Default { get; } = new(42, 14, 8, 11, false, false, true, false);
}
