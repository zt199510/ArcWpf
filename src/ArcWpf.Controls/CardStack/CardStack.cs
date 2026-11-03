using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public enum CardDecision { Left, Right }

public sealed class CardStackEventArgs(object item, CardDecision decision) : EventArgs
{
    public object Item { get; } = item;
    public CardDecision Decision { get; } = decision;
}

internal static class CardStackMath
{
    /// <summary>Cards rendered in the stack. The last one waits invisibly one step behind and fades in as it rises.</summary>
    public const int Visible = 4;
    public const double Peek = 12, Shrink = .05, Lift = .4, LiftDistance = 160, Rotate = .065, Flick = 500, FreeY = 48, StretchY = 140;

    /// <summary>Vertical travel is not a decision, so past a small free zone it resists like a rubber band.</summary>
    public static double ResistY(double raw)
    {
        double distance = Math.Abs(raw);
        return distance <= FreeY ? raw : Math.Sign(raw) * (FreeY + (1 - 1 / ((distance - FreeY) * .55 / StretchY + 1)) * StretchY);
    }

    public static double UnresistY(double shown)
    {
        double distance = Math.Abs(shown);
        if (distance <= FreeY) { return shown; }
        double stretch = Math.Min(distance - FreeY, StretchY - 1);
        return Math.Sign(shown) * (FreeY + (1 / (1 - stretch / StretchY) - 1) * StretchY / .55);
    }

    public static Vector Velocity(IReadOnlyList<(int T, double X, double Y)> samples, int now)
    {
        var recent = samples.Where(sample => now - sample.T <= 80).ToList();
        if (recent.Count < 2 || now - recent[^1].T > 60 || recent[^1].T == recent[0].T) { return default; }
        double seconds = (recent[^1].T - recent[0].T) / 1000d;
        return new Vector((recent[^1].X - recent[0].X) / seconds, (recent[^1].Y - recent[0].Y) / seconds);
    }

    /// <summary>True when a release counts as a decision: a flick the way it travelled, or far enough without flicking back.</summary>
    public static bool Decides(double offsetX, double velocityX, double width)
    {
        int direction = Math.Sign(offsetX);
        if (direction == 0) { return false; }
        bool flick = Math.Abs(velocityX) > Flick && Math.Sign(velocityX) == direction && Math.Abs(offsetX) > 12;
        bool far = Math.Abs(offsetX) > Math.Min(width * .35, 140) && !(Math.Sign(velocityX) == -direction && Math.Abs(velocityX) > 300);
        return flick || far;
    }

    public static double Clamp(double from, double to, double fromValue, double toValue, double value)
    {
        double t = Math.Clamp((value - from) / (to - from), 0, 1);
        return fromValue + (toValue - fromValue) * t;
    }

    /// <summary>Where a thrown card leaves: along the flick when it heads the chosen way, otherwise along the drag, otherwise straight out.</summary>
    public static Point Exit(int direction, double x, double y, Vector velocity, double width)
    {
        double ux = velocity.X, uy = velocity.Y;
        if (velocity.Length < 200 || Math.Sign(ux) != direction) { ux = Math.Abs(x) > 8 ? x : direction; uy = Math.Abs(x) > 8 ? y * .5 : -.12; }
        double length = Math.Max(Math.Sqrt(ux * ux + uy * uy), 1e-9);
        ux /= length; uy /= length;
        if (Math.Abs(ux) < .45) { ux = direction * .45; uy = (uy == 0 ? -1 : Math.Sign(uy)) * Math.Sqrt(1 - .45 * .45); }
        double reach = width * 1.6;
        return new Point(x + ux * reach, y + uy * reach);
    }
}

/// <summary>A deck of cards reviewed one at a time: drag or flick the top card left or right, or use the buttons and arrow keys.</summary>
public class CardStack : Control
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IReadOnlyList<object>), typeof(CardStack), new PropertyMetadata(null, (d, _) => ((CardStack)d).Restart()));
    public static readonly DependencyProperty CardTemplateProperty = DependencyProperty.Register(nameof(CardTemplate), typeof(DataTemplate), typeof(CardStack), new PropertyMetadata(null, (d, _) => ((CardStack)d).Restart()));
    public static readonly DependencyProperty LeftLabelProperty = Text(nameof(LeftLabel), "Pass");
    public static readonly DependencyProperty RightLabelProperty = Text(nameof(RightLabel), "Keep");
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(CardStack), new PropertyMetadata("Cards", (d, _) => ((CardStack)d).Rebuild()));
    public static readonly DependencyProperty CardHeightProperty = DependencyProperty.Register(nameof(CardHeight), typeof(double), typeof(CardStack), new PropertyMetadata(304d, (d, _) => ((CardStack)d).Rebuild()));

    private static DependencyProperty Text(string name, string fallback) => DependencyProperty.Register(name, typeof(string), typeof(CardStack), new PropertyMetadata(fallback, (d, _) => ((CardStack)d).Rebuild()));

    public IReadOnlyList<object>? ItemsSource { get => (IReadOnlyList<object>?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public DataTemplate? CardTemplate { get => (DataTemplate?)GetValue(CardTemplateProperty); set => SetValue(CardTemplateProperty, value); }
    public string LeftLabel { get => (string)GetValue(LeftLabelProperty); set => SetValue(LeftLabelProperty, value); }
    public string RightLabel { get => (string)GetValue(RightLabelProperty); set => SetValue(RightLabelProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public double CardHeight { get => (double)GetValue(CardHeightProperty); set => SetValue(CardHeightProperty, value); }

    /// <summary>A short name for a card, used in its accessible label and announcements. Defaults to ToString.</summary>
    public Func<object, string>? LabelSelector { get; set; }
    /// <summary>Past tense of each action for announcements, such as "skipped" and "shortlisted".</summary>
    public string? LeftOutcome { get; set; }
    public string? RightOutcome { get; set; }
    /// <summary>Builds a custom empty state; it receives the action that brings all cards back.</summary>
    public Func<Action, FrameworkElement>? EmptyFactory { get; set; }
    /// <summary>The default empty state's heading and optional line, asked for each time the deck runs out.</summary>
    public Func<(string Title, string? Detail)>? EmptyText { get; set; }

    public event EventHandler<CardStackEventArgs>? Decided;
    public event EventHandler<CardStackEventArgs>? Undone;
    public event EventHandler? ResetCompleted;

    private readonly List<(object Item, CardDecision Decision)> _history = [];
    private readonly List<object> _flying = [];
    private readonly Dictionary<object, CardView> _cards = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, (Point Exit, double Tilt)> _exits = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, Vector> _flings = new(ReferenceEqualityComparer.Instance);
    private Dictionary<object, (Point From, double Tilt, double Delay)> _returning = new(ReferenceEqualityComparer.Instance);
    private readonly OwnedMotion _motion = new();
    private Decorator? _host;
    private Grid? _stage;
    private TextBlock? _status;
    private FrameworkElement? _empty;
    private StackButton? _left, _undo, _right;
    private int _position;
    private double _lift;

    static CardStack() => DefaultStyleKeyProperty.OverrideMetadata(typeof(CardStack), new FrameworkPropertyMetadata(typeof(CardStack)));

    public CardStack() => Unloaded += (_, _) => { _motion.Stop(); foreach (var card in _cards.Values) { card.Stop(); } };

    private IReadOnlyList<object> Items => ItemsSource ?? [];
    private object? Top => _position < Items.Count ? Items[_position] : null;
    private string CardName(object item) => LabelSelector?.Invoke(item) ?? item.ToString() ?? "";
    private string Word(CardDecision decision) => decision == CardDecision.Right ? RightLabel : LeftLabel;
    private string? Outcome(CardDecision decision) => decision == CardDecision.Right ? RightOutcome : LeftOutcome;
    private bool Moving => IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;

    public int Position => _position;
    public int Remaining => Math.Max(0, Items.Count - _position);
    internal string StatusForTest => _status?.Text ?? "";
    internal bool IsEmptyShownForTest => _empty is not null;
    internal int RenderedCountForTest => _cards.Count;
    internal Grid? StageForTest => _stage;
    internal double LiftForTest => _lift;
    internal (double X, double Y, double Rotation, double Depth, double Keep, double Pass, double ZIndex, bool Top) CardForTest(object item) { var c = _cards[item]; return (c.X, c.Y, c.Rotation, c.ShownDepth, c.KeepOpacity, c.PassOpacity, Panel.GetZIndex(c.Slot), c.Interactive); }
    internal void DownForTest(Point at, int t) => _cards[Top!].Down(at, t);
    internal void MoveForTest(Point at, int t) => _cards[Top!].Move(at, t);
    internal void UpForTest(int t, object? item = null) => _cards[item ?? Top!].Up(t, false);
    internal void FinishMotionForTest() { foreach (var card in _cards.Values.ToList()) { card.Finish(); } }
    internal bool IsDisabledForTest(string which) => (which == "undo" ? _undo : which == "left" ? _left : _right)!.IsInert;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _host = GetTemplateChild("PART_Root") as Decorator;
        Rebuild();
    }

    private void Restart()
    {
        _position = 0;
        _history.Clear();
        _flying.Clear();
        _exits.Clear();
        _flings.Clear();
        _returning = new(ReferenceEqualityComparer.Instance);
        Rebuild();
    }

    private void Rebuild()
    {
        if (_host is null) { return; }
        foreach (var card in _cards.Values) { card.Stop(); }
        _cards.Clear();
        _empty = null;
        _stage = new Grid { Height = CardHeight + 24, MaxWidth = 320, Focusable = true, FocusVisualStyle = null, Background = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(_stage, Label);
        AutomationProperties.SetHelpText(_stage, $"Drag the top card, or use the left and right arrow keys to {LeftLabel.ToLowerInvariant()} or {RightLabel.ToLowerInvariant()} it. Backspace undoes the last decision.");
        _stage.PreviewKeyDown += OnKey;
        _stage.GotKeyboardFocus += (_, _) => RefreshFocus();
        _stage.LostKeyboardFocus += (_, _) => RefreshFocus();
        _left = new StackButton("x", LeftLabel, null);
        _undo = new StackButton("rotate-ccw", "Undo", "quiet");
        _right = new StackButton("check", RightLabel, "primary");
        _left.Click += (_, _) => Decide(CardDecision.Left);
        _undo.Click += (_, _) => Undo();
        _right.Click += (_, _) => Decide(CardDecision.Right);
        _undo.Margin = new Thickness(8, 0, 8, 0);
        var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 20, 0, 0), Children = { _left, _undo, _right } };
        _status = new TextBlock { Width = 0, Height = 0, Opacity = 0 };
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        _host.Child = new StackPanel { Children = { _status, _stage, controls } };
        Sync();
    }

    /// <summary>Decides the top card, throwing it to that side. A key or button can do this mid-drag; the throw takes over from the finger.</summary>
    public void Decide(CardDecision decision)
    {
        if (Top is not { } top) { return; }
        _lift = 0;
        _position++;
        _history.Add((top, decision));
        _flying.Add(top);
        _returning = new(ReferenceEqualityComparer.Instance);
        int left = Remaining;
        string outcome = Outcome(decision) is { } past ? " " + past : ": " + Word(decision);
        ArcEffects.Announce(_status, $"{CardName(top)}{outcome}. {(left > 0 ? left + " left." : "All cards reviewed.")}");
        Decided?.Invoke(this, new CardStackEventArgs(top, decision));
        Sync();
    }

    /// <summary>Brings the last decided card back from where it left the screen.</summary>
    public void Undo()
    {
        if (_history.Count == 0) { return; }
        var last = _history[^1];
        _position--;
        _history.RemoveAt(_history.Count - 1);
        _flying.Remove(last.Item);
        _returning = new(ReferenceEqualityComparer.Instance);
        if (_exits.TryGetValue(last.Item, out var exit)) { _returning[last.Item] = (exit.Exit, exit.Tilt, 0); }
        ArcEffects.Announce(_status, $"{CardName(last.Item)} restored.");
        Undone?.Invoke(this, new CardStackEventArgs(last.Item, last.Decision));
        Sync();
    }

    /// <summary>The deck deals itself back: the card that will sit deepest returns first, the top card last.</summary>
    public void Reset()
    {
        if (_history.Count == 0) { return; }
        int count = Math.Min(CardStackMath.Visible, Items.Count);
        var back = new Dictionary<object, (Point, double, double)>(ReferenceEqualityComparer.Instance);
        for (int index = 0; index < count; index++)
        {
            if (index < _position && _exits.TryGetValue(Items[index], out var exit)) { back[Items[index]] = (exit.Exit, exit.Tilt, (count - 1 - index) * .06); }
        }
        _position = 0;
        _history.Clear();
        _flying.Clear();
        _returning = back;
        ArcEffects.Announce(_status, "All cards are back in the stack.");
        ResetCompleted?.Invoke(this, EventArgs.Empty);
        Sync();
    }

    private bool Release(object item, Point offset, Vector velocity, double width)
    {
        if (!ReferenceEquals(Top, item) || !CardStackMath.Decides(offset.X, velocity.X, width)) { return false; }
        _flings[item] = velocity;
        Decide(offset.X > 0 ? CardDecision.Right : CardDecision.Left);
        return true;
    }

    private void Gone(object item) { if (_flying.Remove(item)) { Sync(); } }

    private void SetLift(double value)
    {
        if (_lift == value) { return; }
        _lift = value;
        foreach (var card in _cards.Values) { card.UpdateDepth(); }
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, _stage)) { return; }
        if (e.Key is Key.Left or Key.Right) { e.Handled = true; Decide(e.Key == Key.Right ? CardDecision.Right : CardDecision.Left); }
        else if (e.Key == Key.Back || (e.Key == Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))) { e.Handled = true; Undo(); }
    }

    private void RefreshFocus()
    {
        bool focus = _stage is not null && ThreadUi.FocusVisible(_stage);
        foreach (var card in _cards.Values) { card.ShowFocus(focus && card.Interactive); }
        if (_empty is Border box) { box.BorderBrush = (Brush)FindResource(focus ? "ArcWpf.Brush.Focus" : "ArcWpf.Brush.Border"); }
    }

    private void Sync()
    {
        if (_stage is null) { return; }
        var decided = new Dictionary<object, CardDecision>(ReferenceEqualityComparer.Instance);
        foreach (var (item, decision) in _history) { decided[item] = decision; }
        var window = new HashSet<object>(Items.Skip(_position).Take(CardStackMath.Visible), ReferenceEqualityComparer.Instance);
        var rendered = Items.Where(item => window.Contains(item) || _flying.Contains(item)).ToList();

        foreach (var stale in _cards.Keys.Where(key => !rendered.Contains(key)).ToList())
        {
            _cards[stale].Stop();
            _stage.Children.Remove(_cards[stale].Slot);
            _cards.Remove(stale);
        }
        foreach (var item in rendered)
        {
            int index = IndexOf(item);
            bool isDecided = decided.TryGetValue(item, out var decision);
            int depth = isDecided ? 0 : index - _position;
            if (!_cards.TryGetValue(item, out var card))
            {
                _returning.TryGetValue(item, out var from);
                card = new CardView(this, item, _returning.ContainsKey(item) ? from : null);
                _cards[item] = card;
                _stage.Children.Add(card.Slot);
            }
            bool interactive = !isDecided && depth == 0;
            card.Update(depth, isDecided ? decision : null, interactive, isDecided ? 20 + _flying.IndexOf(item) : CardStackMath.Visible - depth, $"{CardName(item)}, {index + 1} of {Items.Count}", _returning.TryGetValue(item, out var back) ? back : null);
        }

        bool empty = _position >= Items.Count && _flying.Count == 0;
        if (empty && _empty is null) { ShowEmpty(); }
        else if (!empty && _empty is not null) { HideEmpty(); }
        if (_left is not null)
        {
            _left.IsInert = _right!.IsInert = Top is null;
            _undo!.IsInert = _history.Count == 0;
        }
        RefreshFocus();
    }

    private int IndexOf(object item) { for (int i = 0; i < Items.Count; i++) { if (ReferenceEquals(Items[i], item)) { return i; } } return -1; }

    private void ShowEmpty()
    {
        FrameworkElement content;
        if (EmptyFactory is not null) { content = EmptyFactory(Reset); }
        else
        {
            var (heading, detail) = EmptyText?.Invoke() ?? ("All cards reviewed", null);
            var title = ArcText.Block(heading, 16, 22.4, medium: true);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { title } };
            if (detail is null) { title.Margin = new Thickness(0, 0, 0, 12); }
            else
            {
                title.Margin = new Thickness(0, 0, 0, 8);
                var line = ArcText.Block(detail, 14, 19.6, "TextSecondary");
                line.HorizontalAlignment = HorizontalAlignment.Center;
                line.TextAlignment = TextAlignment.Center;
                line.TextWrapping = TextWrapping.Wrap;
                line.Margin = new Thickness(0, 0, 0, 16);
                stack.Children.Add(line);
            }
            var over = new StackButton("rotate-ccw", "Start over", null) { HorizontalAlignment = HorizontalAlignment.Center, MinHeight = 36 };
            over.Click += (_, _) => Reset();
            stack.Children.Add(over);
            content = stack;
        }
        var box = new Border { Height = CardHeight, VerticalAlignment = VerticalAlignment.Top, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(34), Padding = new Thickness(24), Child = content };
        box.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
        box.BorderBrush = (Brush)FindResource("ArcWpf.Brush.Border");
        _empty = box;
        _stage!.Children.Add(box);
        if (Moving) { Tween.Present(box, true, 8, 4 * 2, Tween.Standard, ArcEasing.Enter); }
    }

    private void HideEmpty()
    {
        var box = _empty!;
        _empty = null;
        if (!IsLoaded) { _stage!.Children.Remove(box); return; }
        Tween.To(box, OpacityProperty, 0, Tween.Instant, null, done: () => _stage?.Children.Remove(box));
    }

    private sealed class StackButton : ToolbarButton
    {
        private readonly string? _tone;
        private readonly ScaleTransform _scale = new();

        // A primary fill carries no border: the same colour drawn twice leaves a pale seam around the rounded edge.
        public StackButton(string icon, string text, string? tone) : base(18, tone == "primary" ? 0 : 1)
        {
            _tone = tone;
            MinHeight = 44;
            Padding = tone == "primary" ? new Thickness(17, 0, 17, 0) : new Thickness(16, 0, 16, 0);
            Roles = Colors;
            RenderTransformOrigin = new Point(.5, .5);
            RenderTransform = _scale;
            var label = ArcText.Block(text, 14, double.NaN, medium: true);
            label.VerticalAlignment = VerticalAlignment.Center;
            label.Foreground = Ink;
            label.Margin = new Thickness(8, 0, 0, 0);
            Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { ArcText.Icon(icon, 16, Ink), label } };
            AutomationProperties.SetName(this, text);
            PressedChanged += pressed =>
            {
                if (IsInert || !IsLoaded || Tween.Level(this) != ArcMotionLevel.Full) { return; }
                Tween.Spring(_scale, ScaleTransform.ScaleXProperty, pressed ? .97 : 1, Tween.Snappy);
                Tween.Spring(_scale, ScaleTransform.ScaleYProperty, pressed ? .97 : 1, Tween.Snappy);
            };
        }

        public new bool IsInert
        {
            get => base.IsInert;
            set { base.IsInert = value; Opacity = value ? .42 : 1; if (value) { Cursor = Cursors.Arrow; } }
        }

        private (string?, string?, string?) Colors(ArcChromeButton _)
        {
            bool live = !IsInert, over = live && IsMouseOver, down = live && IsPressed;
            return _tone switch
            {
                "primary" => (over || down ? "StackAccentStrong" : "StackAccent", over || down ? "StackAccentStrong" : "StackAccent", "Background"),
                "quiet" => (down ? "SurfaceMuted" : null, null, over ? "Foreground" : "TextSecondary"),
                _ => (over || down ? "SurfaceMuted" : "Surface", "Border", "Foreground"),
            };
        }
    }

    private sealed class CardView
    {
        private readonly CardStack _owner;
        private readonly object _item;
        private readonly OwnedMotion _motion = new();
        private readonly SpringDriver _driver;
        private readonly ControlSpring _x, _y, _depth;
        private readonly Grid _card = new();
        private readonly Border _face, _plate, _focus, _shade, _keep, _pass;
        private readonly ScaleTransform _slotScale = new();
        private readonly TranslateTransform _slotMove = new(), _cardMove = new();
        private readonly RotateTransform _rotate = new();
        private readonly List<(int T, double X, double Y)> _samples = [];
        private readonly (Point From, double Tilt, double Delay)? _returnFrom;
        private DispatcherTimer? _delay;
        private CardDecision? _decision;
        private double _tilt = 1, _offsetX, _offsetY, _targetDepth;
        private bool _dragging, _homing, _wasDecided, _settled, _exiting;

        public Grid Slot { get; } = new() { VerticalAlignment = VerticalAlignment.Top };
        public bool Interactive { get; private set; }
        public double X => _x.Position;
        public double Y => _y.Position;
        public double Rotation => _rotate.Angle;
        public double ShownDepth => _depth.Position;
        public double KeepOpacity => _keep.Opacity;
        public double PassOpacity => _pass.Opacity;

        public CardView(CardStack owner, object item, (Point From, double Tilt, double Delay)? returnFrom)
        {
            _owner = owner;
            _item = item;
            _returnFrom = returnFrom;
            bool travel = returnFrom is not null && owner.Moving;
            _settled = returnFrom is null;
            _tilt = travel ? returnFrom!.Value.Tilt : 1;
            _driver = new SpringDriver(Apply);
            _x = _driver.Add(travel ? returnFrom!.Value.From.X : 0, ArcSpring.Smooth);
            _y = _driver.Add(travel ? returnFrom!.Value.From.Y : 0, ArcSpring.Smooth);
            _depth = _driver.Add(0, ArcSpring.Smooth);

            Slot.Height = owner.CardHeight;
            Slot.RenderTransformOrigin = new Point(.5, 1);
            Slot.RenderTransform = new TransformGroup { Children = { _slotScale, _slotMove } };
            _card.RenderTransformOrigin = new Point(.5, .5);
            _card.RenderTransform = new TransformGroup { Children = { _rotate, _cardMove } };
            _card.Opacity = returnFrom is not null ? 0 : 1;

            // Resting cards sit flat on their borders; a card in the hand or in flight floats and casts a shadow.
            _plate = FloatingShadow.Plate(34);
            _plate.Opacity = 0;
            var content = new ContentPresenter { Content = item, ContentTemplate = owner.CardTemplate };
            _shade = new Border { IsHitTestVisible = false, Opacity = 0 };
            _shade.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.StackShade");
            var clip = new Grid { Children = { content, _shade } };
            _face = new Border { CornerRadius = new CornerRadius(34), BorderThickness = new Thickness(1), Child = clip };
            _face.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
            _face.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
            clip.SizeChanged += (_, e) => clip.Clip = new RectangleGeometry(new Rect(e.NewSize), 33, 33);
            _keep = Stamp("check", owner.RightLabel, true);
            _pass = Stamp("x", owner.LeftLabel, false);
            // Keyboard focus on the stage outlines the top card instead of the whole stage.
            _focus = new Border { Margin = new Thickness(-5), BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(39), IsHitTestVisible = false, Visibility = Visibility.Collapsed, Opacity = .72 };
            _focus.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Focus");
            _card.Children.Add(_plate);
            _card.Children.Add(_face);
            _card.Children.Add(_keep);
            _card.Children.Add(_pass);
            _card.Children.Add(_focus);
            Slot.Children.Add(_card);

            _card.MouseLeftButtonDown += (_, e) =>
            {
                if (ButtonWithin(e.OriginalSource as DependencyObject)) { return; }
                Down(e.GetPosition(_owner._stage), e.Timestamp);
                if (_dragging) { _card.CaptureMouse(); e.Handled = true; _owner._stage?.Focus(); }
            };
            _card.MouseMove += (_, e) => { if (_dragging) { Move(e.GetPosition(_owner._stage), e.Timestamp); } };
            _card.MouseLeftButtonUp += (_, e) => { if (_dragging) { _card.ReleaseMouseCapture(); Up(e.Timestamp, false); } };
            _card.LostMouseCapture += (_, e) => { if (_dragging) { Up(e.Timestamp, true); } };
            Apply();
        }

        private bool ButtonWithin(DependencyObject? source)
        {
            for (var node = source; node is not null && !ReferenceEquals(node, _card); node = VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node))
            {
                if (node is ButtonBase or TextBox or ComboBox or System.Windows.Documents.Hyperlink) { return true; }
            }
            return false;
        }

        private Border Stamp(string icon, string text, bool keep)
        {
            var stroke = new SolidColorBrush();
            stroke.Color = Tween.Resolve(_owner, keep ? "ArcWpf.Brush.ArcSuccess" : "ArcWpf.Brush.TextSecondary");
            var label = ArcText.Block(text, 14, 19.6, medium: true);
            label.Margin = new Thickness(6, 0, 0, 0);
            var stamp = new Border { Height = 32, CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1), Padding = new Thickness(10, 0, 12, 0), Margin = new Thickness(16), HorizontalAlignment = keep ? HorizontalAlignment.Left : HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false, Opacity = 0, Child = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { ArcText.Icon(icon, 16, stroke, 2), label } } };
            stamp.SetResourceReference(Border.BackgroundProperty, keep ? "ArcWpf.Brush.StackStampKeep" : "ArcWpf.Brush.SurfaceRaised");
            stamp.SetResourceReference(Border.BorderBrushProperty, keep ? "ArcWpf.Brush.StackStampKeepEdge" : "ArcWpf.Brush.Border");
            return stamp;
        }

        public void ShowFocus(bool show) => _focus.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        public void Stop() { _driver.Stop(); _motion.Stop(); _delay?.Stop(); }
        public void Finish()
        {
            _driver.Finish();
            if (_exiting && _decision is not null) { _exiting = false; _owner.Gone(_item); }
        }

        public void Update(int depth, CardDecision? decision, bool interactive, int zIndex, string label, (Point From, double Tilt, double Delay)? returnFrom)
        {
            Panel.SetZIndex(Slot, zIndex);
            Interactive = interactive;
            Slot.IsHitTestVisible = interactive;
            _card.Cursor = interactive ? Cursors.Hand : null;
            AutomationProperties.SetName(_card, interactive ? label : "");
            _targetDepth = depth;
            UpdateDepth();
            if (_decision != decision)
            {
                _decision = decision;
                if (decision is { } chosen) { Throw(chosen); }
            }
            RefreshStamps();
            if (decision is null && (_wasDecided || !_settled)) { Return(returnFrom?.Delay ?? _returnFrom?.Delay ?? 0); }
            UpdateShadow();
        }

        private bool _depthPlaced;

        public void UpdateDepth()
        {
            double target = _targetDepth - (_targetDepth > 0 ? _owner._lift * CardStackMath.Lift : 0);
            if (!_depthPlaced || !_owner.Moving) { _depthPlaced = true; _driver.Snap(_depth, target); return; }
            _driver.Animate(_depth, target, ArcSpring.Smooth);
        }

        private void Throw(CardDecision decision)
        {
            _dragging = false;
            _homing = false;
            _wasDecided = true;
            _delay?.Stop();
            if (_card.IsMouseCaptured) { _card.ReleaseMouseCapture(); }
            int direction = decision == CardDecision.Right ? 1 : -1;
            double width = _card.ActualWidth > 0 ? _card.ActualWidth : 320;
            // Buttons and keys throw with a default flick; a drag keeps the finger's velocity.
            var velocity = _owner._flings.Remove(_item, out var fling) ? fling : new Vector(direction * 900, -120);
            var exit = CardStackMath.Exit(direction, _x.Position, _y.Position, velocity, width);
            _owner._exits[_item] = (exit, _tilt);
            UpdateShadow();
            _exiting = true;
            if (!_owner.Moving)
            {
                Tween.To(_card, UIElement.OpacityProperty, 0, _owner.IsLoaded ? .15 : .001, null, done: FinishExit);
                return;
            }
            double seconds = ArcSpring.Of(.5);
            _x.Launch(velocity.X);
            _y.Launch(velocity.Y);
            _driver.Animate(_x, exit.X, seconds);
            _driver.Animate(_y, exit.Y, seconds);
            Tween.To(_card, UIElement.OpacityProperty, 0, .22, ArcEasing.Standard, .12, FinishExit);
        }

        private void FinishExit() { if (_exiting) { _exiting = false; _owner.Gone(_item); } }

        // Undo, or a first mount from where the card left: come back from there, catching any flight in progress.
        private void Return(double delay)
        {
            _wasDecided = false;
            _settled = true;
            _exiting = false;
            UpdateShadow();
            if (!_owner.Moving)
            {
                _x.Snap(0); _y.Snap(0); Apply();
                Tween.To(_card, UIElement.OpacityProperty, 1, .15, null, delay);
                return;
            }
            _homing = true;
            void Fly()
            {
                _driver.Animate(_x, 0, ArcSpring.Smooth);
                _driver.Animate(_y, 0, ArcSpring.Smooth);
                Tween.To(_card, UIElement.OpacityProperty, 1, Tween.Fast, ArcEasing.Enter);
            }
            if (delay <= 0) { Fly(); return; }
            _delay = new DispatcherTimer { Interval = TimeSpan.FromSeconds(delay) };
            _delay.Tick += (_, _) => { _delay.Stop(); Fly(); };
            _delay.Start();
        }

        public void Down(Point at, int timestamp)
        {
            if (!Interactive) { return; }
            _driver.Stop();
            _delay?.Stop();
            _homing = false;
            // Grabbing the top half tips the card the way the hand moves; grabbing the bottom half tips it the other way.
            if (Math.Abs(_x.Position) < 2) { _tilt = at.Y < _owner.CardHeight / 2 ? 1 : -1; }
            _x.Snap(_x.Position); _y.Snap(_y.Position);
            _offsetX = at.X - _x.Position;
            _offsetY = at.Y - CardStackMath.UnresistY(_y.Position);
            _samples.Clear();
            _samples.Add((timestamp, at.X, at.Y));
            _dragging = true;
            UpdateShadow();
        }

        public void Move(Point at, int timestamp)
        {
            if (!_dragging || !Interactive) { return; }
            _x.Snap(at.X - _offsetX);
            _y.Snap(CardStackMath.ResistY(at.Y - _offsetY));
            _samples.Add((timestamp, at.X, at.Y));
            if (_samples.Count > 12) { _samples.RemoveAt(0); }
            Apply();
            ReportLift();
        }

        public void Up(int timestamp, bool cancel)
        {
            if (!_dragging) { return; }
            _dragging = false;
            UpdateShadow();
            if (!Interactive) { return; }
            var velocity = cancel ? default : CardStackMath.Velocity(_samples, timestamp);
            double width = _card.ActualWidth > 0 ? _card.ActualWidth : 320;
            if (_owner.Release(_item, new Point(_x.Position, _y.Position), velocity, width)) { return; }
            // Below the threshold the card springs home, keeping the release velocity so it settles instead of stopping dead.
            if (!_owner.Moving) { _x.Snap(0); _y.Snap(0); Apply(); ReportLift(); return; }
            _x.Launch(velocity.X);
            _y.Launch(velocity.Y);
            _driver.Animate(_x, 0, ArcSpring.Morph);
            _driver.Animate(_y, 0, ArcSpring.Morph);
        }

        private void ReportLift()
        {
            if (!Interactive || _homing) { return; }
            double y = _y.Position * .5;
            _owner.SetLift(Math.Clamp(Math.Sqrt(_x.Position * _x.Position + y * y) / CardStackMath.LiftDistance, 0, 1));
        }

        private void RefreshStamps()
        {
            // Cards returning to the back of the stack carry no verdict.
            var visibility = Interactive || _decision is not null ? Visibility.Visible : Visibility.Hidden;
            _keep.Visibility = _pass.Visibility = visibility;
        }

        private bool? _shadowOn;

        private void UpdateShadow()
        {
            bool on = _dragging || _decision is not null;
            if (_shadowOn == on) { return; }
            _shadowOn = on;
            if (_owner.Moving) { Tween.To(_plate, UIElement.OpacityProperty, on ? 1 : 0, Tween.Fast, ArcEasing.Standard); } else { Tween.Set(_plate, UIElement.OpacityProperty, on ? 1 : 0); }
        }

        private void Apply()
        {
            double x = _x.Position, y = _y.Position, depth = _depth.Position;
            _cardMove.X = x; _cardMove.Y = y;
            _rotate.Angle = x * CardStackMath.Rotate * _tilt;
            double scale = 1 - depth * CardStackMath.Shrink;
            _slotScale.ScaleX = _slotScale.ScaleY = scale;
            _slotMove.Y = depth * CardStackMath.Peek;
            Slot.Opacity = CardStackMath.Clamp(CardStackMath.Visible - 2, CardStackMath.Visible - 1, 1, 0, depth);
            _shade.Opacity = CardStackMath.Clamp(0, 2, 0, 1, depth);
            _keep.Opacity = CardStackMath.Clamp(16, 96, 0, 1, x);
            _pass.Opacity = CardStackMath.Clamp(-96, -16, 1, 0, x);
            if (_driver.IsRunning && !_dragging) { ReportLift(); }
            if (_homing && _driver.IsRunning && Math.Abs(x) < .5 && Math.Abs(y) < .5) { _homing = false; }
        }
    }
}
