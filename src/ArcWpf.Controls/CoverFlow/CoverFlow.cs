using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ArcWpf.Controls;

public sealed class CoverFlowItem
{
    public ImageSource? Image { get; init; }
    public string Title { get; init; } = "";
    public string Subtitle { get; init; } = "";
}

internal readonly record struct CoverFlowPose(double X, double Z, double Sin, double Cos, double Veil, double Reflection, double Edge, double Opacity);

/// <summary>All numbers come from measuring the public preview at a 521.6 wide stage; they are kept as shares of the card width.</summary>
internal static class CoverFlowMath
{
    public const double CardShare = .31, Aspect = 1.25, TopInset = 8, PerspectiveShare = 3.4, RadiusShare = .085, ReflectionShare = .15, ReflectionGap = 2, FloorGap = 10;
    private const double Step1 = 129.6 / 161.7, Step2 = 59.62 / 161.7, Step3 = 20.86 / 161.7, Depth1 = 61.56 / 161.7, DepthN = 22.68 / 161.7, MaxDegrees = 45, FadeEnd = 2.7;

    public static double CardWidth(double stage) => stage * CardShare;
    public static double CardHeight(double stage) => CardWidth(stage) * Aspect;
    public static double Floor(double stage) => TopInset + CardHeight(stage);
    public static double StageHeight(double stage) => Floor(stage) + ReflectionGap + CardHeight(stage) * ReflectionShare + FloorGap;
    public static double Perspective(double stage) => CardWidth(stage) * PerspectiveShare;

    /// <summary>A card's pose from its signed distance to the center: it swings to the side within one step, then stacks.</summary>
    public static CoverFlowPose Pose(double distance, double card)
    {
        double d = Math.Abs(distance), sign = Math.Sign(distance);
        double x = d <= 1 ? Step1 * d : d <= 2 ? Step1 + Step2 * (d - 1) : Step1 + Step2 + Step3 * (d - 2);
        // Past the third place cards only slide: they have already faded out by then.
        double held = Math.Min(d, 3);
        double z = held <= 1 ? Depth1 * held : Depth1 + DepthN * (held - 1);
        double angle = Math.Min(d, 1) * MaxDegrees * Math.PI / 180;
        double veil = held <= 1 ? .42 * held : .42 + .16 * (held - 1);
        double near = Math.Min(d, 1);
        return new CoverFlowPose(sign * x * card, -z * card, sign * Math.Sin(angle), Math.Cos(angle), veil, 1 - .45 * near, .45 * near, Math.Clamp((FadeEnd - d) / (FadeEnd - 2), 0, 1));
    }

    public static int ZIndex(double distance) => (int)Math.Round(100 - 10 * Math.Abs(distance));

    /// <summary>A point of the card plane (x from the card center, height above the floor) on screen, in stage coordinates.</summary>
    public static Point Project(CoverFlowPose pose, double stage, double localX, double height)
    {
        double p = Perspective(stage);
        double wx = pose.X + localX * pose.Cos, wz = pose.Z + localX * pose.Sin;
        double scale = p / (p - wz);
        return new Point(stage / 2 + wx * scale, Floor(stage) - height * scale);
    }

    /// <summary>The screen box of a card's face, matching getBoundingClientRect in the preview.</summary>
    /// <summary>The inverse of Project for one screen column: the card-plane x it shows, and how much a screen pixel is worth on the plane there.</summary>
    public static (double LocalX, double Unscale) Unproject(CoverFlowPose pose, double stage, double screenX)
    {
        double p = Perspective(stage), a = screenX - stage / 2;
        double local = (a * (p - pose.Z) - pose.X * p) / (p * pose.Cos + a * pose.Sin);
        return (local, (p - pose.Z - local * pose.Sin) / p);
    }

    public static Rect FaceBounds(double distance, double stage)
    {
        double card = CardWidth(stage), h = CardHeight(stage);
        var pose = Pose(distance, card);
        var a = Project(pose, stage, -card / 2, 0);
        var b = Project(pose, stage, card / 2, h);
        var c = Project(pose, stage, -card / 2, h);
        var d = Project(pose, stage, card / 2, 0);
        double left = Math.Min(a.X, b.X), right = Math.Max(a.X, b.X), top = Math.Min(b.Y, c.Y), bottom = Math.Max(a.Y, d.Y);
        return new Rect(left, top, right - left, bottom - top);
    }

    public static bool FaceContains(double distance, double stage, Point at)
    {
        double card = CardWidth(stage), h = CardHeight(stage);
        var pose = Pose(distance, card);
        Point[] corners = [Project(pose, stage, -card / 2, 0), Project(pose, stage, card / 2, 0), Project(pose, stage, card / 2, h), Project(pose, stage, -card / 2, h)];
        int sign = 0;
        for (int i = 0; i < 4; i++)
        {
            var from = corners[i]; var to = corners[(i + 1) % 4];
            double cross = (to.X - from.X) * (at.Y - from.Y) - (to.Y - from.Y) * (at.X - from.X);
            int side = Math.Sign(cross);
            if (side == 0) { continue; }
            if (sign != 0 && side != sign) { return false; }
            sign = side;
        }
        return true;
    }

    /// <summary>Where a release lands: the current position nudged by the throw, one card at most, as the preview behaves.</summary>
    public static int Landing(double position, double velocityCardsPerSecond, int count)
    {
        double nudge = Math.Clamp(velocityCardsPerSecond * .15, -1, 1);
        return (int)Math.Clamp(Math.Round(position + nudge), 0, Math.Max(0, count - 1));
    }

    /// <summary>Past either end the rail resists.</summary>
    public static double Resist(double position, int count)
    {
        double last = Math.Max(0, count - 1);
        if (position < 0) { return position / (1 - position * 2.4); }
        if (position > last) { double over = position - last; return last + over / (1 + over * 2.4); }
        return position;
    }
}

/// <summary>A depth rail of images: each card is a true perspective plane, resampled in software so it needs no 3D stack, with a soft shadow and a quiet reflection, in a stage you can drag, throw or step through.</summary>
public class CoverFlow : Control
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IReadOnlyList<CoverFlowItem>), typeof(CoverFlow), new PropertyMetadata(null, (d, _) => ((CoverFlow)d).Build()));
    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(CoverFlow), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((CoverFlow)d).OnIndex((int)e.NewValue)));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(CoverFlow), new PropertyMetadata("", (d, e) => AutomationProperties.SetName(d, (string)e.NewValue)));

    public IReadOnlyList<CoverFlowItem>? ItemsSource { get => (IReadOnlyList<CoverFlowItem>?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public int SelectedIndex { get => (int)GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public event EventHandler<int>? SelectionChanged;

    private const double GlideSeconds = .3, CaptionShift = 18, CaptionFade = 1.6, TickWidth = 16, ViewMargin = 16;

    private readonly SpringDriver _driver;
    private readonly ControlSpring _position;
    private readonly List<CardPlane> _cards = [];
    private readonly List<(StackPanel Panel, TranslateTransform Move)> _captions = [];
    private readonly TranslateTransform _thumbMove = new();
    private readonly List<(int T, double X)> _samples = [];
    private readonly OwnedMotion _motion = new();
    private Decorator? _host;
    private Grid? _stage;
    private readonly Image _view = new() { IsHitTestVisible = false, Stretch = Stretch.Fill, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private CardShared? _shared;
    private WriteableBitmap? _bitmap;
    private uint[] _pixels = [];
    private CardRun[] _runs = [];
    private Border? _focusRing;
    private TextBlock? _status;
    private double _stageWidth;
    private bool _dragging, _moved, _building;
    private double _grabX, _grabPosition;

    static CoverFlow() => DefaultStyleKeyProperty.OverrideMetadata(typeof(CoverFlow), new FrameworkPropertyMetadata(typeof(CoverFlow)));

    public CoverFlow()
    {
        _driver = new SpringDriver(Apply);
        _position = _driver.Add(0, ArcSpring.Of(GlideSeconds));
        Unloaded += (_, _) => { _driver.Stop(); _motion.Stop(); };
        ThemeWatch.Add(this, Rebake);
    }

    private int Count => ItemsSource?.Count ?? 0;
    private bool Moving => IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;

    internal double PositionForTest => _position.Position;
    internal double PositionTargetForTest => _position.Target;
    internal double StageWidthForTest => _stageWidth;
    internal string StatusForTest => _status?.Text ?? "";
    internal double CaptionOpacityForTest(int index) => _captions[index].Panel.Opacity;
    internal double ThumbXForTest => _thumbMove.X;
    internal int ZIndexForTest(int index) => CoverFlowMath.ZIndex(index - _position.Position);
    internal CoverFlowPose PoseForTest(int index) => CoverFlowMath.Pose(index - _position.Position, CoverFlowMath.CardWidth(_stageWidth));
    /// <summary>The composed premultiplied BGRA pixel at a point in stage coordinates.</summary>
    internal uint PixelForTest(Point at)
    {
        double dpi = _shared?.Dpi ?? 1;
        return _pixels[(int)((at.Y + ViewMargin) * dpi) * _bitmap!.PixelWidth + (int)((at.X + ViewMargin) * dpi)];
    }
    internal void FinishMotionForTest() => _driver.Finish();
    internal void DownForTest(double x, int t) => BeginDrag(x, t);
    internal void MoveForTest(double x, int t) => Drag(x, t);
    internal void UpForTest(Point at, int t) => EndDrag(at, t);
    internal int? HitForTest(Point at) => CardAt(at);

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _host = GetTemplateChild("PART_Root") as Decorator;
        Build();
    }

    private void Build()
    {
        if (_host is null) { return; }
        _building = true;
        _driver.Stop();
        _cards.Clear();
        _captions.Clear();
        _stageWidth = 0;
        _stage = new Grid { Background = Brushes.Transparent, Focusable = true, FocusVisualStyle = null, ClipToBounds = false, VerticalAlignment = VerticalAlignment.Top };
        _focusRing = new Border { BorderThickness = new Thickness(2), Margin = new Thickness(-4), Opacity = .72, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        _focusRing.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Focus");
        Panel.SetZIndex(_focusRing, 200);
        _stage.Children.Add(_focusRing);
        _stage.Children.Add(_view);
        _stage.MouseLeftButtonDown += (_, e) => { _stage.Focus(); BeginDrag(e.GetPosition(_stage).X, e.Timestamp); _stage.CaptureMouse(); e.Handled = true; };
        _stage.MouseMove += (_, e) =>
        {
            if (_dragging) { Drag(e.GetPosition(_stage).X, e.Timestamp); return; }
            int? hit = CardAt(e.GetPosition(_stage));
            _stage.Cursor = hit is { } i && i != SelectedIndex ? Cursors.Hand : Cursors.SizeWE;
        };
        _stage.MouseLeftButtonUp += (_, e) => { if (_dragging) { _stage.ReleaseMouseCapture(); EndDrag(e.GetPosition(_stage), e.Timestamp); } };
        _stage.LostMouseCapture += (_, e) => { if (_dragging) { _dragging = false; GoTo(Math.Clamp((int)Math.Round(_position.Position), 0, Math.Max(0, Count - 1)), true); } };
        _stage.PreviewKeyDown += OnKey;
        _stage.GotKeyboardFocus += (_, _) => _focusRing.Visibility = ThreadUi.FocusVisible(_stage) ? Visibility.Visible : Visibility.Collapsed;
        _stage.LostKeyboardFocus += (_, _) => _focusRing.Visibility = Visibility.Collapsed;
        _stage.SizeChanged += (_, e) => { if (e.WidthChanged) { Bake(e.NewSize.Width); } };

        var caption = new Grid { Height = 48, IsHitTestVisible = false };
        var ticks = new Canvas { Height = 24, HorizontalAlignment = HorizontalAlignment.Center, Width = Count * TickWidth, Margin = new Thickness(0, 12, 0, 0) };
        int index = 0;
        foreach (var item in ItemsSource ?? [])
        {
            _cards.Add(new CardPlane(item));
            var title = ArcText.Block(item.Title, 18, 23.4, medium: true);
            title.TextAlignment = TextAlignment.Center;
            var subtitle = ArcText.Block(item.Subtitle, 14, 19.6, "TextSecondary");
            subtitle.TextAlignment = TextAlignment.Center;
            subtitle.Margin = new Thickness(0, 2, 0, 0);
            var move = new TranslateTransform();
            var panel = new StackPanel { Children = { title, subtitle }, RenderTransform = move, Opacity = 0 };
            caption.Children.Add(panel);
            _captions.Add((panel, move));
            ticks.Children.Add(Tick(index, item.Title));
            index++;
        }
        var thumb = new Border { Width = 12, Height = 5, CornerRadius = new CornerRadius(2.5), RenderTransform = _thumbMove, IsHitTestVisible = false };
        thumb.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Foreground");
        Canvas.SetLeft(thumb, 2);
        Canvas.SetTop(thumb, 9.5);
        ticks.Children.Add(thumb);
        _status = new TextBlock { Width = 0, Height = 0, Opacity = 0 };
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        caption.Margin = new Thickness(16, 16, 16, 0);
        _host.Child = new StackPanel { Children = { _status, _stage, caption, ticks } };
        int start = Math.Clamp(SelectedIndex, 0, Math.Max(0, Count - 1));
        if (start != SelectedIndex) { SetCurrentValue(SelectedIndexProperty, start); }
        _position.Snap(start);
        _building = false;
        Apply();
    }

    private FrameworkElement Tick(int index, string title)
    {
        var dot = new Border { Width = 5, Height = 5, CornerRadius = new CornerRadius(2.5), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        dot.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.BorderStrong");
        var tick = new ArcChromeButton(2.5) { Width = TickWidth, Height = 24, Content = dot, Padding = new Thickness(0), Background = Brushes.Transparent };
        AutomationProperties.SetName(tick, "Show " + title);
        tick.Click += (_, _) => GoTo(index, true);
        Canvas.SetLeft(tick, index * TickWidth);
        return tick;
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Rebake();
    }

    private void Rebake()
    {
        if (_stageWidth <= 0) { return; }
        double width = _stageWidth;
        _stageWidth = 0;
        Bake(width);
    }

    // Each layer of a card is baked once to plane-space pixels; only the resampling runs per frame.
    private void Bake(double width)
    {
        if (_stage is null || width <= 0 || Math.Abs(width - _stageWidth) < .5) { return; }
        _stageWidth = width;
        _stage.Height = CoverFlowMath.StageHeight(width);
        _focusRing!.Height = _stage.Height + 8;
        double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        _shared = new CardShared(width, dpi, Tween.Resolve(this, "ArcWpf.Brush.Foreground"));
        foreach (var card in _cards) { card.Bake(_shared); }
        int w = (int)Math.Ceiling((width + ViewMargin * 2) * dpi), h = (int)Math.Ceiling((_stage.Height + ViewMargin * 2) * dpi);
        _bitmap = new WriteableBitmap(w, h, 96 * dpi, 96 * dpi, PixelFormats.Pbgra32, null);
        _pixels = new uint[w * h];
        _view.Source = _bitmap;
        _view.Width = w / dpi;
        _view.Height = h / dpi;
        _view.Margin = new Thickness(-ViewMargin);
        _runs = _cards.Select(_ => new CardRun(w)).ToArray();
        Apply();
    }

    private void Apply()
    {
        if (_building) { return; }
        double position = _position.Position;
        for (int i = 0; i < _cards.Count; i++)
        {
            double distance = i - position;
            double fade = Math.Max(0, 1 - Math.Abs(distance) * CaptionFade);
            var (panel, move) = _captions[i];
            panel.Opacity = fade;
            move.X = Math.Clamp(distance, -1, 1) * CaptionShift;
        }
        _thumbMove.X = position * TickWidth;
        Compose(position);
    }

    // Cards are stacked by distance as the browser stacks flat transformed layers, so they never intersect in depth.
    private void Compose(double position)
    {
        if (_bitmap is null || _shared is null || _runs.Length != _cards.Count) { return; }
        int width = _bitmap.PixelWidth, height = _bitmap.PixelHeight;
        for (int i = 0; i < _cards.Count; i++) { _runs[i].Prepare(_cards[i], i - position, _stageWidth, _shared, ViewMargin, width, height); }
        var order = _runs.OrderByDescending(r => Math.Abs(r.Distance)).ToArray();
        var pixels = _pixels;
        var shared = _shared;
        Parallel.For(0, height, row =>
        {
            var line = pixels.AsSpan(row * width, width);
            line.Clear();
            foreach (var run in order) { run.Draw(line, row, shared); }
        });
        _bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
    }

    private void OnIndex(int index)
    {
        // Before the items arrive there is nothing to clamp against: Build honours the index once they do.
        if (Count == 0) { return; }
        int clamped = Math.Clamp(index, 0, Count - 1);
        if (clamped != index) { SetCurrentValue(SelectedIndexProperty, clamped); return; }
        if (!_dragging) { Glide(clamped); }
        if (_captions.Count > clamped && Count > 0)
        {
            var item = ItemsSource![clamped];
            ArcEffects.Announce(_status, $"{item.Title}, {clamped + 1} of {Count}");
        }
        SelectionChanged?.Invoke(this, clamped);
    }

    private void Glide(int index)
    {
        if (Moving) { _driver.Animate(_position, index, ArcSpring.Of(GlideSeconds)); } else { _driver.Snap(_position, index); }
    }

    private void GoTo(int index, bool glide)
    {
        int clamped = Math.Clamp(index, 0, Math.Max(0, Count - 1));
        if (clamped == SelectedIndex) { if (glide) { Glide(clamped); } return; }
        SetCurrentValue(SelectedIndexProperty, clamped);
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        int target = e.Key switch { Key.Left => SelectedIndex - 1, Key.Right => SelectedIndex + 1, Key.Home => 0, Key.End => Count - 1, _ => -2 };
        if (target == -2) { return; }
        e.Handled = true;
        GoTo(target, true);
    }

    private void BeginDrag(double x, int t)
    {
        _dragging = true;
        _moved = false;
        _grabX = x;
        _grabPosition = _position.Position;
        _driver.Stop();
        _position.Snap(_position.Position);
        _samples.Clear();
        _samples.Add((t, x));
    }

    private void Drag(double x, int t)
    {
        if (!_dragging || _stageWidth <= 0) { return; }
        if (Math.Abs(x - _grabX) > 3) { _moved = true; }
        double raw = _grabPosition - (x - _grabX) / CoverFlowMath.CardWidth(_stageWidth);
        _position.Snap(CoverFlowMath.Resist(raw, Count));
        _samples.Add((t, x));
        if (_samples.Count > 12) { _samples.RemoveAt(0); }
        Apply();
    }

    private void EndDrag(Point at, int t)
    {
        if (!_dragging) { return; }
        _dragging = false;
        if (!_moved)
        {
            if (CardAt(at) is { } hit) { GoTo(hit, true); } else { Glide(SelectedIndex); }
            return;
        }
        var velocity = CardStackMath.Velocity(_samples.Select(s => (s.T, s.X, 0d)).ToList(), t);
        double cardsPerSecond = _stageWidth > 0 ? -velocity.X / CoverFlowMath.CardWidth(_stageWidth) : 0;
        GoTo(CoverFlowMath.Landing(_position.Position, cardsPerSecond, Count), true);
    }

    private int? CardAt(Point at)
    {
        if (_stageWidth <= 0) { return null; }
        int? best = null;
        int bestZ = int.MinValue;
        for (int i = 0; i < _cards.Count; i++)
        {
            double distance = i - _position.Position;
            int z = CoverFlowMath.ZIndex(distance);
            if (CoverFlowMath.Pose(distance, 1).Opacity <= 0) { continue; }
            if (z > bestZ && CoverFlowMath.FaceContains(distance, _stageWidth, at)) { best = i; bestZ = z; }
        }
        return best;
    }

    /// <summary>The geometry and shadow pixels every card shares for one stage width. Plane space runs from the card's top-left outward, at Density pixels per unit.</summary>
    private sealed class CardShared
    {
        public const double Pad = 2;
        public double Dpi { get; }
        public double Density { get; }
        public double Stage { get; }
        public double Card { get; }
        public double Height { get; }
        public double Radius { get; }
        public double HalfExtent { get; }
        public double Top { get; }
        public int PixelWidth { get; }
        public int PixelHeight { get; }
        /// <summary>Ambient alpha in the low byte, contact alpha in the next: both are pure black, and the reflection falls between them.</summary>
        public uint[] Shadow { get; }
        public Pen Rim { get; }

        public CardShared(double stage, double dpi, Color ink)
        {
            Rim = new Pen(new SolidColorBrush(Color.FromArgb(20, ink.R, ink.G, ink.B)), 1);
            Dpi = dpi;
            Density = dpi * 1.5;
            Stage = stage;
            Card = CoverFlowMath.CardWidth(stage);
            Height = CoverFlowMath.CardHeight(stage);
            Radius = Card * CoverFlowMath.RadiusShare;
            HalfExtent = Card * .53 + Pad;
            Top = Height + Pad;
            PixelWidth = (int)Math.Ceiling(HalfExtent * 2 * Density);
            PixelHeight = (int)Math.Ceiling((Top + CoverFlowMath.ReflectionGap + Height * CoverFlowMath.ReflectionShare + Pad) * Density);
            var ambient = Render(dc => dc.DrawRectangle(Radial(.14, .056, .55), null, new Rect(HalfExtent - Card * .53, Top - Height * .94, Card * 1.06, Height * .98)));
            var contact = Render(dc => dc.DrawRectangle(Radial(.2232, .07812, .6), null, new Rect(HalfExtent - Card * .45, Top - Height * .0346, Card * .9, Card * .099)));
            Shadow = new uint[ambient.Length];
            for (int i = 0; i < Shadow.Length; i++) { Shadow[i] = ambient[i] >> 24 | (contact[i] >> 24) << 8; }
        }

        public Rect FaceBox => new(HalfExtent - Card / 2, Top - Height, Card, Height);
        public Rect ReflectionBox => new(HalfExtent - Card / 2, Top + CoverFlowMath.ReflectionGap, Card, Height * CoverFlowMath.ReflectionShare);

        private static Brush Radial(double center, double middle, double at) => new RadialGradientBrush { GradientStops = { new GradientStop(Shade(center), 0), new GradientStop(Shade(middle), at), new GradientStop(Shade(0), 1) } };

        public static Color Shade(double alpha) => Color.FromArgb((byte)Math.Round(255 * alpha), 0, 0, 0);

        public uint[] Render(Action<DrawingContext> draw)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) { draw(dc); }
            var bitmap = new RenderTargetBitmap(PixelWidth, PixelHeight, 96 * Density, 96 * Density, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var pixels = new uint[PixelWidth * PixelHeight];
            bitmap.CopyPixels(pixels, PixelWidth * 4, 0);
            return pixels;
        }
    }

    /// <summary>One card's baked layers in plane space.</summary>
    private sealed class CardPlane(CoverFlowItem item)
    {
        public uint[] Face { get; private set; } = [];
        public uint[] Reflection { get; private set; } = [];

        public void Bake(CardShared shared)
        {
            Face = shared.Render(dc =>
            {
                DrawFace(dc, shared.FaceBox, shared.Radius);
                var box = shared.FaceBox;
                box.Inflate(-.5, -.5);
                dc.DrawRoundedRectangle(null, shared.Rim, box, shared.Radius - .5, shared.Radius - .5);
            });
            Reflection = shared.Render(dc => DrawReflection(dc, shared.ReflectionBox, shared.Height, shared.Radius));
        }

        private void DrawFace(DrawingContext dc, Rect box, double radius)
        {
            dc.PushClip(new RectangleGeometry(box, radius, radius));
            dc.DrawRectangle(Brushes.LightGray, null, box);
            if (item.Image is { } image) { dc.DrawImage(image, Cover(image, box)); }
            dc.Pop();
        }

        // A mirrored copy of the picture hanging from the floor, fading out within its short height.
        private void DrawReflection(DrawingContext dc, Rect box, double fullHeight, double radius)
        {
            dc.PushClip(new RectangleGeometry(box, radius, radius));
            // Absolute: relative to the bounding box the gradient would stretch over the whole mirrored picture.
            dc.PushOpacityMask(new LinearGradientBrush { StartPoint = new Point(0, box.Top), EndPoint = new Point(0, box.Bottom), MappingMode = BrushMappingMode.Absolute, GradientStops = { new GradientStop(CardShared.Shade(.28), 0), new GradientStop(CardShared.Shade(0), .88) } });
            dc.DrawRectangle(Brushes.Transparent, null, box);
            if (item.Image is { } image)
            {
                var full = new Rect(box.X, box.Y, box.Width, fullHeight);
                dc.PushTransform(new ScaleTransform(1, -1, 0, box.Y + fullHeight / 2));
                dc.DrawImage(image, Cover(image, full));
                dc.Pop();
            }
            dc.Pop();
            dc.Pop();
        }

        private static Rect Cover(ImageSource image, Rect box)
        {
            double scale = Math.Max(box.Width / image.Width, box.Height / image.Height);
            double w = image.Width * scale, h = image.Height * scale;
            return new Rect(box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2, w, h);
        }
    }

    /// <summary>A card's pose for one frame: per-column inverse projection, then each row is linear in screen y.</summary>
    private sealed class CardRun(int columns)
    {
        private readonly double[] _planeX = new double[columns], _unscale = new double[columns];
        private readonly uint[] _ink = new uint[columns];
        private CardPlane? _plane;
        private int _left, _right, _top, _bottom;
        private uint _reflection, _opacity;
        private double _floor, _dpi, _margin;

        public double Distance { get; private set; }

        public void Prepare(CardPlane plane, double distance, double stage, CardShared shared, double margin, int width, int height)
        {
            _plane = plane;
            Distance = distance;
            _dpi = shared.Dpi;
            _margin = margin;
            _floor = CoverFlowMath.Floor(stage);
            var pose = CoverFlowMath.Pose(distance, shared.Card);
            _reflection = (uint)Math.Round(pose.Reflection * 256);
            _opacity = (uint)Math.Round(pose.Opacity * 256);
            if (_opacity == 0) { _left = _right = 0; return; }
            double bottomExtent = shared.PixelHeight / shared.Density - shared.Top;
            Point[] corners = [CoverFlowMath.Project(pose, stage, -shared.HalfExtent, shared.Top), CoverFlowMath.Project(pose, stage, shared.HalfExtent, shared.Top), CoverFlowMath.Project(pose, stage, -shared.HalfExtent, -bottomExtent), CoverFlowMath.Project(pose, stage, shared.HalfExtent, -bottomExtent)];
            _left = Math.Clamp((int)Math.Floor((corners.Min(c => c.X) + margin) * _dpi), 0, width);
            _right = Math.Clamp((int)Math.Ceiling((corners.Max(c => c.X) + margin) * _dpi), 0, width);
            _top = Math.Clamp((int)Math.Floor((corners.Min(c => c.Y) + margin) * _dpi), 0, height);
            _bottom = Math.Clamp((int)Math.Ceiling((corners.Max(c => c.Y) + margin) * _dpi), 0, height);
            // Cards on either side darken toward the center, so the gradient flips as a card crosses.
            double veil = 1 - .62 * pose.Veil;
            for (int x = _left; x < _right; x++)
            {
                var (local, unscale) = CoverFlowMath.Unproject(pose, stage, (x + .5) / _dpi - margin);
                _planeX[x] = (local + shared.HalfExtent) * shared.Density - .5;
                _unscale[x] = unscale;
                double t = (local + shared.Card / 2) / shared.Card;
                double along = distance > 0 ? 1 - t : t;
                double edge = .5 * pose.Edge * Math.Clamp((along - .4) / .6, 0, 1);
                _ink[x] = (uint)Math.Round(veil * (1 - edge) * 256);
            }
        }

        public void Draw(Span<uint> line, int row, CardShared shared)
        {
            if (row < _top || row >= _bottom) { return; }
            double down = _floor - ((row + .5) / _dpi - _margin);
            double density = shared.Density, top = shared.Top;
            int w = shared.PixelWidth, h = shared.PixelHeight;
            uint[] face = _plane!.Face, reflection = _plane.Reflection, shadow = shared.Shadow;
            for (int x = _left; x < _right; x++)
            {
                double px = _planeX[x], py = (top - down * _unscale[x]) * density - .5;
                if (px < 0 || py < 0 || px >= w - 1 || py >= h - 1) { continue; }
                int ix = (int)px, iy = (int)py;
                uint fx = (uint)((px - ix) * 256), fy = (uint)((py - iy) * 256);
                int at = iy * w + ix;
                uint f = Bilinear(face, at, w, fx, fy);
                f = Scale(f, _ink[x]) & 0x00FFFFFF | f & 0xFF000000;
                if (f >> 24 < 255)
                {
                    uint under = Bilinear(shadow, at, w, fx, fy);
                    uint contact = under >> 8 & 255;
                    uint color = under & 255;
                    color <<= 24;
                    color = Over(Scale(Bilinear(reflection, at, w, fx, fy), _reflection), color);
                    color = Over(contact << 24, color);
                    f = Over(f, color);
                }
                if (f != 0) { line[x] = Over(_opacity == 256 ? f : Scale(f, _opacity), line[x]); }
            }
        }

        private static uint Bilinear(uint[] source, int at, int stride, uint fx, uint fy)
        {
            uint a = source[at], b = source[at + 1], c = source[at + stride], d = source[at + stride + 1];
            if ((a | b | c | d) == 0) { return 0; }
            return Mix(Mix(a, b, fx), Mix(c, d, fx), fy);
        }

        // Weighs before shifting: adding two scaled channels would lose a step each, and an opaque face must stay exactly opaque.
        private static uint Mix(uint a, uint b, uint t)
        {
            uint u = 256 - t;
            return ((a & 0x00FF00FF) * u + (b & 0x00FF00FF) * t) >> 8 & 0x00FF00FF | ((a >> 8 & 0x00FF00FF) * u + (b >> 8 & 0x00FF00FF) * t) & 0xFF00FF00;
        }

        // Scales all four 8-bit channels at once, k running 0 to 256.
        private static uint Scale(uint c, uint k) => (c & 0x00FF00FF) * k >> 8 & 0x00FF00FF | (c >> 8 & 0x00FF00FF) * k & 0xFF00FF00;

        private static uint Over(uint top, uint bottom) => top + Scale(bottom, 256 - (top >> 24));
    }
}
