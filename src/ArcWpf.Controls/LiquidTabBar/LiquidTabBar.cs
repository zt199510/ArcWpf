using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public enum LiquidTabLabels { Selected, All, None }

public sealed class LiquidTab
{
    public string Id { get; init; } = "";
    public string Text { get; init; } = "";
    public string Icon { get; init; } = "";
    /// <summary>A count shown on the icon. Zero hides it.</summary>
    public int Count { get; init; }
}

internal static class LiquidTabMath
{
    public const double Pad = 4, Gap = 2, Height = 40, Slot = 44, Lean = 6.44;

    /// <summary>The widths of the row: a slot for an icon, wider where a label shows. Whole pixels, as the reference measures them.</summary>
    public static double TabWidth(double textWidth, bool hasCount) => Math.Round(12 + 20 + 8 + textWidth + (hasCount ? 6 : 0) + 14);

    public static double BarWidth(IEnumerable<double> widths)
    {
        double sum = 0; int count = 0;
        foreach (double width in widths) { sum += width; count++; }
        return Pad * 2 + sum + Gap * Math.Max(0, count - 1);
    }
}

/// <summary>Tabs with a dark lens that slides between them: the lens is a second copy of the row, clipped, so icons fill and labels invert exactly where it passes.</summary>
public class LiquidTabBar : Control
{
    public static readonly DependencyProperty TabsProperty = DependencyProperty.Register(nameof(Tabs), typeof(IReadOnlyList<LiquidTab>), typeof(LiquidTabBar), new PropertyMetadata(null, (d, _) => ((LiquidTabBar)d).Build()));
    public static readonly DependencyProperty SelectedIdProperty = DependencyProperty.Register(nameof(SelectedId), typeof(string), typeof(LiquidTabBar), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((LiquidTabBar)d).OnSelected((string?)e.NewValue)));
    public static readonly DependencyProperty ShowLabelsProperty = DependencyProperty.Register(nameof(ShowLabels), typeof(LiquidTabLabels), typeof(LiquidTabBar), new PropertyMetadata(LiquidTabLabels.Selected, (d, _) => ((LiquidTabBar)d).Relayout(true)));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(LiquidTabBar), new PropertyMetadata("", (d, e) => AutomationProperties.SetName(d, (string)e.NewValue)));

    public IReadOnlyList<LiquidTab>? Tabs { get => (IReadOnlyList<LiquidTab>?)GetValue(TabsProperty); set => SetValue(TabsProperty, value); }
    public string? SelectedId { get => (string?)GetValue(SelectedIdProperty); set => SetValue(SelectedIdProperty, value); }
    public LiquidTabLabels ShowLabels { get => (LiquidTabLabels)GetValue(ShowLabelsProperty); set => SetValue(ShowLabelsProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public event EventHandler<string>? SelectionChanged;

    // Fitted to the public preview: the trailing edge leaves first and quickly, the leading edge follows late and slowly.
    private const double TrailingSeconds = .275, LeadingSeconds = .476, LeadingDelay = .05, WidthSeconds = .42, LabelDelay = .32, LabelSeconds = .48;

    private readonly SpringDriver _driver;
    private readonly ControlSpring _left, _right, _leanLeft, _leanRight;
    private readonly List<TabView> _views = [];
    private readonly DispatcherTimer _leading = new();
    private readonly OwnedMotion _motion = new();
    private Decorator? _host;
    private Border? _bar, _ring;
    private Grid? _lens;
    private Canvas? _baseRow, _lensRow;
    private readonly RectangleGeometry _lensClip = new() { RadiusX = 20, RadiusY = 20 };
    private double _leadingTarget;
    private ControlSpring? _leadingEdge;
    private TabView? _focused;

    static LiquidTabBar() => DefaultStyleKeyProperty.OverrideMetadata(typeof(LiquidTabBar), new FrameworkPropertyMetadata(typeof(LiquidTabBar)));

    public LiquidTabBar()
    {
        _driver = new SpringDriver(Layout);
        _left = _driver.Add(LiquidTabMath.Pad, ArcSpring.Of(TrailingSeconds));
        _right = _driver.Add(LiquidTabMath.Pad, ArcSpring.Of(LeadingSeconds));
        _leanLeft = _driver.Add(0, ArcSpring.Snappy);
        _leanRight = _driver.Add(0, ArcSpring.Snappy);
        _leading.Tick += (_, _) => { _leading.Stop(); if (_leadingEdge is not null) { _driver.Animate(_leadingEdge, _leadingTarget, ArcSpring.Of(LeadingSeconds)); } };
        Unloaded += (_, _) => { _driver.Stop(); _leading.Stop(); _motion.Stop(); };
        ThemeWatch.Add(this, () => { foreach (var view in _views) { view.Refresh(false); } });
    }

    private bool Moving => IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;
    internal Rect LensTargetForTest => new(_left.Target - _leanLeft.Target, LiquidTabMath.Pad, _right.Target + _leanRight.Target - (_left.Target - _leanLeft.Target), LiquidTabMath.Height);
    internal Rect LensForTest => _lensClip.Rect;
    internal double BarWidthForTest => _bar?.Width ?? 0;
    internal double TabWidthForTest(string id) => _views.First(v => v.Tab.Id == id).Width.Position;
    internal double TabLeftForTest(string id) => Canvas.GetLeft(_views.First(v => v.Tab.Id == id).Hit);
    internal RadioButton HitForTest(string id) => _views.First(v => v.Tab.Id == id).Hit;
    internal void HoverForTest(string? id) => Lean(_views.FirstOrDefault(v => v.Tab.Id == id));
    internal double LensLabelOpacityForTest(string id) => _views.First(v => v.Tab.Id == id).LensLabel.Opacity;
    internal void FinishMotionForTest() { _leading.Stop(); if (_leadingEdge is not null) { _driver.Animate(_leadingEdge, _leadingTarget, ArcSpring.Of(LeadingSeconds)); } _driver.Finish(); foreach (var view in _views) { view.SettleLabel(); } }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _host = GetTemplateChild("PART_Root") as Decorator;
        Build();
    }

    private void Build()
    {
        if (_host is null) { return; }
        _driver.Stop();
        _views.Clear();
        _baseRow = new Canvas { Height = 48 };
        _lensRow = new Canvas { Height = 48, IsHitTestVisible = false };
        foreach (var tab in Tabs ?? [])
        {
            var view = new TabView(this, tab);
            _views.Add(view);
            _baseRow.Children.Add(view.Hit);
            _lensRow.Children.Add(view.LensPart);
        }
        _lens = new Grid { IsHitTestVisible = false, Clip = _lensClip, Children = { _lensRow } };
        _lens.SetResourceReference(Panel.BackgroundProperty, "ArcWpf.Brush.Foreground");
        _ring = new Border { Height = 48, BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(24), Opacity = .72, IsHitTestVisible = false, Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        _ring.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Focus");
        _bar = new Border { Height = 48, CornerRadius = new CornerRadius(24), BorderThickness = new Thickness(1) };
        _bar.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
        _bar.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderSubtle");
        // The inset ring is a hairline on the bar; the rows measure from its outer edge, so they sit in a grid above it.
        var stack = new Grid { Children = { _bar, _baseRow, _lens, _ring }, HorizontalAlignment = HorizontalAlignment.Center, Height = 48 };
        stack.MouseLeave += (_, _) => Lean(null);
        stack.PreviewKeyDown += OnKey;
        _root = stack;
        _host.Child = stack;
        _building = true;
        if (SelectedId is null && _views.Count > 0) { SetCurrentValue(SelectedIdProperty, _views[0].Tab.Id); }
        _building = false;
        foreach (var view in _views) { view.Selected = view.Tab.Id == SelectedId; view.Hit.IsChecked = view.Selected; }
        Relayout(false);
    }

    private Grid? _root;
    private bool _building;

    private double TargetWidth(TabView view) => ShowLabels switch
    {
        LiquidTabLabels.None => LiquidTabMath.Slot,
        LiquidTabLabels.All => view.WideWidth,
        _ => view.Selected ? view.WideWidth : LiquidTabMath.Slot,
    };

    private (double Left, double Right) TargetEdges()
    {
        double x = LiquidTabMath.Pad;
        foreach (var view in _views)
        {
            double w = TargetWidth(view);
            if (view.Selected) { return (x, x + w); }
            x += w + LiquidTabMath.Gap;
        }
        return (LiquidTabMath.Pad, LiquidTabMath.Pad + LiquidTabMath.Slot);
    }

    private void Relayout(bool animate)
    {
        if (_bar is null) { return; }
        bool moving = animate && Moving;
        foreach (var view in _views)
        {
            double width = TargetWidth(view);
            if (moving) { _driver.Animate(view.Width, width, ArcSpring.Of(WidthSeconds)); } else { view.Width.Snap(width); }
            view.Refresh(moving);
        }
        var (left, right) = TargetEdges();
        _leading.Stop();
        if (moving)
        {
            // Whichever edge the lens moves away from goes first; the far edge follows after a beat.
            bool toRight = left + right > _left.Target + _right.Target;
            var trailing = toRight ? _left : _right;
            _leadingEdge = toRight ? _right : _left;
            _leadingTarget = toRight ? right : left;
            _driver.Animate(trailing, toRight ? left : right, ArcSpring.Of(TrailingSeconds));
            _leading.Interval = TimeSpan.FromSeconds(LeadingDelay);
            _leadingEdge.Target = _leadingEdge.Position;
            _leading.Start();
        }
        else
        {
            _leadingEdge = null;
            _left.Snap(left);
            _right.Snap(right);
        }
        Layout();
    }

    private void Layout()
    {
        if (_bar is null || _root is null) { return; }
        double x = LiquidTabMath.Pad;
        foreach (var view in _views)
        {
            double w = view.Width.Position;
            view.Place(x, w);
            x += w + LiquidTabMath.Gap;
        }
        double width = x - LiquidTabMath.Gap + LiquidTabMath.Pad;
        _root.Width = width;
        _bar.Width = width;
        double l = _left.Position - _leanLeft.Position, r = _right.Position + _leanRight.Position;
        _lensClip.Rect = new Rect(l, LiquidTabMath.Pad, Math.Max(0, r - l), LiquidTabMath.Height);
        if (_ring is not null && _focused is not null)
        {
            _ring.Width = _focused.Width.Position + 8;
            _ring.Margin = new Thickness(Canvas.GetLeft(_focused.Hit) - 4, 0, 0, 0);
        }
    }

    private void OnSelected(string? id)
    {
        foreach (var view in _views)
        {
            bool selected = view.Tab.Id == id;
            if (view.Selected == selected) { continue; }
            view.Selected = selected;
            view.Hit.IsChecked = selected;
        }
        Lean(null);
        Relayout(true);
        if (id is not null && !_building) { SelectionChanged?.Invoke(this, id); }
    }

    // The lens leans a few pixels toward the tab the pointer is on, on whichever side that is.
    private void Lean(TabView? over)
    {
        double left = 0, right = 0;
        var selected = _views.FindIndex(v => v.Selected);
        int index = over is null ? -1 : _views.IndexOf(over);
        if (index >= 0 && selected >= 0 && index != selected)
        {
            if (index < selected) { left = LiquidTabMath.Lean; } else { right = LiquidTabMath.Lean; }
        }
        if (Moving)
        {
            _driver.Animate(_leanLeft, left, ArcSpring.Snappy);
            _driver.Animate(_leanRight, right, ArcSpring.Snappy);
        }
        else { _leanLeft.Snap(left); _leanRight.Snap(right); Layout(); }
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        int index = _views.FindIndex(v => v.Hit.IsKeyboardFocused);
        if (index < 0) { return; }
        int target = ToolbarKeys.Next(_views.Select(_ => true).ToList(), index, e.Key, false);
        if (target < 0) { return; }
        e.Handled = true;
        SetCurrentValue(SelectedIdProperty, _views[target].Tab.Id);
        _views[target].Hit.Focus();
    }

    private void ShowRing(TabView? view)
    {
        _focused = view;
        if (_ring is null) { return; }
        _ring.Visibility = view is null ? Visibility.Collapsed : Visibility.Visible;
        Layout();
    }

    private static ControlTemplate? _hitTemplate;

    private static ControlTemplate HitTemplate()
    {
        if (_hitTemplate is not null) { return _hitTemplate; }
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Stretch);
        border.AppendChild(content);
        _hitTemplate = new ControlTemplate(typeof(RadioButton)) { VisualTree = border };
        _hitTemplate.Seal();
        return _hitTemplate;
    }

    private sealed class TabView
    {
        private readonly LiquidTabBar _owner;
        private readonly ThemedBrush _ink;
        private readonly OwnedMotion _motion = new();
        private readonly TextBlock _baseLabel;
        private readonly TranslateTransform _labelMove = new(-4, 0);
        private bool? _shown;

        public LiquidTab Tab { get; }
        public RadioButton Hit { get; }
        public Canvas LensPart { get; } = new() { Height = 40 };
        public TextBlock LensLabel { get; }
        public ControlSpring Width { get; }
        public double WideWidth { get; }
        public bool Selected { get; set; }

        public TabView(LiquidTabBar owner, LiquidTab tab)
        {
            _owner = owner;
            Tab = tab;
            Width = owner._driver.Add(LiquidTabMath.Slot, ArcSpring.Of(WidthSeconds));
            _ink = new ThemedBrush(owner, _motion);
            double labelX = tab.Count > 0 ? 46 : 40;

            // The plain row: outlined icons, and labels only when every tab shows one.
            _baseLabel = ArcText.Block(tab.Text, 14, 14, medium: true);
            _baseLabel.Foreground = _ink.Brush;
            _baseLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            WideWidth = LiquidTabMath.TabWidth(_baseLabel.DesiredSize.Width, tab.Count > 0);
            Canvas.SetLeft(_baseLabel, labelX);
            Canvas.SetTop(_baseLabel, 13);
            var icon = new ArcDataGridIcon { Icon = tab.Icon, Size = 20, Stroke = _ink.Brush };
            Canvas.SetLeft(icon, 12);
            Canvas.SetTop(icon, 10);
            var face = new Canvas { Children = { icon, _baseLabel } };
            if (tab.Count > 0) { face.Children.Add(Badge(tab.Count, false)); }
            Hit = new RadioButton { OverridesDefaultStyle = true, Template = HitTemplate(), Height = 40, Content = face, Cursor = Cursors.Hand, FocusVisualStyle = null, IsTabStop = true };
            Canvas.SetTop(Hit, LiquidTabMath.Pad);
            AutomationProperties.SetName(Hit, tab.Count > 0 ? $"{tab.Text}, {tab.Count} unread" : tab.Text);
            Hit.Checked += (_, _) => { if (_owner.SelectedId != Tab.Id) { _owner.SetCurrentValue(SelectedIdProperty, Tab.Id); } };
            Hit.MouseEnter += (_, _) => { _owner.Lean(this); Refresh(true); };
            Hit.MouseLeave += (_, _) => Refresh(true);
            Hit.GotKeyboardFocus += (_, _) => _owner.ShowRing(ThreadUi.FocusVisible(Hit) ? this : null);
            Hit.LostKeyboardFocus += (_, _) => { if (_owner._focused == this) { _owner.ShowRing(null); } };

            // The lens row: the same tab, filled and inverted.
            var filled = new ArcDataGridIcon { Icon = tab.Icon, Size = 20 };
            filled.SetResourceReference(ArcDataGridIcon.StrokeProperty, "ArcWpf.Brush.Background");
            filled.SetResourceReference(ArcDataGridIcon.FillProperty, "ArcWpf.Brush.Background");
            Canvas.SetLeft(filled, 12);
            Canvas.SetTop(filled, 10);
            LensLabel = ArcText.Block(tab.Text, 14, 14, "Background", true);
            LensLabel.RenderTransform = _labelMove;
            LensLabel.Opacity = 0;
            Canvas.SetLeft(LensLabel, labelX);
            Canvas.SetTop(LensLabel, 13);
            Canvas.SetTop(LensPart, LiquidTabMath.Pad);
            LensPart.Children.Add(filled);
            LensPart.Children.Add(LensLabel);
            if (tab.Count > 0) { LensPart.Children.Add(Badge(tab.Count, true)); }
        }

        private static FrameworkElement Badge(int count, bool inverted)
        {
            var number = new ThreadCount(1, true) { Height = 16, HorizontalAlignment = HorizontalAlignment.Center };
            TextElement(number, inverted);
            number.Set(count);
            var pill = new Border { MinWidth = 17, Height = 16, CornerRadius = new CornerRadius(8), Padding = new Thickness(5, 0, 5, 0), Child = number };
            pill.SetResourceReference(Border.BackgroundProperty, inverted ? "ArcWpf.Brush.Background" : "ArcWpf.Brush.Foreground");
            var ring = new Border { Padding = new Thickness(2), CornerRadius = new CornerRadius(10), Child = pill };
            ring.SetResourceReference(Border.BackgroundProperty, inverted ? "ArcWpf.Brush.Foreground" : "ArcWpf.Brush.SurfaceMuted");
            Canvas.SetLeft(ring, 23);
            Canvas.SetTop(ring, 3);
            return ring;
        }

        private static void TextElement(FrameworkElement element, bool inverted)
        {
            System.Windows.Documents.TextElement.SetFontSize(element, 11);
            System.Windows.Documents.TextElement.SetFontWeight(element, FontWeights.Medium);
            element.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, inverted ? "ArcWpf.Brush.Foreground" : "ArcWpf.Brush.Background");
        }

        public void Place(double x, double width)
        {
            Hit.Width = width;
            Canvas.SetLeft(Hit, x);
            Canvas.SetLeft(LensPart, x);
        }

        public void SettleLabel()
        {
            Tween.Set(LensLabel, UIElement.OpacityProperty, _shown == true ? 1 : 0);
            Tween.Set(_labelMove, TranslateTransform.XProperty, _shown == true ? 0 : -4);
        }

        public void Refresh(bool animate)
        {
            _ink.To(Hit.IsMouseOver || Selected ? "Foreground" : "TextSecondary", animate, Tween.Fast);
            var mode = _owner.ShowLabels;
            bool all = mode == LiquidTabLabels.All;
            bool show = mode != LiquidTabLabels.None && (all || Selected);
            _baseLabel.Visibility = all ? Visibility.Visible : Visibility.Hidden;
            if (_shown == show) { return; }
            _shown = show;
            if (!animate || !_owner.Moving)
            {
                Tween.Set(LensLabel, UIElement.OpacityProperty, show ? 1 : 0);
                Tween.Set(_labelMove, TranslateTransform.XProperty, show ? 0 : -4);
                return;
            }
            if (show)
            {
                // The label waits for the width to open: it would otherwise be clipped by the lens on its way in.
                Tween.To(LensLabel, UIElement.OpacityProperty, 1, LabelSeconds, ArcEasing.Enter, all ? 0 : LabelDelay);
                Tween.To(_labelMove, TranslateTransform.XProperty, 0, LabelSeconds, ArcEasing.Enter, all ? 0 : LabelDelay);
            }
            else
            {
                Tween.To(LensLabel, UIElement.OpacityProperty, 0, .08, ArcEasing.Standard);
                Tween.To(_labelMove, TranslateTransform.XProperty, -4, .08, ArcEasing.Standard);
            }
        }
    }
}
