using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ArcWpf.Controls;

public partial class ResizablePanels : Control
{
    public static readonly DependencyProperty LeftContentProperty = DependencyProperty.Register(nameof(LeftContent), typeof(object), typeof(ResizablePanels));
    public static readonly DependencyProperty RightContentProperty = DependencyProperty.Register(nameof(RightContent), typeof(object), typeof(ResizablePanels));
    public static readonly DependencyProperty SplitRatioProperty = DependencyProperty.Register(nameof(SplitRatio), typeof(double), typeof(ResizablePanels), new FrameworkPropertyMetadata(.5, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualChanged, (_, value) => Math.Clamp((double)value, 0, 1)));
    public static readonly DependencyProperty MinLeftProperty = DependencyProperty.Register(nameof(MinLeft), typeof(double), typeof(ResizablePanels), new PropertyMetadata(160d, OnVisualChanged), value => value is double number && double.IsFinite(number) && number >= 0);
    public static readonly DependencyProperty MinRightProperty = DependencyProperty.Register(nameof(MinRight), typeof(double), typeof(ResizablePanels), new PropertyMetadata(160d, OnVisualChanged), value => value is double number && double.IsFinite(number) && number >= 0);
    public static readonly DependencyProperty CanCollapseLeftProperty = DependencyProperty.Register(nameof(CanCollapseLeft), typeof(bool), typeof(ResizablePanels), new PropertyMetadata(false));
    public static readonly DependencyProperty CanCollapseRightProperty = DependencyProperty.Register(nameof(CanCollapseRight), typeof(bool), typeof(ResizablePanels), new PropertyMetadata(false));
    public static readonly DependencyProperty IsLeftCollapsedProperty = DependencyProperty.Register(nameof(IsLeftCollapsed), typeof(bool), typeof(ResizablePanels), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCollapseChanged));
    public static readonly DependencyProperty IsRightCollapsedProperty = DependencyProperty.Register(nameof(IsRightCollapsed), typeof(bool), typeof(ResizablePanels), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCollapseChanged));
    public static readonly DependencyProperty DefaultSplitRatioProperty = DependencyProperty.Register(nameof(DefaultSplitRatio), typeof(double), typeof(ResizablePanels), new PropertyMetadata(.5d), value => value is double ratio && double.IsFinite(ratio) && ratio is >= 0 and <= 1);
    public object? LeftContent { get => GetValue(LeftContentProperty); set => SetValue(LeftContentProperty, value); }
    public object? RightContent { get => GetValue(RightContentProperty); set => SetValue(RightContentProperty, value); }
    public double SplitRatio { get => (double)GetValue(SplitRatioProperty); set => SetValue(SplitRatioProperty, value); }
    public double MinLeft { get => (double)GetValue(MinLeftProperty); set => SetValue(MinLeftProperty, value); }
    public double MinRight { get => (double)GetValue(MinRightProperty); set => SetValue(MinRightProperty, value); }
    public bool CanCollapseLeft { get => (bool)GetValue(CanCollapseLeftProperty); set => SetValue(CanCollapseLeftProperty, value); }
    public bool CanCollapseRight { get => (bool)GetValue(CanCollapseRightProperty); set => SetValue(CanCollapseRightProperty, value); }
    public bool IsLeftCollapsed { get => (bool)GetValue(IsLeftCollapsedProperty); set => SetValue(IsLeftCollapsedProperty, value); }
    public bool IsRightCollapsed { get => (bool)GetValue(IsRightCollapsedProperty); set => SetValue(IsRightCollapsedProperty, value); }
    public double DefaultSplitRatio { get => (double)GetValue(DefaultSplitRatioProperty); set => SetValue(DefaultSplitRatioProperty, value); }
    private Grid? _layout;
    private GridSplitter? _splitter;
    private TranslateTransform? _edgeOffset;
    private readonly OwnedMotion _edgeMotion = new();
    private readonly DragProjection _dragProjection = new();
    private bool _updating;
    private bool _updatingCollapse;
    private double _dragStartRatio, _overshoot;
    private bool _dragging;
    private ContentPresenter? _leftPresenter, _rightPresenter;
    private ButtonBase? _restoreButton;
    static ResizablePanels() { DefaultStyleKeyProperty.OverrideMetadata(typeof(ResizablePanels), new FrameworkPropertyMetadata(typeof(ResizablePanels))); }
    public ResizablePanels()
    {
        Panels.CollectionChanged += PanelsChanged;
        Unloaded += (_, _) =>
        {
            _edgeMotion.Stop();
            if (_edgeOffset is not null) _edgeOffset.X = 0;
            _dragging = false; _overshoot = 0;
            StopMultiMotion();
        };
    }
    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var panels = (ResizablePanels)d;
        if (!panels._updating) panels.ApplyRatio();
    }
    private static void OnCollapseChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var panels = (ResizablePanels)d;
        if (panels._updatingCollapse) return;
        panels._updatingCollapse = true;
        try
        {
            if (e.Property == IsLeftCollapsedProperty && (bool)e.NewValue) panels.SetCurrentValue(IsRightCollapsedProperty, false);
            if (e.Property == IsRightCollapsedProperty && (bool)e.NewValue) panels.SetCurrentValue(IsLeftCollapsedProperty, false);
        }
        finally { panels._updatingCollapse = false; }
        panels.ApplyRatio();
        if (panels.Panels.Count == 0) panels.RaiseLayoutChanged();
    }
    public override void OnApplyTemplate()
    {
        ReleaseMulti();
        if (_layout is not null) _layout.SizeChanged -= LayoutSizeChanged;
        if (_splitter is not null)
        {
            _splitter.DragDelta -= SplitMoved;
            _splitter.DragCompleted -= SplitCompleted;
            _splitter.DragStarted -= SplitStarted;
            _splitter.PreviewKeyDown -= SplitKeyDown;
            _splitter.MouseDoubleClick -= SplitDoubleClicked;
        }
        if (_restoreButton is not null) _restoreButton.Click -= RestoreClicked;
        _edgeMotion.Stop();
        base.OnApplyTemplate();
        _layout = GetTemplateChild("PART_Layout") as Grid;
        _splitter = GetTemplateChild("PART_Splitter") as GridSplitter;
        _leftPresenter = GetTemplateChild("PART_LeftPresenter") as ContentPresenter;
        _rightPresenter = GetTemplateChild("PART_RightPresenter") as ContentPresenter;
        _restoreButton = GetTemplateChild("PART_RestoreButton") as ButtonBase;
        if (_layout is not null) _layout.SizeChanged += LayoutSizeChanged;
        if (_splitter is not null)
        {
            _edgeOffset = new TranslateTransform();
            _splitter.RenderTransform = _edgeOffset;
            _splitter.DragDelta += SplitMoved;
            _splitter.DragCompleted += SplitCompleted;
            _splitter.DragStarted += SplitStarted;
            _splitter.PreviewKeyDown += SplitKeyDown;
            _splitter.MouseDoubleClick += SplitDoubleClicked;
        }
        if (_restoreButton is not null) _restoreButton.Click += RestoreClicked;
        if (Panels.Count > 0) BuildMulti(); else ApplyRatio();
    }
    private void SplitMoved(object sender, DragDeltaEventArgs e)
    {
        if (_layout is not { ColumnDefinitions.Count: 3 } layout || _edgeOffset is null || IsLeftCollapsed || IsRightCollapsed) return;
        _dragProjection.Add(e.HorizontalChange);
        bool leftLimit = layout.ColumnDefinitions[0].ActualWidth <= MinLeft + .5 && e.HorizontalChange < 0;
        bool rightLimit = layout.ColumnDefinitions[2].ActualWidth <= MinRight + .5 && e.HorizontalChange > 0;
        _overshoot = leftLimit ? Math.Min(0, _overshoot + e.HorizontalChange) : rightLimit ? Math.Max(0, _overshoot + e.HorizontalChange) : 0;
        if ((!leftLimit && !rightLimit) || ArcMotion.GetLevel(this) != ArcMotionLevel.Full) return;
        _edgeMotion.Stop();
        double distance = Math.Abs(_overshoot);
        double rubber = distance * 44 * .55 / (44 + .55 * distance);
        _edgeOffset.X = Math.CopySign(rubber, _overshoot);
    }
    private void SplitStarted(object sender, DragStartedEventArgs e)
    {
        _edgeMotion.Stop();
        if (_edgeOffset is not null) _edgeOffset.X = 0;
        _dragging = true; _dragStartRatio = SplitRatio; _overshoot = 0;
        _dragProjection.Start();
    }
    private void LayoutSizeChanged(object sender, SizeChangedEventArgs e) => ApplyRatio();
    private void SplitCompleted(object sender, DragCompletedEventArgs e)
    {
        _dragging = false;
        if (_edgeOffset is not null)
            _edgeMotion.Double(_edgeOffset, TranslateTransform.XProperty, 0, .26, ArcMotion.GetLevel(this) == ArcMotionLevel.Full, spring: true);
        if (e.Canceled) { SetCurrentValue(SplitRatioProperty, _dragStartRatio); ApplyRatio(); return; }
        if ((IsLeftCollapsed || IsRightCollapsed) && _layout is { ColumnDefinitions.Count: 3 } layout)
        {
            double available = layout.ActualWidth - layout.ColumnDefinitions[1].ActualWidth;
            if (available > 0)
            {
                double ratio = layout.ColumnDefinitions[0].ActualWidth / available;
                SetCurrentValue(SplitRatioProperty, ratio);
                SetCurrentValue(IsLeftCollapsedProperty, false);
                SetCurrentValue(IsRightCollapsedProperty, false);
            }
            return;
        }
        if (_layout is not { ColumnDefinitions.Count: 3 } columns) return;
        double raw = columns.ColumnDefinitions[0].ActualWidth + _overshoot + _dragProjection.Project();
        double pair = columns.ActualWidth - columns.ColumnDefinitions[1].ActualWidth;
        if (raw < columns.ColumnDefinitions[0].MinWidth - Math.Max(MinLeft * .5, 40) && CanCollapseLeft) SetCurrentValue(IsLeftCollapsedProperty, true);
        else if (pair - raw < columns.ColumnDefinitions[2].MinWidth - Math.Max(MinRight * .5, 40) && CanCollapseRight) SetCurrentValue(IsRightCollapsedProperty, true);
        else SyncRatioFromColumns();
        _overshoot = 0;
    }
    private void SplitDoubleClicked(object sender, System.Windows.Input.MouseButtonEventArgs e) { Reset(); e.Handled = true; }
    private void RestoreClicked(object sender, RoutedEventArgs e)
    {
        SetCurrentValue(IsLeftCollapsedProperty, false);
        SetCurrentValue(IsRightCollapsedProperty, false);
        _splitter?.Focus();
        e.Handled = true;
    }
    public void Reset()
    {
        if (Panels.Count > 0) { ResetMulti(); return; }
        SetCurrentValue(IsLeftCollapsedProperty, false);
        SetCurrentValue(IsRightCollapsedProperty, false);
        SetCurrentValue(SplitRatioProperty, DefaultSplitRatio);
        ApplyRatio();
        RaiseLayoutChanged();
    }
    private void SplitKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_layout is not { ColumnDefinitions.Count: 3 } layout) return;
        if (e.Key == System.Windows.Input.Key.Escape && _dragging)
        {
            _splitter?.CancelDrag();
            e.Handled = true;
            return;
        }
        if (e.Key is not (System.Windows.Input.Key.Left or System.Windows.Input.Key.Right or System.Windows.Input.Key.Home or System.Windows.Input.Key.End or System.Windows.Input.Key.Enter)) return;
        if (IsLeftCollapsed || IsRightCollapsed)
        {
            if (e.Key == System.Windows.Input.Key.Enter || IsLeftCollapsed && e.Key == System.Windows.Input.Key.Right || IsRightCollapsed && e.Key == System.Windows.Input.Key.Left)
            {
                SetCurrentValue(IsLeftCollapsedProperty, false);
                SetCurrentValue(IsRightCollapsedProperty, false);
            }
            e.Handled = true;
            return;
        }
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            if (CanCollapseLeft) SetCurrentValue(IsLeftCollapsedProperty, true);
            else if (CanCollapseRight) SetCurrentValue(IsRightCollapsedProperty, true);
            e.Handled = true;
            return;
        }
        double available = layout.ActualWidth - layout.ColumnDefinitions[1].ActualWidth;
        if (available <= 0) return;
        double leftMin = layout.ColumnDefinitions[0].MinWidth;
        double rightMin = layout.ColumnDefinitions[2].MinWidth;
        double current = Math.Clamp(SplitRatio * available, leftMin, Math.Max(leftMin, available - rightMin));
        if (e.Key == System.Windows.Input.Key.Left && current <= leftMin + .5 && CanCollapseLeft)
        {
            SetCurrentValue(IsLeftCollapsedProperty, true); e.Handled = true; return;
        }
        if (e.Key == System.Windows.Input.Key.Right && current >= available - rightMin - .5 && CanCollapseRight)
        {
            SetCurrentValue(IsRightCollapsedProperty, true); e.Handled = true; return;
        }
        double step = e.KeyboardDevice.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift) ? 64 : 16;
        double target = e.Key switch
        {
            System.Windows.Input.Key.Left => current - step,
            System.Windows.Input.Key.Right => current + step,
            System.Windows.Input.Key.Home => leftMin,
            _ => available - rightMin
        };
        SetCurrentValue(SplitRatioProperty, Math.Clamp(target, leftMin, Math.Max(leftMin, available - rightMin)) / available);
        RaiseLayoutChanged();
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
        RaiseLayoutChanged();
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ArcMotion.LevelProperty && ArcMotion.GetLevel(this) != ArcMotionLevel.Full)
        {
            _edgeMotion.Stop();
            if (_edgeOffset is not null) _edgeOffset.X = 0;
            StopMultiMotion();
        }
    }
    private void ApplyRatio()
    {
        if (Panels.Count > 0) { ApplyMultiLayout(); return; }
        if (_layout is not { ColumnDefinitions.Count: 3 } layout) return;
        double available = layout.ActualWidth - layout.ColumnDefinitions[1].ActualWidth;
        if (available <= 0) return;
        double minimumTotal = MinLeft + MinRight;
        double scale = minimumTotal > available ? available / minimumTotal : 1;
        double leftMin = MinLeft * scale;
        double rightMin = MinRight * scale;
        layout.ColumnDefinitions[0].MinWidth = IsLeftCollapsed || IsRightCollapsed ? 0 : leftMin;
        layout.ColumnDefinitions[2].MinWidth = IsLeftCollapsed || IsRightCollapsed ? 0 : rightMin;
        double left = IsLeftCollapsed ? 0 : IsRightCollapsed ? available : Math.Clamp(available * SplitRatio, leftMin, available - rightMin);
        layout.ColumnDefinitions[0].Width = new GridLength(left, GridUnitType.Pixel);
        layout.ColumnDefinitions[2].Width = new GridLength(Math.Max(0, available - left), GridUnitType.Pixel);
        if (_leftPresenter is not null) _leftPresenter.Visibility = IsLeftCollapsed ? Visibility.Collapsed : Visibility.Visible;
        if (_rightPresenter is not null) _rightPresenter.Visibility = IsRightCollapsed ? Visibility.Collapsed : Visibility.Visible;
        if (_restoreButton is not null)
        {
            _restoreButton.Visibility = IsLeftCollapsed || IsRightCollapsed ? Visibility.Visible : Visibility.Collapsed;
            _restoreButton.Tag = IsRightCollapsed ? "Right" : "Left";
            System.Windows.Automation.AutomationProperties.SetName(_restoreButton, IsLeftCollapsed ? "Show left panel" : "Show right panel");
        }
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new ResizablePanelsPeer(this);
    private sealed class ResizablePanelsPeer(ResizablePanels owner) : FrameworkElementAutomationPeer(owner) { protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group; protected override string GetNameCore() => owner.Label; }
}
