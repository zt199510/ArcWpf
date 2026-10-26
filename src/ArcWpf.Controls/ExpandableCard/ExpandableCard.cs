using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace ArcWpf.Controls;

[TemplatePart(Name = "PART_Surface", Type = typeof(Border))]
[TemplatePart(Name = "PART_Trigger", Type = typeof(ToggleButton))]
[TemplatePart(Name = "PART_Panel", Type = typeof(Border))]
[TemplatePart(Name = "PART_PanelInner", Type = typeof(Border))]
[TemplatePart(Name = "PART_Arrow", Type = typeof(Path))]
public class ExpandableCard : ContentControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(ExpandableCard), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(ExpandableCard), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty DefaultExpandedProperty = DependencyProperty.Register(nameof(DefaultExpanded), typeof(bool), typeof(ExpandableCard), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(ExpandableCard), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsExpandedChanged));
    public static readonly DependencyProperty CollapsedWidthProperty = DependencyProperty.Register(nameof(CollapsedWidth), typeof(double), typeof(ExpandableCard), new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure, OnSizingChanged), IsWidthValue);
    public static readonly DependencyProperty ExpandedWidthProperty = DependencyProperty.Register(nameof(ExpandedWidth), typeof(double), typeof(ExpandableCard), new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure, OnSizingChanged), IsWidthValue);
    public static readonly RoutedEvent ExpandedEvent = EventManager.RegisterRoutedEvent(nameof(Expanded), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ExpandableCard));
    public static readonly RoutedEvent CollapsedEvent = EventManager.RegisterRoutedEvent(nameof(Collapsed), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ExpandableCard));

    private Border? _surface, _panel, _panelInner;
    private ToggleButton? _trigger;
    private Path? _arrow;
    private TranslateTransform? _contentMove;
    private BlurEffect? _contentBlur;
    private readonly OwnedMotion _motion = new();
    private int _transition;
    private bool _initialized;

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public bool DefaultExpanded { get => (bool)GetValue(DefaultExpandedProperty); set => SetValue(DefaultExpandedProperty, value); }
    public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public double CollapsedWidth { get => (double)GetValue(CollapsedWidthProperty); set => SetValue(CollapsedWidthProperty, value); }
    public double ExpandedWidth { get => (double)GetValue(ExpandedWidthProperty); set => SetValue(ExpandedWidthProperty, value); }
    public event RoutedEventHandler Expanded { add => AddHandler(ExpandedEvent, value); remove => RemoveHandler(ExpandedEvent, value); }
    public event RoutedEventHandler Collapsed { add => AddHandler(CollapsedEvent, value); remove => RemoveHandler(CollapsedEvent, value); }
    internal bool IsMotionRunning => _motion.IsRunning;

    static ExpandableCard() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ExpandableCard), new FrameworkPropertyMetadata(typeof(ExpandableCard)));

    public ExpandableCard()
    {
        Loaded += (_, _) => { if (!_initialized) { SetCurrentValue(IsExpandedProperty, DefaultExpanded); _initialized = true; } UpdatePresentation(false); };
        Unloaded += (_, _) => StopMotion();
    }

    public override void OnApplyTemplate()
    {
        ReleaseTemplate();
        base.OnApplyTemplate();
        _surface = GetTemplateChild("PART_Surface") as Border;
        _trigger = GetTemplateChild("PART_Trigger") as ToggleButton;
        _panel = GetTemplateChild("PART_Panel") as Border;
        _panelInner = GetTemplateChild("PART_PanelInner") as Border;
        _arrow = GetTemplateChild("PART_Arrow") as Path;
        _contentMove = new TranslateTransform(0, IsExpanded ? 0 : -6);
        _contentBlur = new BlurEffect { Radius = IsExpanded ? 0 : 2 };
        if (_panelInner is not null) { _panelInner.RenderTransform = _contentMove; _panelInner.Effect = _contentBlur; }
        if (_trigger is not null) _trigger.Click += TriggerClick;
        UpdatePresentation(false);
    }

    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate) { ReleaseTemplate(); base.OnTemplateChanged(oldTemplate, newTemplate); }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ArcMotion.LevelProperty) { StopMotion(); UpdatePresentation(false); }
        if (e.Property == DefaultExpandedProperty && !_initialized)
            SetCurrentValue(IsExpandedProperty, (bool)e.NewValue);
        if (e.Property == TitleProperty || e.Property == DescriptionProperty || e.Property == ContentProperty || e.Property == ContentTemplateProperty)
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() => UpdatePresentation(false)));
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (!_motion.IsRunning) UpdatePresentation(false);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && IsExpanded)
        {
            e.Handled = true; SetCurrentValue(IsExpandedProperty, false); _trigger?.Focus();
        }
    }

    internal void UpdatePresentation(bool animate)
    {
        if (_surface is null || _panel is null) return;
        _transition++;
        double available = Math.Max(0, ActualWidth);
        if (available <= .01) available = Math.Max(0, DesiredSize.Width);
        double currentWidth = Math.Max(0, _surface.ActualWidth);
        double currentHeight = Math.Max(0, _panel.ActualHeight);
        double collapsed = Fit(CollapsedWidth, available);
        double expanded = Fit(double.IsNaN(ExpandedWidth) ? (double.IsNaN(CollapsedWidth) ? double.NaN : CollapsedWidth) : ExpandedWidth, available);
        if (double.IsNaN(expanded)) expanded = available;
        if (double.IsNaN(collapsed)) collapsed = available;
        double targetWidth = IsExpanded ? expanded : collapsed;
        if (targetWidth <= .01) targetWidth = available;
        _surface.Width = Math.Max(0, targetWidth);
        _panel.Visibility = Visibility.Visible;
        _panel.Height = double.NaN;
        _surface.Measure(new Size(Math.Max(0, targetWidth), double.PositiveInfinity));
        double openHeight = Math.Max(0, _panel.DesiredSize.Height);
        if (!IsExpanded) { _panel.Height = 0; _panel.Visibility = Visibility.Hidden; }
        else _panel.Height = double.NaN;
        bool motion = animate && IsEnabled && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        StopMotion(); UpdateArrow(motion);
        if (!motion) { SetContentState(IsExpanded ? 1 : 0, IsExpanded ? 0 : -6, IsExpanded ? 0 : 2); return; }
        double fromWidth = currentWidth;
        if (fromWidth <= .01) fromWidth = targetWidth;
        _surface.Width = fromWidth;
        _panel.Visibility = Visibility.Visible; _panel.IsHitTestVisible = IsExpanded;
        if (!IsExpanded) _panel.Height = currentHeight;
        int token = _transition;
        _motion.Double(_surface, FrameworkElement.WidthProperty, targetWidth, .4, true, spring: true);
        _motion.Double(_panel, FrameworkElement.HeightProperty, IsExpanded ? openHeight : 0, IsExpanded ? .4 : .16, true, spring: true, completed: () =>
        {
            if (token != _transition) return;
            if (IsExpanded) _panel!.SetCurrentValue(FrameworkElement.HeightProperty, double.NaN);
            else { _panel!.Visibility = Visibility.Hidden; _panel.IsHitTestVisible = false; }
        });
        _motion.Double(_panelInner!, UIElement.OpacityProperty, IsExpanded ? 1 : 0, IsExpanded ? .24 : .1, true, delay: IsExpanded ? .12 : 0, enter: true);
        _motion.Double(_contentMove!, TranslateTransform.YProperty, IsExpanded ? 0 : -6, .24, true, delay: IsExpanded ? .12 : 0, enter: true);
        _motion.Double(_contentBlur!, BlurEffect.RadiusProperty, IsExpanded ? 0 : 2, .24, true, delay: IsExpanded ? .12 : 0, enter: true);
    }

    private static double Fit(double value, double available) => double.IsNaN(value) ? double.NaN : Math.Min(Math.Max(0, value), available);
    private void UpdateArrow(bool animate)
    {
        if (_arrow is null) return;
        _arrow.RenderTransformOrigin = new Point(.5, .5);
        if (_arrow.RenderTransform is not RotateTransform rotate) { rotate = new RotateTransform(); _arrow.RenderTransform = rotate; }
        _motion.Double(rotate, RotateTransform.AngleProperty, IsExpanded ? 180 : 0, .26, animate, spring: animate);
    }
    private void SetContentState(double opacity, double y, double blur) { if (_panelInner is not null) _panelInner.Opacity = opacity; if (_contentMove is not null) _contentMove.Y = y; if (_contentBlur is not null) _contentBlur.Radius = blur; }
    private void StopMotion() => _motion.Stop();
    private void TriggerClick(object sender, RoutedEventArgs e) { SetCurrentValue(IsExpandedProperty, !IsExpanded); e.Handled = true; }
    private void ReleaseTemplate() { if (_trigger is not null) _trigger.Click -= TriggerClick; StopMotion(); _surface = _panel = _panelInner = null; _trigger = null; _arrow = null; _contentMove = null; _contentBlur = null; }
    private static bool IsWidthValue(object value) => value is double d && (double.IsNaN(d) || (double.IsFinite(d) && d >= 0));
    private static void OnSizingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ExpandableCard)d).UpdatePresentation(false);
    private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (ExpandableCard)d; bool expanded = (bool)e.NewValue; card.UpdatePresentation(true);
        card.RaiseEvent(new RoutedEventArgs(expanded ? ExpandedEvent : CollapsedEvent));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ExpandableCardPeer(this);
    private sealed class ExpandableCardPeer(ExpandableCard owner) : FrameworkElementAutomationPeer(owner), IExpandCollapseProvider
    {
        public ExpandCollapseState ExpandCollapseState => owner.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;
        public void Expand() { if (owner.IsEnabled) owner.SetCurrentValue(IsExpandedProperty, true); }
        public void Collapse() { if (owner.IsEnabled) owner.SetCurrentValue(IsExpandedProperty, false); }
        public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.ExpandCollapse ? this : base.GetPattern(patternInterface);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetClassNameCore() => nameof(ExpandableCard);
        protected override string GetNameCore() => AutomationProperties.GetName(owner) is { Length: > 0 } name ? name : owner.Title;
    }
}
