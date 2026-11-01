using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace ArcWpf.Controls;

/// <summary>
/// A color field whose swatch grows into a full picker (color-picker.tsx, frozen 2026-10-31). Pick saturation and brightness on the area, hue and
/// opacity on the sliders, or type a value in hex, RGB, HSL, or OKLCH; the format button cycles between them. Saved swatches can be added, applied,
/// dragged or moved with Alt and the arrow keys, and removed with Delete. A contrast readout compares the color with the background it will sit on.
/// Arrow keys move every thumb, Shift takes bigger steps, and Escape closes the panel. WPF has no screen eyedropper, so that button is not shown.
/// </summary>
[TemplatePart(Name = "PART_Trigger", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
[TemplatePart(Name = "PART_Float", Type = typeof(Grid))]
public class ColorPicker : Control
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(ColorPicker), new FrameworkPropertyMetadata("#2F6BFF", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((ColorPicker)d).OnValueChanged((string?)e.NewValue)));
    public static readonly DependencyProperty BackgroundColorProperty = DependencyProperty.Register(nameof(BackgroundColor), typeof(string), typeof(ColorPicker), new PropertyMetadata("#FFFFFF", (d, _) => ((ColorPicker)d).Render(true)));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ColorPicker), new PropertyMetadata("Color", (d, _) => ((ColorPicker)d).Render(false)));
    public static readonly DependencyProperty SwatchesProperty = DependencyProperty.Register(nameof(Swatches), typeof(IList<ColorSwatch>), typeof(ColorPicker), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ColorPicker)d).BuildSwatches(false)));
    public static readonly DependencyProperty MaxSwatchesProperty = DependencyProperty.Register(nameof(MaxSwatches), typeof(int), typeof(ColorPicker), new PropertyMetadata(7));
    public static readonly DependencyProperty FormatProperty = DependencyProperty.Register(nameof(Format), typeof(ColorFormat), typeof(ColorPicker), new PropertyMetadata(ColorFormat.Hex, (d, _) => ((ColorPicker)d).Render(false)));
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(ColorPicker), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((ColorPicker)d).OnOpenChanged((bool)e.NewValue)));
    public static readonly RoutedEvent ValueChangedEvent = EventManager.RegisterRoutedEvent(nameof(ValueChanged), RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<string>), typeof(ColorPicker));
    private static readonly DependencyProperty ProgressProperty = DependencyProperty.Register("Progress", typeof(double), typeof(ColorPicker), new PropertyMetadata(0d, (d, _) => ((ColorPicker)d).ApplyProgress()));
    private static readonly DependencyProperty RatioProperty = DependencyProperty.Register("Ratio", typeof(double), typeof(ColorPicker), new PropertyMetadata(1d, (d, e) => { if (((ColorPicker)d)._ratio is { } text) text.Text = ((double)e.NewValue).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture); }));
    private static readonly DependencyProperty ThemeProbeProperty = DependencyProperty.Register("ThemeProbe", typeof(object), typeof(ColorPicker), new PropertyMetadata(null, (d, _) => ((ColorPicker)d).Render(false)));

    /// <summary>The color as hex, with two alpha digits when it is not opaque. Setting it accepts any color the picker can read.</summary>
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    /// <summary>The background the color will sit on, for the contrast readout.</summary>
    public string BackgroundColor { get => (string)GetValue(BackgroundColorProperty); set => SetValue(BackgroundColorProperty, value); }
    /// <summary>Name shown on the swatch, such as "Accent".</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public IList<ColorSwatch>? Swatches { get => (IList<ColorSwatch>?)GetValue(SwatchesProperty); set => SetValue(SwatchesProperty, value); }
    /// <summary>Most saved swatches. Saving past it drops the oldest.</summary>
    public int MaxSwatches { get => (int)GetValue(MaxSwatchesProperty); set => SetValue(MaxSwatchesProperty, value); }
    public ColorFormat Format { get => (ColorFormat)GetValue(FormatProperty); set => SetValue(FormatProperty, value); }
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public event RoutedPropertyChangedEventHandler<string> ValueChanged { add => AddHandler(ValueChangedEvent, value); remove => RemoveHandler(ValueChangedEvent, value); }

    private const double PanelWidth = 304;
    private Hsva _hsva = new(222, .82, 1, 1);
    private System.Windows.Controls.Button? _trigger;
    private Popup? _popup;
    private Grid? _float;
    private Border? _panel;
    private readonly RectangleGeometry _clip = new();
    private readonly TranslateTransform _contentMove = new(), _fieldShake = new();
    private FrameworkElement? _content, _headActions;
    private readonly List<(Border Chip, TextBlock Name, TextBlock Hex)> _faces = [];
    private Grid? _area;
    private readonly SolidColorBrush _hue = new(Colors.Red), _opaque = new(Colors.Blue), _color = new(Colors.Blue), _alphaEnd = new(Colors.Blue), _gradeFill = new(Colors.Transparent), _gradeInk = new(Colors.Green), _sampleBg = new(Colors.White), _fieldEdge = new(Colors.Gray);
    private Thumb2D? _areaThumb;
    private SliderTrack? _hueSlider, _alphaSlider;
    private TextBox? _input;
    private TextBlock? _formatText, _error, _ratio;
    private Grid? _grade, _swatchList;
    private string? _gradeShown;
    private string? _draft;
    private bool _invalid, _syncing, _shown;
    private string? _focusSwatch;
    private Window? _window;

    static ColorPicker()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ColorPicker), new FrameworkPropertyMetadata(typeof(ColorPicker)));
        FocusableProperty.OverrideMetadata(typeof(ColorPicker), new FrameworkPropertyMetadata(false));
    }

    public ColorPicker()
    {
        SetResourceReference(ThemeProbeProperty, "ArcWpf.Brush.Border");
        Unloaded += (_, _) => { if (IsOpen) SetCurrentValue(IsOpenProperty, false); };
    }

    private bool Full => IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;
    private bool Reduced => Tween.Level(this) != ArcMotionLevel.Full;
    private string Hex => ColorMath.ToHex(_hsva);

    public override void OnApplyTemplate()
    {
        if (_trigger is not null) _trigger.Click -= TriggerClick;
        base.OnApplyTemplate();
        _trigger = GetTemplateChild("PART_Trigger") as System.Windows.Controls.Button;
        _popup = GetTemplateChild("PART_Popup") as Popup;
        _float = GetTemplateChild("PART_Float") as Grid;
        _faces.Clear();
        _hsva = ColorMath.Parse(Value) ?? _hsva;
        if (_trigger is not null)
        {
            _trigger.Content = Face();
            _trigger.Click += TriggerClick;
        }
        if (_popup is not null)
        {
            _popup.Placement = PlacementMode.Custom;
            _popup.PlacementTarget = _trigger;
            // The swatch is the panel's top left corner: the panel opens over it and grows out of its box.
            _popup.CustomPopupPlacementCallback = (_, _, _) => [new CustomPopupPlacement(new Point(-1, -1), PopupPrimaryAxis.None)];
            if (_popup.Child is not null) BindingOperations.SetBinding(_popup.Child, ArcMotion.LevelProperty, new Binding { Source = this, Path = new PropertyPath(ArcMotion.LevelProperty) });
        }
        if (_float is not null) BuildPanel(_float);
        Render(false);
        BuildSwatches(false);
    }

    /* ---------------------------------------------- Value flow ---------------------------------------------- */

    private void OnValueChanged(string? value)
    {
        if (_syncing || value is null) return;
        if (string.Equals(value, Hex, StringComparison.OrdinalIgnoreCase)) return;
        if (ColorMath.Parse(value, _hsva.H) is { } next) { _hsva = next; Render(true); }
    }

    private void Commit(Hsva next)
    {
        string before = Hex;
        _hsva = next;
        string after = Hex;
        if (after != before)
        {
            _syncing = true;
            SetCurrentValue(ValueProperty, after);
            _syncing = false;
            RaiseEvent(new RoutedPropertyChangedEventArgs<string>(before, after, ValueChangedEvent));
        }
        Render(true);
    }

    /* ---------------------------------------------- Pieces ---------------------------------------------- */

    private Brush Checker()
    {
        bool dark = Tween.Resolve(this, "ArcWpf.Brush.Surface") is var surface && surface.R + surface.G + surface.B < 3 * 128;
        var a = dark ? Color.FromRgb(77, 77, 77) : Color.FromRgb(216, 216, 216);
        var b = dark ? Color.FromRgb(46, 46, 46) : Colors.White;
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(new SolidColorBrush(b), null, new RectangleGeometry(new Rect(0, 0, 10, 10))));
        group.Children.Add(new GeometryDrawing(new SolidColorBrush(a), null, new GeometryGroup { Children = { new RectangleGeometry(new Rect(0, 0, 5, 5)), new RectangleGeometry(new Rect(5, 5, 5, 5)) } }));
        var brush = new DrawingBrush(group) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 10, 10), ViewportUnits = BrushMappingMode.Absolute, Stretch = Stretch.None };
        brush.Freeze();
        return brush;
    }

    // A chip over a checkerboard, so opacity reads as opacity.
    private Border Chip(Brush color, double size = 32)
    {
        var fill = new Border { CornerRadius = new CornerRadius(size / 2), Background = color, BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromArgb(26, 0, 0, 0)) };
        return new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), Background = Checker(), Child = fill, VerticalAlignment = VerticalAlignment.Center };
    }

    // .trigger / .head: a 32 DIP chip, then the name (xs secondary) over the hex (14/500 tabular), 10 apart.
    private FrameworkElement Face()
    {
        var chip = Chip(_color);
        var name = new TextBlock { FontSize = 12, LineHeight = 13.8, LineStackingStrategy = LineStackingStrategy.BlockLineHeight };
        name.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextSecondary");
        var hex = new TextBlock { FontWeight = FontWeights.Medium, LineHeight = 16.1, LineStackingStrategy = LineStackingStrategy.BlockLineHeight };
        var text = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Children = { name, hex } };
        _faces.Add((chip, name, hex));
        return new DockPanel { Children = { chip, text } };
    }

    private void BuildPanel(Grid host)
    {
        host.Children.Clear();
        // The floating layer's drop shadow follows the clipped panel, so the shadow grows with it.
        host.Effect = new DropShadowEffect { BlurRadius = 36, ShadowDepth = 18, Direction = 270, Opacity = .12, Color = Colors.Black };
        _panel = new Border { Width = PanelWidth, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(26), Padding = new Thickness(0, 0, 0, 14), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Clip = _clip };
        _panel.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        _panel.SizeChanged += (_, _) => ApplyProgress();
        var stack = new StackPanel();
        // Head: same face as the trigger, plus the Done action.
        var head = new DockPanel { Height = 44, Margin = new Thickness(6, 6, 6, 6), LastChildFill = true };
        var done = new FieldIconButton(Lucide.Check, 18, 32, 12) { PressScale = .94 };
        AutomationProperties.SetName(done, "Done");
        done.Click += (_, _) => Hide(true);
        _headActions = done;
        DockPanel.SetDock(done, Dock.Right);
        head.Children.Add(done);
        head.Children.Add(Face());
        stack.Children.Add(head);
        var body = new StackPanel { Margin = new Thickness(14, 8 - 6, 14, 0), RenderTransform = _contentMove };
        _content = body;
        body.Children.Add(BuildArea());
        _hueSlider = new SliderTrack(this, 360, h => Commit(_hsva with { H = Math.Clamp(h, 0, 359.9) }), "Hue") { Margin = new Thickness(0, 14, 0, 0) };
        _alphaSlider = new SliderTrack(this, 100, a => Commit(_hsva with { A = Math.Clamp(a / 100, 0, 1) }), "Opacity", Checker()) { Margin = new Thickness(0, 14, 0, 0) };
        body.Children.Add(_hueSlider);
        body.Children.Add(_alphaSlider);
        body.Children.Add(BuildField());
        _error = new TextBlock { Text = "Enter a hex, RGB, HSL, or OKLCH color", FontSize = 12, Margin = new Thickness(2, 4, 2, 0), Visibility = Visibility.Collapsed };
        _error.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.Danger");
        AutomationProperties.SetLiveSetting(_error, AutomationLiveSetting.Polite);
        body.Children.Add(_error);
        body.Children.Add(BuildContrast());
        body.Children.Add(BuildSwatchRow());
        stack.Children.Add(body);
        _panel.Child = stack;
        host.Children.Add(_panel);
        AutomationProperties.SetName(_panel, $"{Label} color");
    }

    // Saturation runs left to right, brightness bottom to top, over the pure hue.
    private FrameworkElement BuildArea()
    {
        _area = new Grid { Height = 164, Cursor = Cursors.Cross, Background = Brushes.Transparent };
        var radius = new CornerRadius(16);
        _area.Children.Add(new Border { CornerRadius = radius, Background = _hue });
        _area.Children.Add(new Border { CornerRadius = radius, Background = new LinearGradientBrush(Colors.White, Color.FromArgb(0, 255, 255, 255), 0) });
        _area.Children.Add(new Border { CornerRadius = radius, Background = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Colors.Black, 90), BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromArgb(20, 0, 0, 0)) });
        var canvas = new Canvas { ClipToBounds = false };
        _areaThumb = new Thumb2D(_opaque, null);
        AutomationProperties.SetName(_areaThumb, "Saturation and brightness");
        canvas.Children.Add(_areaThumb);
        _area.Children.Add(canvas);
        _area.SizeChanged += (_, _) => PlaceArea(false);
        void Read(Point at) => Commit(_hsva with { S = Math.Clamp(at.X / _area.ActualWidth, 0, 1), V = 1 - Math.Clamp(at.Y / _area.ActualHeight, 0, 1) });
        _area.MouseLeftButtonDown += (_, e) => { _area.CaptureMouse(); _areaThumb.Active = true; _areaThumb.Focus(); Read(e.GetPosition(_area)); e.Handled = true; };
        _area.MouseMove += (_, e) => { if (_area.IsMouseCaptured) Read(e.GetPosition(_area)); };
        _area.MouseLeftButtonUp += (_, _) => { _area.ReleaseMouseCapture(); };
        _area.LostMouseCapture += (_, _) => { _areaThumb.Active = false; PlaceArea(true); };
        _areaThumb.PreviewKeyDown += (_, e) =>
        {
            var step = StepFor(e.Key, true);
            if (step is null) return;
            e.Handled = true;
            Commit(_hsva with { S = Math.Clamp(_hsva.S + step.Value.X / 100, 0, 1), V = Math.Clamp(_hsva.V + step.Value.Y / 100, 0, 1) });
        };
        return _area;
    }

    /// <summary>Arrow keys step by one, Shift or Page keys by ten.</summary>
    internal static (double X, double Y)? StepFor(Key key, bool both)
    {
        double size = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || key is Key.PageUp or Key.PageDown ? 10 : 1;
        return key switch
        {
            Key.Right => (size, 0), Key.Left => (-size, 0), Key.Up => both ? (0, size) : (size, 0), Key.Down => both ? (0, -size) : (-size, 0),
            Key.PageUp => (10, 0), Key.PageDown => (-10, 0), _ => null,
        };
    }

    private void PlaceArea(bool animate)
    {
        if (_area is null || _areaThumb is null) return;
        _areaThumb.MoveTo(_hsva.S * _area.ActualWidth, (1 - _hsva.V) * _area.ActualHeight, animate && Full);
        AutomationProperties.SetItemStatus(_areaThumb, $"Saturation {Math.Round(_hsva.S * 100)}%, brightness {Math.Round(_hsva.V * 100)}%");
    }

    // .field: 40 high, radius 14, surface-muted; the format button (min 56, xs 500) cycles Hex → RGB → HSL → OKLCH; invalid input shakes once.
    private FrameworkElement BuildField()
    {
        var field = new Border { Height = 40, Margin = new Thickness(0, 16, 0, 0), CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1), BorderBrush = _fieldEdge, RenderTransform = _fieldShake, ClipToBounds = true };
        field.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        _formatText = new TextBlock { FontSize = 12, FontWeight = FontWeights.Medium, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, RenderTransform = new TranslateTransform() };
        var formatFill = new SolidColorBrush(Colors.Transparent);
        var format = new System.Windows.Controls.Button
        {
            Cursor = Cursors.Hand, FocusVisualStyle = null,
            Content = new Border { MinWidth = 56, Padding = new Thickness(10, 0, 10, 0), Background = formatFill, BorderThickness = new Thickness(0, 0, 1, 0), Child = new Grid { Children = { _formatText } } },
            Template = BareTemplate,
        };
        ((Border)format.Content).SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        void Hot(bool hot) { var color = Tween.Resolve(this, "ArcWpf.Brush.ColorFormatHover"); if (!hot) color.A = 0; Tween.Color(formatFill, color, Tween.Fast, Full); }
        format.MouseEnter += (_, _) => Hot(true);
        format.MouseLeave += (_, _) => Hot(false);
        format.Click += (_, _) => CycleFormat();
        grid.Children.Add(format);
        _input = new TextBox { FontSize = 12, Padding = new Thickness(10, 0, 10, 0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), VerticalContentAlignment = VerticalAlignment.Center, FocusVisualStyle = null };
        _input.SetResourceReference(StyleProperty, "ArcWpf.PhoneInput.Editor");
        _input.SetResourceReference(ForegroundProperty, "ArcWpf.Brush.Foreground");
        _input.TextChanged += (_, _) => { if (_syncing) return; _draft = _input.Text; SetInvalid(false); };
        _input.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { e.Handled = true; if (SubmitDraft()) _input.SelectAll(); }
            if (e.Key == Key.Escape && _draft is not null) { e.Handled = true; _draft = null; SetInvalid(false); Render(false); }
        };
        _input.LostKeyboardFocus += (_, _) => { if (!SubmitDraft()) { _draft = null; SetInvalid(false); Render(false); } };
        Grid.SetColumn(_input, 1);
        grid.Children.Add(_input);
        field.Child = grid;
        return field;
    }

    private static readonly ControlTemplate BareTemplate = CreateBare();
    private static ControlTemplate CreateBare()
    {
        var template = new ControlTemplate(typeof(System.Windows.Controls.Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) };
        template.Seal();
        return template;
    }

    // Contrast: an "Aa" sample on the background, the ratio (springs to its value), "against background", and a grade pill.
    private FrameworkElement BuildContrast()
    {
        var row = new DockPanel { Margin = new Thickness(0, 14, 0, 0), LastChildFill = true };
        var sample = new Border { Width = 36, Height = 28, CornerRadius = new CornerRadius(9), BorderThickness = new Thickness(1), Background = _sampleBg, Child = new TextBlock { Text = "Aa", FontWeight = FontWeights.Medium, Foreground = _color, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        sample.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        _ratio = new TextBlock { FontWeight = FontWeights.Medium };
        var ratio = new StackPanel { Orientation = Orientation.Horizontal, MinWidth = 36, Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center, Children = { _ratio, new TextBlock { Text = ":1", FontWeight = FontWeights.Medium } } };
        _grade = new Grid { Height = 22, ClipToBounds = false };
        var pill = new Border { CornerRadius = new CornerRadius(11), Padding = new Thickness(8, 0, 8, 0), Background = _gradeFill, Child = _grade, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        pill.SetValue(TextElement.ForegroundProperty, _gradeInk);
        pill.SetValue(TextElement.FontSizeProperty, 12d);
        pill.SetValue(TextElement.FontWeightProperty, FontWeights.Medium);
        var against = new TextBlock { Text = "against background", FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        against.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextSecondary");
        DockPanel.SetDock(sample, Dock.Left);
        DockPanel.SetDock(ratio, Dock.Left);
        DockPanel.SetDock(pill, Dock.Right);
        row.Children.Add(sample);
        row.Children.Add(ratio);
        row.Children.Add(pill);
        row.Children.Add(against);
        return row;
    }

    private FrameworkElement BuildSwatchRow()
    {
        var row = new DockPanel { Margin = new Thickness(0, 14, 0, 0), MinHeight = 32, LastChildFill = true };
        var ring = new Ellipse { Width = 30, Height = 30, StrokeThickness = 1, StrokeDashArray = [3, 2] };
        ring.SetResourceReference(Shape.StrokeProperty, "ArcWpf.Brush.BorderStrong");
        var plus = Lucide.Glyph(Lucide.Plus, 16);
        var add = new System.Windows.Controls.Button { Width = 30, Height = 30, Cursor = Cursors.Hand, FocusVisualStyle = null, Template = BareTemplate, Content = new Grid { Background = Brushes.Transparent, Children = { ring, plus } }, RenderTransformOrigin = new Point(.5, .5), RenderTransform = new ScaleTransform(1, 1) };
        add.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.TextSecondary");
        add.MouseEnter += (_, _) => { ring.SetResourceReference(Shape.StrokeProperty, "ArcWpf.Brush.Foreground"); add.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.Foreground"); };
        add.MouseLeave += (_, _) => { ring.SetResourceReference(Shape.StrokeProperty, "ArcWpf.Brush.BorderStrong"); add.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.TextSecondary"); };
        add.PreviewMouseLeftButtonDown += (_, _) => Tween.Press((ScaleTransform)add.RenderTransform, true, .92, Full, Tween.Fast);
        add.PreviewMouseLeftButtonUp += (_, _) => Tween.Press((ScaleTransform)add.RenderTransform, false, .92, Full);
        add.Click += (_, _) => SaveSwatch();
        DockPanel.SetDock(add, Dock.Left);
        row.Children.Add(add);
        _swatchList = new Grid { Margin = new Thickness(6, 0, 0, 0) };
        AutomationProperties.SetName(_swatchList, "Saved colors");
        row.Children.Add(_swatchList);
        AutomationProperties.SetName(add, $"Save {Hex}");
        add.Tag = "add";
        return row;
    }

    /* ---------------------------------------------- Rendering ---------------------------------------------- */

    private void Render(bool animate)
    {
        var color = ColorMath.ToColor(_hsva);
        _color.Color = color;
        _opaque.Color = ColorMath.ToColor(_hsva, opaque: true);
        _alphaEnd.Color = _opaque.Color;
        _hue.Color = ColorMath.ToColor(new Hsva(_hsva.H, 1, 1, 1));
        foreach (var (_, name, hex) in _faces) { name.Text = Label; hex.Text = Hex; }
        if (_trigger is not null) AutomationProperties.SetName(_trigger, $"{Label} {Hex}");
        PlaceArea(animate);
        _hueSlider?.Set(_hsva.H, $"{Math.Round(_hsva.H)} degrees", _hue, HueTrack(), animate && Full);
        _alphaSlider?.Set(_hsva.A * 100, $"{Math.Round(_hsva.A * 100)}%", _color, AlphaTrack(), animate && Full);
        if (_input is not null && _draft is null)
        {
            _syncing = true;
            _input.Text = ColorMath.Format(_hsva, Format);
            _syncing = false;
            AutomationProperties.SetName(_input, $"{Label} in {ColorMath.FormatName(Format)}");
        }
        if (_formatText is not null && _formatText.Text != ColorMath.FormatName(Format)) _formatText.Text = ColorMath.FormatName(Format);
        var background = ColorMath.Parse(BackgroundColor) is { } bg ? ColorMath.HsvToRgb(bg) : new Rgba(1, 1, 1, 1);
        _sampleBg.Color = ColorMath.ToColor(ColorMath.RgbToHsv(background));
        double ratio = ColorMath.Contrast(_hsva, background);
        // The ratio follows the color on the thumb spring (.26).
        if (animate && Full) Tween.Spring(this, RatioProperty, ratio, .26); else Tween.Set(this, RatioProperty, ratio);
        SetGrade(ColorMath.Grade(ratio), animate);
        PaintField(animate);
        PaintSwatches(animate);
    }

    private Brush HueTrack()
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        var stops = new[] { Colors.Red, Colors.Yellow, Colors.Lime, Colors.Cyan, Colors.Blue, Colors.Magenta, Colors.Red };
        for (int i = 0; i < stops.Length; i++) brush.GradientStops.Add(new GradientStop(stops[i], i / 6d));
        return brush;
    }

    private Brush AlphaTrack() => Checker();

    // The grade swaps in place: the new label rises .3em out of a subtle blur (.24 enter), the old one leaves in .12.
    private void SetGrade(string grade, bool animate)
    {
        if (_grade is null) return;
        string tone = grade == "Fails" ? "Fail" : grade == "AA large" ? "Large" : "Pass";
        string ink = tone switch { "Fail" => "Danger", "Large" => "Warning", _ => "Success" };
        Tween.Color(_gradeFill, Tween.Resolve(this, $"ArcWpf.Brush.Contrast{tone}"), Tween.Standard, animate && Full);
        Tween.Color(_gradeInk, Tween.Resolve(this, $"ArcWpf.Brush.{ink}"), Tween.Standard, animate && Full);
        if (grade == _gradeShown) return;
        _gradeShown = grade;
        var old = _grade.Children.OfType<TextBlock>().LastOrDefault();
        var text = new TextBlock { Text = grade, VerticalAlignment = VerticalAlignment.Center, RenderTransform = new TranslateTransform() };
        _grade.Children.Add(text);
        if (old is not null)
        {
            if (animate && Full) Tween.Present(old, false, 3.6, 2, Tween.Instant, ArcEasing.Standard, done: () => _grade.Children.Remove(old));
            else _grade.Children.Remove(old);
        }
        if (animate && Full) Tween.Present(text, true, 3.6, 2, Tween.Standard, ArcEasing.Enter);
        else if (animate) Tween.Present(text, true, 0, 0, Tween.Instant, ArcEasing.Standard);
        if (_ratio is not null) AutomationProperties.SetHelpText(_ratio, $"Contrast {ColorMath.Contrast(_hsva, new Rgba(1, 1, 1, 1)):0.00} to 1, {(grade == "Fails" ? "fails" : $"passes {grade}")}");
    }

    private void PaintField(bool animate)
    {
        Tween.Color(_fieldEdge, Tween.Resolve(this, _invalid ? "ArcWpf.Brush.Danger" : "ArcWpf.Brush.Border"), Tween.Fast, animate && Full);
        if (_error is not null) _error.Visibility = _invalid ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetInvalid(bool invalid)
    {
        if (_invalid == invalid) return;
        _invalid = invalid;
        PaintField(true);
    }

    // Format change: the old text leaves while the new one resolves in (a cross-fade standing in for the source's TextMorph character morph).
    private void CycleFormat()
    {
        var formats = Enum.GetValues<ColorFormat>();
        var next = formats[(Array.IndexOf(formats, Format) + 1) % formats.Length];
        _draft = null;
        SetInvalid(false);
        SetCurrentValue(FormatProperty, next);
        if (!Full || _input is null || _formatText is null) return;
        foreach (var element in new FrameworkElement[] { _input, _formatText }) Tween.Present(element, true, 0, 2, Tween.Standard, ArcEasing.Enter);
    }

    private bool SubmitDraft()
    {
        if (_draft is null) return true;
        if (ColorMath.Parse(_draft, _hsva.H) is not { } next)
        {
            SetInvalid(true);
            if (Full)
            {
                var frames = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(.32) };
                double[] values = [0, -5, 4, -2, 0];
                for (int i = 0; i < values.Length; i++) frames.KeyFrames.Add(new EasingDoubleKeyFrame(values[i], KeyTime.FromPercent(i / 4d), ArcEasing.Standard));
                _fieldShake.BeginAnimation(TranslateTransform.XProperty, frames);
            }
            return false;
        }
        _draft = null;
        SetInvalid(false);
        Commit(next);
        return true;
    }

    /* ---------------------------------------------- Open and close ---------------------------------------------- */

    private void TriggerClick(object sender, RoutedEventArgs e) => SetCurrentValue(IsOpenProperty, !IsOpen);

    private void Hide(bool returnFocus)
    {
        SetCurrentValue(IsOpenProperty, false);
        if (returnFocus) _trigger?.Focus();
    }

    // One progress value grows the swatch's box into the panel's box (morph spring with a little bounce; closing physical .3); the content fades
    // in behind the leading edge (progress .35 → .9) and settles from 10 DIP above.
    private void OnOpenChanged(bool open)
    {
        if (_popup is null || _panel is null) return;
        if (open)
        {
            _shown = true;
            _draft = null;
            SetInvalid(false);
            Render(false);
            _popup.IsOpen = true;
            _panel.UpdateLayout();
            AttachOutside();
            if (Full) { Tween.Set(this, ProgressProperty, 0); _panel.Opacity = 1; Tween.To(this, ProgressProperty, 1, .62, ArcEasing.Spring(.42, .62)); }
            else { Tween.Set(this, ProgressProperty, 1); _panel.Opacity = 0; Tween.To(_panel, OpacityProperty, 1, Tween.Fast, ArcEasing.Standard); }
            Dispatcher.BeginInvoke(() => _areaThumb?.Focus(), System.Windows.Threading.DispatcherPriority.Input);
            return;
        }
        DetachOutside();
        if (!_shown) return;
        _shown = false;
        if (!IsLoaded || Tween.Level(this) == ArcMotionLevel.Off) { _popup.IsOpen = false; return; }
        if (Full) Tween.Spring(this, ProgressProperty, 0, .3, done: () => { if (!IsOpen) _popup.IsOpen = false; });
        else Tween.To(_panel, OpacityProperty, 0, Tween.Fast, ArcEasing.Standard, done: () => { if (!IsOpen) _popup.IsOpen = false; });
    }

    private void ApplyProgress()
    {
        if (_panel is null || _trigger is null) return;
        double q = Math.Clamp((double)GetValue(ProgressProperty), 0, 1.2);
        double W = _panel.ActualWidth, H = _panel.ActualHeight, w = _trigger.ActualWidth, h = _trigger.ActualHeight;
        double width = W - (W - w) * (1 - q), height = H - (H - h) * (1 - q), r = 22 + 4 * Math.Min(q, 1);
        _clip.Rect = new Rect(0, 0, Math.Max(0, width), Math.Max(0, height));
        _clip.RadiusX = _clip.RadiusY = r;
        double content = Math.Clamp((q - .35) / .55, 0, 1);
        if (_content is not null) _content.Opacity = content;
        if (_headActions is not null) _headActions.Opacity = content;
        _contentMove.Y = -10 * (1 - Math.Min(q, 1));
        _panel.SetResourceReference(Border.BackgroundProperty, Tween.Resolve(this, "ArcWpf.Brush.Surface") is var s && s.R + s.G + s.B < 3 * 128 ? "ArcWpf.Brush.SurfaceRaised" : "ArcWpf.Brush.Surface");
    }

    private void AttachOutside()
    {
        _window = Window.GetWindow(this);
        if (_window is null) return;
        _window.PreviewMouseDown += OutsideDown;
        _window.Deactivated += OutsideDeactivated;
        _window.PreviewKeyDown += WindowKey;
        if (_popup?.Child is not null) _popup.Child.PreviewKeyDown += WindowKey;
    }

    private void DetachOutside()
    {
        if (_popup?.Child is not null) _popup.Child.PreviewKeyDown -= WindowKey;
        if (_window is null) return;
        _window.PreviewMouseDown -= OutsideDown;
        _window.Deactivated -= OutsideDeactivated;
        _window.PreviewKeyDown -= WindowKey;
        _window = null;
    }

    private void OutsideDown(object sender, MouseButtonEventArgs e) { if (_trigger?.IsMouseOver != true) Hide(false); }
    private void OutsideDeactivated(object? sender, EventArgs e) => Hide(false);

    private void WindowKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || !IsOpen) return;
        if (_input?.IsKeyboardFocused == true && _draft is not null) return;
        e.Handled = true;
        Hide(true);
    }

    /* ---------------------------------------------- Swatches ---------------------------------------------- */

    private List<ColorSwatch> SwatchList => Swatches?.ToList() ?? [];

    private void SetSwatches(List<ColorSwatch> next) => SetCurrentValue(SwatchesProperty, next);

    private void SaveSwatch()
    {
        var entry = new ColorSwatch($"swatch-{Guid.NewGuid():N}", Hex);
        _focusSwatch = entry.Id;
        SetSwatches([entry, .. SwatchList.Take(Math.Max(0, MaxSwatches - 1))]);
    }

    private readonly Dictionary<string, System.Windows.Controls.Button> _swatchButtons = [];

    // Swatches pop in from scale .4 (snappy) and leave in .16; the rest glide to their new places (smooth).
    private void BuildSwatches(bool animateIgnored)
    {
        if (_swatchList is null) return;
        bool animate = Full && _shown;
        var list = SwatchList;
        var panel = _swatchList.Children.OfType<StackPanel>().FirstOrDefault();
        if (panel is null) { panel = new StackPanel { Orientation = Orientation.Horizontal }; _swatchList.Children.Add(panel); }
        var before = animate ? Flip.Capture(panel) : [];
        foreach (var id in _swatchButtons.Keys.Where(id => list.All(entry => entry.Id != id)).ToList())
        {
            var button = _swatchButtons[id];
            _swatchButtons.Remove(id);
            if (!animate) { panel.Children.Remove(button); continue; }
            button.IsHitTestVisible = false;
            var scale = new ScaleTransform(1, 1);
            ((FrameworkElement)button.Content).RenderTransform = scale;
            Tween.To(scale, ScaleTransform.ScaleXProperty, .4, Tween.Fast, null);
            Tween.To(scale, ScaleTransform.ScaleYProperty, .4, Tween.Fast, null);
            Tween.To(button, OpacityProperty, 0, Tween.Fast, null, done: () => panel.Children.Remove(button));
        }
        for (int index = 0; index < list.Count; index++)
        {
            var entry = list[index];
            if (!_swatchButtons.TryGetValue(entry.Id, out var button))
            {
                button = SwatchButton(entry);
                _swatchButtons[entry.Id] = button;
                panel.Children.Insert(Math.Min(index, panel.Children.Count), button);
                if (animate)
                {
                    var inner = (FrameworkElement)button.Content;
                    var scale = new ScaleTransform(.4, .4);
                    inner.RenderTransform = scale;
                    inner.RenderTransformOrigin = new Point(.5, .5);
                    button.Opacity = 0;
                    Tween.Spring(scale, ScaleTransform.ScaleXProperty, 1, Tween.Snappy);
                    Tween.Spring(scale, ScaleTransform.ScaleYProperty, 1, Tween.Snappy);
                    Tween.Spring(button, OpacityProperty, 1, Tween.Snappy);
                }
            }
            else
            {
                int at = panel.Children.IndexOf(button);
                if (at != index) { panel.Children.RemoveAt(at); panel.Children.Insert(Math.Min(index, panel.Children.Count), button); }
            }
        }
        if (animate) Flip.Play(panel, before, Tween.Smooth);
        PaintSwatches(animate);
        if (_focusSwatch is not null && _swatchButtons.TryGetValue(_focusSwatch, out var focus) && _shown && panel.IsKeyboardFocusWithin) focus.Focus();
    }

    private System.Windows.Controls.Button SwatchButton(ColorSwatch entry)
    {
        var color = ColorMath.Parse(entry.Color) is { } parsed ? ColorMath.ToColor(parsed) : Colors.Transparent;
        var chip = Chip(new SolidColorBrush(color), 26);
        chip.RenderTransformOrigin = new Point(.5, .5);
        chip.RenderTransform = new ScaleTransform(1, 1);
        var ring = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(15), BorderThickness = new Thickness(2), Opacity = 0 };
        ring.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Foreground");
        var content = new Grid { Width = 30, Height = 30, Background = Brushes.Transparent, Children = { ring, chip } };
        var button = new System.Windows.Controls.Button { Width = 30, Height = 30, Margin = new Thickness(0, 0, 6, 0), Cursor = Cursors.Hand, FocusVisualStyle = null, Template = BareTemplate, Content = content, Tag = entry.Id, RenderTransform = new TranslateTransform() };
        AutomationProperties.SetName(button, $"{entry.Color}. Alt and arrow keys to move, Delete to remove");
        button.GotKeyboardFocus += (_, _) => _focusSwatch = entry.Id;
        button.PreviewKeyDown += (_, e) => SwatchKey(e, entry);
        var drag = new SwatchDrag(this, button, entry);
        button.Click += (_, _) => { if (drag.Dragged) return; if (ColorMath.Parse(entry.Color, _hsva.H) is { } next) Commit(next); };
        return button;
    }

    private void PaintSwatches(bool animate)
    {
        foreach (var entry in SwatchList)
        {
            if (!_swatchButtons.TryGetValue(entry.Id, out var button) || button.Content is not Grid { Children: [Border ring, Border chip] }) continue;
            bool current = string.Equals(entry.Color, Hex, StringComparison.OrdinalIgnoreCase);
            ring.Opacity = current ? 1 : 0;
            var scale = (ScaleTransform)chip.RenderTransform;
            if (animate && Full) { Tween.To(scale, ScaleTransform.ScaleXProperty, current ? .78 : 1, Tween.SpringTokenSeconds, Tween.SpringToken); Tween.To(scale, ScaleTransform.ScaleYProperty, current ? .78 : 1, Tween.SpringTokenSeconds, Tween.SpringToken); }
            else { Tween.Set(scale, ScaleTransform.ScaleXProperty, current ? .78 : 1); Tween.Set(scale, ScaleTransform.ScaleYProperty, current ? .78 : 1); }
            AutomationProperties.SetItemStatus(button, current ? "Selected" : "");
            KeyboardNavigation.SetIsTabStop(button, entry.Id == (SwatchList.Any(item => item.Id == _focusSwatch) ? _focusSwatch : SwatchList.FirstOrDefault()?.Id));
        }
    }

    private void SwatchKey(KeyEventArgs e, ColorSwatch item)
    {
        var list = SwatchList;
        int index = list.FindIndex(entry => entry.Id == item.Id);
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.Delete or Key.Back)
        {
            e.Handled = true;
            list.RemoveAt(index);
            var neighbor = list.Count > 0 ? list[Math.Min(index, list.Count - 1)] : null;
            _focusSwatch = neighbor?.Id;
            SetSwatches(list);
            Dispatcher.BeginInvoke(() => { if (neighbor is not null && _swatchButtons.TryGetValue(neighbor.Id, out var next)) next.Focus(); else _input?.Focus(); }, System.Windows.Threading.DispatcherPriority.Input);
            return;
        }
        int move = key == Key.Right ? 1 : key == Key.Left ? -1 : 0;
        if (move == 0 && key is not (Key.Home or Key.End)) return;
        e.Handled = true;
        if (move != 0 && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            int to = index + move;
            if (to < 0 || to >= list.Count) return;
            list.RemoveAt(index);
            list.Insert(to, item);
            SetSwatches(list);
            Dispatcher.BeginInvoke(() => { if (_swatchButtons.TryGetValue(item.Id, out var same)) same.Focus(); }, System.Windows.Threading.DispatcherPriority.Input);
            return;
        }
        int target = key == Key.Home ? 0 : key == Key.End ? list.Count - 1 : (index + move + list.Count) % list.Count;
        _focusSwatch = list[target].Id;
        if (_swatchButtons.TryGetValue(list[target].Id, out var button)) button.Focus();
    }

    internal void Reorder(ColorSwatch item, int to)
    {
        var list = SwatchList;
        int from = list.FindIndex(entry => entry.Id == item.Id);
        to = Math.Clamp(to, 0, list.Count - 1);
        if (from < 0 || from == to) return;
        list.RemoveAt(from);
        list.Insert(to, item);
        SetSwatches(list);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);

    /// <summary>Drags a swatch along the row (scale 1.15 while held); releasing drops it at the nearest slot.</summary>
    private sealed class SwatchDrag
    {
        private readonly ColorPicker _owner;
        private readonly System.Windows.Controls.Button _button;
        private readonly ColorSwatch _entry;
        private Point _start;
        private bool _down;

        public SwatchDrag(ColorPicker owner, System.Windows.Controls.Button button, ColorSwatch entry)
        {
            _owner = owner;
            _button = button;
            _entry = entry;
            button.PreviewMouseLeftButtonDown += (_, e) => { _down = true; Dragged = false; _start = e.GetPosition(button.Parent as UIElement); };
            button.PreviewMouseMove += (_, e) =>
            {
                if (!_down || button.Parent is not UIElement parent) return;
                double dx = e.GetPosition(parent).X - _start.X;
                if (!Dragged && Math.Abs(dx) < 4) return;
                if (!Dragged) { Dragged = true; button.CaptureMouse(); Panel.SetZIndex(button, 2); if (button.Content is FrameworkElement inner) { inner.RenderTransformOrigin = new Point(.5, .5); inner.LayoutTransform = Transform.Identity; inner.RenderTransform = new ScaleTransform(1.15, 1.15); } }
                ((TranslateTransform)button.RenderTransform).X = dx;
            };
            button.PreviewMouseLeftButtonUp += (_, e) => Release(e);
            button.LostMouseCapture += (_, _) => { if (_down && Dragged) Finish(0); };
        }

        public bool Dragged { get; private set; }

        private void Release(MouseButtonEventArgs e)
        {
            if (!_down) return;
            _down = false;
            if (!Dragged) return;
            double dx = ((TranslateTransform)_button.RenderTransform).X;
            _button.ReleaseMouseCapture();
            Finish(dx);
            e.Handled = true;
            _owner.Dispatcher.BeginInvoke(() => Dragged = false, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void Finish(double dx)
        {
            _down = false;
            Panel.SetZIndex(_button, 0);
            if (_button.Content is FrameworkElement inner) inner.RenderTransform = new ScaleTransform(1, 1);
            var move = (TranslateTransform)_button.RenderTransform;
            int shift = (int)Math.Round(dx / 36);
            var list = _owner.SwatchList;
            int from = list.FindIndex(entry => entry.Id == _entry.Id);
            Tween.Set(move, TranslateTransform.XProperty, 0);
            if (shift != 0) _owner.Reorder(_entry, from + shift);
        }
    }
}

/// <summary>A 22 DIP thumb with a 3 DIP white ring and a soft shadow; it chases its target on a spring (.26, .1 while dragged) and grows to 1.2 while active.</summary>
internal sealed class Thumb2D : Grid
{
    private readonly TranslateTransform _move = new();
    private readonly ScaleTransform _scale = new(1, 1);
    private bool _active;

    public Thumb2D(Brush fill, Brush? under)
    {
        Width = Height = 28;
        Focusable = true;
        FocusVisualStyle = null;
        Cursor = Cursors.Hand;
        IsHitTestVisible = true;
        var ring = new Ellipse { Fill = Brushes.White, Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 2, Direction = 270, Opacity = .3 } };
        Children.Add(ring);
        if (under is not null) Children.Add(new Ellipse { Width = 22, Height = 22, Fill = under });
        Children.Add(new Ellipse { Width = 22, Height = 22, Fill = fill, Stroke = new SolidColorBrush(Color.FromArgb(31, 0, 0, 0)), StrokeThickness = 1 });
        RenderTransform = new TransformGroup { Children = { _scale, _move } };
        RenderTransformOrigin = new Point(.5, .5);
    }

    public bool Active
    {
        get => _active;
        set
        {
            _active = value;
            bool full = IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;
            double to = value ? 1.2 : 1;
            if (full) { Tween.To(_scale, ScaleTransform.ScaleXProperty, to, Tween.SpringTokenSeconds, Tween.SpringToken); Tween.To(_scale, ScaleTransform.ScaleYProperty, to, Tween.SpringTokenSeconds, Tween.SpringToken); }
            else { Tween.Set(_scale, ScaleTransform.ScaleXProperty, 1); Tween.Set(_scale, ScaleTransform.ScaleYProperty, 1); }
            Cursor = value ? Cursors.SizeAll : Cursors.Hand;
        }
    }

    public void MoveTo(double x, double y, bool animate)
    {
        double vd = _active ? .1 : .26;
        if (animate) { Tween.Spring(_move, TranslateTransform.XProperty, x - 14, vd); Tween.Spring(_move, TranslateTransform.YProperty, y - 14, vd); }
        else { Tween.Set(_move, TranslateTransform.XProperty, x - 14); Tween.Set(_move, TranslateTransform.YProperty, y - 14); }
    }
}

/// <summary>A 14 DIP slider whose track runs 11 DIP past the thumb's travel, so the color under the thumb is the value it reports.</summary>
internal sealed class SliderTrack : Grid
{
    private readonly Border _track = new() { CornerRadius = new CornerRadius(7) };
    private readonly Border _overlay = new() { CornerRadius = new CornerRadius(7), BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromArgb(20, 0, 0, 0)) };
    private readonly Canvas _travel = new() { Margin = new Thickness(11, 0, 11, 0) };
    private readonly Thumb2D _thumb;
    private readonly SolidColorBrush _fill = new(Colors.Red);
    private readonly double _max;
    private readonly Action<double> _change;
    private double _value;

    public SliderTrack(FrameworkElement owner, double max, Action<double> change, string label, Brush? under = null)
    {
        _max = max;
        _change = change;
        Height = 14;
        Cursor = Cursors.Hand;
        Background = Brushes.Transparent;
        _thumb = new Thumb2D(_fill, under);
        AutomationProperties.SetName(_thumb, label);
        _travel.Children.Add(_thumb);
        Children.Add(_track);
        Children.Add(_overlay);
        Children.Add(_travel);
        SizeChanged += (_, _) => Place(false);
        MouseLeftButtonDown += (_, e) => { CaptureMouse(); _thumb.Active = true; _thumb.Focus(); Read(e); e.Handled = true; };
        MouseMove += (_, e) => { if (IsMouseCaptured) Read(e); };
        MouseLeftButtonUp += (_, _) => ReleaseMouseCapture();
        LostMouseCapture += (_, _) => { _thumb.Active = false; Place(true); };
        _thumb.PreviewKeyDown += (_, e) =>
        {
            if (e.Key is Key.Home or Key.End) { e.Handled = true; _change(e.Key == Key.Home ? 0 : _max); return; }
            var step = ColorPicker.StepFor(e.Key, false);
            if (step is null) return;
            e.Handled = true;
            _change(Math.Clamp(_value + step.Value.X, 0, _max));
        };
    }

    private void Read(MouseEventArgs e)
    {
        double width = Math.Max(1, _travel.ActualWidth);
        _change(Math.Clamp(e.GetPosition(_travel).X / width, 0, 1) * _max);
    }

    public void Set(double value, string valueText, SolidColorBrush fill, Brush track, bool animate)
    {
        _value = value;
        _fill.Color = fill.Color;
        if (_max == 360)
        {
            // Hue stops sit at the thumb's travel ends.
            double width = Math.Max(23, ActualWidth);
            var brush = (LinearGradientBrush)track;
            foreach (var stop in brush.GradientStops) stop.Offset = (11 + stop.Offset * (width - 22)) / width;
            _track.Background = brush;
        }
        else
        {
            double width = Math.Max(23, ActualWidth);
            var end = fill.Color; end.A = 255;
            var gradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0), GradientStops = { new GradientStop(Color.FromArgb(0, end.R, end.G, end.B), 11 / width), new GradientStop(end, (width - 11) / width) } };
            _track.Background = track;
            _overlay.Background = gradient;
        }
        AutomationProperties.SetItemStatus(_thumb, valueText);
        Place(animate);
    }

    private void Place(bool animate) => _thumb.MoveTo(_value / _max * _travel.ActualWidth, 7, animate);
}
