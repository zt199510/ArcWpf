using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ArcWpf.Controls;

// filter-toolbar.tsx FilterMenu (frozen 2026-10-31): an Add filter button that grows into its own menu. Pick a field,
// then a value. The surface springs from the button's rounded rectangle to the panel and back; the face fades with
// the morph and the content reveals behind the growing edge. WPF hosts the morph in a Popup so it overlays siblings.
[TemplatePart(Name = "PART_Trigger", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_Rest", Type = typeof(Border))]
[TemplatePart(Name = "PART_Face", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
public class FilterMenu : Control
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(FilterMenu), new FrameworkPropertyMetadata("Add filter"));
    public static readonly DependencyProperty AlignmentProperty = DependencyProperty.Register(nameof(Alignment), typeof(FilterMenuAlignment), typeof(FilterMenu), new FrameworkPropertyMetadata(FilterMenuAlignment.End));
    public static readonly DependencyProperty ActiveProperty = DependencyProperty.Register(nameof(Active), typeof(IEnumerable<FilterChip>), typeof(FilterMenu));
    public static readonly RoutedEvent FilterSelectedEvent = EventManager.RegisterRoutedEvent(nameof(FilterSelected), RoutingStrategy.Bubble, typeof(EventHandler<FilterChipEventArgs>), typeof(FilterMenu));

    /// <summary>Trigger text and the title of the field step.</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <summary>The trigger edge the panel lines up with; it flips or shifts when the window has no room.</summary>
    public FilterMenuAlignment Alignment { get => (FilterMenuAlignment)GetValue(AlignmentProperty); set => SetValue(AlignmentProperty, value); }
    /// <summary>Applied filters. Each field shows its current value and the value step marks it.</summary>
    public IEnumerable<FilterChip>? Active { get => (IEnumerable<FilterChip>?)GetValue(ActiveProperty); set => SetValue(ActiveProperty, value); }
    public ObservableCollection<FilterField> Fields { get; } = [];
    public event EventHandler<FilterChipEventArgs> FilterSelected { add => AddHandler(FilterSelectedEvent, value); remove => RemoveHandler(FilterSelectedEvent, value); }
    public bool IsOpen => _phase == Phase.Open;

    internal static readonly Geometry PlusIcon = Frozen("M5,12 h14 M12,5 v14");
    internal static readonly Geometry ChevronLeftIcon = Frozen("M15,18 l-6,-6 l6,-6");
    internal static readonly Geometry ChevronRightIcon = Frozen("M9,18 l6,-6 l-6,-6");
    internal static readonly Geometry CheckIcon = Frozen("M20,6 L9,17 l-5,-5");
    internal static readonly Geometry XIcon = Frozen("M18,6 L6,18 M6,6 l12,12");

    private enum Phase { Closed, Open, Closing }
    private const double G = 24, G_BOTTOM = 48, PANEL = 256, ROOM = 16, MORPH = .42, SNAPPY = .26, SMOOTH = .4, CONTROL = 18, PANEL_RADIUS = 26;
    private ButtonBase? _trigger;
    private Border? _rest;
    private FrameworkElement? _face;
    private Popup? _popup;
    private Canvas? _stage, _inner, _stepLeaving;
    private Border? _surface, _faceCopy, _panel;
    private RectangleGeometry? _clip;
    private Grid? _titleSlot, _stepHost;
    private Canvas? _titleLeaving;
    private TextBlock? _title;
    private System.Windows.Controls.Button? _back;
    private ScaleTransform? _contentScale;
    private TranslateTransform? _contentMove;
    private ScaleTransform? _faceScale;
    private MenuList? _list;
    private readonly SpringDriver _driver;
    private readonly ControlSpring _w, _h, _l, _r;
    private Phase _phase;
    private FilterField? _field;
    private bool _up;
    private double _x, _from, _to, _trigW, _trigH, _panelH, _minX, _minY;
    private Window? _window;

    static FilterMenu() { DefaultStyleKeyProperty.OverrideMetadata(typeof(FilterMenu), new FrameworkPropertyMetadata(typeof(FilterMenu))); }

    public FilterMenu()
    {
        _driver = new SpringDriver(Apply) { Settled = Settled };
        _w = _driver.Add(0, MORPH);
        _h = _driver.Add(0, MORPH);
        _l = _driver.Add(0, MORPH);
        _r = _driver.Add(CONTROL, MORPH);
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => { Detach(); if (_phase != Phase.Closed) Finish(); };
    }

    private ArcMotionLevel Level => ArcMotion.GetLevel(this);
    private bool Full => Level == ArcMotionLevel.Full;

    public override void OnApplyTemplate()
    {
        if (_trigger is not null) { _trigger.Click -= TriggerClick; _trigger.PreviewKeyDown -= TriggerKey; }
        base.OnApplyTemplate();
        _trigger = GetTemplateChild("PART_Trigger") as ButtonBase;
        _rest = GetTemplateChild("PART_Rest") as Border;
        _face = GetTemplateChild("PART_Face") as FrameworkElement;
        _popup = GetTemplateChild("PART_Popup") as Popup;
        if (_trigger is not null) { _trigger.Click += TriggerClick; _trigger.PreviewKeyDown += TriggerKey; }
        if (_popup is not null) _popup.Child = BuildStage();
    }

    // Stage (transparent gutter for the shadow) > surface (the morphing rounded rectangle) > clipped inner canvas
    // holding a copy of the trigger face and the panel at its final position.
    private Canvas BuildStage()
    {
        _stage = new Canvas { Background = Brushes.Transparent };
        _surface = new Border { BorderThickness = new Thickness(1) };
        _surface.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        _surface.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceRaised");
        _surface.SetResourceReference(EffectProperty, "ArcWpf.Shadow.Floating");
        _clip = new RectangleGeometry();
        _inner = new Canvas { Clip = _clip };
        _surface.Child = _inner;
        _stage.Children.Add(_surface);

        _faceScale = new ScaleTransform(1, 1);
        _faceCopy = new Border { BorderThickness = new Thickness(1), Padding = new Thickness(12, 0, 12, 0), Child = Face(Label), RenderTransformOrigin = new Point(.5, .5), RenderTransform = _faceScale, IsHitTestVisible = false };
        _inner.Children.Add(_faceCopy);

        _back = new System.Windows.Controls.Button { Width = 28, Height = 28, Margin = new Thickness(-6, 0, 2, 0), Content = Glyph(ChevronLeftIcon, 16, 1.8, null), Visibility = Visibility.Collapsed, RenderTransformOrigin = new Point(.5, .5), RenderTransform = new ScaleTransform(1, 1) };
        _back.SetResourceReference(StyleProperty, new ComponentResourceKey(typeof(FilterMenu), "Back"));
        AutomationProperties.SetName(_back, "Back to fields");
        _back.Click += (_, _) => GoBack(false);
        _titleSlot = new Grid { VerticalAlignment = VerticalAlignment.Center };
        _titleLeaving = new Canvas { IsHitTestVisible = false };
        _titleSlot.Children.Add(_titleLeaving);
        var head = new DockPanel { Height = 32, Margin = new Thickness(10, 0, 8, 0), LastChildFill = true };
        DockPanel.SetDock(_back, Dock.Left);
        head.Children.Add(_back);
        head.Children.Add(_titleSlot);
        _stepLeaving = new Canvas { IsHitTestVisible = false };
        _stepHost = new Grid { Margin = new Thickness(0, 2, 0, 0), Children = { _stepLeaving } };
        _contentScale = new ScaleTransform(1, 1);
        _contentMove = new TranslateTransform();
        var content = new StackPanel { Children = { head, _stepHost }, RenderTransform = new TransformGroup { Children = { _contentScale, _contentMove } } };
        _panel = new Border { Width = PANEL, Padding = new Thickness(6), Focusable = true, FocusVisualStyle = null, Child = content };
        AutomationProperties.SetName(_panel, Label);
        _panel.PreviewKeyDown += PanelPreviewKey;
        _panel.KeyDown += PanelKey;
        _inner.Children.Add(_panel);
        _stage.PreviewKeyDown += EscapeKey;
        return _stage;
    }

    // The trigger face: plus 15 (stroke 1.8) and the label, gap 8, 14/500.
    internal static StackPanel Face(string label)
    {
        var text = new TextBlock { Text = label, FontSize = 14, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center };
        text.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.Foreground");
        var plus = Glyph(PlusIcon, 15, 1.8, "ArcWpf.Brush.Foreground");
        plus.Margin = new Thickness(0, 0, 8, 0);
        return new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { plus, text } };
    }

    /// <summary>Draws a 24-unit Geometry as a stroke at <paramref name="size"/>; other icons are hosted as they are.</summary>
    internal static FrameworkElement Glyph(object? icon, double size, double stroke, string? brush)
    {
        if (icon is not Geometry geometry) return new ContentPresenter { Content = icon, VerticalAlignment = VerticalAlignment.Center };
        var path = new Path { Data = geometry, Width = 24, Height = 24, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round };
        // The Viewbox scales the 24 grid down; dividing keeps the stroke at its CSS pixel width.
        path.StrokeThickness = stroke * 24 / size;
        if (brush is not null) path.SetResourceReference(Shape.StrokeProperty, brush);
        else path.SetBinding(Shape.StrokeProperty, new System.Windows.Data.Binding { Path = new PropertyPath(TextElement.ForegroundProperty), RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.Self) });
        return new Viewbox { Width = size, Height = size, Child = path, VerticalAlignment = VerticalAlignment.Center };
    }

    private static Geometry Frozen(string data) { var geometry = Geometry.Parse(data); geometry.Freeze(); return geometry; }

    private void TriggerClick(object sender, RoutedEventArgs e)
    {
        if (_phase == Phase.Open) return;
        // A click from the keyboard focuses the first item; a pointer click focuses the dialog itself.
        Open(Keyboard.IsKeyDown(Key.Enter) || Keyboard.IsKeyDown(Key.Space) ? FocusTarget.First : FocusTarget.Panel);
    }

    private void TriggerKey(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Down or Key.Up) { e.Handled = true; Open(e.Key == Key.Down ? FocusTarget.First : FocusTarget.Last); }
        else if (e.Key is Key.Enter && _phase != Phase.Open) { e.Handled = true; Open(FocusTarget.First); }
    }

    private void EscapeKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || _phase != Phase.Open) return;
        e.Handled = true;
        Close(true);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        EscapeKey(this, e);
    }

    internal enum FocusTarget { First, Last, Checked, Panel, Key }

    private void Open(FocusTarget focus)
    {
        if (_trigger is null || _popup is null || _panel is null || Fields.Count == 0) return;
        _trigW = ActualWidth;
        _trigH = ActualHeight;
        ShowStep(null, -1, false, focus, null);
        double panelW = Math.Min(PANEL, Math.Max(160, RoomWidth() - 2 * ROOM)) ;
        _panel.Width = panelW;
        _panel.Measure(new Size(panelW, double.PositiveInfinity));
        double width = panelW + 2, height = _panel.DesiredSize.Height + 2;
        // Line up with the chosen edge; flip to the other edge, then shift, when the window has no room.
        var (roomLeft, roomTop, roomRight, roomBottom) = Room();
        double min = roomLeft + ROOM, max = roomRight - ROOM - width, end = _trigW - width;
        double x = Alignment == FilterMenuAlignment.End ? (end >= min ? end : 0 <= max ? 0 : end) : (0 <= max ? 0 : end >= min ? end : 0);
        _x = Math.Round(Math.Min(Math.Max(x, min), Math.Max(min, max)));
        double below = roomBottom, above = _trigH - roomTop;
        _up = below < height + ROOM && above > below;
        if (_phase == Phase.Closed)
        {
            _w.Snap(_trigW); _h.Snap(_trigH); _l.Snap(0); _r.Snap(CONTROL);
        }
        _from = _trigW;
        _to = width;
        _phase = Phase.Open;
        if (_faceCopy?.Child is StackPanel face && face.Children[1] is TextBlock label) label.Text = Label;
        if (_faceCopy is not null) { _faceCopy.Width = _trigW; _faceCopy.Height = _trigH; }
        Size(height);
        _popup.IsOpen = true;
        if (_rest is not null) _rest.Opacity = 0;
        if (_face is not null) _face.Opacity = 0;
        _trigger.IsHitTestVisible = false;
        if (Full)
        {
            _driver.Animate(_l, _x, MORPH);
            _driver.Animate(_w, width, MORPH);
            _driver.Animate(_h, height, MORPH);
            _driver.Animate(_r, PANEL_RADIUS, MORPH);
        }
        else
        {
            _driver.Stop();
            _l.Snap(_x); _w.Snap(width); _h.Snap(height); _r.Snap(PANEL_RADIUS);
            Apply();
        }
        RequestFocus(focus, null);
    }

    // Closing is quicker than opening: the snappy spring lands the shape back on the button.
    private void Close(bool restore)
    {
        if (_phase != Phase.Open || _trigger is null) return;
        _phase = Phase.Closing;
        if (restore) _trigger.Focus();
        if (!Full) { Finish(); return; }
        _from = _trigW;
        _driver.Animate(_l, 0, SNAPPY);
        _driver.Animate(_w, _trigW, SNAPPY);
        _driver.Animate(_h, _trigH, SNAPPY);
        _driver.Animate(_r, CONTROL, SNAPPY);
    }

    private void Settled() { if (_phase == Phase.Closing) Finish(); }

    private void Finish()
    {
        _driver.Stop();
        _phase = Phase.Closed;
        _field = null;
        if (_popup is not null) _popup.IsOpen = false;
        if (_rest is not null) _rest.Opacity = 1;
        if (_face is not null) _face.Opacity = 1;
        if (_trigger is not null) _trigger.IsHitTestVisible = true;
    }

    // The popup stage covers the trigger and the panel plus the shadow gutter; it moves when the panel flips up.
    private void Size(double panelH)
    {
        if (_stage is null || _popup is null) return;
        _panelH = panelH;
        _minX = Math.Min(0, _x);
        _minY = _up ? Math.Min(0, _trigH - panelH) : 0;
        _stage.Width = Math.Max(_trigW, _x + _to) - _minX + 2 * G;
        _stage.Height = Math.Max(_trigH, panelH) + G + G_BOTTOM;
        _popup.HorizontalOffset = _minX - G;
        _popup.VerticalOffset = _minY - G;
        Apply();
    }

    private void Apply()
    {
        if (_surface is null || _inner is null || _clip is null || _faceCopy is null || _panel is null || _faceScale is null || _contentScale is null || _contentMove is null) return;
        double w = Math.Max(2, _w.Position), h = Math.Max(2, _h.Position), l = _l.Position, r = Math.Max(0, _r.Position);
        // How far the shape has grown from the button (0) to the panel (1); face and content read it so they reveal with
        // the morph and reverse with it when a close interrupts an open.
        double progress = _to > _from ? Math.Clamp((w - _from) / (_to - _from), 0, 1) : 1;
        double faceOut = Math.Clamp(progress / .35, 0, 1), contentIn = Math.Clamp((progress - .4) / .5, 0, 1);
        double top = _up ? _trigH - h : 0;
        _surface.Width = w;
        _surface.Height = h;
        _surface.CornerRadius = new CornerRadius(r);
        Canvas.SetLeft(_surface, l - _minX + G);
        Canvas.SetTop(_surface, top - _minY + G);
        _clip.Rect = new Rect(0, 0, w - 2, h - 2);
        _clip.RadiusX = _clip.RadiusY = Math.Max(0, r - 1);

        Canvas.SetLeft(_faceCopy, -l - 1);
        Canvas.SetTop(_faceCopy, -top - 1);
        _faceCopy.Opacity = 1 - faceOut;
        _faceScale.ScaleX = _faceScale.ScaleY = 1 - faceOut * .04;
        _faceCopy.Effect = faceOut > 0 && faceOut < 1 ? new BlurEffect { Radius = faceOut * 2 } : null;

        double panelTop = _up ? h - 2 - (_panelH - 2) : 0;
        Canvas.SetLeft(_panel, _x - l);
        Canvas.SetTop(_panel, panelTop);
        // The content scales up from the trigger's center, so it reads as growing out of the button.
        _panel.Opacity = contentIn;
        _panel.IsHitTestVisible = _phase == Phase.Open;
        _contentScale.CenterX = Math.Round(_trigW / 2 - _x);
        _contentScale.CenterY = _up ? _panelH : 0;
        _contentScale.ScaleX = _contentScale.ScaleY = .96 + contentIn * .04;
        _contentMove.Y = (1 - contentIn) * (_up ? -6 : 6);
        _panel.Effect = contentIn < 1 ? new BlurEffect { Radius = (1 - contentIn) * 4 } : null;
    }

    // Window client area relative to this menu: the room the panel may use.
    private (double Left, double Top, double Right, double Bottom) Room()
    {
        var root = Window.GetWindow(this)?.Content as FrameworkElement;
        if (root is null || !root.IsAncestorOf(this)) return (-1e4, -1e4, 1e4, 1e4);
        var origin = TransformToAncestor(root).Transform(new Point(0, 0));
        return (-origin.X, -origin.Y, root.ActualWidth - origin.X, root.ActualHeight - origin.Y);
    }

    private double RoomWidth() { var (left, _, right, _) = Room(); return right - left; }

    private double ListMaxHeight() => Math.Min(320, (Window.GetWindow(this)?.ActualHeight ?? 600) * .55);

    private FilterChip? Current(FilterField field) => Active?.FirstOrDefault(filter => filter.Id == field.Id);

    // Steps travel in the direction of the move: forward slides in from the end edge, back from the start edge.
    private void ShowStep(FilterField? field, int direction, bool animate, FocusTarget focus, string? key)
    {
        if (_stepHost is null || _stepLeaving is null || _titleSlot is null || _titleLeaving is null || _back is null) return;
        _field = field;
        bool motion = animate && Level != ArcMotionLevel.Off;
        bool full = motion && Full;
        RollTitle(field?.Label ?? Label, motion, full);
        Back(field is not null, motion, full);

        var rows = field is null
            ? Fields.Select(entry => new MenuList.Row(entry.Id, entry.Label, entry.Icon, FieldMeta(entry), false, true)).ToList()
            : field.Options.Select(option => { bool on = Current(field)?.Value == option.Text; return new MenuList.Row(option.Value, option.Text, option.Icon, ValueMeta(option, on), on, false); }).ToList();
        var list = new MenuList(rows, field is not null, ListMaxHeight(), Level) { RenderTransform = new TranslateTransform() };
        AutomationProperties.SetName(list, field?.Label ?? Label);
        list.Chosen += (row, keyboard) => { if (_field is null) ChooseField(row, keyboard); else ChooseValue(row, keyboard); };
        list.Back += keyboard => GoBack(keyboard);
        list.Left += () => _panel?.Focus();
        var old = _list;
        _list = list;
        if (old is not null)
        {
            _stepHost.Children.Remove(old);
            if (motion) { old.Width = old.ActualWidth; _stepLeaving.Children.Add(old); }
        }
        _stepHost.Children.Add(list);
        if (motion)
        {
            if (full)
            {
                TimePicker.Fly(list, 0, 1, 0, 0, 4, 0, .24, ArcEasing.Enter, null);
                Slide(list, direction * 18, 0, ArcEasing.Spring(SMOOTH, .8), .8);
                if (old is not null)
                {
                    TimePicker.Fly(old, old.Opacity, 0, 0, 0, 0, 2, .12, ArcEasing.Standard, () => _stepLeaving.Children.Remove(old));
                    Slide(old, 0, direction * -18, ArcEasing.Standard, .12);
                }
            }
            else
            {
                TimePicker.Fly(list, 0, 1, 0, 0, 0, 0, .12, ArcEasing.Standard, null);
                if (old is not null) _stepLeaving.Children.Remove(old);
            }
        }
        if (_phase == Phase.Open) Follow();
        if (_phase == Phase.Open) RequestFocus(focus, key);
    }

    private static void Slide(UIElement element, double from, double to, IEasingFunction ease, double seconds)
    {
        if (element.RenderTransform is not TranslateTransform move) return;
        move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(from, to, TimeSpan.FromSeconds(seconds)) { EasingFunction = ease });
    }

    // A new step changes the panel height; the surface follows on the smooth spring instead of snapping.
    private void Follow()
    {
        if (_panel is null) return;
        _panel.Measure(new Size(_panel.Width, double.PositiveInfinity));
        double height = _panel.DesiredSize.Height + 2;
        if (Math.Abs(height - _panelH) < .5) return;
        if (height > _panelH || !_driver.IsRunning) Size(Math.Max(height, _panelH));
        if (Full) _driver.Animate(_h, height, SMOOTH); else { _h.Snap(height); Apply(); }
        _panelH = height;
        Size(height);
    }

    private void RollTitle(string text, bool motion, bool full)
    {
        if (_titleSlot is null || _titleLeaving is null || _title?.Text == text) return;
        var old = _title;
        var fresh = new TextBlock { Text = text, FontSize = 14, FontWeight = FontWeights.Medium, RenderTransform = new TranslateTransform(), TextTrimming = TextTrimming.CharacterEllipsis };
        fresh.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.Foreground");
        _title = fresh;
        if (old is not null)
        {
            _titleSlot.Children.Remove(old);
            if (motion) _titleLeaving.Children.Add(old);
        }
        _titleSlot.Children.Add(fresh);
        if (!motion) return;
        TextIn(fresh, 14, full);
        if (old is not null) TextOut(old, 14, full, () => _titleLeaving.Children.Remove(old));
    }

    /// <summary>textMotion: rise from .3em with a soft blur (.24 enter); reduced fades (.12).</summary>
    internal static void TextIn(FrameworkElement element, double em, bool full, double delay = 0)
    {
        if (delay > 0 && full)
        {
            element.Opacity = 0;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(delay) };
            timer.Tick += (_, _) => { timer.Stop(); TextIn(element, em, full); };
            timer.Start();
            return;
        }
        if (full) TimePicker.Fly(element, 0, 1, .3 * em, 0, 4, 0, .24, ArcEasing.Enter, null);
        else TimePicker.Fly(element, 0, 1, 0, 0, 0, 0, .12, ArcEasing.Standard, null);
    }

    /// <summary>textMotion exit: leave upward to -.3em with a subtle blur (.12); reduced removes at once.</summary>
    internal static void TextOut(FrameworkElement element, double em, bool full, Action done)
    {
        if (full) TimePicker.Fly(element, element.Opacity, 0, 0, -.3 * em, 0, 2, .12, ArcEasing.Standard, done);
        else done();
    }

    // The back button grows from zero width with the snappy spring (opacity .24 enter), and collapses on exit.
    private void Back(bool show, bool motion, bool full)
    {
        if (_back is null) return;
        bool shown = _back.Visibility == Visibility.Visible && _back.Tag is not "leaving";
        if (show == shown) return;
        var scale = (ScaleTransform)_back.RenderTransform;
        var duration = TimeSpan.FromSeconds(.5);
        if (show)
        {
            _back.Tag = null;
            _back.Visibility = Visibility.Visible;
            if (!full) { Reset(); if (motion) TimePicker.Fly(_back, 0, 1, 0, 0, 0, 0, .12, ArcEasing.Standard, null); return; }
            Grow(0, 28, -6, 2, .6, 1, ArcEasing.Spring(SNAPPY, .5));
            TimePicker.Fly(_back, 0, 1, 0, 0, 0, 0, .24, ArcEasing.Enter, null);
        }
        else
        {
            if (!full) { _back.Visibility = Visibility.Collapsed; Reset(); return; }
            _back.Tag = "leaving";
            Grow(28, 0, 2, 0, 1, .6, ArcEasing.Spring(SNAPPY, .5));
            TimePicker.Fly(_back, _back.Opacity, 0, 0, 0, 0, 0, .12, ArcEasing.Standard, () => { if (_back.Tag is "leaving") { _back.Visibility = Visibility.Collapsed; Reset(); } });
        }

        void Reset()
        {
            _back.BeginAnimation(WidthProperty, null); _back.Width = 28;
            _back.BeginAnimation(MarginProperty, null); _back.Margin = new Thickness(-6, 0, 2, 0);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null); scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            _back.BeginAnimation(OpacityProperty, null); _back.Opacity = 1;
        }

        void Grow(double fromWidth, double toWidth, double fromRight, double toRight, double fromScale, double toScale, IEasingFunction ease)
        {
            _back.BeginAnimation(WidthProperty, new DoubleAnimation(fromWidth, toWidth, duration) { EasingFunction = ease });
            _back.BeginAnimation(MarginProperty, new ThicknessAnimation(new Thickness(fromWidth == 0 ? 0 : -6, 0, fromRight, 0), new Thickness(toWidth == 0 ? 0 : -6, 0, toRight, 0), duration) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(fromScale, toScale, duration) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(fromScale, toScale, duration) { EasingFunction = ease });
        }
    }

    // Field rows: current value (12 muted) and chevron-right 15. Value rows: hint and a check 15 shown when applied.
    private UIElement FieldMeta(FilterField field)
    {
        var meta = new StackPanel { Orientation = Orientation.Horizontal };
        if (Current(field)?.Value is { } value)
        {
            var text = new TextBlock { Text = value, Margin = new Thickness(0, 0, 6, 0), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            meta.Children.Add(text);
        }
        meta.Children.Add(Glyph(ChevronRightIcon, 15, 1.8, "ArcWpf.Brush.TextMuted"));
        return meta;
    }

    private static UIElement? ValueMeta(FilterOption option, bool on)
    {
        if (option.Hint is null && !on) return null;
        var meta = new StackPanel { Orientation = Orientation.Horizontal };
        if (option.Hint is not null) meta.Children.Add(new TextBlock { Text = option.Hint.ToString(), MinWidth = 14, TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
        var check = Glyph(CheckIcon, 15, 2, "ArcWpf.Brush.Foreground");
        check.Opacity = on ? 1 : 0;
        meta.Children.Add(check);
        return meta;
    }

    private void ChooseField(MenuList.Row row, bool keyboard)
    {
        var field = Fields.FirstOrDefault(entry => entry.Id == row.Key);
        if (field is null) return;
        ShowStep(field, 1, true, keyboard ? FocusTarget.Checked : FocusTarget.Panel, null);
    }

    private void ChooseValue(MenuList.Row row, bool keyboard)
    {
        if (_field is null) return;
        var field = _field;
        RaiseEvent(new FilterChipEventArgs(FilterSelectedEvent, this, new FilterChip { Id = field.Id, Label = field.Label, Value = row.Label }, field));
        Close(true);
    }

    private void GoBack(bool keyboard)
    {
        if (_field is null) return;
        var key = _field.Id;
        ShowStep(null, -1, true, keyboard ? FocusTarget.Key : FocusTarget.Panel, key);
    }

    // Focus moves into the panel once it is interactive: an item for keyboard users, the dialog itself for the pointer.
    private void RequestFocus(FocusTarget focus, string? key) => Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
    {
        if (_phase != Phase.Open || _list is null || _panel is null) return;
        if (focus == FocusTarget.Panel) { _panel.Focus(); return; }
        _list.FocusItem(focus, key);
    });

    private void PanelPreviewKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Left && _field is not null && !(_list?.IsKeyboardFocusWithin ?? false)) { e.Handled = true; GoBack(true); }
    }

    private void PanelKey(object sender, KeyEventArgs e)
    {
        if (e.Handled || _list is null) return;
        if (e.Key is Key.Down or Key.Up) { e.Handled = true; _list.FocusItem(e.Key == Key.Down ? FocusTarget.First : FocusTarget.Last, null); }
    }

    private void Attach()
    {
        Detach();
        _window = Window.GetWindow(this);
        if (_window is null) return;
        _window.PreviewMouseDown += WindowPointerDown;
        _window.Deactivated += WindowDeactivated;
        _window.LocationChanged += WindowMoved;
    }

    private void Detach()
    {
        if (_window is null) return;
        _window.PreviewMouseDown -= WindowPointerDown;
        _window.Deactivated -= WindowDeactivated;
        _window.LocationChanged -= WindowMoved;
        _window = null;
    }

    private void WindowDeactivated(object? sender, EventArgs e) => Close(false);
    private void WindowMoved(object? sender, EventArgs e) { if (_popup?.IsOpen == true) { _popup.HorizontalOffset += .001; _popup.HorizontalOffset -= .001; } }

    // A press outside closes the menu; a press on empty space hands focus back to the trigger.
    private void WindowPointerDown(object sender, MouseButtonEventArgs e)
    {
        if (_phase != Phase.Open || e.OriginalSource is not DependencyObject source || Owns(source)) return;
        Close(false);
        bool control = Ancestors(source).Any(node => node is UIElement { Focusable: true } and (ButtonBase or TextBoxBase or Selector or Control { IsTabStop: true }));
        if (!control) Dispatcher.BeginInvoke(DispatcherPriority.Input, () => { if (Keyboard.FocusedElement is null || Keyboard.FocusedElement is DependencyObject focused && Owns(focused)) _trigger?.Focus(); });
    }

    private static IEnumerable<DependencyObject> Ancestors(DependencyObject source)
    {
        for (DependencyObject? node = source; node is not null; node = LogicalTreeHelper.GetParent(node) ?? (node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : null))
            yield return node;
    }

    internal bool Owns(DependencyObject source) => Ancestors(source).Any(node => ReferenceEquals(node, this) || ReferenceEquals(node, _popup) || ReferenceEquals(node, _stage));

    protected override void OnIsKeyboardFocusWithinChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnIsKeyboardFocusWithinChanged(e);
        if ((bool)e.NewValue || _phase != Phase.Open) return;
        // Focus that leaves to somewhere else closes; focus that is only lost with the window stays.
        if (Keyboard.FocusedElement is DependencyObject focused && !Owns(focused)) Close(false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new FilterMenuPeer(this);

    private sealed class FilterMenuPeer(FilterMenu owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetNameCore() => owner.Label;
    }

    // A roving list with one highlight that glides under the pointer and jumps with the keyboard.
    internal sealed class MenuList : Grid
    {
        public sealed record Row(string Key, string Label, object? Icon, UIElement? Meta, bool Checked, bool Drill);

        public event Action<Row, bool>? Chosen;
        public event Action<bool>? Back;
        public event Action? Left;

        private const double SNAPPY = .26;
        private readonly List<(System.Windows.Controls.Button Button, Row Row)> _items = [];
        private readonly Border _highlight, _ring;
        private readonly TranslateTransform _highlightMove = new();
        private readonly ScrollViewer _scroller;
        private readonly SpringDriver _driver;
        private readonly ControlSpring _top, _height;
        private readonly ArcMotionLevel _level;
        private bool _pointer, _shown;
        private string _typed = string.Empty;
        private DateTime _typedAt;

        public MenuList(IReadOnlyList<Row> rows, bool radio, double maxHeight, ArcMotionLevel level)
        {
            _level = level;
            _driver = new SpringDriver(Apply);
            _top = _driver.Add(0, SNAPPY);
            _height = _driver.Add(36, SNAPPY);
            _ring = new Border { BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(20), Visibility = Visibility.Collapsed };
            _ring.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Focus");
            _highlight = new Border { VerticalAlignment = VerticalAlignment.Top, CornerRadius = new CornerRadius(20), Opacity = 0, IsHitTestVisible = false, RenderTransform = _highlightMove, Child = _ring };
            _highlight.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.FilterMenuHighlight");
            var stack = new StackPanel();
            foreach (var row in rows)
            {
                var button = new System.Windows.Controls.Button { Content = Content(row), Tag = row.Key };
                button.SetResourceReference(StyleProperty, new ComponentResourceKey(typeof(FilterMenu), "Item"));
                AutomationProperties.SetName(button, row.Label);
                if (radio) AutomationProperties.SetItemStatus(button, row.Checked ? "checked" : string.Empty);
                button.MouseMove += (_, _) => { _pointer = true; if (!button.IsKeyboardFocused) button.Focus(); };
                button.Click += (_, _) => Chosen?.Invoke(row, false);
                button.GotKeyboardFocus += (_, _) => Highlight(button);
                stack.Children.Add(button);
                _items.Add((button, row));
            }
            KeyboardNavigation.SetTabNavigation(stack, KeyboardNavigationMode.Once);
            KeyboardNavigation.SetDirectionalNavigation(stack, KeyboardNavigationMode.None);
            _scroller = new ScrollViewer { MaxHeight = maxHeight, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false, Content = new Grid { Children = { _highlight, stack } } };
            Children.Add(_scroller);
            PreviewKeyDown += Key;
            MouseLeave += (_, _) => { if (_pointer && IsKeyboardFocusWithin) Left?.Invoke(); };
            LostKeyboardFocus += (_, e) =>
            {
                // The highlight fades where it is; it keeps its place so the fade is visible.
                if (e.NewFocus is DependencyObject next && IsAncestorOf(next)) return;
                _shown = false;
                _ring.Visibility = Visibility.Collapsed;
                Fade(0);
            };
        }

        private static DockPanel Content(Row row)
        {
            var panel = new DockPanel { LastChildFill = true };
            if (row.Icon is not null)
            {
                var icon = new Border { MinWidth = 16, Margin = new Thickness(0, 0, 10, 0), Child = Glyph(row.Icon, 16, 1.75, "ArcWpf.Brush.TextSecondary") };
                DockPanel.SetDock(icon, Dock.Left);
                panel.Children.Add(icon);
            }
            if (row.Meta is not null)
            {
                var meta = new ContentPresenter { Content = row.Meta, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                TextElementHelper.Muted(meta);
                DockPanel.SetDock(meta, Dock.Right);
                panel.Children.Add(meta);
            }
            panel.Children.Add(new TextBlock { Text = row.Label, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
            return panel;
        }

        public void FocusItem(FocusTarget focus, string? key)
        {
            if (_items.Count == 0) return;
            var target = focus switch
            {
                FocusTarget.Last => _items[^1].Button,
                FocusTarget.Checked => _items.FirstOrDefault(item => item.Row.Checked).Button ?? _items[0].Button,
                FocusTarget.Key => _items.FirstOrDefault(item => item.Row.Key == key).Button ?? _items[0].Button,
                _ => _items[0].Button,
            };
            _pointer = false;
            Move(target);
        }

        private void Move(System.Windows.Controls.Button item)
        {
            item.Focus();
            item.BringIntoView();
        }

        private void Key(object sender, KeyEventArgs e)
        {
            int index = _items.FindIndex(item => item.Button.IsKeyboardFocused);
            void Go(int next) { e.Handled = true; _pointer = false; Move(_items[next].Button); }
            switch (e.Key)
            {
                case System.Windows.Input.Key.Down: Go(index < 0 ? 0 : (index + 1) % _items.Count); return;
                case System.Windows.Input.Key.Up: Go(index < 0 ? _items.Count - 1 : (index - 1 + _items.Count) % _items.Count); return;
                case System.Windows.Input.Key.Home: Go(0); return;
                case System.Windows.Input.Key.End: Go(_items.Count - 1); return;
                case System.Windows.Input.Key.Right when index >= 0 && _items[index].Row.Drill: e.Handled = true; Chosen?.Invoke(_items[index].Row, true); return;
                case System.Windows.Input.Key.Left when Back is not null: e.Handled = true; Back.Invoke(true); return;
                case System.Windows.Input.Key.Enter or System.Windows.Input.Key.Space when index >= 0: e.Handled = true; Chosen?.Invoke(_items[index].Row, true); return;
            }
            // Typeahead: letters typed within 700ms build a prefix; a single letter starts after the current item.
            var text = KeyText(e.Key);
            if (text is null || Keyboard.Modifiers is ModifierKeys.Control or ModifierKeys.Alt or ModifierKeys.Windows) return;
            var now = DateTime.UtcNow;
            _typed = (now - _typedAt).TotalMilliseconds < 700 ? _typed + text : text;
            _typedAt = now;
            int start = index + (_typed.Length > 1 ? 0 : 1);
            var order = _items.Skip(Math.Max(0, start)).Concat(_items.Take(Math.Max(0, start))).ToList();
            var match = order.FirstOrDefault(item => item.Row.Label.StartsWith(_typed, StringComparison.OrdinalIgnoreCase));
            if (match.Button is not null) Go(_items.IndexOf(match));
        }

        private static string? KeyText(Key key) => key switch
        {
            >= System.Windows.Input.Key.A and <= System.Windows.Input.Key.Z => ((char)('a' + (key - System.Windows.Input.Key.A))).ToString(),
            >= System.Windows.Input.Key.D0 and <= System.Windows.Input.Key.D9 => ((char)('0' + (key - System.Windows.Input.Key.D0))).ToString(),
            _ => null,
        };

        // Pointer focus glides the highlight on the snappy spring; keyboard focus jumps and draws the ring inside it.
        private void Highlight(System.Windows.Controls.Button item)
        {
            if (!item.IsLoaded) { item.Loaded += Once; return; }
            void Once(object? s, RoutedEventArgs e) { item.Loaded -= Once; Highlight(item); }
            var top = item.TranslatePoint(new Point(0, 0), (UIElement)_highlight.Parent).Y;
            bool glide = _pointer && _shown && _level == ArcMotionLevel.Full;
            _ring.Visibility = _pointer ? Visibility.Collapsed : Visibility.Visible;
            if (glide) { _driver.Animate(_top, top, SNAPPY); _driver.Animate(_height, item.ActualHeight, SNAPPY); }
            else { _driver.Stop(); _top.Snap(top); _height.Snap(item.ActualHeight); Apply(); }
            _shown = true;
            Fade(1);
        }

        private void Fade(double to)
        {
            if (_level == ArcMotionLevel.Full) _highlight.BeginAnimation(OpacityProperty, new DoubleAnimation(to, TimeSpan.FromSeconds(.08)));
            else { _highlight.BeginAnimation(OpacityProperty, null); _highlight.Opacity = to; }
        }

        private void Apply() { _highlightMove.Y = _top.Position; _highlight.Height = Math.Max(0, _height.Position); }
    }
}

internal static class TextElementHelper
{
    public static void Muted(FrameworkElement element)
    {
        element.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.TextMuted");
        TextElement.SetFontSize(element, 12);
        Typography.SetNumeralAlignment(element, FontNumeralAlignment.Tabular);
    }
}
