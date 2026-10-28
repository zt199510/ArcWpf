using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace ArcWpf.Controls;

[TemplatePart(Name = "PART_Clear", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "PART_ClearVisual", Type = typeof(FrameworkElement))]
public class Search : Input
{
    private static readonly DependencyPropertyKey HasQueryPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasQuery), typeof(bool), typeof(Search), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty HasQueryProperty = HasQueryPropertyKey.DependencyProperty;
    public bool HasQuery => (bool)GetValue(HasQueryProperty);
    public static readonly RoutedUICommand ClearCommand = new("Clear search", nameof(ClearCommand), typeof(Search));
    private System.Windows.Controls.Button? _clear;
    private FrameworkElement? _visual;
    private ScaleTransform? _scale;
    private BlurEffect? _blur;
    private readonly OwnedMotion _motion = new();
    // Resource probes retain DynamicResource ownership; only these private brush copies animate.
    private static readonly DependencyProperty MutedBrushProperty = DependencyProperty.Register("MutedBrush", typeof(Brush), typeof(Search), new PropertyMetadata(null, ColorsChanged));
    private static readonly DependencyProperty HoverBrushProperty = DependencyProperty.Register("HoverBrush", typeof(Brush), typeof(Search), new PropertyMetadata(null, ColorsChanged));
    private readonly OwnedMotion _colors = new();
    private Border? _surface, _clearSurface;
    private System.Windows.Shapes.Path? _icon, _clearIcon;
    private SolidColorBrush? _borderColor, _iconColor, _clearBackground, _clearForeground;
    private static void ColorsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Search)d).UpdateColors(false);
    private bool _composing, _pressed;
    private DispatcherTimer? _clearHideTimer;
    private DispatcherTimer? _colorSettleTimer;
    internal bool IsClearAnimating => _motion.IsRunning;

    static Search()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Search), new FrameworkPropertyMetadata(typeof(Search)));
        CommandManager.RegisterClassCommandBinding(typeof(Search), new CommandBinding(ClearCommand,
            (sender, e) => { ((Search)sender).ClearQuery(); e.Handled = true; },
            (sender, e) => { var search = (Search)sender; e.CanExecute = search.HasQuery && search.IsEnabled && !search.IsReadOnly && !search._composing; e.Handled = true; }));
    }

    public Search()
    {
        AddHandler(TextCompositionManager.PreviewTextInputStartEvent, new TextCompositionEventHandler(CompositionStarted), true);
        AddHandler(TextCompositionManager.PreviewTextInputUpdateEvent, new TextCompositionEventHandler(CompositionStarted), true);
        AddHandler(TextCompositionManager.PreviewTextInputEvent, new TextCompositionEventHandler(CompositionEnded), true);
        SetResourceReference(MutedBrushProperty, "ArcWpf.Brush.TextMuted");
        SetResourceReference(HoverBrushProperty, "ArcWpf.Brush.SurfaceMuted");
        Loaded += (_, _) => { UpdateClear(false); UpdateColors(false); };
        Unloaded += (_, _) => { _composing = false; _pressed = false; _clearHideTimer?.Stop(); _colorSettleTimer?.Stop(); UpdateClear(false); _motion.Stop(); UpdateColors(false); _colors.Stop(); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) { UpdateClear(false); UpdateColors(false); } };
    }

    private void CompositionStarted(object sender, TextCompositionEventArgs e) { _composing = true; CommandManager.InvalidateRequerySuggested(); }
    private void CompositionEnded(object sender, TextCompositionEventArgs e) { _composing = false; CommandManager.InvalidateRequerySuggested(); }
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnLostKeyboardFocus(e); _composing = false; }
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && !_composing && HasQuery && IsEnabled && !IsReadOnly)
        { ClearQuery(); e.Handled = true; }
    }
    private void ClearQuery()
    {
        if (!HasQuery || !IsEnabled || IsReadOnly || _composing) return;
        BeginChange();
        try { SelectAll(); SelectedText = string.Empty; }
        finally { EndChange(); }
        Focus();
    }
    protected override void OnTextChanged(TextChangedEventArgs e)
    {
        base.OnTextChanged(e);
        SetValue(HasQueryPropertyKey, !string.IsNullOrEmpty(Text));
        UpdateClear(true);
        UpdateColors(true);
        CommandManager.InvalidateRequerySuggested();
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ArcMotion.LevelProperty) { UpdateClear(false); UpdateColors(false); }
        else if (e.Property == IsReadOnlyProperty || e.Property == IsEnabledProperty) UpdateClear(true);
        if (e.Property == BorderBrushProperty || e.Property == ForegroundProperty || e.Property == IsKeyboardFocusWithinProperty || e.Property == IsEnabledProperty)
            UpdateColors(true);
    }
    public override void OnApplyTemplate()
    {
        ReleaseParts();
        base.OnApplyTemplate();
        _clear = GetTemplateChild("PART_Clear") as System.Windows.Controls.Button;
        _visual = GetTemplateChild("PART_ClearVisual") as FrameworkElement;
        if (_visual is not null)
        {
            _scale = new ScaleTransform(1, 1); _blur = new BlurEffect();
            _visual.RenderTransformOrigin = new Point(.5, .5);
            _visual.RenderTransform = _scale; _visual.Effect = _blur;
        }
        if (_clear is not null)
        {
            _clear.PreviewMouseLeftButtonDown += Press;
            _clear.PreviewMouseLeftButtonUp += Release;
            _clear.LostMouseCapture += Release;
            _clear.PreviewKeyDown += KeyPress;
            _clear.PreviewKeyUp += KeyRelease;
            _clear.LostKeyboardFocus += Release;
        }
        _surface = GetTemplateChild("Surface") as Border;
        _icon = GetTemplateChild("SearchIcon") as System.Windows.Shapes.Path;
        _clear?.ApplyTemplate();
        _clearSurface = _clear?.Template.FindName("ClearSurface", _clear) as Border;
        _clearIcon = _clear?.Template.FindName("ClearIcon", _clear) as System.Windows.Shapes.Path;
        if (_clear is not null) { _clear.MouseEnter += ClearHover; _clear.MouseLeave += ClearHover; }
        UpdateColors(false);
        UpdateClear(false);
    }
    private void ClearHover(object sender, MouseEventArgs e) => UpdateColors(true);
    private void UpdateColors(bool animate)
    {
        if (_surface is null || _icon is null) return;
        bool moving = animate && IsLoaded && IsVisible && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        Paint(_surface, Border.BorderBrushProperty, BorderBrush, ref _borderColor, moving);
        Paint(_icon, System.Windows.Shapes.Shape.StrokeProperty,
            HasQuery || IsKeyboardFocusWithin ? Foreground : (Brush)GetValue(MutedBrushProperty), ref _iconColor, moving);
        bool hover = _clear?.IsMouseOver == true && IsEnabled;
        Paint(_clearSurface, Border.BackgroundProperty, hover ? (Brush)GetValue(HoverBrushProperty) : Brushes.Transparent, ref _clearBackground, moving);
        Paint(_clearIcon, System.Windows.Shapes.Shape.StrokeProperty, hover ? Foreground : (Brush)GetValue(MutedBrushProperty), ref _clearForeground, moving);
    }
    private void Paint(DependencyObject? target, DependencyProperty property, Brush? source, ref SolidColorBrush? owned, bool animate)
    {
        if (target is null) return;
        if (source is not SolidColorBrush solid) { target.SetValue(property, source); owned = null; return; }
        if (owned is null) { owned = new SolidColorBrush(solid.Color); target.SetValue(property, owned); }
        owned.Opacity = solid.Opacity;
        // Keep the color transition within the fast interaction token. A short
        // owned clock also guarantees that successive theme/resource changes
        // settle before the next layout pass observes the brush.
        _colors.Color(owned, solid.Color, .08, animate);
        _colorSettleTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        _colorSettleTimer.Stop();
        _colorSettleTimer.Tick -= SettleColors;
        _colorSettleTimer.Tick += SettleColors;
        if (animate) { _colorSettleTimer.Tag = owned; _colorSettleTimer.Start(); }
    }
    internal bool IsColorAnimating => _colors.IsRunning;
    private void SettleColors(object? sender, EventArgs e)
    {
        _colorSettleTimer?.Stop();
        _colors.Stop();
        UpdateColors(false);
    }
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    { ReleaseParts(); base.OnTemplateChanged(oldTemplate, newTemplate); }
    private void ReleaseParts()
    {
        _motion?.Stop(); _colors?.Stop();
        if (_clear is not null) { _clear.MouseEnter -= ClearHover; _clear.MouseLeave -= ClearHover; }
        _surface = _clearSurface = null; _icon = _clearIcon = null;
        _borderColor = _iconColor = _clearBackground = _clearForeground = null;
        if (_clear is not null)
        {
            _clear.PreviewMouseLeftButtonDown -= Press; _clear.PreviewMouseLeftButtonUp -= Release;
            _clear.LostMouseCapture -= Release; _clear.PreviewKeyDown -= KeyPress; _clear.PreviewKeyUp -= KeyRelease;
            _clear.LostKeyboardFocus -= Release;
        }
        _clear = null; _visual = null; _scale = null; _blur = null; _pressed = false;
    }
    private void Press(object sender, MouseButtonEventArgs e) { _pressed = true; UpdateClear(true); }
    private void Release(object sender, RoutedEventArgs e) { _pressed = false; UpdateClear(true); }
    private void KeyPress(object sender, KeyEventArgs e) { if (e.Key == Key.Space) { _pressed = true; UpdateClear(true); } }
    private void KeyRelease(object sender, KeyEventArgs e) { if (e.Key == Key.Space) { _pressed = false; UpdateClear(true); } }
    private void UpdateClear(bool animate)
    {
        if (_clear is null || _visual is null || _scale is null || _blur is null) return;
        bool shown = HasQuery && !IsReadOnly;
        bool enabled = shown && IsEnabled;
        var level = ArcMotion.GetLevel(this);
        bool full = level == ArcMotionLevel.Full;
        bool moving = animate && IsLoaded && IsVisible && level != ArcMotionLevel.Off;
        _clear.IsHitTestVisible = enabled; _clear.Focusable = enabled;
        if (!enabled && _clear.IsKeyboardFocusWithin) Focus();
        if (shown) _clear.Visibility = Visibility.Visible;
        bool exit = !shown;
        _clearHideTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        _clearHideTimer.Stop();
        if (exit) { _clearHideTimer.Tick -= HideClear; _clearHideTimer.Tick += HideClear; _clearHideTimer.Start(); }
        _motion.Double(_visual, OpacityProperty, shown ? 1 : 0, full ? (exit ? .12 : .16) : .12,
            moving && (shown || full), completed: () => { if (!HasQuery || IsReadOnly) _clear.Visibility = Visibility.Hidden; });
        double scale = full ? (shown ? (_pressed ? .96 : 1) : .8) : 1;
        _motion.Double(_scale, ScaleTransform.ScaleXProperty, scale, exit || _pressed ? .12 : .26, moving && full, spring: shown && !_pressed);
        _motion.Double(_scale, ScaleTransform.ScaleYProperty, scale, exit || _pressed ? .12 : .26, moving && full, spring: shown && !_pressed);
        _motion.Double(_blur, BlurEffect.RadiusProperty, full && exit ? 2 : 0, exit ? .12 : .16, moving && full);
    }
    private void HideClear(object? sender, EventArgs e)
    {
        _clearHideTimer?.Stop();
        if (_clear is not null && !HasQuery) _clear.Visibility = Visibility.Hidden;
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new SearchPeer(this);
    private sealed class SearchPeer(Search owner) : TextBoxAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(Search);
        protected override string GetNameCore() => string.IsNullOrEmpty(base.GetNameCore()) ? owner.Label : base.GetNameCore();
        protected override string GetHelpTextCore() => string.IsNullOrEmpty(AutomationProperties.GetHelpText(owner)) ? string.Join(" ", new[] { owner.Description, owner.EffectiveErrorMessage }.Where(s => !string.IsNullOrWhiteSpace(s))) : AutomationProperties.GetHelpText(owner);
        protected override List<AutomationPeer>? GetChildrenCore()
        {
            var children = base.GetChildrenCore() ?? new List<AutomationPeer>();
            if (owner._clear is { Visibility: Visibility.Visible, Focusable: true } button && UIElementAutomationPeer.CreatePeerForElement(button) is { } peer && !children.Contains(peer)) children.Add(peer);
            return children;
        }
    }
}
