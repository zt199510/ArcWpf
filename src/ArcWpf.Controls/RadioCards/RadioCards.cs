using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

public sealed class RadioCardOption
{
    public string Value { get; set; } = string.Empty;
    public object? Label { get; set; }
    public object? Description { get; set; }
    public object? Meta { get; set; }
    public object? Icon { get; set; }
    public bool Disabled { get; set; }
    public object? DisabledReason { get; set; }
}

public enum RadioCardsLayout { Grid, List }

/// <summary>Selectable option cards for choices that need more than a label. One ring glides from card to card on a spring and the group behaves as a radio group: one tab stop, arrow keys move and select.</summary>
[TemplatePart(Name = "PART_Items", Type = typeof(RadioCardsPanel))]
public class RadioCards : Control
{
    public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(nameof(Options), typeof(ObservableCollection<RadioCardOption>), typeof(RadioCards), new FrameworkPropertyMetadata(null, OnOptionsChanged));
    public static readonly DependencyProperty SelectedValueProperty = DependencyProperty.Register(nameof(SelectedValue), typeof(string), typeof(RadioCards), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedChanged));
    public static readonly DependencyProperty LayoutProperty = DependencyProperty.Register(nameof(Layout), typeof(RadioCardsLayout), typeof(RadioCards), new FrameworkPropertyMetadata(RadioCardsLayout.Grid, (d, _) => ((RadioCards)d).Rebuild()));
    public static readonly DependencyProperty MinColumnWidthProperty = DependencyProperty.Register(nameof(MinColumnWidth), typeof(double), typeof(RadioCards), new FrameworkPropertyMetadata(180d, (d, _) => ((RadioCards)d)._panel?.InvalidateMeasure(), (_, value) => Math.Max(1d, (double)value)));
    public static readonly DependencyProperty GroupLabelProperty = DependencyProperty.Register(nameof(GroupLabel), typeof(string), typeof(RadioCards), new FrameworkPropertyMetadata("Options", (d, e) => AutomationProperties.SetName(d, (string)e.NewValue)));
    public static readonly DependencyProperty IsGroupDisabledProperty = DependencyProperty.Register(nameof(IsGroupDisabled), typeof(bool), typeof(RadioCards), new FrameworkPropertyMetadata(false, (d, _) => ((RadioCards)d).Rebuild()));

    public ObservableCollection<RadioCardOption> Options { get => (ObservableCollection<RadioCardOption>)GetValue(OptionsProperty); set => SetValue(OptionsProperty, value); }
    public string? SelectedValue { get => (string?)GetValue(SelectedValueProperty); set => SetValue(SelectedValueProperty, value); }
    public RadioCardsLayout Layout { get => (RadioCardsLayout)GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }
    public double MinColumnWidth { get => (double)GetValue(MinColumnWidthProperty); set => SetValue(MinColumnWidthProperty, value); }
    public string GroupLabel { get => (string)GetValue(GroupLabelProperty); set => SetValue(GroupLabelProperty, value); }
    public bool IsGroupDisabled { get => (bool)GetValue(IsGroupDisabledProperty); set => SetValue(IsGroupDisabledProperty, value); }
    public event EventHandler? SelectedValueChanged;

    private RadioCardsPanel? _panel;
    private readonly Border _ring = new() { BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(20), IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Opacity = 0 };
    private readonly SpringDriver _driver;
    private readonly ControlSpring _x, _y, _w, _h;
    private readonly OwnedMotion _ringFade = new();
    private bool _placed;
    private Rect _box = Rect.Empty;

    static RadioCards() => DefaultStyleKeyProperty.OverrideMetadata(typeof(RadioCards), new FrameworkPropertyMetadata(typeof(RadioCards)));

    public RadioCards()
    {
        _driver = new SpringDriver(ApplyRing);
        _x = _driver.Add(0, ArcSpring.Morph);
        _y = _driver.Add(0, ArcSpring.Morph);
        _w = _driver.Add(0, ArcSpring.Morph);
        _h = _driver.Add(0, ArcSpring.Morph);
        _ring.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.ControlOn");
        _ring.RenderTransform = new TranslateTransform();
        Panel.SetZIndex(_ring, 1);
        AutomationProperties.SetName(this, GroupLabel);
        Options = [];
        Unloaded += (_, _) => _driver.Finish();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RadioCardsAutomationPeer(this);

    private static void OnOptionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var cards = (RadioCards)d;
        if (e.OldValue is INotifyCollectionChanged old) { old.CollectionChanged -= cards.OnCollectionChanged; }
        if (e.NewValue is INotifyCollectionChanged next) { next.CollectionChanged += cards.OnCollectionChanged; }
        cards.Rebuild();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private static void OnSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var cards = (RadioCards)d;
        cards.UpdateCards(true);
        cards.Place(true);
        cards.SelectedValueChanged?.Invoke(cards, EventArgs.Empty);
    }

    internal bool IsUsable(RadioCardOption option) => !IsGroupDisabled && !option.Disabled;

    public void SelectOption(RadioCardOption option)
    {
        if (!IsUsable(option) || SelectedValue == option.Value) { return; }
        SelectedValue = option.Value;
    }

    internal bool IsOptionTabStop(RadioCardOption option)
    {
        if (!IsUsable(option)) { return false; }
        var selected = Options.FirstOrDefault(candidate => candidate.Value == SelectedValue);
        return ReferenceEquals(selected is not null && IsUsable(selected) ? selected : Options.FirstOrDefault(IsUsable), option);
    }

    public override void OnApplyTemplate()
    {
        _panel?.Children.Remove(_ring);
        base.OnApplyTemplate();
        _panel = GetTemplateChild("PART_Items") as RadioCardsPanel;
        if (_panel is not null) { _panel.Owner = this; }
        Rebuild();
    }

    private void Rebuild()
    {
        if (_panel is null) { return; }
        _panel.Children.Clear();
        _panel.Children.Add(_ring);
        foreach (var option in Options ?? []) { _panel.Children.Add(new RadioCardItem(this, option)); }
        UpdateCards(false);
        _placed = false;
        _panel.InvalidateMeasure();
    }

    private IEnumerable<RadioCardItem> Cards => _panel?.Children.OfType<RadioCardItem>() ?? [];

    private void UpdateCards(bool animate)
    {
        foreach (var card in Cards) { card.Sync(animate); }
    }

    /// <summary>Runs after the panel arranges. A resize snaps the ring; only a selection change glides it.</summary>
    internal void OnPanelArranged()
    {
        var box = Cards.FirstOrDefault(item => item.Option.Value == SelectedValue)?.Box ?? Rect.Empty;
        // The ring's own size changes re-arrange the panel while it glides; only a moved card counts as a resize.
        if (_placed && box == _box) { return; }
        Place(!_placed);
    }

    private void Place(bool spring)
    {
        if (_panel is null) { return; }
        var card = Cards.FirstOrDefault(item => item.Option.Value == SelectedValue);
        if (card is null || card.Box.Width <= 0) { _ringFade.Stop(); _ring.Opacity = 0; _placed = false; return; }
        _box = card.Box;
        // Measured like offsetLeft and offsetWidth, so a 1.5 px ring lands on whole pixels instead of smearing.
        var box = new Rect(Math.Round(card.Box.X), Math.Round(card.Box.Y), Math.Round(card.Box.Width), Math.Round(card.Box.Height));
        bool reduced = ArcMotion.GetLevel(this) != ArcMotionLevel.Full;
        if (!spring || !_placed || reduced || !IsLoaded)
        {
            _driver.Stop();
            _x.Snap(box.X); _y.Snap(box.Y); _w.Snap(box.Width); _h.Snap(box.Height);
            ApplyRing();
            if (!_placed && !reduced && spring) { _ring.Opacity = 0; _ringFade.Double(_ring, OpacityProperty, 1, .16, true); }
            else if (!_ringFade.IsRunning) { _ring.Opacity = 1; }
            _placed = true;
            return;
        }
        _ring.Opacity = 1;
        _driver.Animate(_x, box.X, ArcSpring.Morph);
        _driver.Animate(_y, box.Y, ArcSpring.Morph);
        _driver.Animate(_w, box.Width, ArcSpring.Morph);
        _driver.Animate(_h, box.Height, ArcSpring.Morph);
    }

    private void ApplyRing()
    {
        var move = (TranslateTransform)_ring.RenderTransform;
        move.X = _x.Position;
        move.Y = _y.Position;
        _ring.Width = Math.Max(0, _w.Position);
        _ring.Height = Math.Max(0, _h.Position);
    }

    internal Rect RingBox => new(_x.Position, _y.Position, _w.Position, _h.Position);
    internal double RingOpacity => _ring.Opacity;
    internal void AdvanceRing(double seconds) => _driver.Advance(seconds);

    internal void HandleKey(KeyEventArgs e, RadioCardItem card)
    {
        if (e.Key is Key.Space or Key.Enter) { e.Handled = true; SelectOption(card.Option); return; }
        int step = e.Key is Key.Right or Key.Down ? 1 : e.Key is Key.Left or Key.Up ? -1 : 0;
        if (step == 0 && e.Key is not (Key.Home or Key.End)) { return; }
        e.Handled = true;
        int count = Options.Count, at = e.Key == Key.Home ? -1 : e.Key == Key.End ? count : Options.IndexOf(card.Option);
        int direction = e.Key == Key.Home ? 1 : e.Key == Key.End ? -1 : FlowDirection == FlowDirection.RightToLeft && e.Key is Key.Left or Key.Right ? -step : step;
        for (int tries = 0; tries < count; tries++)
        {
            at = (at + direction + count) % count;
            if (!IsUsable(Options[at])) { continue; }
            SelectOption(Options[at]);
            Cards.ElementAt(at).Focus();
            return;
        }
    }
}

/// <summary>CSS repeat(auto-fill, minmax(min, 1fr)) for grid layout, a single column for list, with stretched row heights so prices share a baseline.</summary>
public sealed class RadioCardsPanel : Panel
{
    internal RadioCards? Owner { get; set; }
    private int _columns = 1;
    private double[] _rows = [];

    private double Gap => Owner?.Layout == RadioCardsLayout.List ? 8 : 10;

    protected override Size MeasureOverride(Size availableSize)
    {
        var cards = InternalChildren.OfType<RadioCardItem>().ToList();
        double width = double.IsInfinity(availableSize.Width) ? 520 : availableSize.Width;
        double min = Math.Min(width, Owner?.MinColumnWidth ?? 180);
        _columns = Owner?.Layout == RadioCardsLayout.List ? 1 : Math.Max(1, (int)Math.Floor((width + Gap) / (min + Gap)));
        double column = (width - Gap * (_columns - 1)) / _columns;
        _rows = new double[(int)Math.Ceiling(cards.Count / (double)_columns)];
        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].Measure(new Size(column, double.PositiveInfinity));
            _rows[i / _columns] = Math.Max(_rows[i / _columns], cards[i].DesiredSize.Height);
        }
        foreach (UIElement child in InternalChildren) { if (child is not RadioCardItem) { child.Measure(new Size(width, double.PositiveInfinity)); } }
        return new Size(width, _rows.Sum() + Gap * Math.Max(0, _rows.Length - 1));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var cards = InternalChildren.OfType<RadioCardItem>().ToList();
        double column = (finalSize.Width - Gap * (_columns - 1)) / _columns;
        for (int i = 0; i < cards.Count; i++)
        {
            int row = i / _columns, col = i % _columns;
            double y = _rows.Take(row).Sum() + Gap * row;
            cards[i].Box = new Rect((column + Gap) * col, y, column, _rows[row]);
            cards[i].Arrange(cards[i].Box);
        }
        foreach (UIElement child in InternalChildren) { if (child is not RadioCardItem) { child.Arrange(new Rect(0, 0, child.DesiredSize.Width, child.DesiredSize.Height)); } }
        Owner?.OnPanelArranged();
        return finalSize;
    }
}

/// <summary>One option card. Focus and input live here; the group owns selection and the ring.</summary>
public sealed class RadioCardItem : ContentControl
{
    private readonly RadioCards _owner;
    private readonly OwnedMotion _motion = new();
    private readonly ThemedBrush _fill, _edge, _indicatorFill, _indicatorEdge, _iconStroke;
    private readonly ScaleTransform _dotScale = new(0, 0, 3, 3);
    private readonly SpringDriver _driver;
    private readonly ControlSpring _dot;
    private readonly Border _frame;
    private readonly Border _indicator;
    private readonly TextBlock? _label, _meta;
    internal RadioCards Owner => _owner;
    internal RadioCardOption Option { get; }
    internal Rect Box { get; set; }
    internal bool IsChecked => _owner.SelectedValue == Option.Value;
    private bool Off => !_owner.IsUsable(Option);

    internal RadioCardItem(RadioCards owner, RadioCardOption option)
    {
        _owner = owner;
        Option = option;
        OverridesDefaultStyle = true;
        Template = CardTemplate;
        FocusVisualStyle = null;
        _fill = new ThemedBrush(this, _motion);
        _edge = new ThemedBrush(this, _motion);
        _indicatorFill = new ThemedBrush(this, _motion);
        _indicatorEdge = new ThemedBrush(this, _motion);
        _iconStroke = new ThemedBrush(this, _motion);
        _driver = new SpringDriver(() => _dotScale.ScaleX = _dotScale.ScaleY = Math.Max(0, _dot.Position));
        _dot = _driver.Add(0, ArcSpring.Snappy);
        bool list = owner.Layout == RadioCardsLayout.List;

        var dot = new Border { Width = 6, Height = 6, CornerRadius = new CornerRadius(3), RenderTransform = _dotScale, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        dot.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.ControlGlyph");
        _indicator = new Border { Width = 18, Height = 18, BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(9), Child = dot, Background = _indicatorFill.Brush, BorderBrush = _indicatorEdge.Brush, Margin = new Thickness(0, list ? 0 : 1, 0, 0) };

        var labelRow = new DockPanel { LastChildFill = true };
        if (option.Icon is string icon)
        {
            var glyph = ArcText.Icon(icon, 18, _iconStroke.Brush);
            glyph.Margin = new Thickness(0, 0, 8, 0);
            DockPanel.SetDock(glyph, Dock.Left);
            labelRow.Children.Add(glyph);
        }
        else if (option.Icon is UIElement custom) { labelRow.Children.Add(new ContentPresenter { Content = custom, Width = 18, Height = 18, Margin = new Thickness(0, 0, 8, 0) }); }
        var labelElement = Part(option.Label, 14, 19.6, "Foreground", true, out _label);
        labelRow.Children.Add(labelElement);
        var body = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
        body.Children.Add(labelRow);
        var description = !_owner.IsUsable(option) && option.DisabledReason is not null ? option.DisabledReason : option.Description;
        if (description is not null)
        {
            var text = Part(description, 12, 16.8, "TextSecondary", false, out var block);
            if (block is not null) { block.TextTrimming = TextTrimming.None; block.TextWrapping = TextWrapping.Wrap; }
            text.Margin = new Thickness(0, 2, 0, 0);
            body.Children.Add(text);
        }

        var grid = new Grid();
        if (list)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _indicator.Margin = new Thickness(0, 0, 12, 0);
            _indicator.VerticalAlignment = VerticalAlignment.Center;
            body.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(body, 1);
            grid.Children.Add(_indicator);
            grid.Children.Add(body);
            if (option.Meta is not null)
            {
                var meta = Part(option.Meta, 14, 19.6, "Foreground", true, out _meta);
                meta.Margin = new Thickness(12, 0, 0, 0);
                meta.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(meta, 2);
                grid.Children.Add(meta);
            }
        }
        else
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _indicator.Margin = new Thickness(12, 1, 0, 0);
            _indicator.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetColumn(_indicator, 1);
            grid.Children.Add(body);
            grid.Children.Add(_indicator);
            if (option.Meta is not null)
            {
                var meta = Part(option.Meta, 18, 25.2, "Foreground", false, out _meta);
                meta.Margin = new Thickness(0, 14, 0, 0);
                Grid.SetRow(meta, 1);
                Grid.SetColumnSpan(meta, 2);
                grid.Children.Add(meta);
            }
        }
        _frame = new Border { CornerRadius = new CornerRadius(20), BorderThickness = new Thickness(1), Background = _fill.Brush, BorderBrush = _edge.Brush, Padding = list ? new Thickness(14, 13, 16, 13) : new Thickness(16, 14, 16, 14), Child = grid };
        Content = _frame;
        AutomationProperties.SetName(this, (option.Label as string) ?? option.Value);
        if (description is string help) { AutomationProperties.SetHelpText(this, help); }
        Loaded += (_, _) => Sync(false);
        Unloaded += (_, _) => { _motion.Stop(); _driver.Finish(); };
        ThemeWatch.Add(this, () => Sync(false));
        MouseEnter += (_, _) => Sync(true);
        MouseLeave += (_, _) => Sync(true);
    }

    private static readonly ControlTemplate CardTemplate = BuildTemplate();
    private static ControlTemplate BuildTemplate()
    {
        var template = new ControlTemplate(typeof(RadioCardItem)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) };
        template.Seal();
        return template;
    }

    private static FrameworkElement Part(object? value, double size, double lineHeight, string role, bool medium, out TextBlock? block)
    {
        if (value is string text || value is null)
        {
            block = ArcText.Block(value as string ?? string.Empty, size, lineHeight, role, medium);
            block.TextTrimming = TextTrimming.CharacterEllipsis;
            return block;
        }
        block = null;
        var presenter = new ContentPresenter { Content = value };
        TextElement.SetFontSize(presenter, size);
        return presenter;
    }

    internal void Sync(bool animate)
    {
        bool off = Off, chosen = IsChecked, focused = IsKeyboardFocused && InputManager.Current.MostRecentInputDevice is KeyboardDevice;
        IsTabStop = _owner.IsOptionTabStop(Option);
        Focusable = !off;
        Cursor = off ? Cursors.No : Cursors.Hand;
        _fill.To(chosen ? "RadioCardChecked" : off || _pressed || focused ? "SurfaceMuted" : "Surface", animate);
        _edge.To(!chosen && !off && (IsMouseOver || focused) ? "BorderStrong" : "Border", animate);
        _indicatorFill.To(chosen ? "ControlOn" : null, animate);
        _indicatorEdge.To(chosen ? "ControlOn" : off ? "Border" : "BorderStrong", animate);
        _iconStroke.To(chosen ? "Foreground" : "TextSecondary", animate);
        if (_label is not null) { _label.SetResourceReference(TextBlock.ForegroundProperty, off ? "ArcWpf.Brush.TextMuted" : "ArcWpf.Brush.Foreground"); }
        if (_meta is not null) { _meta.SetResourceReference(TextBlock.ForegroundProperty, off ? "ArcWpf.Brush.TextMuted" : "ArcWpf.Brush.Foreground"); }
        double scale = chosen ? 1 : 0;
        if (animate && IsLoaded && ArcMotion.GetLevel(this) == ArcMotionLevel.Full) { _driver.Animate(_dot, scale, ArcSpring.Snappy); }
        else { _driver.Snap(_dot, scale); _dotScale.ScaleX = _dotScale.ScaleY = scale; }
    }

    private bool _pressed;
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (Off) { return; }
        _pressed = true;
        CaptureMouse();
        Sync(true);
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_pressed) { return; }
        _pressed = false;
        ReleaseMouseCapture();
        bool inside = new Rect(RenderSize).Contains(e.GetPosition(this));
        if (inside) { _owner.SelectOption(Option); Focus(); }
        Sync(true);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled) { _owner.HandleKey(e, this); }
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnGotKeyboardFocus(e); Sync(true); }
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnLostKeyboardFocus(e); Sync(true); }
    protected override AutomationPeer OnCreateAutomationPeer() => new RadioCardItemAutomationPeer(this);
}

internal sealed class RadioCardsAutomationPeer(RadioCards owner) : FrameworkElementAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
}

internal sealed class RadioCardItemAutomationPeer(RadioCardItem owner) : FrameworkElementAutomationPeer(owner), ISelectionItemProvider
{
    private readonly RadioCardItem _item = owner;
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.RadioButton;
    public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.SelectionItem ? this : base.GetPattern(patternInterface);
    public bool IsSelected => _item.IsChecked;
    public IRawElementProviderSimple? SelectionContainer => null;
    public void AddToSelection() => Select();
    public void RemoveFromSelection() { }
    public void Select() => _item.Owner.SelectOption(_item.Option);
}
