using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace ArcWpf.Controls;

[TemplatePart(Name = "PART_Content", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Inner", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Toggle", Type = typeof(System.Windows.Controls.Button))]
public class ExpandableText : ContentControl
{
    public static readonly DependencyProperty LinesProperty = DependencyProperty.Register(nameof(Lines), typeof(int), typeof(ExpandableText), new FrameworkPropertyMetadata(3, FrameworkPropertyMetadataOptions.AffectsMeasure, OnLayoutChanged));
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(ExpandableText), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnExpandedChanged));
    public static readonly DependencyProperty MoreLabelProperty = DependencyProperty.Register(nameof(MoreLabel), typeof(string), typeof(ExpandableText), new FrameworkPropertyMetadata("Read more"));
    public static readonly DependencyProperty LessLabelProperty = DependencyProperty.Register(nameof(LessLabel), typeof(string), typeof(ExpandableText), new FrameworkPropertyMetadata("Show less"));
    public int Lines { get => (int)GetValue(LinesProperty); set => SetValue(LinesProperty, Math.Max(1, value)); }
    public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public string MoreLabel { get => (string)GetValue(MoreLabelProperty); set => SetValue(MoreLabelProperty, value); }
    public string LessLabel { get => (string)GetValue(LessLabelProperty); set => SetValue(LessLabelProperty, value); }
    public event RoutedEventHandler? ExpandedChanged;
    private FrameworkElement? _content, _inner; private System.Windows.Controls.Button? _toggle;
    static ExpandableText() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ExpandableText), new FrameworkPropertyMetadata(typeof(ExpandableText)));
    public ExpandableText() { Loaded += (_, _) => UpdateLayoutState(false); SizeChanged += (_, _) => UpdateLayoutState(false); }
    public override void OnApplyTemplate() { base.OnApplyTemplate(); _content = GetTemplateChild("PART_Content") as FrameworkElement; _inner = GetTemplateChild("PART_Inner") as FrameworkElement; _toggle = GetTemplateChild("PART_Toggle") as System.Windows.Controls.Button; if (_toggle is not null) _toggle.Click += ToggleClick; Dispatcher.BeginInvoke(new Action(() => UpdateLayoutState(false)), System.Windows.Threading.DispatcherPriority.Loaded); }
    private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ExpandableText)d).UpdateLayoutState(false);
    private static void OnExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var c = (ExpandableText)d; c.UpdateLayoutState(true); c.ExpandedChanged?.Invoke(c, new RoutedEventArgs()); }
    private void ToggleClick(object sender, RoutedEventArgs e) { SetCurrentValue(IsExpandedProperty, !IsExpanded); e.Handled = true; }
    private void UpdateLayoutState(bool animate)
    {
        if (_content is null || _inner is null || _toggle is null || ActualWidth <= 0) return;
        var full = _inner.DesiredSize.Height; var line = FontSize * 1.4; var clamp = Math.Max(line, Lines * line); bool overflow = full > clamp + 1; _toggle.Visibility = overflow ? Visibility.Visible : Visibility.Collapsed; double target = IsExpanded ? full : clamp; double current = double.IsNaN(_content.MaxHeight) || double.IsInfinity(_content.MaxHeight) ? (IsExpanded ? clamp : full) : _content.MaxHeight; _content.BeginAnimation(MaxHeightProperty, null); if (animate && ArcMotion.GetLevel(this) == ArcMotionLevel.Full) _content.BeginAnimation(MaxHeightProperty, new DoubleAnimation(current, target, TimeSpan.FromSeconds(.45)) { EasingFunction = ArcEasing.Enter }); else _content.MaxHeight = target; if (_toggle.Content is TextBlock text) text.Text = IsExpanded ? LessLabel : MoreLabel;
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeerImpl(this);
    private sealed class FrameworkElementAutomationPeerImpl(ExpandableText owner) : FrameworkElementAutomationPeer(owner) { protected override string GetNameCore() => owner.IsExpanded ? owner.LessLabel : owner.MoreLabel; protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group; }
}
