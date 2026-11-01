using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace ArcWpf.Controls;

public class ResizablePanels : Control
{
    public static readonly DependencyProperty LeftContentProperty = DependencyProperty.Register(nameof(LeftContent), typeof(object), typeof(ResizablePanels));
    public static readonly DependencyProperty RightContentProperty = DependencyProperty.Register(nameof(RightContent), typeof(object), typeof(ResizablePanels));
    public static readonly DependencyProperty SplitRatioProperty = DependencyProperty.Register(nameof(SplitRatio), typeof(double), typeof(ResizablePanels), new FrameworkPropertyMetadata(.5, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualChanged));
    public static readonly DependencyProperty MinLeftProperty = DependencyProperty.Register(nameof(MinLeft), typeof(double), typeof(ResizablePanels), new PropertyMetadata(160d));
    public static readonly DependencyProperty MinRightProperty = DependencyProperty.Register(nameof(MinRight), typeof(double), typeof(ResizablePanels), new PropertyMetadata(160d));
    public object? LeftContent { get => GetValue(LeftContentProperty); set => SetValue(LeftContentProperty, value); }
    public object? RightContent { get => GetValue(RightContentProperty); set => SetValue(RightContentProperty, value); }
    public double SplitRatio { get => (double)GetValue(SplitRatioProperty); set => SetValue(SplitRatioProperty, value); }
    public double MinLeft { get => (double)GetValue(MinLeftProperty); set => SetValue(MinLeftProperty, value); }
    public double MinRight { get => (double)GetValue(MinRightProperty); set => SetValue(MinRightProperty, value); }
    static ResizablePanels() { DefaultStyleKeyProperty.OverrideMetadata(typeof(ResizablePanels), new FrameworkPropertyMetadata(typeof(ResizablePanels))); }
    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ResizablePanels)d).ApplyRatio();
    public override void OnApplyTemplate() { base.OnApplyTemplate(); ApplyRatio(); }
    private void ApplyRatio() { }
    protected override AutomationPeer OnCreateAutomationPeer() => new ResizablePanelsPeer(this);
    private sealed class ResizablePanelsPeer(ResizablePanels owner) : FrameworkElementAutomationPeer(owner) { protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane; protected override string GetNameCore() => "Resizable panels"; }
}
