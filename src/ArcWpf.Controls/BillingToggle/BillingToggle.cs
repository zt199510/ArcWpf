using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace ArcWpf.Controls;

public sealed class BillingToggleOption
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    /// <summary>Short savings note, such as "Save 20%".</summary>
    public string? Badge { get; set; }
    /// <summary>Badge text once this option is selected. Defaults to <see cref="Badge"/>.</summary>
    public string? ActiveBadge { get; set; }
}

public enum BillingToggleSize { Medium, Large }

/// <summary>Text that swaps in place. Every candidate sizes the same cell, so nothing around it moves when the text changes.</summary>
internal sealed class StableSwap : Grid
{
    private TextBlock? _shown;
    public double Em { get; init; } = 12;
    public Func<string, TextBlock> Make { get; init; } = text => new TextBlock { Text = text };

    public void SetCandidates(IEnumerable<string> candidates)
    {
        foreach (var sizer in Children.OfType<TextBlock>().Where(block => block.Visibility == Visibility.Hidden).ToArray()) { Children.Remove(sizer); }
        foreach (var candidate in candidates.Distinct()) { var sizer = Make(candidate); sizer.Visibility = Visibility.Hidden; sizer.HorizontalAlignment = HorizontalAlignment.Center; Children.Insert(0, sizer); }
    }

    public string Text => _shown?.Text ?? string.Empty;

    public void Swap(string text, bool animate, bool full)
    {
        if (_shown?.Text == text) { return; }
        var old = _shown;
        var next = Make(text);
        next.HorizontalAlignment = HorizontalAlignment.Center;
        Children.Add(next);
        _shown = next;
        if (!animate || old is null)
        {
            if (old is not null) { Children.Remove(old); }
            return;
        }
        if (full)
        {
            // swap variants: .45em and a subtle blur, standard in on ease-enter, fast out on ease-standard.
            Tween.Present(next, true, .45 * Em, 2, Tween.Standard, ArcEasing.Enter);
            Tween.Present(old, false, -.45 * Em, 2, Tween.Fast, ArcEasing.Standard, done: () => Children.Remove(old));
        }
        else
        {
            next.Opacity = 0;
            Tween.To(next, OpacityProperty, 1, Tween.Fast, null);
            Children.Remove(old);
        }
    }
}

/// <summary>A billing period switch: one thumb glides between the options and the savings note tints and rewrites itself once chosen.</summary>
public class BillingToggle : Control
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(BillingToggle), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => { var toggle = (BillingToggle)d; toggle.OnValue(); toggle.RaiseEvent(new RoutedPropertyChangedEventArgs<string>((string)e.OldValue, (string)e.NewValue, ValueChangedEvent)); }));
    public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(nameof(Options), typeof(IEnumerable), typeof(BillingToggle), new PropertyMetadata(null, (d, _) => ((BillingToggle)d).Rebuild()));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(BillingToggle), new PropertyMetadata("Billing period", (d, e) => AutomationProperties.SetName(d, (string)e.NewValue)));
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(BillingToggleSize), typeof(BillingToggle), new PropertyMetadata(BillingToggleSize.Medium, (d, _) => ((BillingToggle)d).Rebuild()));
    public static readonly RoutedEvent ValueChangedEvent = EventManager.RegisterRoutedEvent(nameof(ValueChanged), RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<string>), typeof(BillingToggle));

    private static readonly BillingToggleOption[] Defaults = [new() { Value = "monthly", Label = "Monthly" }, new() { Value = "yearly", Label = "Yearly", Badge = "Save 20%" }];
    // const glide: critically damped, visualDuration .34.
    private const double Glide = .34;
    private readonly string _group = "BillingToggle" + Guid.NewGuid().ToString("N");
    private readonly List<OptionView> _views = [];
    private readonly OwnedMotion _motion = new();
    private readonly Border _thumb = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Stretch };
    private readonly TranslateTransform _thumbMove = new();
    private readonly StackPanel _row = new() { Orientation = Orientation.Horizontal };
    private readonly SpringDriver _driver;
    private readonly ControlSpring _x, _width;
    private Decorator? _root;
    private bool _placed;

    static BillingToggle() => DefaultStyleKeyProperty.OverrideMetadata(typeof(BillingToggle), new FrameworkPropertyMetadata(typeof(BillingToggle)));

    public BillingToggle()
    {
        _driver = new SpringDriver(ApplyThumb);
        _x = _driver.Add(0, Glide);
        _width = _driver.Add(0, Glide);
        AutomationProperties.SetName(this, "Billing period");
        KeyboardNavigation.SetDirectionalNavigation(this, KeyboardNavigationMode.None);
        Unloaded += (_, _) => { _driver.Finish(); _motion.Stop(); };
        ThemeWatch.Add(this, ApplyTheme);
    }

    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public IEnumerable? Options { get => (IEnumerable?)GetValue(OptionsProperty); set => SetValue(OptionsProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public BillingToggleSize Size { get => (BillingToggleSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public event RoutedPropertyChangedEventHandler<string> ValueChanged { add => AddHandler(ValueChangedEvent, value); remove => RemoveHandler(ValueChangedEvent, value); }

    internal double ThumbTargetXForTest => _x.Target;
    internal double ThumbTargetWidthForTest => _width.Target;
    internal string BadgeTextForTest(int index) => _views[index].Swap?.Text ?? string.Empty;
    internal RadioButton OptionForTest(int index) => _views[index].Button;

    private IReadOnlyList<BillingToggleOption> Items => Options?.Cast<BillingToggleOption>().ToArray() ?? Defaults;
    private double RowHeight => Size == BillingToggleSize.Large ? 42 : 34;
    private double Pad => Size == BillingToggleSize.Large ? 4 : 3;
    private int SelectedIndex => Math.Max(0, Items.ToList().FindIndex(option => option.Value == Value));

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _root = GetTemplateChild("PART_Root") as Decorator;
        Rebuild();
    }

    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        var result = base.ArrangeOverride(arrangeBounds);
        Dispatcher.BeginInvoke(MeasureThumb, System.Windows.Threading.DispatcherPriority.Loaded);
        return result;
    }

    private void Rebuild()
    {
        if (_root is null) { return; }
        _row.Children.Clear();
        _views.Clear();
        var items = Items;
        double fontSize = Size == BillingToggleSize.Large ? 16 : 14;
        foreach (var (option, index) in items.Select((option, index) => (option, index)))
        {
            var view = new OptionView(this, option, RowHeight, fontSize, _group);
            _views.Add(view);
            _row.Children.Add(view.Button);
            int at = index;
            view.Button.Click += (_, _) => Select(at, false);
            // UI Automation's Select sets IsChecked without a click.
            view.Button.Checked += (_, _) => { var list = Items; if (at < list.Count && Value != list[at].Value) { Select(at, false); } };
            view.Button.PreviewKeyDown += (_, e) => OnOptionKey(e, at);
        }
        _thumb.CornerRadius = new CornerRadius((RowHeight + 2) / 2);
        _thumb.Margin = new Thickness(-1, Pad - 1, 0, Pad - 1);
        _thumb.RenderTransform = _thumbMove;
        _thumb.BorderThickness = new Thickness(1);
        _thumb.SnapsToDevicePixels = false;
        _thumb.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceRaised");
        _thumb.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderSubtle");
        var grid = new Grid();
        grid.Children.Add(_thumb);
        _row.Margin = new Thickness(Pad);
        _row.SnapsToDevicePixels = false;
        grid.Children.Add(_row);
        var track = new Border { Child = grid, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius((RowHeight + 2 * Pad + 2) / 2), HorizontalAlignment = HorizontalAlignment.Left };
        track.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderSubtle");
        track.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
        _root.Child = track;
        _placed = false;
        ApplyTheme();
        Refresh(false);
    }

    private void ApplyTheme()
    {
        bool dark = Tween.Resolve(this, "ArcWpf.Brush.Surface").R < 128;
        _thumb.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = dark ? 2 : 6, ShadowDepth = dark ? 1 : 2, Direction = 270, Opacity = dark ? .3 : .05 };
        _thumb.SetResourceReference(Border.BorderBrushProperty, dark ? "ArcWpf.Brush.Border" : "ArcWpf.Brush.BorderSubtle");
        Refresh(false);
    }

    private void OnValue()
    {
        Refresh(IsLoaded);
        MeasureThumb();
    }

    private void Select(int index, bool focus)
    {
        var items = Items;
        if (index < 0 || index >= items.Count) { return; }
        string next = items[index].Value;
        if (focus) { _views[index].Button.Focus(); }
        if (Value == next) { Refresh(true); return; }
        SetCurrentValue(ValueProperty, next);
    }

    private void OnOptionKey(KeyEventArgs e, int index)
    {
        int count = _views.Count;
        int target = e.Key switch
        {
            Key.Home => 0,
            Key.End => count - 1,
            Key.Right or Key.Down => (index + 1) % count,
            Key.Left or Key.Up => (index - 1 + count) % count,
            _ => -1,
        };
        if (target < 0) { return; }
        e.Handled = true;
        Select(target, true);
    }

    private void Refresh(bool animate)
    {
        if (_views.Count == 0) { return; }
        int selected = SelectedIndex;
        bool full = Tween.Level(this) == ArcMotionLevel.Full;
        for (int i = 0; i < _views.Count; i++) { _views[i].Update(i == selected, animate && IsLoaded, full); }
    }

    private void MeasureThumb()
    {
        if (_views.Count == 0 || !_row.IsVisible && !IsLoaded) { return; }
        var button = _views[SelectedIndex].Button;
        if (button.ActualWidth <= 0 || _thumb.Parent is not Grid grid) { return; }
        double x = button.TranslatePoint(new Point(0, 0), grid).X, width = button.ActualWidth;
        bool animate = _placed && IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;
        if (animate) { _driver.Animate(_x, x, Glide); _driver.Animate(_width, width, Glide); }
        else { _x.Snap(x); _width.Snap(width); ApplyThumb(); }
        _placed = true;
    }

    private void ApplyThumb()
    {
        _thumbMove.X = _x.Position;
        // The ring is part of the thumb's box here: box-shadow 0 0 0 1px.
        _thumb.Width = Math.Max(0, _width.Position + 2);
    }

    private sealed class OptionView
    {
        private readonly BillingToggle _owner;
        private readonly BillingToggleOption _option;
        private readonly ThemedBrush _ink;
        private readonly ThemedBrush? _badgeFill, _badgeInk;
        private readonly double _em;
        public RadioButton Button { get; }
        public StableSwap? Swap { get; }

        public OptionView(BillingToggle owner, BillingToggleOption option, double height, double fontSize, string group)
        {
            _owner = owner;
            _option = option;
            Button = new RadioButton { GroupName = group, Height = height, Cursor = Cursors.Hand, OverridesDefaultStyle = true, Template = OptionTemplate, FocusVisualStyle = ArcChromeButton.FocusFor(height / 2), SnapsToDevicePixels = false };
            AutomationProperties.SetName(Button, option.Label);
            _ink = new ThemedBrush(Button, owner._motion);
            Button.Foreground = _ink.Brush;
            bool large = owner.Size == BillingToggleSize.Large;
            bool hasBadge = !string.IsNullOrEmpty(option.Badge) || !string.IsNullOrEmpty(option.ActiveBadge);
            Button.Padding = hasBadge ? new Thickness(large ? 18 : 14, 0, large ? 6 : 5, 0) : new Thickness(large ? 18 : 14, 0, large ? 18 : 14, 0);
            var content = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, RenderTransformOrigin = new Point(.5, .5) };
            var press = new ScaleTransform();
            content.RenderTransform = press;
            content.Children.Add(new TextBlock { Text = option.Label, FontSize = fontSize, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center });
            Button.PreviewMouseLeftButtonDown += (_, _) => { if (Button.IsChecked != true) { Tween.Press(press, true, .97, Tween.Level(owner) == ArcMotionLevel.Full, Tween.Fast); } };
            Button.PreviewMouseLeftButtonUp += (_, _) => Tween.Set(press, ScaleTransform.ScaleXProperty, 1);
            Button.PreviewMouseLeftButtonUp += (_, _) => Tween.Set(press, ScaleTransform.ScaleYProperty, 1);
            Button.MouseLeave += (_, _) => { Tween.Set(press, ScaleTransform.ScaleXProperty, 1); Tween.Set(press, ScaleTransform.ScaleYProperty, 1); RefreshInk(true); };
            Button.MouseEnter += (_, _) => RefreshInk(true);
            if (hasBadge)
            {
                _em = 12;
                _badgeFill = new ThemedBrush(Button, owner._motion);
                _badgeInk = new ThemedBrush(Button, owner._motion);
                Swap = new StableSwap { Em = _em, Make = text => new TextBlock { Text = text, FontSize = 12, FontWeight = FontWeights.Medium, TextAlignment = TextAlignment.Center } };
                Swap.SetCandidates(new[] { option.Badge, option.ActiveBadge }.Where(text => !string.IsNullOrEmpty(text))!);
                var badge = new Border { Height = height - 10, Padding = new Thickness(8, 0, 8, 0), Margin = new Thickness(8, 0, 0, 0), CornerRadius = new CornerRadius((height - 10) / 2), Background = _badgeFill.Brush, Child = Swap, VerticalAlignment = VerticalAlignment.Center };
                TextElement.SetForeground(badge, _badgeInk.Brush);
                Swap.VerticalAlignment = VerticalAlignment.Center;
                content.Children.Add(badge);
            }
            Button.Content = content;
        }

        private void RefreshInk(bool animate)
        {
            bool selected = Button.IsChecked == true;
            _ink.To(selected ? "Foreground" : Button.IsMouseOver ? "TextSecondary" : "TextMuted", animate && _owner.IsLoaded);
        }

        public void Update(bool selected, bool animate, bool full)
        {
            Button.IsChecked = selected;
            Button.IsTabStop = selected;
            Button.Cursor = selected ? Cursors.Arrow : Cursors.Hand;
            RefreshInk(animate);
            _badgeFill?.To(selected ? "BillingBadgeActive" : "BillingBadge", animate, Tween.Standard);
            _badgeInk?.To(selected ? "BillingBadgeActiveInk" : "TextSecondary", animate, Tween.Standard);
            string? text = selected ? _option.ActiveBadge ?? _option.Badge : _option.Badge;
            if (Swap is not null && !string.IsNullOrEmpty(text)) { Swap.Swap(text, animate, full); }
        }

        private static readonly ControlTemplate OptionTemplate = BuildTemplate();
        private static ControlTemplate BuildTemplate()
        {
            var frame = new FrameworkElementFactory(typeof(Border));
            frame.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            frame.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding(nameof(Padding)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            frame.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
            var template = new ControlTemplate(typeof(RadioButton)) { VisualTree = frame };
            template.Seal();
            return template;
        }
    }
}

/// <summary>A price that rolls to its new amount. The old price and the period ease their width, so nothing beside them jumps.</summary>
public class BillingPrice : Control
{
    public static readonly DependencyProperty AmountProperty = DependencyProperty.Register(nameof(Amount), typeof(double), typeof(BillingPrice), new PropertyMetadata(0d, (d, _) => ((BillingPrice)d).Refresh(true)));
    public static readonly DependencyProperty CurrencyProperty = DependencyProperty.Register(nameof(Currency), typeof(string), typeof(BillingPrice), new PropertyMetadata("$", (d, _) => ((BillingPrice)d).Refresh(true)));
    public static readonly DependencyProperty PeriodProperty = DependencyProperty.Register(nameof(Period), typeof(string), typeof(BillingPrice), new PropertyMetadata(null, (d, _) => ((BillingPrice)d).Refresh(true)));
    public static readonly DependencyProperty WasProperty = DependencyProperty.Register(nameof(Was), typeof(double?), typeof(BillingPrice), new PropertyMetadata(null, (d, _) => ((BillingPrice)d).Refresh(true)));
    public static readonly DependencyProperty DecimalsProperty = DependencyProperty.Register(nameof(Decimals), typeof(int), typeof(BillingPrice), new PropertyMetadata(0, (d, _) => ((BillingPrice)d).Refresh(true)));
    public static readonly DependencyProperty AmountSizeProperty = DependencyProperty.Register(nameof(AmountSize), typeof(double), typeof(BillingPrice), new PropertyMetadata(36d, (d, _) => ((BillingPrice)d).ApplySize()));

    // const clip: visualDuration .36, bounce 0.
    private const double Clip = .36;
    private readonly OdometerNumber _number = new() { RevealOnLoad = false, Tracking = -.03 };
    private readonly TextBlock _currency = new();
    private readonly TextBlock _was = new() { TextDecorations = System.Windows.TextDecorations.Strikethrough, Margin = new Thickness(0, 0, 6, 0) };
    private readonly Grid _wasSlot = new() { ClipToBounds = true };
    private readonly Grid _periodSlot = new() { ClipToBounds = true };
    private readonly StableSwap _period;
    private readonly SpringDriver _driver;
    private readonly ControlSpring _wasWidth, _periodWidth;
    private readonly StackPanel _amount = new() { Orientation = Orientation.Horizontal };
    private Decorator? _root;
    private string? _shownPeriod;
    private bool _wasShown;
    private bool _built;

    static BillingPrice() => DefaultStyleKeyProperty.OverrideMetadata(typeof(BillingPrice), new FrameworkPropertyMetadata(typeof(BillingPrice)));

    public BillingPrice()
    {
        _period = new StableSwap { Em = 14, Make = text => new TextBlock { Text = text, FontSize = 14, LineHeight = 19.6, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, HorizontalAlignment = HorizontalAlignment.Left } };
        _driver = new SpringDriver(ApplyWidths);
        _wasWidth = _driver.Add(0, Clip);
        _periodWidth = _driver.Add(0, Clip);
        Loaded += (_, _) => { _number.Reveal(); Refresh(false); };
        Unloaded += (_, _) => _driver.Finish();
    }

    public double Amount { get => (double)GetValue(AmountProperty); set => SetValue(AmountProperty, value); }
    public string Currency { get => (string)GetValue(CurrencyProperty); set => SetValue(CurrencyProperty, value); }
    public string? Period { get => (string?)GetValue(PeriodProperty); set => SetValue(PeriodProperty, value); }
    /// <summary>The previous price, struck through when it is higher than <see cref="Amount"/>.</summary>
    public double? Was { get => (double?)GetValue(WasProperty); set => SetValue(WasProperty, value); }
    public int Decimals { get => (int)GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }
    /// <summary>--text-3xl by default.</summary>
    public double AmountSize { get => (double)GetValue(AmountSizeProperty); set => SetValue(AmountSizeProperty, value); }

    internal bool WasVisibleForTest => _wasShown;
    internal string WasTextForTest => _was.Text;
    internal string PeriodTextForTest => _period.Text;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _root = GetTemplateChild("PART_Root") as Decorator;
        if (_root is null) { return; }
        _currency.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextSecondary");
        _number.SetResourceReference(ForegroundProperty, "ArcWpf.Brush.Foreground");
        _amount.Children.Clear();
        _amount.Children.Add(_currency);
        _amount.Children.Add(_number);
        _wasSlot.Children.Clear();
        _wasSlot.Children.Add(_was);
        _was.HorizontalAlignment = HorizontalAlignment.Left;
        _was.FontSize = 14;
        _was.LineHeight = 19.6;
        _was.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        _was.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
        _periodSlot.Children.Clear();
        _periodSlot.Children.Add(_period);
        _period.HorizontalAlignment = HorizontalAlignment.Left;
        var aside = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(6, 4.2, 0, 0) };
        TextElement.SetFontSize(aside, 14);
        aside.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.TextMuted");
        aside.Children.Add(_wasSlot);
        aside.Children.Add(_periodSlot);
        _root.Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { _amount, aside } };
        _built = true;
        ApplySize();
        Refresh(false);
    }

    private void ApplySize()
    {
        double size = AmountSize;
        _number.FontSize = size;
        _number.Height = size;
        _number.VerticalAlignment = VerticalAlignment.Top;
        _currency.FontSize = size * .55;
        _currency.LineHeight = size * .55;
        _currency.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        _currency.VerticalAlignment = VerticalAlignment.Top;
        _currency.Margin = new Thickness(0, size * .55 * .12, size * .55 * .04, 0);
    }

    private void Refresh(bool animate)
    {
        if (!_built) { return; }
        animate = animate && IsLoaded && Tween.Level(this) != ArcMotionLevel.Off;
        bool full = animate && Tween.Level(this) == ArcMotionLevel.Full;
        string format = "F" + Decimals;
        _currency.Text = Currency;
        _number.Set(Amount, Amount.ToString(format, CultureInfo.InvariantCulture), animate);
        bool showWas = Was is { } was && was > Amount;
        if (showWas) { _was.Text = Currency + Was!.Value.ToString(format, CultureInfo.InvariantCulture); }
        _was.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double wasTarget = showWas ? _was.DesiredSize.Width + 6 : 0;
        if (showWas != _wasShown || !animate)
        {
            _wasShown = showWas;
            if (full) { _driver.Animate(_wasWidth, wasTarget, Clip); }
            else { _wasWidth.Snap(wasTarget); }
            if (animate) { Tween.To(_wasSlot, OpacityProperty, showWas ? 1 : 0, showWas ? Tween.Standard : Tween.Fast, showWas ? ArcEasing.Enter : null); } else { Tween.Set(_wasSlot, OpacityProperty, showWas ? 1 : 0); }
        }
        else if (showWas && _wasWidth.Target != wasTarget) { _wasWidth.Snap(wasTarget); }
        string period = Period ?? string.Empty;
        if (period != _shownPeriod)
        {
            var sizer = _period.Make(period);
            sizer.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double target = sizer.DesiredSize.Width;
            _shownPeriod = period;
            _period.Swap(period, animate, full);
            if (full) { _driver.Animate(_periodWidth, target, Clip); } else { _periodWidth.Snap(target); }
        }
        ApplyWidths();
    }

    private void ApplyWidths()
    {
        _wasSlot.Width = Math.Max(0, _wasWidth.Position);
        _periodSlot.Width = Math.Max(0, _periodWidth.Position);
    }
}
