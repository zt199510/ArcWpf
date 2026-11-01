using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ArcWpf.Controls;

public class ResizablePanels : Control
{
    public static readonly DependencyProperty LeftContentProperty = DependencyProperty.Register(nameof(LeftContent), typeof(object), typeof(ResizablePanels));
    public static readonly DependencyProperty RightContentProperty = DependencyProperty.Register(nameof(RightContent), typeof(object), typeof(ResizablePanels));
    public static readonly DependencyProperty SplitRatioProperty = DependencyProperty.Register(nameof(SplitRatio), typeof(double), typeof(ResizablePanels), new FrameworkPropertyMetadata(.5, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualChanged, (_, value) => Math.Clamp((double)value, 0, 1)));
    public static readonly DependencyProperty MinLeftProperty = DependencyProperty.Register(nameof(MinLeft), typeof(double), typeof(ResizablePanels), new PropertyMetadata(160d, OnVisualChanged), value => value is double number && double.IsFinite(number) && number >= 0);
    public static readonly DependencyProperty MinRightProperty = DependencyProperty.Register(nameof(MinRight), typeof(double), typeof(ResizablePanels), new PropertyMetadata(160d, OnVisualChanged), value => value is double number && double.IsFinite(number) && number >= 0);
    public object? LeftContent { get => GetValue(LeftContentProperty); set => SetValue(LeftContentProperty, value); }
    public object? RightContent { get => GetValue(RightContentProperty); set => SetValue(RightContentProperty, value); }
    public double SplitRatio { get => (double)GetValue(SplitRatioProperty); set => SetValue(SplitRatioProperty, value); }
    public double MinLeft { get => (double)GetValue(MinLeftProperty); set => SetValue(MinLeftProperty, value); }
    public double MinRight { get => (double)GetValue(MinRightProperty); set => SetValue(MinRightProperty, value); }
    private Grid? _layout;
    private GridSplitter? _splitter;
    private TranslateTransform? _edgeOffset;
    private readonly OwnedMotion _edgeMotion = new();
    private bool _updating;
    static ResizablePanels() { DefaultStyleKeyProperty.OverrideMetadata(typeof(ResizablePanels), new FrameworkPropertyMetadata(typeof(ResizablePanels))); }
    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var panels = (ResizablePanels)d;
        if (!panels._updating) panels.ApplyRatio();
    }
    public override void OnApplyTemplate()
    {
        if (_layout is not null) _layout.SizeChanged -= LayoutSizeChanged;
        if (_splitter is not null)
        {
            _splitter.DragDelta -= SplitMoved;
            _splitter.DragCompleted -= SplitCompleted;
            _splitter.PreviewKeyDown -= SplitKeyDown;
        }
        _edgeMotion.Stop();
        base.OnApplyTemplate();
        _layout = GetTemplateChild("PART_Layout") as Grid;
        _splitter = GetTemplateChild("PART_Splitter") as GridSplitter;
        if (_layout is not null) _layout.SizeChanged += LayoutSizeChanged;
        if (_splitter is not null)
        {
            _edgeOffset = new TranslateTransform();
            _splitter.RenderTransform = _edgeOffset;
            _splitter.DragDelta += SplitMoved;
            _splitter.DragCompleted += SplitCompleted;
            _splitter.PreviewKeyDown += SplitKeyDown;
        }
        ApplyRatio();
    }
    private void SplitMoved(object sender, DragDeltaEventArgs e)
    {
        if (_layout is not { ColumnDefinitions.Count: 3 } layout || _edgeOffset is null || ArcMotion.GetLevel(this) != ArcMotionLevel.Full) return;
        bool leftLimit = layout.ColumnDefinitions[0].ActualWidth <= MinLeft + .5 && e.HorizontalChange < 0;
        bool rightLimit = layout.ColumnDefinitions[2].ActualWidth <= MinRight + .5 && e.HorizontalChange > 0;
        if (!leftLimit && !rightLimit) return;
        _edgeMotion.Stop();
        _edgeOffset.X = Math.Clamp(e.HorizontalChange * .35, -8, 8);
        _edgeMotion.Double(_edgeOffset, TranslateTransform.XProperty, 0, .26, true, spring: true);
    }
    private void LayoutSizeChanged(object sender, SizeChangedEventArgs e) => ApplyRatio();
    private void SplitCompleted(object sender, DragCompletedEventArgs e) => SyncRatioFromColumns();
    private void SplitKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_layout is not { ColumnDefinitions.Count: 3 } layout || e.Key is not (System.Windows.Input.Key.Left or System.Windows.Input.Key.Right or System.Windows.Input.Key.Home or System.Windows.Input.Key.End)) return;
        double available = layout.ActualWidth - layout.ColumnDefinitions[1].ActualWidth;
        if (available <= 0) return;
        double target = e.Key switch
        {
            System.Windows.Input.Key.Left => layout.ColumnDefinitions[0].ActualWidth - 10,
            System.Windows.Input.Key.Right => layout.ColumnDefinitions[0].ActualWidth + 10,
            System.Windows.Input.Key.Home => MinLeft,
            _ => available - MinRight
        };
        SetCurrentValue(SplitRatioProperty, Math.Clamp(target, MinLeft, Math.Max(MinLeft, available - MinRight)) / available);
        e.Handled = true;
    }
    private void SyncRatioFromColumns()
    {
        if (_layout is not { ColumnDefinitions.Count: 3 } layout) return;
        double available = layout.ActualWidth - layout.ColumnDefinitions[1].ActualWidth;
        if (available <= 0) return;
        _updating = true;
        try { SetCurrentValue(SplitRatioProperty, layout.ColumnDefinitions[0].ActualWidth / available); }
        finally { _updating = false; }
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ArcMotion.LevelProperty && ArcMotion.GetLevel(this) != ArcMotionLevel.Full)
        {
            _edgeMotion.Stop();
            if (_edgeOffset is not null) _edgeOffset.X = 0;
        }
    }
    private void ApplyRatio()
    {
        if (_layout is not { ColumnDefinitions.Count: 3 } layout) return;
        double available = layout.ActualWidth - layout.ColumnDefinitions[1].ActualWidth;
        if (available <= 0) return;
        double leftMin = Math.Min(MinLeft, available);
        double rightMin = Math.Min(MinRight, Math.Max(0, available - leftMin));
        double left = Math.Clamp(available * SplitRatio, leftMin, available - rightMin);
        layout.ColumnDefinitions[0].Width = new GridLength(left, GridUnitType.Pixel);
        layout.ColumnDefinitions[2].Width = new GridLength(Math.Max(0, available - left), GridUnitType.Pixel);
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new ResizablePanelsPeer(this);
    private sealed class ResizablePanelsPeer(ResizablePanels owner) : FrameworkElementAutomationPeer(owner) { protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane; protected override string GetNameCore() => "Resizable panels"; }
}
