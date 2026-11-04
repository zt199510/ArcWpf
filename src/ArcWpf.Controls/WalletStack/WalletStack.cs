using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace ArcWpf.Controls;

public enum WalletCardVariant { Metal, Black, Glass, Color }

public sealed class WalletTransaction
{
    public string Id { get; set; } = "";
    public string Merchant { get; set; } = "";
    public string Detail { get; set; } = "";
    /// <summary>Negative for spending, positive for money in.</summary>
    public double Amount { get; set; }
    /// <summary>An icon name from ArcDataGridIcon.</summary>
    public string? Icon { get; set; }
    /// <summary>Replaces the icon for transfers between people.</summary>
    public ImageSource? Avatar { get; set; }
}

public sealed class WalletTransactionCollection : Collection<WalletTransaction>;

/// <summary>Never pass real card numbers; only LastFour is shown. The network mark is fictional.</summary>
public sealed class WalletStackCard
{
    public string Id { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Product { get; set; } = "";
    public string LastFour { get; set; } = "";
    public string Holder { get; set; } = "";
    public WalletCardVariant Variant { get; set; } = WalletCardVariant.Metal;
    /// <summary>The material color of the Color variant.</summary>
    public Color? Tint { get; set; }
    public string? Expires { get; set; }
    public bool ShowNetwork { get; set; } = true;
    /// <summary>Optional artwork under the finish.</summary>
    public ImageSource? Image { get; set; }
    public double Balance { get; set; }
    public string BalanceLabel { get; set; } = "";
    public IList<WalletTransaction> Transactions { get; set; } = new WalletTransactionCollection();
}

public sealed class WalletStackCardCollection : Collection<WalletStackCard>;

public sealed class WalletSelectedEventArgs(WalletStackCard? card) : EventArgs
{
    public WalletStackCard? Card { get; } = card;
}

// Painted once: a bitmap for the finish, cached per variant and tint. The stack only moves it.
internal static class WalletMaterials
{
    private const int PixelWidth = 592, PixelHeight = 374;
    private static readonly Dictionary<string, BitmapSource> Cache = [];
    private static double Smooth(double from, double to, double at) => from + (to - from) * at;

    private readonly record struct Stop(double At, Color Color);

    private static Color Sample(Stop[] stops, double at)
    {
        if (at <= stops[0].At) { return stops[0].Color; }
        for (int i = 1; i < stops.Length; i++)
        {
            if (at <= stops[i].At)
            {
                double t = (at - stops[i - 1].At) / Math.Max(1e-6, stops[i].At - stops[i - 1].At);
                var a = stops[i - 1].Color; var b = stops[i].Color;
                return Color.FromArgb((byte)Smooth(a.A, b.A, t), (byte)Smooth(a.R, b.R, t), (byte)Smooth(a.G, b.G, t), (byte)Smooth(a.B, b.B, t));
            }
        }
        return stops[^1].Color;
    }

    // Source-over, straight alpha.
    private static (double R, double G, double B, double A) Over((double R, double G, double B, double A) under, Color top)
    {
        double a = top.A / 255d, keep = under.A * (1 - a), all = a + keep;
        if (all <= 0) { return (0, 0, 0, 0); }
        return ((top.R * a + under.R * keep) / all, (top.G * a + under.G * keep) / all, (top.B * a + under.B * keep) / all, all);
    }

    private static double Radial(double x, double y, double cx, double cy, double rx, double ry) => Math.Sqrt(Math.Pow((x - cx) / rx, 2) + Math.Pow((y - cy) / ry, 2));

    public static BitmapSource For(WalletCardVariant variant, Color? tint)
    {
        string key = variant + (tint?.ToString() ?? "");
        if (Cache.TryGetValue(key, out var ready)) { return ready; }
        var pixels = new byte[PixelWidth * PixelHeight * 4];
        var metal = new[] { new Stop(0, OkColor.FromLch(.86, .004, 260)), new Stop(1 / 6d, OkColor.FromLch(.96, .002, 260)), new Stop(2 / 6d, OkColor.FromLch(.79, .006, 260)), new Stop(3 / 6d, OkColor.FromLch(.93, .003, 260)), new Stop(4 / 6d, OkColor.FromLch(.83, .005, 260)), new Stop(5 / 6d, OkColor.FromLch(.97, .002, 260)), new Stop(1, OkColor.FromLch(.86, .004, 260)) };
        var bloom = new[] { new Stop(0, OkColor.FromLch(1, 0, 0, 204)), new Stop(.5, OkColor.FromLch(1, 0, 0, 0)) };
        var sweepMetal = new[] { new Stop(.38, Color.FromArgb(0, 255, 255, 255)), new Stop(.47, Color.FromArgb(87, 255, 255, 255)), new Stop(.58, Color.FromArgb(0, 255, 255, 255)) };
        var ink = new[] { new Stop(0, OkColor.FromLch(.29, .03, 265)), new Stop(.55, OkColor.FromLch(.17, .022, 265)), new Stop(1, OkColor.FromLch(.12, .016, 265)) };
        var sweepBlack = new[] { new Stop(.3, Color.FromArgb(0, 255, 255, 255)), new Stop(.44, Color.FromArgb(18, 255, 255, 255)), new Stop(.58, Color.FromArgb(0, 255, 255, 255)) };
        var sweepGlass = new[] { new Stop(0, Color.FromArgb(150, 255, 255, 255)), new Stop(.34, Color.FromArgb(0, 255, 255, 255)) };
        var sweepColor = new[] { new Stop(.22, Color.FromArgb(0, 255, 255, 255)), new Stop(.38, Color.FromArgb(41, 255, 255, 255)), new Stop(.52, Color.FromArgb(8, 255, 255, 255)), new Stop(.64, Color.FromArgb(0, 255, 255, 255)) };
        var glass = new[] { new Stop(0, OkColor.FromLch(.97, .02, 215, 204)), new Stop(.55, OkColor.FromLch(.91, .035, 225, 163)), new Stop(1, OkColor.FromLch(.94, .03, 250, 189)) };
        var deep = tint ?? OkColor.FromLch(.42, .15, 262);
        var (l, a, b) = OkColor.Lab(deep);
        double chroma = Math.Sqrt(a * a + b * b), hue = Math.Atan2(b, a) * 180 / Math.PI;
        var color = new[] { new Stop(0, OkColor.FromLch(l + .12, chroma, hue)), new Stop(.48, OkColor.FromLch(l, chroma, hue)), new Stop(1, OkColor.FromLch(Math.Max(.05, l - .14), Math.Max(0, chroma - .015), hue)) };
        var rng = new Random(key.Aggregate(17, (seed, c) => seed * 31 + c));
        // Brushed texture: rows of noise that run along the card, laid over the finish.
        var brushed = new double[PixelHeight];
        for (int y = 0; y < PixelHeight; y++) { brushed[y] = (rng.NextDouble() - .5) * 2; }
        for (int py = 0; py < PixelHeight; py++)
        {
            double y = (py + .5) / PixelHeight;
            for (int px = 0; px < PixelWidth; px++)
            {
                double x = (px + .5) / PixelWidth;
                (double R, double G, double B, double A) at = (0, 0, 0, 0);
                switch (variant)
                {
                    case WalletCardVariant.Metal:
                    {
                        // A conic gradient from 215 degrees at 58% 46%, in the card's pixels.
                        double angle = Math.Atan2((x - .58) * PixelWidth, -(y - .46) * PixelHeight) * 180 / Math.PI;
                        double turn = (((angle - 215) % 360) + 360) % 360 / 360;
                        at = Over(at, Sample(metal, turn));
                        at = Over(at, Sample(bloom, Radial(x, y, .22, .12, 1.2, .9)));
                        double streak = brushed[py] * .5 + (rng.NextDouble() - .5) * .4 + .25;
                        at = Over(at, streak > 0 ? Color.FromArgb((byte)(streak * 16), 255, 255, 255) : Color.FromArgb((byte)(-streak * 16), 0, 0, 0));
                        at = Over(at, Sample(sweepMetal, Project(x, y, 104)));
                        break;
                    }
                    case WalletCardVariant.Black:
                        at = Over(at, Sample(ink, Radial(x, y, .08, 0, 1.1, 1.3)));
                        at = Over(at, Color.FromArgb((byte)(rng.NextDouble() * 26), 255, 255, 255));
                        at = Over(at, Sample(sweepBlack, Project(x, y, 118)));
                        break;
                    case WalletCardVariant.Glass:
                        // Slightly more solid than the measured alpha: WPF has no backdrop blur to soften what is behind.
                        at = Over(at, Sample(glass, Project(x, y, 135)));
                        at = Over(at, Color.FromArgb(46, 240, 248, 248));
                        at = Over(at, Color.FromArgb((byte)(rng.NextDouble() * 8), 255, 255, 255));
                        at = Over(at, Sample(sweepGlass, Project(x, y, 160)));
                        break;
                    default:
                        at = Over(at, Sample(color, Radial(x, y, .12, 0, 1.3, 1.2)));
                        at = Over(at, Color.FromArgb((byte)(rng.NextDouble() * 8), 255, 255, 255));
                        at = Over(at, Sample(sweepColor, Project(x, y, 112)));
                        break;
                }
                int i = (py * PixelWidth + px) * 4;
                // Premultiplied BGRA.
                pixels[i] = (byte)(at.B * at.A);
                pixels[i + 1] = (byte)(at.G * at.A);
                pixels[i + 2] = (byte)(at.R * at.A);
                pixels[i + 3] = (byte)(at.A * 255);
            }
        }
        var bitmap = BitmapSource.Create(PixelWidth, PixelHeight, 192, 192, PixelFormats.Pbgra32, null, pixels, PixelWidth * 4);
        bitmap.Freeze();
        return Cache[key] = bitmap;
    }

    // Where a point falls along a CSS linear-gradient of this angle, in a box of the card's shape.
    private static double Project(double x, double y, double degrees)
    {
        double angle = degrees * Math.PI / 180, dx = Math.Sin(angle), dy = -Math.Cos(angle), w = PixelWidth, h = PixelHeight;
        double length = Math.Abs(w * dx) + Math.Abs(h * dy);
        return ((x - .5) * w * dx + (y - .5) * h * dy) / length + .5;
    }
}

// Arc Pro: no source. Built from the public docs and preview geometry; see docs/references/components/WalletStack.md.
public class WalletStack : Control
{
    public static readonly DependencyProperty CardsProperty = DependencyProperty.Register(nameof(Cards), typeof(IList<WalletStackCard>), typeof(WalletStack), new FrameworkPropertyMetadata(null, (d, _) => ((WalletStack)d).Rebuild()));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(WalletStack), new FrameworkPropertyMetadata("Wallet", (d, _) => ((WalletStack)d).SyncHeader()));
    public static readonly DependencyProperty CurrencyProperty = DependencyProperty.Register(nameof(Currency), typeof(string), typeof(WalletStack), new FrameworkPropertyMetadata("USD", (d, _) => ((WalletStack)d).RefreshDetail()));
    public static readonly DependencyProperty LocaleProperty = DependencyProperty.Register(nameof(Locale), typeof(string), typeof(WalletStack), new FrameworkPropertyMetadata("en-US", (d, _) => ((WalletStack)d).RefreshDetail()));

    public IList<WalletStackCard>? Cards { get => (IList<WalletStackCard>?)GetValue(CardsProperty); set => SetValue(CardsProperty, value); }
    /// <summary>Heading above the stack, also its accessible name.</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <summary>ISO currency code for balances and amounts.</summary>
    public string Currency { get => (string)GetValue(CurrencyProperty); set => SetValue(CurrencyProperty, value); }
    public string Locale { get => (string)GetValue(LocaleProperty); set => SetValue(LocaleProperty, value); }
    /// <summary>Raised with the opened card, or null when it goes back into the stack.</summary>
    public event EventHandler<WalletSelectedEventArgs>? SelectedChanged;

    internal const double CardWidth = 296, CardHeight = CardWidth * 53.98 / 85.6, StageHeight = 394.7, DetailTop = 178.3;
    private const double RestY = 100, RestStep = 11.2, ScaleStep = .05, FanReach = 44.8, PocketY = 365, PocketStep = 7, PocketScale = .9, PocketScaleStep = .02;
    private const double TuckY = 377, TuckStep = 6, TuckScale = .94, TuckScaleStep = .02, OpenScale = .88, LiftY = 8, MaxTilt = 5, HoverShadow = .35;

    internal readonly record struct Pose(double Scale, double Y, double Shadow);

    // Every card's transform is one pure function of a continuous stack position, the open amount and the fan amount,
    // so springs, drags and interruptions all move the same shape.
    internal static Pose PoseOf(int index, int count, double position, double open, double fan, int selected, double lift = 0)
    {
        double rel = index - position;
        double scale, y, shadow;
        if (rel >= 0)
        {
            scale = 1 - ScaleStep * rel;
            double spread = count > 1 ? FanReach * (1 - 2 * rel / (count - 1)) : 0;
            y = RestY - RestStep * rel + fan * spread;
            shadow = rel >= 1 ? .15 : .45 - .3 * rel;
        }
        else
        {
            double deep = -rel;
            double frontY = RestY + fan * (count > 1 ? FanReach : 0);
            if (deep <= 1) { scale = 1 + (PocketScale - 1) * deep; y = frontY + (PocketY - frontY) * deep; shadow = .45 * (1 - deep); }
            else { scale = PocketScale + PocketScaleStep * (deep - 1); y = PocketY + PocketStep * (deep - 1); shadow = 0; }
        }
        y -= rel > .5 ? LiftY * lift : 0;
        shadow += HoverShadow * lift;
        if (open <= 0) { return new Pose(scale, y, shadow); }
        double openScale, openY, openShadow;
        if (index == selected) { (openScale, openY, openShadow) = (OpenScale, 0, .45); }
        else
        {
            // The others tuck into the pocket, in stack order.
            int tuck = index < selected ? index : index - 1;
            (openScale, openY, openShadow) = (TuckScale - TuckScaleStep * tuck, TuckY - TuckStep * tuck, 0);
        }
        return new Pose(scale + (openScale - scale) * open, y + (openY - y) * open, shadow + (openShadow - shadow) * open);
    }

    private sealed class CardFace(WalletStackCard card, WalletStack owner) : FrameworkElement
    {
        protected override void OnRender(DrawingContext dc) => owner.DrawFace(dc, card);
        protected override Size MeasureOverride(Size availableSize) => new(CardWidth, CardHeight);
    }

    private sealed class Slot : Grid
    {
        public required WalletStack Owner;
        public required WalletStackCard Card;
        public required int Index;
        public readonly MatrixTransform Move = new();
        public readonly Border Shadow = new() { Opacity = .15, IsHitTestVisible = false };
        public readonly Grid Surface = new() { Width = CardWidth, Height = CardHeight };
        public readonly SkewTransform Tilt = new(0, 0, CardWidth / 2, CardHeight / 2);
        public readonly Border Sheen = new() { Width = CardWidth * 2, Height = CardHeight * 2, IsHitTestVisible = false, Opacity = .25, RenderTransform = new TranslateTransform(-CardWidth / 2, -CardHeight / 2), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        public readonly Border Ring = new() { CornerRadius = new CornerRadius(13), BorderThickness = new Thickness(2), Margin = new Thickness(-2), IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        public readonly GestureSpring Lift = new(0, .002, .02), TiltX = new(0, .01, .1), TiltY = new(0, .01, .1), Light = new(0, .002, .02);
        public FocusVisible? Focused;
        protected override AutomationPeer OnCreateAutomationPeer() => new DisclosurePeer(this, () => Owner.SpokenCard(Index), () => Owner._selected == Index && Owner._isOpen, () => Owner.Choose(Index));
    }

    private readonly List<Slot> _slots = [];
    private readonly Canvas _stage = new();
    private readonly TranslateTransform _detailMove = new();
    private readonly StackPanel _detail = new() { Width = CardWidth, Opacity = 0, Visibility = Visibility.Collapsed };
    private readonly Border _balanceRow = new() { BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(2, 0, 2, 8) };
    private readonly TextBlock _balanceLabel = ArcText.Block("", 14, 19.6, "TextSecondary");
    private readonly TrackedText _money;
    private readonly StackPanel _activity = new();
    private readonly SolidColorBrush _ink = new(), _muted = new(), _shadowInk = new();
    private readonly TrackedText _title;
    private readonly OdometerText _countValue = new(14, 1.4, -.16, FontWeights.Normal);
    private readonly TrackedText _countRest;
    private readonly StackPanel _count = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _back = new() { Height = 32, CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1), Padding = new Thickness(10, 0, 12, 0), HorizontalAlignment = HorizontalAlignment.Right, Opacity = 0, Visibility = Visibility.Collapsed, Cursor = Cursors.Hand, Focusable = true, FocusVisualStyle = null };
    private readonly FrameLoop _loop;
    private readonly GestureSpring _position = new(0, .001, .01), _open = new(0, .001, .01), _fan = new(0, .002, .02), _balance = new(0, .05, .5);
    private Decorator? _host;
    private int _selected, _hover = -1;
    private bool _isOpen, _pressed, _dragging, _overStage;
    private double _downY, _startPosition, _startOpen, _lastY, _flick, _pointerX, _pointerY, _shownBalance = double.NaN, _detailTime;
    private long _stamp;
    private int _pressedIndex = -1;

    static WalletStack() => DefaultStyleKeyProperty.OverrideMetadata(typeof(WalletStack), new FrameworkPropertyMetadata(typeof(WalletStack)));

    public WalletStack()
    {
        _title = CopyMotion.Text("", 16, 1.1, -.48, FontWeights.Medium, _ink);
        _money = CopyMotion.Text("", 22, 1.1, -.66, FontWeights.Normal, _ink);
        _countRest = CopyMotion.Text("", 14, 1.4, -.16, FontWeights.Normal, _muted);
        _loop = new FrameLoop(Frame);
        Loaded += (_, _) => { Inks(); Apply(); };
        Unloaded += (_, _) => _loop.Stop();
        ThemeWatch.Add(this, () => { Inks(); foreach (var slot in _slots) { foreach (var child in slot.Surface.Children.OfType<CardFace>()) { child.InvalidateVisual(); } } RefreshDetail(); });
    }

    internal double PositionForTest => _position.Position;
    internal double OpenForTest => _open.Position;
    internal double FanForTest => _fan.Position;
    internal int SelectedForTest => _selected;
    internal bool IsOpenForTest => _isOpen;
    internal bool RunningForTest => _loop.IsRunning;
    internal string MoneyForTest => _money.Text;
    internal string CountForTest => _countValue.Text + _countRest.Text;
    internal bool BackVisibleForTest => _back.Visibility == Visibility.Visible;
    internal int ActivityCountForTest => _activity.Children.Count;
    internal string SpokenForTest(int index) => SpokenCard(index);
    internal Matrix SlotMatrixForTest(int index) => _slots[index].Move.Matrix;
    internal double SlotShadowForTest(int index) => _slots[index].Shadow.Opacity;
    internal FrameworkElement SlotForTest(int index) => _slots[index];
    internal double DetailOpacityForTest => _detail.Opacity;
    internal void AdvanceForTest(double seconds) => _loop.Advance(seconds);
    internal void KeyForTest(Key key) => HandleKey(key);
    internal void HoverForTest(int index, double x = CardWidth / 2, double y = CardHeight / 2) { _overStage = true; _hover = index; _pointerX = x; _pointerY = y; _fan.To(1, SpringTune.Smooth); _loop.Start(); }
    internal void LeaveForTest() { _overStage = false; _hover = -1; _fan.To(0, SpringTune.Smooth); _loop.Start(); }
    internal void DownForTest(double y, int index = 0) => Down(y, index);
    internal void MoveForTest(double y, double dt = 1d / 60) => DragTo(y, dt);
    internal void UpForTest() => Up();
    internal void ChooseForTest(int index) => Choose(index);
    internal static string MoneyTextForTest(double value, string currency, string locale, bool signed = false) => FormatMoney(value, currency, locale, signed);

    protected override AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);

    private sealed class GroupPeer(WalletStack owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetNameCore() => owner.Label;
        protected override string GetHelpTextCore() => "Arrow keys switch cards. Enter opens a card and Escape puts it back.";
    }

    private bool Reduced => Tween.Level(this) != ArcMotionLevel.Full;
    private int Count => _slots.Count;
    private int FrontIndex => Count == 0 ? 0 : Math.Clamp((int)Math.Round(_position.Target), 0, Count - 1);

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _host = GetTemplateChild("PART_Root") as Decorator;
        Build();
    }

    private void Build()
    {
        if (_host is null) { return; }
        Inks();
        _countValue.SetValue(OdometerText.ForegroundProperty, _muted);
        _count.Children.Clear();
        _count.Children.Add(_countValue);
        _count.Children.Add(_countRest);
        var backRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var chevron = new ArcDataGridIcon { Icon = "chevron-left", Size = 16, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
        chevron.SetResourceReference(ArcDataGridIcon.StrokeProperty, "ArcWpf.Brush.Foreground");
        backRow.Children.Add(chevron);
        backRow.Children.Add(ArcText.Block("All cards", 14, 19.6, "Foreground", true));
        _back.Child = backRow;
        _back.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
        _back.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        _back.MouseLeftButtonUp += (_, _) => Close();
        _back.PreviewKeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Space) { Close(); e.Handled = true; } };
        var header = new Grid { Height = 32, Width = CardWidth };
        _title.VerticalAlignment = VerticalAlignment.Center;
        _title.HorizontalAlignment = HorizontalAlignment.Left;
        header.Children.Add(_title);
        header.Children.Add(_count);
        header.Children.Add(_back);
        _balanceRow.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        var balance = new Grid();
        _balanceLabel.VerticalAlignment = VerticalAlignment.Center;
        _money.HorizontalAlignment = HorizontalAlignment.Right;
        balance.Children.Add(_balanceLabel);
        balance.Children.Add(_money);
        _balanceRow.Child = balance;
        _detail.Children.Clear();
        _detail.Children.Add(_balanceRow);
        _detail.Children.Add(_activity);
        _detail.RenderTransform = _detailMove;
        Panel.SetZIndex(_detail, 0);
        _stage.Width = CardWidth;
        _stage.Height = StageHeight;
        _stage.Background = Brushes.Transparent;
        // The stage clips vertically: cards fall into the pocket at the bottom, and their sides stay free.
        _stage.Clip = new RectangleGeometry(new Rect(-12, 0, CardWidth + 24, StageHeight));
        _stage.Margin = new Thickness(0, 10, 0, 0);
        _stage.MouseEnter += (_, e) => { _overStage = true; if (!_isOpen && !Reduced) { _fan.To(1, SpringTune.Smooth); _loop.Start(); } };
        _stage.MouseLeave += (_, _) => { _overStage = false; if (!_pressed) { _hover = -1; _fan.To(0, SpringTune.Smooth); _loop.Start(); } };
        _stage.MouseMove += OnStageMove;
        _stage.PreviewMouseLeftButtonDown += (_, e) => { int index = SlotAt(e.OriginalSource as DependencyObject); if (index >= 0) { Down(e.GetPosition(_stage).Y, index); _stage.CaptureMouse(); } };
        _stage.PreviewMouseLeftButtonUp += (_, _) => { if (_pressed) { Up(); _stage.ReleaseMouseCapture(); } };
        _stage.LostMouseCapture += (_, _) => { if (_pressed) { Up(); } };
        var root = new StackPanel { Width = CardWidth };
        root.Children.Add(header);
        root.Children.Add(_stage);
        _host.Child = root;
        Rebuild();
    }

    private void Inks()
    {
        _ink.Color = Tween.Resolve(this, "ArcWpf.Brush.Foreground");
        _muted.Color = Tween.Resolve(this, "ArcWpf.Brush.TextMuted");
        _shadowInk.Color = OkColor.FromLch(.22, .02, 260);
    }

    private void Rebuild()
    {
        if (_host is null) { return; }
        _stage.Children.Clear();
        _stage.Children.Add(_detail);
        Canvas.SetTop(_detail, DetailTop);
        _slots.Clear();
        var cards = Cards ?? [];
        for (int i = 0; i < cards.Count; i++)
        {
            var slot = MakeSlot(cards[i], i);
            _slots.Add(slot);
            _stage.Children.Add(slot);
            // Z order never changes, so cards never pop over one another.
            Panel.SetZIndex(slot, cards.Count - i + 1);
        }
        _position.Jump(0);
        _open.Jump(0);
        _isOpen = false;
        _selected = 0;
        Roving();
        SyncHeader();
        RefreshDetail();
        Apply();
    }

    private Slot MakeSlot(WalletStackCard card, int index)
    {
        var slot = new Slot { Owner = this, Card = card, Index = index, Width = CardWidth, Height = CardHeight, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Focusable = true, FocusVisualStyle = null, Cursor = Cursors.Hand };
        slot.RenderTransform = slot.Move;
        slot.Shadow.Margin = new Thickness(CardWidth * .05, CardHeight * .08, CardWidth * .05, 0);
        slot.Shadow.CornerRadius = new CornerRadius(20);
        slot.Shadow.Background = new SolidColorBrush(OkColor.FromLch(.22, .02, 260));
        slot.Shadow.Effect = new DropShadowEffect { Color = _shadowInk.Color, BlurRadius = 48, ShadowDepth = 24, Direction = 270, Opacity = .3 };
        slot.Children.Add(slot.Shadow);
        var geometry = new RectangleGeometry(new Rect(0, 0, CardWidth, CardHeight), 11, 11);
        var face = new CardFace(card, this);
        // A circle of 36% of the distance to the corner of the doubled card, so it fades out before the box ends.
        double sheenRadius = .36 * Math.Sqrt(CardWidth * CardWidth + CardHeight * CardHeight);
        var sheenBrush = new RadialGradientBrush { GradientOrigin = new Point(.5, .5), Center = new Point(.5, .5), RadiusX = sheenRadius / (CardWidth * 2), RadiusY = sheenRadius / (CardHeight * 2) };
        sheenBrush.GradientStops.Add(new GradientStop(Color.FromArgb(107, 255, 255, 255), 0));
        sheenBrush.GradientStops.Add(new GradientStop(Color.FromArgb(31, 255, 255, 255), .5));
        sheenBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1));
        sheenBrush.Freeze();
        slot.Sheen.Background = sheenBrush;
        var inner = new Grid { Clip = geometry };
        inner.Children.Add(face);
        inner.Children.Add(slot.Sheen);
        slot.Surface.Children.Add(inner);
        slot.Surface.RenderTransform = slot.Tilt;
        slot.Ring.BorderBrush = FocusVisible.RingBrush(this);
        slot.Surface.Children.Add(slot.Ring);
        slot.Children.Add(slot.Surface);
        slot.Focused = new FocusVisible(slot, () => slot.Ring.Visibility = slot.Focused!.Shown ? Visibility.Visible : Visibility.Collapsed);
        slot.PreviewKeyDown += (_, e) => { if (HandleKey(e.Key)) { e.Handled = true; } };
        return slot;
    }

    private void Roving()
    {
        int front = _isOpen ? _selected : FrontIndex;
        foreach (var slot in _slots) { KeyboardNavigation.SetIsTabStop(slot, slot.Index == front); }
    }

    private void SyncHeader()
    {
        if (_host is null) { return; }
        _title.Text = Label ?? "";
        _countValue.Set((FrontIndex + 1).ToString(CultureInfo.InvariantCulture));
        _countRest.Text = $" of {Count}";
        _count.Visibility = Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private string SpokenCard(int index)
    {
        if (index < 0 || index >= Count) { return Label; }
        var card = _slots[index].Card;
        return $"{card.Issuer} {card.Product} ending {card.LastFour}, card {index + 1} of {Count}, {card.BalanceLabel} {FormatMoney(card.Balance, Currency, Locale)}";
    }

    private static CultureInfo CultureOf(string locale) { try { return CultureInfo.GetCultureInfo(locale); } catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; } }

    internal static string FormatMoney(double value, string currency, string locale, bool signed = false)
    {
        var format = (NumberFormatInfo)CultureOf(locale).NumberFormat.Clone();
        format.CurrencySymbol = currency switch { "USD" => "$", "EUR" => "€", "GBP" => "£", "JPY" => "¥", "CNY" => "CN¥", _ => currency + " " };
        format.CurrencyNegativePattern = 1;
        format.CurrencyDecimalDigits = currency == "JPY" ? 0 : 2;
        string text = Math.Abs(value).ToString("C", format);
        if (value < 0) { return "−" + text; }
        return signed ? "+" + text : text;
    }

    private void RefreshDetail()
    {
        if (_host is null || Count == 0) { return; }
        var card = _slots[Math.Clamp(_selected, 0, Count - 1)].Card;
        _balanceLabel.Text = card.BalanceLabel;
        _money.Text = FormatMoney(double.IsNaN(_shownBalance) ? card.Balance : _shownBalance, Currency, Locale);
        _activity.Children.Clear();
        foreach (var item in card.Transactions.Take(3)) { _activity.Children.Add(MakeRow(item)); }
    }

    private FrameworkElement MakeRow(WalletTransaction item)
    {
        var row = new Grid { Margin = new Thickness(2, 4, 2, 4), Height = 33.8, ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(42) }, new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } } };
        var icon = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(16), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, ClipToBounds = true };
        icon.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
        if (item.Avatar is { } avatar) { icon.Background = new ImageBrush(avatar) { Stretch = Stretch.UniformToFill }; }
        else
        {
            var glyph = new ArcDataGridIcon { Icon = item.Icon ?? "wallet", Size = 16, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            glyph.SetResourceReference(ArcDataGridIcon.StrokeProperty, "ArcWpf.Brush.TextSecondary");
            icon.Child = glyph;
        }
        row.Children.Add(icon);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var merchant = ArcText.Block(item.Merchant, 14, 18.2);
        merchant.TextTrimming = TextTrimming.CharacterEllipsis;
        var detail = ArcText.Block(item.Detail, 12, 15.6, "TextMuted");
        detail.TextTrimming = TextTrimming.CharacterEllipsis;
        text.Children.Add(merchant);
        text.Children.Add(detail);
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        var amount = ArcText.Block(FormatMoney(item.Amount, Currency, Locale, true), 14, 19.6);
        if (item.Amount > 0) { amount.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.ArcSuccess"); }
        amount.VerticalAlignment = VerticalAlignment.Center;
        amount.Margin = new Thickness(10, 0, 0, 0);
        Grid.SetColumn(amount, 2);
        row.Children.Add(amount);
        row.RenderTransform = new TranslateTransform();
        return row;
    }

    private void Kick() { if (IsLoaded && !Reduced) { _loop.Start(); } else { Apply(); } }

    private bool Frame(double seconds)
    {
        _position.Step(seconds);
        _open.Step(seconds);
        _fan.Step(seconds);
        _balance.Step(seconds);
        bool running = _position.Active || _open.Active || _fan.Active || _balance.Active;
        if (_detail.Visibility == Visibility.Visible && _detailTime < 1) { _detailTime += seconds; running = true; }
        int front = FrontIndex;
        foreach (var slot in _slots)
        {
            bool hot = !_isOpen && _hover == slot.Index;
            double aim = hot ? 1 : 0;
            if (slot.Lift.Target != aim) { slot.Lift.To(aim, SpringTune.Smooth); }
            if (slot.Light.Target != aim) { slot.Light.To(aim, SpringTune.Smooth); }
            // The front card tilts toward the pointer and a highlight follows it.
            bool tilting = hot && slot.Index == front;
            double tiltX = tilting ? Math.Clamp((_pointerX / CardWidth - .5) * 2, -1, 1) * MaxTilt : 0, tiltY = tilting ? Math.Clamp((_pointerY / CardHeight - .5) * 2, -1, 1) * MaxTilt : 0;
            slot.TiltX.Chase(tiltX, SpringTune.Snappy);
            slot.TiltY.Chase(tiltY, SpringTune.Snappy);
            slot.Lift.Step(seconds);
            slot.Light.Step(seconds);
            slot.TiltX.Step(seconds);
            slot.TiltY.Step(seconds);
            running |= slot.Lift.Active || slot.Light.Active || slot.TiltX.Active || slot.TiltY.Active;
        }
        Apply();
        return running;
    }

    private void Apply()
    {
        int count = Count;
        double open = Math.Clamp(_open.Position, 0, 1), fan = Math.Clamp(_fan.Position, 0, 1), position = _position.Position;
        foreach (var slot in _slots)
        {
            var pose = PoseOf(slot.Index, count, position, open, fan, _selected, Math.Clamp(slot.Lift.Position, 0, 1));
            double x = CardWidth * (1 - pose.Scale) / 2;
            slot.Move.Matrix = new Matrix(pose.Scale, 0, 0, pose.Scale, x, pose.Y);
            // Only the shadow's opacity moves: it is painted once. A card with no shadow left skips the effect altogether.
            slot.Shadow.Opacity = Math.Clamp(pose.Shadow, 0, 1);
            slot.Shadow.Visibility = pose.Shadow <= .001 ? Visibility.Hidden : Visibility.Visible;
            // Skew stands in for the 3D tilt: WPF has no perspective on a 2D transform without leaving the visual tree.
            slot.Tilt.AngleX = -slot.TiltX.Position * .35 * (1 - open);
            slot.Tilt.AngleY = slot.TiltY.Position * .35 * (1 - open);
            slot.Sheen.Opacity = .25 + .75 * Math.Clamp(slot.Light.Position, 0, 1);
            var sheenMove = (TranslateTransform)slot.Sheen.RenderTransform;
            sheenMove.X = Math.Clamp(_pointerX, 0, CardWidth) - CardWidth;
            sheenMove.Y = Math.Clamp(_pointerY, 0, CardHeight) - CardHeight;
        }
        ApplyDetail(open);
        _money.Text = FormatMoney(_detail.Visibility == Visibility.Visible ? _balance.Position : _slots.ElementAtOrDefault(_selected)?.Card.Balance ?? 0, Currency, Locale);
    }

    // The balance and three transactions arrive as the card lifts. The region is inert until a card is open.
    private void ApplyDetail(double open)
    {
        bool shown = open > .01 || _isOpen;
        _detail.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
        _detail.IsHitTestVisible = _isOpen;
        _detail.Opacity = Math.Clamp((open - .2) / .6, 0, 1);
        _detailMove.Y = (1 - open) * 12;
        for (int i = 0; i < _activity.Children.Count; i++)
        {
            double delay = .06 * i, t = Reduced ? 1 : Math.Clamp((_detailTime - delay) / .36, 0, 1), eased = ArcEasing.Standard.Ease(t);
            var row = (FrameworkElement)_activity.Children[i];
            row.Opacity = eased * Math.Clamp((open - .2) / .6, 0, 1);
            ((TranslateTransform)row.RenderTransform).Y = (1 - eased) * 10;
        }
        _count.Opacity = 1 - open;
        _count.Visibility = open >= .99 ? Visibility.Collapsed : Visibility.Visible;
        _back.Opacity = open;
        _back.Visibility = open <= .01 ? Visibility.Collapsed : Visibility.Visible;
    }

    private int SlotAt(DependencyObject? from)
    {
        for (var at = from; at is not null && !ReferenceEquals(at, _stage); at = at is Visual ? VisualTreeHelper.GetParent(at) : LogicalTreeHelper.GetParent(at)) { if (at is Slot slot) { return slot.Index; } }
        return -1;
    }

    private void OnStageMove(object sender, MouseEventArgs e)
    {
        if (_pressed) { DragTo(e.GetPosition(_stage).Y, double.NaN); return; }
        if (e.StylusDevice is not null) { return; }
        int index = SlotAt(e.OriginalSource as DependencyObject);
        _hover = index;
        if (index >= 0)
        {
            var at = e.GetPosition(_slots[index].Surface);
            (_pointerX, _pointerY) = (at.X, at.Y);
        }
        Kick();
    }

    private void Down(double y, int index)
    {
        _pressed = true;
        _dragging = false;
        _pressedIndex = index;
        _downY = _lastY = y;
        _startPosition = _position.Position;
        _startOpen = _open.Position;
        _flick = 0;
        _stamp = System.Diagnostics.Stopwatch.GetTimestamp();
    }

    private double DragRange => Math.Max(1, PocketY - (RestY + Math.Clamp(_fan.Position, 0, 1) * FanReach));

    private void DragTo(double y, double seconds)
    {
        if (!_pressed) { return; }
        double travel = y - _downY;
        if (!_dragging && Math.Abs(travel) < 4) { return; }
        _dragging = true;
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        double dt = double.IsNaN(seconds) ? System.Diagnostics.Stopwatch.GetElapsedTime(_stamp, now).TotalSeconds : seconds;
        _stamp = now;
        if (dt > .0005) { _flick = (y - _lastY) / dt; }
        _lastY = y;
        if (_isOpen)
        {
            // Pull the open card down to put it back.
            _open.Jump(Math.Clamp(_startOpen - Math.Max(0, travel) / 260, 0, 1));
        }
        else
        {
            // Dragging down pushes the front card into the pocket and brings the next one forward one to one under the pointer.
            double next = _startPosition + travel / DragRange, last = Math.Max(0, Count - 1);
            if (next < 0) { next = GestureMath.Rubber(next * DragRange, 60) / DragRange; }
            else if (next > last) { next = last + GestureMath.Rubber((next - last) * DragRange, 60) / DragRange; }
            _position.Jump(next);
        }
        Apply();
    }

    private void Up()
    {
        bool dragged = _dragging;
        _pressed = false;
        _dragging = false;
        if (!_overStage) { _hover = -1; _fan.To(0, SpringTune.Smooth); }
        if (!dragged)
        {
            if (_pressedIndex >= 0) { Choose(_pressedIndex); }
            Kick();
            return;
        }
        if (_isOpen)
        {
            double projected = _open.Position - _flick * .18 / 260;
            if (projected < .5) { Close(); } else { _open.To(1, Open); Kick(); }
            return;
        }
        // A flick lands where its momentum projects, one card at a time.
        double momentum = _position.Position + _flick * .18 / DragRange;
        double start = Math.Round(_startPosition);
        double target = Math.Clamp(Math.Round(Math.Clamp(momentum, start - 1, start + 1)), 0, Math.Max(0, Count - 1));
        SettleOn((int)target, _flick / DragRange);
    }

    private static readonly SpringTune Open = SpringTune.Physical(.5);

    private void SettleOn(int index, double? velocity = null)
    {
        bool changed = index != FrontIndex || Math.Round(_startPosition) != index;
        if (Reduced) { _position.Jump(index); } else { _position.To(index, SpringTune.Morph, velocity); }
        Roving();
        SyncHeader();
        if (changed) { Announcer.Say(this, $"{_slots[index].Card.Issuer} {_slots[index].Card.Product}, {index + 1} of {Count}"); }
        Kick();
    }

    private void MoveTo(int index, bool focus = true)
    {
        if (Count == 0) { return; }
        index = Math.Clamp(index, 0, Count - 1);
        _startPosition = _position.Position;
        if (_isOpen)
        {
            // While a card is open the arrows switch the open card.
            if (index == _selected) { return; }
            _selected = index;
            _position.Jump(index);
            StartDetail();
            RefreshDetail();
            Roving();
            SyncHeader();
            if (focus && IsLoaded) { _slots[index].Focus(); }
            _slots[index].Focused?.Key();
            Announcer.Say(this, $"{_slots[index].Card.Issuer} {_slots[index].Card.Product} open");
            SelectedChanged?.Invoke(this, new WalletSelectedEventArgs(_slots[index].Card));
            Kick();
            return;
        }
        SettleOn(index);
        if (focus && IsLoaded) { _slots[index].Focus(); }
        _slots[index].Focused?.Key();
    }

    private void Choose(int index)
    {
        if (Count == 0 || index < 0 || index >= Count) { return; }
        if (_isOpen && index == _selected) { Close(); return; }
        if (_isOpen) { MoveTo(index); return; }
        _selected = index;
        _position.To(index, SpringTune.Morph);
        OpenSelected();
    }

    private void OpenSelected()
    {
        _isOpen = true;
        _hover = -1;
        _fan.To(0, SpringTune.Smooth);
        if (Reduced) { _open.Jump(1); _position.Jump(_selected); _fan.Jump(0); } else { _open.To(1, Open); }
        StartDetail();
        RefreshDetail();
        Roving();
        SyncHeader();
        var slot = _slots[_selected];
        if (IsLoaded && !slot.IsKeyboardFocused) { slot.Focused?.Pointer(); slot.Focus(); }
        Announcer.Say(this, $"{slot.Card.Issuer} {slot.Card.Product} open");
        Peer(slot)?.RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty, ExpandCollapseState.Collapsed, ExpandCollapseState.Expanded);
        SelectedChanged?.Invoke(this, new WalletSelectedEventArgs(slot.Card));
        Kick();
    }

    private static AutomationPeer? Peer(UIElement element) => UIElementAutomationPeer.FromElement(element);

    // The balance counts up.
    private void StartDetail()
    {
        _detailTime = 0;
        double balance = _slots[_selected].Card.Balance;
        if (Reduced) { _balance.Jump(balance); _detailTime = 1; }
        else { _balance.Jump(0); _balance.To(balance, SpringTune.Physical(.6)); }
        _shownBalance = double.NaN;
    }

    private void Close()
    {
        if (!_isOpen) { return; }
        _isOpen = false;
        if (Reduced) { _open.Jump(0); } else { _open.To(0, Open); }
        if (_overStage && !Reduced) { _fan.To(1, SpringTune.Smooth); }
        Roving();
        SyncHeader();
        var slot = _slots[_selected];
        if (IsLoaded) { slot.Focus(); }
        Announcer.Say(this, $"{slot.Card.Issuer} {slot.Card.Product} back in the stack");
        SelectedChanged?.Invoke(this, new WalletSelectedEventArgs(null));
        Kick();
    }

    private bool HandleKey(Key key)
    {
        int current = _isOpen ? _selected : FrontIndex;
        switch (key)
        {
            case Key.Down or Key.Right: MoveTo(current + 1); return true;
            case Key.Up or Key.Left: MoveTo(current - 1); return true;
            case Key.Home: MoveTo(0); return true;
            case Key.End: MoveTo(Count - 1); return true;
            case Key.Enter or Key.Space: if (_isOpen) { Close(); } else { _slots[current].Focused?.Key(); Choose(current); } return true;
            case Key.Escape: if (!_isOpen) { return false; } Close(); return true;
            default: return false;
        }
    }

    private FormattedText Face(string text, double size, FontWeight weight, Brush brush) => new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(FontFamily, FontStyles.Normal, weight, FontStretches.Normal), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private double TextRun(DrawingContext dc, string text, double x, double y, double size, double tracking, FontWeight weight, Brush brush)
    {
        double at = x;
        foreach (char glyph in text)
        {
            var face = Face(glyph.ToString(), size, weight, brush);
            dc.DrawText(face, new Point(at, y + (size - face.Height) / 2));
            at += face.WidthIncludingTrailingWhitespace + tracking;
        }
        return at - x;
    }

    private void DrawFace(DrawingContext dc, WalletStackCard card)
    {
        var clip = new RectangleGeometry(new Rect(0, 0, CardWidth, CardHeight), 11, 11);
        dc.PushClip(clip);
        if (card.Image is { } image) { dc.DrawImage(image, new Rect(0, 0, CardWidth, CardHeight)); }
        dc.DrawImage(WalletMaterials.For(card.Variant, card.Tint), new Rect(0, 0, CardWidth, CardHeight));
        var (ink, soft, numberInk) = card.Variant switch
        {
            WalletCardVariant.Black => (OkColor.FromLch(.9, .006, 260), OkColor.FromLch(.9, .006, 260, 158), OkColor.FromLch(.86, .008, 260)),
            WalletCardVariant.Glass => (OkColor.FromLch(.27, .03, 240), OkColor.FromLch(.27, .03, 240, 184), OkColor.FromLch(.27, .03, 240)),
            WalletCardVariant.Color => (OkColor.FromLch(.98, .01, 260), OkColor.FromLch(.98, .01, 260, 184), OkColor.FromLch(.98, .01, 260)),
            _ => (OkColor.FromLch(.3, .01, 260), OkColor.FromLch(.3, .01, 260, 184), OkColor.FromLch(.3, .01, 260)),
        };
        var inkBrush = new SolidColorBrush(ink);
        var softBrush = new SolidColorBrush(soft);
        var numberBrush = new SolidColorBrush(numberInk);
        if (card.Variant == WalletCardVariant.Black) { DrawGuilloche(dc); }
        TextRun(dc, card.Issuer, 20.7, 20.1, 15.984, -.32, FontWeights.Medium, inkBrush);
        var product = Face(card.Product, 9.768, FontWeights.Normal, softBrush);
        dc.DrawText(product, new Point(CardWidth - 20.7 - product.WidthIncludingTrailingWhitespace, 22.5 + (9.768 - product.Height) / 2));
        DrawChip(dc, card.Variant == WalletCardVariant.Black, new Rect(25.2, 63.6, 40.3, 30.6));
        DrawContactless(dc, new Point(75.5, 69.2), 19.5, softBrush);
        // Three groups of four dots, drawn so the spacing does not depend on the font's bullet and space.
        for (int group = 0; group < 3; group++)
        {
            for (int dot = 0; dot < 4; dot++) { dc.DrawEllipse(numberBrush, null, new Point(22.2 + group * 22.4 + dot * 3.8, 119.8), 1.15, 1.15); }
        }
        TextRun(dc, card.LastFour, 20.7 + 64.8 + 6.5, 111.3, 16.576, 1.49, FontWeights.Normal, numberBrush);
        TextRun(dc, card.Holder, 20.7, 156.7, 10.36, .207, FontWeights.Normal, inkBrush);
        if (!string.IsNullOrEmpty(card.Expires))
        {
            TextRun(dc, "Valid thru", 153.9, 147.9, 6.512, 0, FontWeights.Normal, softBrush);
            TextRun(dc, card.Expires!, 153.9, 157.1, 10.36, .414, FontWeights.Normal, inkBrush);
        }
        if (card.ShowNetwork) { DrawNetwork(dc, inkBrush); }
        DrawEdge(dc, card.Variant);
        dc.Pop();
    }

    // Fine contour lines of a black card: a rose of closed curves around a point right of centre.
    private static void DrawGuilloche(DrawingContext dc)
    {
        var pen = new Pen(new SolidColorBrush(OkColor.FromLch(.8, .03, 250, 33)), .7);
        pen.Freeze();
        for (int ring = 0; ring < 16; ring++)
        {
            double reach = 24 + ring * 6.2;
            var figure = new StreamGeometry();
            using (var context = figure.Open())
            {
                const int Steps = 180;
                for (int i = 0; i <= Steps; i++)
                {
                    double t = i * Math.PI * 2 / Steps, wave = 1 + .035 * Math.Sin(t * 9 + ring * .55) + .02 * Math.Sin(t * 5 - ring * .3);
                    var at = new Point(221 + reach * 1.25 * wave * Math.Cos(t), 84.5 + reach * wave * Math.Sin(t));
                    if (i == 0) { context.BeginFigure(at, false, true); } else { context.LineTo(at, true, true); }
                }
            }
            figure.Freeze();
            dc.DrawGeometry(null, pen, figure);
        }
    }

    private static readonly double[] ChipStops = [0, .25, .5, .75, 1];

    private static LinearGradientBrush ChipBrush(bool silver, Point start, Point end)
    {
        (double L, double C, double H)[] gold = [(.9, .08, 90), (.76, .11, 78), (.93, .06, 95), (.7, .1, 70), (.84, .09, 85)];
        (double L, double C, double H)[] steel = [(.92, .004, 260), (.72, .006, 260), (.95, .003, 260), (.68, .006, 260), (.85, .005, 260)];
        var brush = new LinearGradientBrush { StartPoint = start, EndPoint = end };
        var stops = silver ? steel : gold;
        for (int i = 0; i < stops.Length; i++) { brush.GradientStops.Add(new GradientStop(OkColor.FromLch(stops[i].L, stops[i].C, stops[i].H), ChipStops[i])); }
        brush.Freeze();
        return brush;
    }

    // An EMV chip, drawn in a 50 by 38 box.
    private static void DrawChip(DrawingContext dc, bool silver, Rect box)
    {
        double k = box.Width / 50;
        dc.PushTransform(new MatrixTransform(k, 0, 0, k, box.X, box.Y));
        var seam = new Pen(new SolidColorBrush(Color.FromArgb(82, 0, 0, 0)), .7);
        var rim = new Pen(new SolidColorBrush(Color.FromArgb(71, 0, 0, 0)), .7);
        dc.DrawRoundedRectangle(ChipBrush(silver, new Point(0, 0), new Point(1, 1)), null, new Rect(.5, .5, 49, 37), 7, 7);
        dc.DrawRoundedRectangle(ChipBrush(silver, new Point(1, 0), new Point(0, 1)), null, new Rect(16, 8, 18, 22), 3.5, 3.5);
        foreach (var (x1, y1, x2, y2) in new (double, double, double, double)[] { (0, 12.5, 16, 12.5), (0, 25.5, 16, 25.5), (34, 12.5, 50, 12.5), (34, 25.5, 50, 25.5), (16, 8, 16, 30), (34, 8, 34, 30), (25, 0, 25, 8), (25, 30, 25, 38), (16, 19, 13, 19), (34, 19, 37, 19) }) { dc.DrawLine(seam, new Point(x1, y1), new Point(x2, y2)); }
        dc.DrawRoundedRectangle(null, seam, new Rect(16, 8, 18, 22), 3.5, 3.5);
        var gloss = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        gloss.GradientStops.Add(new GradientStop(Color.FromArgb(140, 255, 255, 255), 0));
        gloss.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), .45));
        dc.DrawRoundedRectangle(gloss, rim, new Rect(.5, .5, 49, 37), 7, 7);
        dc.Pop();
    }

    private static void DrawContactless(DrawingContext dc, Point at, double size, Brush brush)
    {
        double k = size / 24;
        var pen = new Pen(brush, 1.7 * k) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        (double X, double Half, double Bow)[] arcs = [(5.5, 3, 1.1), (9, 5.5, 2.2), (12.5, 8, 3.2), (16, 10.5, 4.3)];
        foreach (var (x, half, bow) in arcs)
        {
            var figure = new StreamGeometry();
            using (var context = figure.Open())
            {
                context.BeginFigure(new Point(at.X + x * k, at.Y + (12 - half) * k), false, false);
                context.QuadraticBezierTo(new Point(at.X + (x + bow * 2) * k, at.Y + 12 * k), new Point(at.X + x * k, at.Y + (12 + half) * k), true, true);
            }
            figure.Freeze();
            dc.DrawGeometry(null, pen, figure);
        }
    }

    // The built-in fictional network mark.
    private void DrawNetwork(DrawingContext dc, Brush ink)
    {
        const double Size = 18.3;
        var center = new Point(241.25, 160.95);
        double radius = 5.75;
        var pen = new Pen(ink, 2.6 * Size / 24) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var ring = new StreamGeometry();
        using (var context = ring.Open())
        {
            double a0 = -22 * Math.PI / 180, a1 = -68 * Math.PI / 180;
            context.BeginFigure(new Point(center.X + radius * Math.Cos(a0), center.Y + radius * Math.Sin(a0)), false, false);
            context.ArcTo(new Point(center.X + radius * Math.Cos(a1), center.Y + radius * Math.Sin(a1)), new Size(radius, radius), 0, true, SweepDirection.Clockwise, true, false);
        }
        ring.Freeze();
        dc.DrawGeometry(null, pen, ring);
        dc.DrawEllipse(ink, null, new Point(center.X + 4.35, center.Y - 5.45), 1.6, 1.6);
        TextRun(dc, "halo", 253.6, 154.7, 12.432, -.373, FontWeights.Medium, ink);
    }

    // The card's edge: a top highlight and a soft bottom, inside the outline.
    private static void DrawEdge(DrawingContext dc, WalletCardVariant variant)
    {
        var box = new Rect(.25, .25, CardWidth - .5, CardHeight - .5);
        dc.DrawRoundedRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(26, 0, 0, 0)), .5), box, 11, 11);
        byte top = variant == WalletCardVariant.Metal ? (byte)217 : (byte)46;
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(top, 255, 255, 255)), 1), new Point(10, .5), new Point(CardWidth - 10, .5));
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(46, 0, 0, 0)), 1), new Point(10, CardHeight - .5), new Point(CardWidth - 10, CardHeight - .5));
    }
}
