using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace ArcWpf.Controls;

[TemplatePart(Name = "PART_HeaderButton", Type = typeof(ToggleButton))]
[TemplatePart(Name = "PART_Panel", Type = typeof(Border))]
[TemplatePart(Name = "PART_PanelInner", Type = typeof(Border))]
[TemplatePart(Name = "PART_Chevron", Type = typeof(Path))]
public class AccordionItem : HeaderedContentControl
{
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
        nameof(IsExpanded), typeof(bool), typeof(AccordionItem), new FrameworkPropertyMetadata(false,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsExpandedChanged));

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(AccordionSize), typeof(AccordionItem), new FrameworkPropertyMetadata(AccordionSize.Medium), IsValidSize);

    public static readonly RoutedEvent ExpandedEvent = EventManager.RegisterRoutedEvent(nameof(Expanded), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(AccordionItem));
    public static readonly RoutedEvent CollapsedEvent = EventManager.RegisterRoutedEvent(nameof(Collapsed), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(AccordionItem));

    private Border? _panel, _panelInner;
    private ToggleButton? _headerButton;
    private Path? _chevron;
    private TranslateTransform? _contentMove;
    private BlurEffect? _contentBlur;
    private readonly OwnedMotion _motion = new();
    private int _transition;

    internal Accordion? Owner { get; set; }
    internal bool IsMotionRunning => _motion.IsRunning;

    public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public AccordionSize Size { get => (AccordionSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public event RoutedEventHandler Expanded { add => AddHandler(ExpandedEvent, value); remove => RemoveHandler(ExpandedEvent, value); }
    public event RoutedEventHandler Collapsed { add => AddHandler(CollapsedEvent, value); remove => RemoveHandler(CollapsedEvent, value); }

    static AccordionItem() => DefaultStyleKeyProperty.OverrideMetadata(typeof(AccordionItem), new FrameworkPropertyMetadata(typeof(AccordionItem)));

    public AccordionItem()
    {
        SetCurrentValue(FocusVisualStyleProperty, null);
        Loaded += (_, _) => UpdatePresentation(false);
        Unloaded += (_, _) => StopMotion();
    }

    public override void OnApplyTemplate()
    {
        ReleaseTemplate();
        base.OnApplyTemplate();
        _headerButton = GetTemplateChild("PART_HeaderButton") as ToggleButton;
        _panel = GetTemplateChild("PART_Panel") as Border;
        _panelInner = GetTemplateChild("PART_PanelInner") as Border;
        _chevron = GetTemplateChild("PART_Chevron") as Path;
        _contentMove = new TranslateTransform(0, IsExpanded ? 0 : -6);
        _contentBlur = new BlurEffect { Radius = IsExpanded ? 0 : 2 };
        if (_panelInner is not null)
        {
            _panelInner.RenderTransform = _contentMove;
            _panelInner.Effect = _contentBlur;
            _panelInner.Opacity = IsExpanded ? 1 : 0;
        }
        UpdateChevron(false);
        UpdatePresentation(false);
    }

    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    {
        ReleaseTemplate();
        base.OnTemplateChanged(oldTemplate, newTemplate);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ArcMotion.LevelProperty)
        {
            StopMotion();
            UpdatePresentation(false);
        }
        if (e.Property == SizeProperty || e.Property == ContentProperty || e.Property == ContentTemplateProperty || e.Property == HeaderProperty || e.Property == HeaderTemplateProperty)
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() => UpdatePresentation(false)));
    }

    internal void UpdatePresentation(bool animate)
    {
        if (_panel is null) return;
        UpdateChevron(animate);
        if (IsExpanded) Open(animate); else Close(animate);
    }

    private void Open(bool animate)
    {
        _transition++;
        var panel = _panel;
        if (panel is null) return;
        panel.Visibility = Visibility.Visible;
        panel.IsHitTestVisible = true;
        panel.Height = double.NaN;
        panel.Measure(new Size(Math.Max(0, ActualWidth), double.PositiveInfinity));
        double target = Math.Max(0, panel.DesiredSize.Height);
        if (target <= .01)
        {
            panel.Height = double.NaN;
            SetContentState(1, 0, 0);
            return;
        }
        panel.Height = animate ? 0 : double.NaN;
        bool motion = animate && IsEnabled && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        StopMotion();
        if (!motion)
        {
            panel.Height = double.NaN;
            SetContentState(1, 0, 0);
            return;
        }
        int token = _transition;
        _motion.Double(panel, FrameworkElement.HeightProperty, target, .4, true, spring: true, completed: () =>
        {
            if (token != _transition || !IsExpanded) return;
            panel.SetCurrentValue(FrameworkElement.HeightProperty, double.NaN);
        });
        _motion.Double(_panelInner!, UIElement.OpacityProperty, 1, .24, true, enter: true);
        _motion.Double(_contentMove!, TranslateTransform.YProperty, 0, .4, ArcMotion.GetLevel(this) == ArcMotionLevel.Full, spring: ArcMotion.GetLevel(this) == ArcMotionLevel.Full, enter: true);
        _motion.Double(_contentBlur!, BlurEffect.RadiusProperty, 0, .24, true, enter: true);
    }

    private void Close(bool animate)
    {
        _transition++;
        if (_panel is null) return;
        double from = Math.Max(0, _panel.ActualHeight);
        if (from <= .01) from = Math.Max(0, _panel.DesiredSize.Height);
        _panel.Visibility = Visibility.Visible;
        _panel.IsHitTestVisible = false;
        _panel.Height = from;
        bool motion = animate && IsEnabled && ArcMotion.GetLevel(this) == ArcMotionLevel.Full && from > .01;
        StopMotion();
        if (!motion)
        {
            _panel.Height = 0;
            _panel.Visibility = Visibility.Hidden;
            SetContentState(0, -6, 2);
            return;
        }
        int token = _transition;
        _motion.Double(_panel, FrameworkElement.HeightProperty, 0, .16, true, spring: true, completed: () =>
        {
            if (token != _transition || IsExpanded) return;
            _panel!.Visibility = Visibility.Hidden;
            _panel.IsHitTestVisible = false;
        });
        _motion.Double(_panelInner!, UIElement.OpacityProperty, 0, .16, true);
        _motion.Double(_contentMove!, TranslateTransform.YProperty, -6, .16, true);
        _motion.Double(_contentBlur!, BlurEffect.RadiusProperty, 2, .16, true);
    }

    private void SetContentState(double opacity, double y, double blur)
    {
        if (_panelInner is not null) _panelInner.Opacity = opacity;
        if (_contentMove is not null) _contentMove.Y = y;
        if (_contentBlur is not null) _contentBlur.Radius = blur;
    }

    private void UpdateChevron(bool animate)
    {
        if (_chevron is null) return;
        _chevron.RenderTransformOrigin = new Point(.5, .5);
        if (_chevron.RenderTransform is not RotateTransform rotate)
        {
            rotate = new RotateTransform();
            _chevron.RenderTransform = rotate;
        }
        bool motion = animate && IsEnabled && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        _motion.Double(rotate, RotateTransform.AngleProperty, IsExpanded ? 180 : 0, .26, motion, spring: motion);
    }

    private void StopMotion()
    {
        _motion.Stop();
    }

    private void ReleaseTemplate()
    {
        StopMotion();
        _panel = _panelInner = null;
        _headerButton = null;
        _chevron = null;
        _contentMove = null;
        _contentBlur = null;
    }

    private static bool IsValidSize(object value) => value is AccordionSize size && Enum.IsDefined(size);
    private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var item = (AccordionItem)d;
        bool expanded = (bool)e.NewValue;
        item.Owner?.ItemExpandedChanged(item, expanded);
        item.UpdatePresentation(item.IsEnabled);
        item.RaiseEvent(new RoutedEventArgs(expanded ? ExpandedEvent : CollapsedEvent));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AccordionItemPeer(this);

    private sealed class AccordionItemPeer(AccordionItem owner) : FrameworkElementAutomationPeer(owner), IExpandCollapseProvider
    {
        public ExpandCollapseState ExpandCollapseState => owner.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;
        public void Expand() { if (owner.IsEnabled) owner.SetCurrentValue(IsExpandedProperty, true); }
        public void Collapse() { if (owner.IsEnabled) owner.SetCurrentValue(IsExpandedProperty, false); }
        public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.ExpandCollapse ? this : base.GetPattern(patternInterface);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetClassNameCore() => nameof(AccordionItem);
        protected override string GetNameCore() => AutomationProperties.GetName(owner) is { Length: > 0 } name ? name : owner.Header?.ToString() ?? nameof(AccordionItem);
    }
}
