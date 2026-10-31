using System.Windows;
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

public enum TimePickerFormat { TwelveHour, TwentyFourHour }

[TemplatePart(Name = "PART_Trigger", Type = typeof(Border))]
[TemplatePart(Name = "PART_Value", Type = typeof(Grid))]
[TemplatePart(Name = "PART_Chevron", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
[TemplatePart(Name = "PART_Menu", Type = typeof(Border))]
[TemplatePart(Name = "PART_Scroller", Type = typeof(ScrollArea))]
[TemplatePart(Name = "PART_Options", Type = typeof(StackPanel))]
public class TimePicker : Control
{
    // time-picker.tsx: a trigger over a scrolling listbox of times; focus stays on the trigger and the list follows an active index.
    public static readonly DependencyProperty SelectedTimeProperty = DependencyProperty.Register(nameof(SelectedTime), typeof(TimeSpan?), typeof(TimePicker), new FrameworkPropertyMetadata(new TimeSpan(9, 0, 0), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedTimeChanged));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(TimePicker), new FrameworkPropertyMetadata("Time"));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(TimePicker), new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(TimePicker), new FrameworkPropertyMetadata("Select a time", OnDisplayChanged));
    public static readonly DependencyProperty MinuteStepProperty = DependencyProperty.Register(nameof(MinuteStep), typeof(int), typeof(TimePicker), new FrameworkPropertyMetadata(15, OnOptionsChanged), v => v is 1 or 5 or 10 or 15 or 30);
    public static readonly DependencyProperty FormatProperty = DependencyProperty.Register(nameof(Format), typeof(TimePickerFormat), typeof(TimePicker), new FrameworkPropertyMetadata(TimePickerFormat.TwelveHour, OnOptionsChanged));
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(TimePicker), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsOpenChanged));
    public static readonly RoutedEvent SelectedTimeChangedEvent = EventManager.RegisterRoutedEvent(nameof(SelectedTimeChanged), RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<TimeSpan?>), typeof(TimePicker));
    private static readonly DependencyProperty BorderRestProperty = DependencyProperty.Register("BorderRest", typeof(Brush), typeof(TimePicker), new PropertyMetadata(null, OnColorsChanged));
    private static readonly DependencyProperty BorderHotProperty = DependencyProperty.Register("BorderHot", typeof(Brush), typeof(TimePicker), new PropertyMetadata(null, OnColorsChanged));
    private static readonly DependencyProperty SurfaceRestProperty = DependencyProperty.Register("SurfaceRest", typeof(Brush), typeof(TimePicker), new PropertyMetadata(null, OnColorsChanged));
    private static readonly DependencyProperty SurfaceHotProperty = DependencyProperty.Register("SurfaceHot", typeof(Brush), typeof(TimePicker), new PropertyMetadata(null, OnColorsChanged));

    public TimeSpan? SelectedTime { get => (TimeSpan?)GetValue(SelectedTimeProperty); set => SetValue(SelectedTimeProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? Description { get => (string?)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public int MinuteStep { get => (int)GetValue(MinuteStepProperty); set => SetValue(MinuteStepProperty, value); }
    public TimePickerFormat Format { get => (TimePickerFormat)GetValue(FormatProperty); set => SetValue(FormatProperty, value); }
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public event RoutedPropertyChangedEventHandler<TimeSpan?> SelectedTimeChanged { add => AddHandler(SelectedTimeChangedEvent, value); remove => RemoveHandler(SelectedTimeChangedEvent, value); }

    private Border? _trigger, _menu;
    private Grid? _value;
    private FrameworkElement? _chevron;
    private Popup? _popup;
    private ScrollArea? _scroller;
    private StackPanel? _options;
    private readonly RotateTransform _chevronTurn = new();
    private readonly TranslateTransform _menuMove = new();
    private readonly ScaleTransform _menuScale = new();
    private readonly SolidColorBrush _borderBrush = new(), _surfaceBrush = new();
    private readonly OwnedMotion _motion = new(), _colors = new(), _turn = new();
    private readonly SpringDriver _menuSpring;
    private readonly ControlSpring _menuY, _menuS;
    private readonly List<(TimeSpan Time, Grid Row, Border Highlight, Ellipse Dot)> _rows = [];
    private TextBlock? _shown;
    private int _active = -1, _menuVersion;
    private bool _pressed, _centerOnOpen, _rowsDirty = true;

    static TimePicker()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(TimePicker), new FrameworkPropertyMetadata(typeof(TimePicker)));
        FocusableProperty.OverrideMetadata(typeof(TimePicker), new FrameworkPropertyMetadata(true));
        IsEnabledProperty.OverrideMetadata(typeof(TimePicker), new FrameworkPropertyMetadata(OnEnabledChanged));
    }

    public TimePicker()
    {
        _menuSpring = new SpringDriver(() => { _menuMove.Y = _menuY!.Position; _menuScale.ScaleX = _menuScale.ScaleY = _menuS!.Position; });
        _menuY = _menuSpring.Add(0, .26); _menuS = _menuSpring.Add(1, .26);
        SetResourceReference(BorderRestProperty, "ArcWpf.Brush.Border");
        SetResourceReference(BorderHotProperty, "ArcWpf.Brush.BorderStrong");
        SetResourceReference(SurfaceRestProperty, "ArcWpf.Brush.Surface");
        SetResourceReference(SurfaceHotProperty, "ArcWpf.Brush.SurfaceMuted");
        Unloaded += (_, _) => { SetCurrentValue(IsOpenProperty, false); FinishMenu(); _motion.Stop(); _colors.Stop(); _turn.Stop(); _chevronTurn.Angle = 0; ShowValue(0, false); UpdateColors(false); };
    }

    public override void OnApplyTemplate()
    {
        Release();
        base.OnApplyTemplate();
        _trigger = GetTemplateChild("PART_Trigger") as Border;
        _value = GetTemplateChild("PART_Value") as Grid;
        _chevron = GetTemplateChild("PART_Chevron") as FrameworkElement;
        _popup = GetTemplateChild("PART_Popup") as Popup;
        _menu = GetTemplateChild("PART_Menu") as Border;
        _scroller = GetTemplateChild("PART_Scroller") as ScrollArea;
        _options = GetTemplateChild("PART_Options") as StackPanel;
        if (_trigger is not null)
        {
            _trigger.Background = _surfaceBrush; _trigger.BorderBrush = _borderBrush;
            _trigger.MouseLeftButtonDown += TriggerDown; _trigger.MouseLeftButtonUp += TriggerUp; _trigger.LostMouseCapture += TriggerLost;
            _trigger.MouseEnter += TriggerHover; _trigger.MouseLeave += TriggerHover;
        }
        if (_chevron is not null) { _chevron.RenderTransformOrigin = new Point(.5, .5); _chevron.RenderTransform = _chevronTurn; }
        if (_menu is not null) { _menu.RenderTransformOrigin = new Point(.5, 0); _menu.RenderTransform = new TransformGroup { Children = { _menuScale, _menuMove } }; }
        if (_popup is not null) { _popup.Opened += PopupOpened; _popup.Closed += PopupClosed; }
        if (_scroller is not null) _scroller.SizeChanged += ScrollerSized;
        _rowsDirty = true; _shown = null;
        ShowValue(0, false);
        UpdateColors(false);
        _chevronTurn.Angle = IsOpen ? 180 : 0;
    }

    private void Release()
    {
        if (_trigger is not null) { _trigger.MouseLeftButtonDown -= TriggerDown; _trigger.MouseLeftButtonUp -= TriggerUp; _trigger.LostMouseCapture -= TriggerLost; _trigger.MouseEnter -= TriggerHover; _trigger.MouseLeave -= TriggerHover; }
        if (_popup is not null) { _popup.Opened -= PopupOpened; _popup.Closed -= PopupClosed; _popup.IsOpen = false; }
        if (_scroller is not null) _scroller.SizeChanged -= ScrollerSized;
        FinishMenu(); _motion.Stop(); _colors.Stop();
        _rows.Clear(); _options?.Children.Clear();
        _trigger = _menu = null; _value = null; _chevron = null; _popup = null; _scroller = null; _options = null;
    }

    internal static string Display(TimeSpan time, TimePickerFormat format)
    {
        int minutes = (int)Math.Floor(time.TotalMinutes) % 1440, hour = minutes / 60, minute = minutes % 60;
        return format == TimePickerFormat.TwentyFourHour ? $"{hour:00}:{minute:00}" : $"{(hour % 12 == 0 ? 12 : hour % 12)}:{minute:00} {(hour < 12 ? "AM" : "PM")}";
    }

    private static void OnSelectedTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (TimePicker)d;
        var previous = (TimeSpan?)e.OldValue; var next = (TimeSpan?)e.NewValue;
        // A later time rises from below, an earlier one drops from above.
        int direction = (next?.TotalMinutes ?? 0) >= (previous?.TotalMinutes ?? 0) ? 1 : -1;
        picker.ShowValue(direction, true);
        picker.UpdateRows();
        picker.RaiseEvent(new RoutedPropertyChangedEventArgs<TimeSpan?>(previous, next, SelectedTimeChangedEvent));
    }
    private static void OnDisplayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((TimePicker)d).ShowValue(0, false);
    private static void OnOptionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var picker = (TimePicker)d; picker._rowsDirty = true; picker.ShowValue(0, false); if (picker.IsOpen) picker.BuildRows(); }
    private static void OnColorsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((TimePicker)d).UpdateColors(false);
    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var picker = (TimePicker)d; if (!(bool)e.NewValue) picker.SetCurrentValue(IsOpenProperty, false); picker.UpdateColors(false); }
    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((TimePicker)d).ChangeOpen((bool)e.NewValue);

    private ArcMotionLevel Level => ArcMotion.GetLevel(this);
    private bool Animating => IsLoaded && IsVisible && Level != ArcMotionLevel.Off;

    // The shown time rolls like a clock face; reduced motion keeps a short crossfade.
    private void ShowValue(int direction, bool animate)
    {
        if (_value is null) return;
        string text = SelectedTime is { } time ? Display(time, Format) : Placeholder;
        bool placeholder = SelectedTime is null;
        if (_shown is not null && (string)_shown.Text == text && (bool)_shown.Tag == placeholder) return;
        var block = new TextBlock { Text = text, Tag = placeholder, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, RenderTransform = new TranslateTransform() };
        if (placeholder) block.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
        else { block.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.Foreground"); Typography.SetNumeralAlignment(block, FontNumeralAlignment.Tabular); }
        var old = _shown;
        _shown = block;
        _value.Children.Add(block);
        if (!animate || !Animating || old is null)
        {
            if (old is not null) _value.Children.Remove(old);
            foreach (var stale in _value.Children.OfType<TextBlock>().Where(b => b != block).ToList()) _value.Children.Remove(stale);
            return;
        }
        bool full = Level == ArcMotionLevel.Full;
        double em = FontSize;
        if (full)
        {
            Fly(block, 0, 1, direction * .35 * em, 0, 4, 0, .24, ArcEasing.Enter, null);
            Fly(old, 1, 0, 0, direction * -.3 * em, 0, 2, .16, ArcEasing.Standard, () => _value?.Children.Remove(old));
        }
        else
        {
            Fly(block, 0, 1, 0, 0, 0, 0, .12, ArcEasing.Standard, null);
            Fly(old, 1, 0, 0, 0, 0, 0, .12, ArcEasing.Standard, () => _value?.Children.Remove(old));
        }
    }

    internal static void Fly(UIElement element, double fromOpacity, double toOpacity, double fromY, double toY, double fromBlur, double toBlur, double seconds, IEasingFunction ease, Action? done)
    {
        var duration = TimeSpan.FromSeconds(seconds);
        var opacity = new DoubleAnimation(fromOpacity, toOpacity, duration) { EasingFunction = ease };
        if (done is not null) opacity.Completed += (_, _) => done();
        element.BeginAnimation(OpacityProperty, opacity);
        if (element.RenderTransform is TranslateTransform move && (fromY != 0 || toY != 0))
            move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(fromY, toY, duration) { EasingFunction = ease });
        if (fromBlur > 0 || toBlur > 0)
        {
            var blur = new BlurEffect { Radius = fromBlur };
            element.Effect = blur;
            var animation = new DoubleAnimation(fromBlur, toBlur, duration) { EasingFunction = ease };
            if (toBlur == 0) animation.Completed += (_, _) => { if (ReferenceEquals(element.Effect, blur)) element.Effect = null; };
            blur.BeginAnimation(BlurEffect.RadiusProperty, animation);
        }
    }

    // The trigger anchors the menu, so press feedback stays in color; it never scales.
    private void UpdateColors(bool animate)
    {
        bool hot = IsEnabled && (_pressed || IsOpen || _trigger?.IsMouseOver == true);
        bool motion = animate && Animating && Level == ArcMotionLevel.Full;
        _colors.Color(_borderBrush, Resolve(hot ? BorderHotProperty : BorderRestProperty), .16, motion);
        _colors.Color(_surfaceBrush, Resolve(hot ? SurfaceHotProperty : SurfaceRestProperty), .16, motion);
    }
    private Color Resolve(DependencyProperty property) => (GetValue(property) as SolidColorBrush)?.Color ?? Colors.Transparent;

    private void TriggerHover(object sender, MouseEventArgs e) => UpdateColors(true);
    private void TriggerDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsEnabled || _trigger is null) return;
        e.Handled = true;
        Focus();
        _pressed = _trigger.CaptureMouse();
        UpdateColors(true);
    }
    private void TriggerUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pressed || _trigger is null) return;
        e.Handled = true;
        bool inside = _trigger.IsMouseOver;
        _trigger.ReleaseMouseCapture();
        if (inside) { if (IsOpen) SetCurrentValue(IsOpenProperty, false); else OpenMenu(); }
    }
    private void TriggerLost(object sender, MouseEventArgs e) { _pressed = false; UpdateColors(true); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !IsEnabled) return;
        switch (e.Key)
        {
            case Key.Escape when IsOpen: e.Handled = true; SetCurrentValue(IsOpenProperty, false); break;
            case Key.Enter or Key.Space:
                e.Handled = true;
                if (IsOpen && _active >= 0 && _active < _rows.Count) Choose(_rows[_active].Time); else OpenMenu();
                break;
            case Key.Down or Key.Up:
                e.Handled = true;
                if (!IsOpen) { OpenMenu(); break; }
                if (_rows.Count == 0) break;
                SetActive((_active + (e.Key == Key.Down ? 1 : -1) + _rows.Count) % _rows.Count, true);
                break;
        }
    }

    private void OpenMenu()
    {
        if (!IsEnabled) return;
        BuildRows();
        int selected = SelectedTime is { } time ? _rows.FindIndex(row => row.Time == time) : -1;
        SetActive(selected >= 0 ? selected : 0, false);
        _centerOnOpen = true;
        SetCurrentValue(IsOpenProperty, true);
    }

    private void Choose(TimeSpan time)
    {
        SetCurrentValue(SelectedTimeProperty, time);
        SetCurrentValue(IsOpenProperty, false);
    }

    private void ChangeOpen(bool open)
    {
        UpdateColors(true);
        bool spin = Animating && Level == ArcMotionLevel.Full;
        _turn.Double(_chevronTurn, RotateTransform.AngleProperty, open ? 180 : 0, .58, spin, spring: true);
        if (_trigger is not null) _trigger.IsHitTestVisible = !open; // An outside click on the trigger closes the open menu rather than reopening it.
        if (_popup is null || _menu is null) return;
        int version = ++_menuVersion;
        if (open)
        {
            if (!IsLoaded || !IsEnabled) { SetCurrentValue(IsOpenProperty, false); return; }
            if (_active < 0) { BuildRows(); int selected = SelectedTime is { } time ? _rows.FindIndex(row => row.Time == time) : -1; SetActive(Math.Max(0, selected), false); _centerOnOpen = true; }
            _menu.Width = _trigger?.ActualWidth ?? ActualWidth;
            _motion.Stop(); _menuSpring.Stop();
            if (spin)
            {
                _menu.Opacity = 0; _menuY.Snap(-6); _menuS.Snap(.97);
                _menuMove.Y = -6; _menuScale.ScaleX = _menuScale.ScaleY = .97;
            }
            else { _menu.Opacity = Animating ? 0 : 1; _menuY.Snap(0); _menuS.Snap(1); _menuMove.Y = 0; _menuScale.ScaleX = _menuScale.ScaleY = 1; }
            _popup.IsOpen = true;
            return;
        }
        _active = -1;
        if (!_popup.IsOpen) return;
        if (!Animating) { FinishMenu(); return; }
        // Exit: a quick fade with a small lift; the popup closes once it lands.
        _menuSpring.Stop();
        bool full = Level == ArcMotionLevel.Full;
        if (full)
        {
            _motion.Double(_menuMove, TranslateTransform.YProperty, -4, .12, true);
            _motion.Double(_menuScale, ScaleTransform.ScaleXProperty, .98, .12, true);
            _motion.Double(_menuScale, ScaleTransform.ScaleYProperty, .98, .12, true);
        }
        _motion.Double(_menu, OpacityProperty, 0, .12, true, completed: () => { if (version == _menuVersion && !IsOpen && _popup is not null) _popup.IsOpen = false; });
    }

    private void FinishMenu()
    {
        _menuSpring.Stop();
        if (_popup is not null) _popup.IsOpen = false;
    }

    private void PopupOpened(object? sender, EventArgs e)
    {
        if (!IsOpen) { FinishMenu(); return; }
        // Runs before the first rendered frame so the menu is already centered on the selected time.
        _scroller?.UpdateLayout();
        KeepActiveInView();
        if (_centerOnOpen) Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(KeepActiveInView));
        if (_menu is null || !Animating) return;
        if (Level == ArcMotionLevel.Full)
        {
            _menuSpring.Animate(_menuY, 0, .26);
            _menuSpring.Animate(_menuS, 1, .26);
            _motion.Double(_menu, OpacityProperty, 1, .16, true, enter: true);
        }
        else _motion.Double(_menu, OpacityProperty, 1, .12, true);
    }

    private void PopupClosed(object? sender, EventArgs e)
    {
        _motion.Stop(); _menuSpring.Stop();
        if (IsOpen) SetCurrentValue(IsOpenProperty, false);
    }

    private void ScrollerSized(object sender, SizeChangedEventArgs e)
    {
        // The menu clips its scrolling rows to its rounded inner edge (radius-panel minus the 1 DIP border).
        if (_scroller is not null) _scroller.Clip = new RectangleGeometry(new Rect(e.NewSize), 25, 25);
    }

    private void BuildRows()
    {
        if (_options is null || !_rowsDirty && _rows.Count > 0) { UpdateRows(); return; }
        _rowsDirty = false;
        _options.Children.Clear(); _rows.Clear();
        int step = MinuteStep;
        for (int index = 0; index < (int)Math.Ceiling(1440d / step); index++)
        {
            var time = TimeSpan.FromMinutes(index * step);
            var highlight = new Border { CornerRadius = new CornerRadius(21), Opacity = 0 };
            highlight.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
            var text = new TextBlock { Text = Display(time, Format), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 12, 0) };
            text.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.Foreground");
            Typography.SetNumeralAlignment(text, FontNumeralAlignment.Tabular);
            var dot = new Ellipse { Width = 6, Height = 6, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0), Visibility = Visibility.Collapsed };
            dot.SetResourceReference(Shape.FillProperty, "ArcWpf.Brush.Foreground");
            var row = new Grid { MinHeight = 36, Background = Brushes.Transparent, Cursor = Cursors.Hand, Children = { highlight, text, dot } };
            int position = index;
            row.MouseMove += (_, _) => { if (_active != position) SetActive(position, false); };
            row.MouseLeftButtonUp += (_, e) => { e.Handled = true; Choose(time); };
            _options.Children.Add(row);
            _rows.Add((time, row, highlight, dot));
        }
        UpdateRows();
    }

    private void UpdateRows()
    {
        foreach (var row in _rows) row.Dot.Visibility = row.Time == SelectedTime ? Visibility.Visible : Visibility.Collapsed;
    }

    // Arrow keys move the highlight often, so it changes almost instantly (80 ms).
    private void SetActive(int index, bool keyboard)
    {
        bool motion = Animating && Level == ArcMotionLevel.Full;
        if (_active >= 0 && _active < _rows.Count && _active != index) _motion.Double(_rows[_active].Highlight, OpacityProperty, 0, .08, motion);
        _active = index;
        if (index >= 0 && index < _rows.Count) _motion.Double(_rows[index].Highlight, OpacityProperty, 1, .08, motion);
        if (keyboard) KeepActiveInView();
    }

    // Scroll only the menu, never the page: open centered, then keep the highlight in view.
    private void KeepActiveInView()
    {
        if (_scroller?.Viewport is not { } viewport || _options is null || _active < 0 || _active >= _rows.Count || viewport.ViewportHeight <= 0) return;
        var row = _rows[_active].Row;
        double top = row.TranslatePoint(new Point(), _options).Y + _options.Margin.Top, bottom = top + row.ActualHeight;
        if (_centerOnOpen) { _centerOnOpen = false; viewport.ScrollToVerticalOffset(top - (viewport.ViewportHeight - row.ActualHeight) / 2); }
        else if (top < viewport.VerticalOffset) viewport.ScrollToVerticalOffset(top);
        else if (bottom > viewport.VerticalOffset + viewport.ViewportHeight) viewport.ScrollToVerticalOffset(bottom - viewport.ViewportHeight);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TimePickerPeer(this);
    private sealed class TimePickerPeer(TimePicker owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(TimePicker);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ComboBox;
        protected override string GetNameCore() => $"{owner.Label} {(owner.SelectedTime is { } time ? Display(time, owner.Format) : owner.Placeholder)}";
    }
}
