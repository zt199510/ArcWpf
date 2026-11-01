using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

public class ResizablePanel : ContentControl
{
    static ResizablePanel() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ResizablePanel), new FrameworkPropertyMetadata(typeof(ResizablePanel)));
    public static readonly DependencyProperty IdProperty = DependencyProperty.Register(nameof(Id), typeof(string), typeof(ResizablePanel), new PropertyMetadata(string.Empty, Changed));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ResizablePanel), new PropertyMetadata(string.Empty, Changed));
    public static readonly DependencyProperty DefaultSizeProperty = DependencyProperty.Register(nameof(DefaultSize), typeof(double), typeof(ResizablePanel), new PropertyMetadata(1d, Changed), IsNonnegativeFinite);
    public static readonly DependencyProperty MinSizeProperty = DependencyProperty.Register(nameof(MinSize), typeof(double), typeof(ResizablePanel), new PropertyMetadata(80d, Changed), IsNonnegativeFinite);
    public static readonly DependencyProperty MaxSizeProperty = DependencyProperty.Register(nameof(MaxSize), typeof(double), typeof(ResizablePanel), new PropertyMetadata(double.PositiveInfinity, Changed), value => value is double number && (double.IsPositiveInfinity(number) || double.IsFinite(number) && number >= 0));
    public static readonly DependencyProperty CollapsibleProperty = DependencyProperty.Register(nameof(Collapsible), typeof(bool), typeof(ResizablePanel), new PropertyMetadata(false, Changed));
    public static readonly DependencyProperty DefaultCollapsedProperty = DependencyProperty.Register(nameof(DefaultCollapsed), typeof(bool), typeof(ResizablePanel), new PropertyMetadata(false, Changed));
    public static readonly DependencyProperty IsCollapsedProperty = DependencyProperty.Register(nameof(IsCollapsed), typeof(bool), typeof(ResizablePanel), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, Changed));
    public string Id { get => (string)GetValue(IdProperty); set => SetValue(IdProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public double DefaultSize { get => (double)GetValue(DefaultSizeProperty); set => SetValue(DefaultSizeProperty, value); }
    public double MinSize { get => (double)GetValue(MinSizeProperty); set => SetValue(MinSizeProperty, value); }
    public double MaxSize { get => (double)GetValue(MaxSizeProperty); set => SetValue(MaxSizeProperty, value); }
    public bool Collapsible { get => (bool)GetValue(CollapsibleProperty); set => SetValue(CollapsibleProperty, value); }
    public bool DefaultCollapsed { get => (bool)GetValue(DefaultCollapsedProperty); set => SetValue(DefaultCollapsedProperty, value); }
    public bool IsCollapsed { get => (bool)GetValue(IsCollapsedProperty); set => SetValue(IsCollapsedProperty, value); }
    public event EventHandler<DependencyPropertyChangedEventArgs>? ConfigurationChanged;
    private static bool IsNonnegativeFinite(object value) => value is double number && double.IsFinite(number) && number >= 0;
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ResizablePanel)d).ConfigurationChanged?.Invoke(d, e);
}

public sealed class ResizablePanelsLayoutChangedEventArgs(IReadOnlyList<double> shares) : EventArgs
{
    public IReadOnlyList<double> Shares { get; } = shares;
}

public partial class ResizablePanels
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ResizablePanels), new PropertyMetadata("Resizable panels"));
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public ObservableCollection<ResizablePanel> Panels { get; } = [];
    public event EventHandler<ResizablePanelsLayoutChangedEventArgs>? LayoutChanged;
    public IReadOnlyList<double> Shares => Panels.Count == 0
        ? IsLeftCollapsed ? [0, 100] : IsRightCollapsed ? [100, 0] : [SplitRatio * 100, (1 - SplitRatio) * 100]
        : _multiShares.Select(value => value * 100).ToArray();

    private sealed record MultiHandle(int Index, GridSplitter Splitter, System.Windows.Controls.Button Restore, TranslateTransform Offset);
    private readonly List<MultiHandle> _multiHandles = [];
    private readonly List<Border> _multiHosts = [];
    private readonly HashSet<ResizablePanel> _observedPanels = [];
    private readonly List<ResizablePanel> _multiSnapshot = [];
    private readonly OwnedMotion _multiMotion = new();
    private double[] _multiShares = [], _restoreShares = [];
    private bool[] _collapsedStates = [];
    private bool _syncingMulti;
    private int _multiDragHandle = -1;
    private double[]? _multiDragStartShares;
    private double _multiOvershoot;
    private double _multiPointerDelta;
    private readonly DragProjection _multiDragProjection = new();

    private void PanelsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var panel in _observedPanels.Except(Panels).ToArray())
        {
            panel.ConfigurationChanged -= PanelChanged;
            _observedPanels.Remove(panel);
        }
        foreach (var panel in Panels)
            if (_observedPanels.Add(panel)) panel.ConfigurationChanged += PanelChanged;
        if (_layout is not null) BuildMulti();
    }
    private void PanelChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        if (_syncingMulti || sender is not ResizablePanel panel) return;
        int index = Panels.IndexOf(panel);
        if (index < 0) return;
        if (e.Property == ResizablePanel.IsCollapsedProperty && index < _collapsedStates.Length)
        {
            bool collapsed = panel.IsCollapsed;
            if (collapsed != _collapsedStates[index])
            {
                int neighbor = index == Panels.Count - 1 ? index - 1 : index + 1;
                if (neighbor >= 0 && neighbor < Panels.Count)
                {
                    if (collapsed)
                    {
                        _restoreShares[index] = _multiShares[index];
                        _multiShares[neighbor] += _multiShares[index];
                        _multiShares[index] = 0;
                    }
                    else
                    {
                        double restored = Math.Min(_restoreShares[index] > 0 ? _restoreShares[index] : .2, _multiShares[neighbor] * .8);
                        _multiShares[index] = restored;
                        _multiShares[neighbor] -= restored;
                    }
                }
                _collapsedStates[index] = collapsed;
                RaiseLayoutChanged();
            }
        }
        ApplyMultiLayout();
    }
    private void ReleaseMulti()
    {
        _multiMotion.Stop();
        if (_layout is not null)
            foreach (var host in _multiHosts)
            {
                _layout.Children.Remove(host);
                host.Child = null;
            }
        _multiHosts.Clear();
        foreach (var handle in _multiHandles)
        {
            handle.Splitter.DragStarted -= MultiDragStarted;
            handle.Splitter.DragDelta -= MultiDragDelta;
            handle.Splitter.DragCompleted -= MultiDragCompleted;
            handle.Splitter.PreviewKeyDown -= MultiKeyDown;
            handle.Splitter.MouseDoubleClick -= MultiDoubleClicked;
            handle.Restore.Click -= MultiRestoreClicked;
        }
        _multiHandles.Clear();
        _multiDragHandle = -1;
    }
    private void StopMultiMotion()
    {
        _multiMotion.Stop();
        foreach (var handle in _multiHandles) handle.Offset.X = 0;
    }
    private void BuildMulti()
    {
        if (_layout is null) return;
        ReleaseMulti();
        if (Panels.Count == 0) { RestoreLegacyLayout(); return; }
        if (Panels.Any(panel => string.IsNullOrWhiteSpace(panel.Id)) || Panels.Select(panel => panel.Id).Distinct(StringComparer.Ordinal).Count() != Panels.Count)
            throw new InvalidOperationException("Each resizable panel needs a unique, nonempty Id.");
        if (_multiSnapshot.Count != Panels.Count || !_multiSnapshot.SequenceEqual(Panels)) InitializeMultiShares();
        _layout.Children.Clear();
        _layout.ColumnDefinitions.Clear();
        for (int index = 0; index < Panels.Count; index++)
        {
            _layout.ColumnDefinitions.Add(new ColumnDefinition());
            var host = new Border { Child = Panels[index], ClipToBounds = true };
            Grid.SetColumn(host, index * 2);
            _layout.Children.Add(host);
            _multiHosts.Add(host);
            if (index == Panels.Count - 1) continue;
            _layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            var splitter = new GridSplitter
            {
                Width = 6, Background = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Stretch,
                ResizeBehavior = GridResizeBehavior.PreviousAndNext, ResizeDirection = GridResizeDirection.Columns,
                ShowsPreview = false, Focusable = true, Tag = index
            };
            splitter.Template = _splitter?.Template;
            var offset = new TranslateTransform();
            splitter.RenderTransform = offset;
            splitter.DragStarted += MultiDragStarted;
            splitter.DragDelta += MultiDragDelta;
            splitter.DragCompleted += MultiDragCompleted;
            splitter.PreviewKeyDown += MultiKeyDown;
            splitter.MouseDoubleClick += MultiDoubleClicked;
            Grid.SetColumn(splitter, index * 2 + 1);
            _layout.Children.Add(splitter);
            var restore = new System.Windows.Controls.Button { Width = 24, Height = 44, Focusable = false, Tag = index, Visibility = Visibility.Collapsed };
            restore.Template = _restoreButton?.Template;
            restore.Click += MultiRestoreClicked;
            Grid.SetColumn(restore, index * 2 + 1);
            System.Windows.Controls.Panel.SetZIndex(restore, 2);
            _layout.Children.Add(restore);
            _multiHandles.Add(new MultiHandle(index, splitter, restore, offset));
        }
        ApplyMultiLayout();
    }
    private void RestoreLegacyLayout()
    {
        if (_layout is null || _multiSnapshot.Count == 0) return;
        _multiSnapshot.Clear();
        _multiShares = []; _restoreShares = []; _collapsedStates = [];
        _layout.Children.Clear();
        _layout.ColumnDefinitions.Clear();
        _layout.ColumnDefinitions.Add(new ColumnDefinition());
        _layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
        _layout.ColumnDefinitions.Add(new ColumnDefinition());
        if (_leftPresenter is not null) { Grid.SetColumn(_leftPresenter, 0); _layout.Children.Add(_leftPresenter); }
        if (_splitter is not null) { Grid.SetColumn(_splitter, 1); _layout.Children.Add(_splitter); }
        if (_restoreButton is not null) { Grid.SetColumn(_restoreButton, 1); _layout.Children.Add(_restoreButton); }
        if (_rightPresenter is not null) { Grid.SetColumn(_rightPresenter, 2); _layout.Children.Add(_rightPresenter); }
        ApplyRatio();
    }
    private void InitializeMultiShares()
    {
        _multiSnapshot.Clear();
        _multiSnapshot.AddRange(Panels);
        _syncingMulti = true;
        try
        {
            foreach (var panel in Panels)
                if (panel.DefaultCollapsed && panel.Collapsible && panel.ReadLocalValue(ResizablePanel.IsCollapsedProperty) == DependencyProperty.UnsetValue)
                    panel.SetCurrentValue(ResizablePanel.IsCollapsedProperty, true);
        }
        finally { _syncingMulti = false; }
        double total = Panels.Where(panel => !panel.IsCollapsed).Sum(panel => panel.DefaultSize);
        if (total <= 0) total = Panels.Count(panel => !panel.IsCollapsed);
        _multiShares = Panels.Select(panel => panel.IsCollapsed ? 0 : (panel.DefaultSize > 0 ? panel.DefaultSize : 1) / total).ToArray();
        double sum = _multiShares.Sum();
        if (sum > 0) for (int index = 0; index < _multiShares.Length; index++) _multiShares[index] /= sum;
        _restoreShares = new double[Panels.Count];
        double defaultTotal = Panels.Sum(panel => panel.DefaultSize);
        if (defaultTotal > 0)
            for (int index = 0; index < Panels.Count; index++) _restoreShares[index] = Panels[index].DefaultSize / defaultTotal;
        _collapsedStates = Panels.Select(panel => panel.IsCollapsed).ToArray();
    }
    private void ApplyMultiLayout()
    {
        if (_layout is null || Panels.Count == 0 || _layout.ColumnDefinitions.Count != Panels.Count * 2 - 1) return;
        double available = _layout.ActualWidth - 6 * (Panels.Count - 1);
        if (available <= 0) return;
        var open = Enumerable.Range(0, Panels.Count).Where(index => !Panels[index].IsCollapsed).ToList();
        if (open.Count == 0) { _syncingMulti = true; try { Panels[0].SetCurrentValue(ResizablePanel.IsCollapsedProperty, false); } finally { _syncingMulti = false; } open.Add(0); _collapsedStates[0] = false; }
        double minimumTotal = open.Sum(index => Panels[index].MinSize);
        double minimumScale = minimumTotal > available ? available / minimumTotal : 1;
        var minima = Panels.Select(panel => panel.MinSize * minimumScale).ToArray();
        var widths = new double[Panels.Count];
        double remaining = available;
        var flex = new List<int>(open);
        // 先固定触及边界的面板，再按原份额分配剩余宽度。
        while (flex.Count > 0)
        {
            double weightTotal = flex.Sum(index => Math.Max(_multiShares[index], .0001));
            int? fixedIndex = null;
            double fixedWidth = 0;
            foreach (int index in flex)
            {
                double proposed = remaining * Math.Max(_multiShares[index], .0001) / weightTotal;
                double bounded = Math.Clamp(proposed, minima[index], Math.Max(minima[index], Panels[index].MaxSize));
                if (Math.Abs(bounded - proposed) > .01) { fixedIndex = index; fixedWidth = bounded; break; }
            }
            if (fixedIndex is null)
            {
                foreach (int index in flex) widths[index] = remaining * Math.Max(_multiShares[index], .0001) / weightTotal;
                break;
            }
            widths[fixedIndex.Value] = fixedWidth;
            remaining = Math.Max(0, remaining - fixedWidth);
            flex.Remove(fixedIndex.Value);
        }
        for (int index = 0; index < Panels.Count; index++)
        {
            var column = _layout.ColumnDefinitions[index * 2];
            column.MinWidth = Panels[index].IsCollapsed ? 0 : minima[index];
            column.MaxWidth = Panels[index].IsCollapsed ? 0 : Math.Max(minima[index], Panels[index].MaxSize);
            column.Width = new GridLength(widths[index], GridUnitType.Pixel);
            _multiHosts[index].Visibility = Panels[index].IsCollapsed ? Visibility.Collapsed : Visibility.Visible;
            _multiHosts[index].IsHitTestVisible = !Panels[index].IsCollapsed;
        }
        foreach (var handle in _multiHandles)
        {
            bool before = Panels[handle.Index].IsCollapsed, after = Panels[handle.Index + 1].IsCollapsed;
            handle.Restore.Visibility = before || after ? Visibility.Visible : Visibility.Collapsed;
            handle.Restore.Tag = after ? "Right" : "Left";
            AutomationProperties.SetName(handle.Splitter, $"Resize {Panels[handle.Index].Label} and {Panels[handle.Index + 1].Label}");
            AutomationProperties.SetHelpText(handle.Splitter, before ? $"{Panels[handle.Index].Label} hidden, press Enter to show" : after ? $"{Panels[handle.Index + 1].Label} hidden, press Enter to show" : $"{Math.Round(widths[handle.Index] / available * 100)}% of width");
            AutomationProperties.SetName(handle.Restore, before ? $"Show {Panels[handle.Index].Label}" : $"Show {Panels[handle.Index + 1].Label}");
        }
    }
    private void RaiseLayoutChanged() => LayoutChanged?.Invoke(this, new ResizablePanelsLayoutChangedEventArgs(Shares));
    private void ResetMulti()
    {
        _syncingMulti = true;
        try
        {
            foreach (var panel in Panels) panel.SetCurrentValue(ResizablePanel.IsCollapsedProperty, panel.Collapsible && panel.DefaultCollapsed);
        }
        finally { _syncingMulti = false; }
        InitializeMultiShares();
        ApplyMultiLayout();
        RaiseLayoutChanged();
    }
}
