using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public enum InViewTitleVariant { Blur, Word, Line, Tracking, Wipe }

// in-view-title.tsx (frozen 2026-11-03): a section title that reveals itself once it scrolls into view. Titles already above the viewport show at once.
public class InViewTitle : Control
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(InViewTitle), new FrameworkPropertyMetadata("", (d, _) => ((InViewTitle)d).Rebuild()));
    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(nameof(Variant), typeof(InViewTitleVariant), typeof(InViewTitle), new FrameworkPropertyMetadata(InViewTitleVariant.Blur, (d, _) => ((InViewTitle)d).Rebuild()));
    public static readonly DependencyProperty LinesProperty = DependencyProperty.Register(nameof(Lines), typeof(IReadOnlyList<string>), typeof(InViewTitle), new FrameworkPropertyMetadata(null, (d, _) => ((InViewTitle)d).Rebuild()));
    public static readonly DependencyProperty OnceProperty = DependencyProperty.Register(nameof(Once), typeof(bool), typeof(InViewTitle), new FrameworkPropertyMetadata(true, (d, _) => ((InViewTitle)d).Evaluate()));
    public static readonly DependencyProperty TrackingProperty = DependencyProperty.Register(nameof(Tracking), typeof(double), typeof(InViewTitle), new FrameworkPropertyMetadata(0d, (d, _) => ((InViewTitle)d).Rebuild()));
    public static readonly DependencyProperty LineRatioProperty = DependencyProperty.Register(nameof(LineRatio), typeof(double), typeof(InViewTitle), new FrameworkPropertyMetadata(1.1, (d, _) => ((InViewTitle)d).Rebuild()));
    private static readonly DependencyProperty WipeProperty = DependencyProperty.Register("Wipe", typeof(double), typeof(InViewTitle), new FrameworkPropertyMetadata(1d, (d, _) => ((InViewTitle)d).PaintWipe()));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public InViewTitleVariant Variant { get => (InViewTitleVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    /// <summary>Where the Line variant breaks; one line by default.</summary>
    public IReadOnlyList<string>? Lines { get => (IReadOnlyList<string>?)GetValue(LinesProperty); set => SetValue(LinesProperty, value); }
    /// <summary>Reveal the first time only; false replays on every entry.</summary>
    public bool Once { get => (bool)GetValue(OnceProperty); set => SetValue(OnceProperty, value); }
    /// <summary>Letter spacing in em.</summary>
    public double Tracking { get => (double)GetValue(TrackingProperty); set => SetValue(TrackingProperty, value); }
    /// <summary>Line height as a multiple of the font size.</summary>
    public double LineRatio { get => (double)GetValue(LineRatioProperty); set => SetValue(LineRatioProperty, value); }

    private const double Amount = .45, WordStep = .06, WordSpan = .4, LineStep = .08, CharStep = .032, TrackEm = .06;

    /// <summary>One animated element, with where it hides. Transform settles longest, blur clears sooner, and opacity leads, so text is readable before it has fully arrived.</summary>
    private sealed record Part(UIElement View, TranslateTransform Move, double HideX, double HideY, double HideBlur, bool Fades, double Delay, double Settle);

    private readonly List<Part> _parts = [];
    private Decorator? _host;
    private Border? _wipeBox;
    private LinearGradientBrush? _wipeMask;
    private ScrollViewer? _scroller;
    private bool _visible, _seen, _passed;

    static InViewTitle() => DefaultStyleKeyProperty.OverrideMetadata(typeof(InViewTitle), new FrameworkPropertyMetadata(typeof(InViewTitle)));

    public InViewTitle()
    {
        AutomationProperties.SetHeadingLevel(this, AutomationHeadingLevel.Level2);
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
        SizeChanged += (_, _) => Evaluate();
    }

    private ArcMotionLevel Level => Tween.Level(this);

    internal bool IsRevealedForTest => _visible;
    internal int PartCountForTest => _parts.Count;
    internal double PartOpacityForTest(int part) => _parts[part].View.Opacity;
    internal double PartDelayForTest(int part) => _parts[part].Delay;
    internal double PartXForTest(int part) => _parts[part].Move.X;
    internal double PartYForTest(int part) => _parts[part].Move.Y;
    internal bool PartBlurredForTest(int part) => _parts[part].View.Effect is not null;
    internal double WipeForTest => (double)GetValue(WipeProperty);
    internal bool IsWipeMaskedForTest => _wipeBox?.OpacityMask is not null;
    internal Point PartAtForTest(int part) => _parts[part].View.TranslatePoint(new Point(), this);
    internal void EvaluateForTest() => Evaluate();
    internal void FinishMotionForTest() => Snap(_visible);

    protected override AutomationPeer OnCreateAutomationPeer() => new ArcTextPeer(this, () => Text);

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _host = GetTemplateChild("PART_Root") as Decorator;
        Rebuild();
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontSizeProperty || e.Property == FontFamilyProperty || e.Property == FontWeightProperty) { Rebuild(); }
    }

    /// <summary>Hides the title and runs the reveal again, the equivalent of remounting it.</summary>
    public void Replay()
    {
        _seen = _passed = _visible = false;
        Snap(false);
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, Evaluate);
    }

    private void Attach()
    {
        Detach();
        for (DependencyObject? at = VisualTreeHelper.GetParent(this); at is not null; at = VisualTreeHelper.GetParent(at))
        {
            if (at is ScrollViewer scroller) { _scroller = scroller; break; }
        }
        if (_scroller is not null) { _scroller.ScrollChanged += OnScroll; }
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, Evaluate);
    }

    private void Detach()
    {
        if (_scroller is not null) { _scroller.ScrollChanged -= OnScroll; _scroller = null; }
    }

    private void OnScroll(object sender, ScrollChangedEventArgs e) => Evaluate();

    private void Evaluate()
    {
        if (!IsLoaded) { return; }
        var (inView, above) = Measure();
        if (above) { _passed = true; }
        if (inView) { _seen = true; }
        bool visible = Level != ArcMotionLevel.Full || _passed || inView || (Once && _seen);
        if (visible == _visible) { return; }
        _visible = visible;
        if (Level != ArcMotionLevel.Full) { Snap(true); } else { Run(visible); }
    }

    private (bool InView, bool Above) Measure()
    {
        if (!IsVisible || ActualHeight <= 0 || ActualWidth <= 0) { return (false, false); }
        FrameworkElement? viewport = _scroller;
        if (viewport is null) { viewport = Window.GetWindow(this)?.Content as FrameworkElement; }
        if (viewport is null || !viewport.IsAncestorOf(this)) { return (false, false); }
        var box = TransformToAncestor(viewport).TransformBounds(new Rect(0, 0, ActualWidth, ActualHeight));
        var view = _scroller is null ? new Rect(0, 0, viewport.ActualWidth, viewport.ActualHeight) : new Rect(0, 0, _scroller.ViewportWidth, _scroller.ViewportHeight);
        var seen = Rect.Intersect(box, view);
        double share = seen.IsEmpty ? 0 : seen.Width * seen.Height / (box.Width * box.Height);
        return (share >= Amount, box.Top < 0);
    }

    private void Rebuild()
    {
        if (_host is null) { return; }
        _parts.Clear();
        _wipeBox = null;
        _wipeMask = null;
        var words = (Text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        double em = FontSize, line = LineRatio * em;
        double step = Math.Min(WordStep, WordSpan / Math.Max(words.Length, 1));
        _host.Child = Variant switch
        {
            InViewTitleVariant.Word => BuildWords(words, em, line, step),
            InViewTitleVariant.Line => BuildLines(em, line),
            InViewTitleVariant.Tracking => BuildTracking(words, em, line),
            InViewTitleVariant.Wipe => BuildWipe(words, em, line),
            _ => BuildBlur(words, em, line, step),
        };
        Snap(_visible);
        if (IsLoaded) { Dispatcher.BeginInvoke(DispatcherPriority.Loaded, Evaluate); }
    }

    private TrackedText Tracked(string text) => new() { Text = text, Tracking = Tracking, LineRatio = LineRatio, IsHitTestVisible = false };

    private WordFlow Flow(double line, IReadOnlyList<UIElement> children, double gap)
    {
        var flow = new WordFlow { LineHeight = line, Gap = gap };
        foreach (var child in children) { flow.Children.Add(child); }
        return flow;
    }

    private UIElement BuildBlur(string[] words, double em, double line, double step)
    {
        var views = new List<UIElement>();
        for (int i = 0; i < words.Length; i++)
        {
            var view = Tracked(words[i]);
            var move = new TranslateTransform();
            view.RenderTransform = move;
            views.Add(view);
            _parts.Add(new Part(view, move, 0, .2 * em, 8, true, i * step, .8));
        }
        return Gapped(Flow(line, views, 0), views);
    }

    // The clip hides the rise; padding with matching negative margins leaves room for descenders without changing layout.
    private UIElement BuildWords(string[] words, double em, double line, double step)
    {
        var clips = new List<UIElement>();
        var views = new List<UIElement>();
        for (int i = 0; i < words.Length; i++)
        {
            var view = Tracked(words[i]);
            var move = new TranslateTransform();
            view.RenderTransform = move;
            views.Add(view);
            clips.Add(new Border { Padding = new Thickness(.14 * em, .08 * em, .14 * em, .2 * em), Margin = new Thickness(-.14 * em, -.08 * em, -.14 * em, -.2 * em), Child = view, ClipToBounds = true, IsHitTestVisible = false });
            _parts.Add(new Part(view, move, 0, .9 * em, 0, true, i * step, .8));
        }
        return Gapped(Flow(line, clips, 0), views);
    }

    private UIElement BuildLines(double em, double line)
    {
        var lines = Lines is { Count: > 0 } ? Lines : [Text ?? ""];
        var stack = new StackPanel();
        for (int i = 0; i < lines.Count; i++)
        {
            var view = Tracked(lines[i]);
            view.HorizontalAlignment = HorizontalAlignment.Left;
            var move = new TranslateTransform();
            view.RenderTransform = move;
            // In the source each clip's margin collapses with the next, which leaves .06em between lines and none above the first.
            stack.Children.Add(new Border { Padding = new Thickness(.12 * em, .06 * em, .12 * em, .18 * em), Margin = new Thickness(-.12 * em, i == 0 ? -.06 * em : 0, -.12 * em, -.18 * em), Child = view, ClipToBounds = true, IsHitTestVisible = false });
            _parts.Add(new Part(view, move, 0, 1.12 * line, 0, true, i * LineStep, .86));
        }
        return stack;
    }

    private UIElement BuildTracking(string[] words, double em, double line)
    {
        var groups = new List<UIElement>();
        var firsts = new List<UIElement>();
        for (int w = 0; w < words.Length; w++)
        {
            double delay = w * CharStep;
            var letters = System.Globalization.StringInfo.ParseCombiningCharacters(words[w]);
            var row = new StackPanel { Orientation = Orientation.Horizontal, IsHitTestVisible = false };
            double center = (letters.Length - 1) / 2d;
            for (int i = 0; i < letters.Length; i++)
            {
                int end = i + 1 < letters.Length ? letters[i + 1] : words[w].Length;
                var view = Tracked(words[w][letters[i]..end]);
                var move = new TranslateTransform();
                view.RenderTransform = move;
                row.Children.Add(view);
                if (w == 0 && i == 0) { firsts.Add(view); }
                _parts.Add(new Part(view, move, (center - i) * TrackEm * em, 0, 0, false, delay, 1));
            }
            var group = new TranslateTransform();
            row.RenderTransform = group;
            groups.Add(row);
            _parts.Add(new Part(row, group, 0, 0, 4, true, delay, .9));
        }
        return Gapped(Flow(line, groups, 0), firsts);
    }

    private UIElement BuildWipe(string[] words, double em, double line)
    {
        var views = words.Select(word => (UIElement)Tracked(word)).ToList();
        var flow = Gapped(Flow(line, views, 0), views);
        var move = new TranslateTransform();
        _wipeMask = new LinearGradientBrush { MappingMode = BrushMappingMode.Absolute, GradientStops = { new GradientStop(Colors.White, 0), new GradientStop(Colors.Transparent, 1) } };
        _wipeBox = new Border { Padding = new Thickness(.12 * em, .1 * em, .12 * em, .22 * em), Margin = new Thickness(-.12 * em, -.1 * em, -.12 * em, -.22 * em), Child = flow, Background = Brushes.Transparent, RenderTransform = move, IsHitTestVisible = false };
        _wipeBox.SizeChanged += (_, _) => PaintWipe();
        _parts.Add(new Part(_wipeBox, move, -.12 * em, 0, 0, false, 0, .9));
        return _wipeBox;
    }

    private static WordFlow Gapped(WordFlow flow, List<UIElement> views)
    {
        // The space's width depends on inherited font properties, which only exist once the words are in the tree.
        flow.Loaded += (_, _) => { if (views.Count > 0 && views[0] is TrackedText first) { flow.Gap = first.SpaceWidth; flow.InvalidateMeasure(); } };
        if (views.Count > 0 && views[0] is TrackedText sample && flow.IsLoaded) { flow.Gap = sample.SpaceWidth; }
        return flow;
    }

    private void PaintWipe()
    {
        if (_wipeBox is null || _wipeMask is null) { return; }
        double w = _wipeBox.ActualWidth, p = (double)GetValue(WipeProperty), offset = -1.3 * w * p;
        // The mask is 2.3 times the title's width with a feathered middle, so its soft edge sits fully outside the title at both ends of the sweep.
        _wipeMask.StartPoint = new Point(offset + .44 * 2.3 * w, 0);
        _wipeMask.EndPoint = new Point(offset + .56 * 2.3 * w, 0);
    }

    private void Run(bool visible)
    {
        foreach (var part in _parts)
        {
            if (visible) { Show(part); } else { Hide(part); }
        }
        if (_wipeBox is null) { return; }
        _wipeBox.OpacityMask = _wipeMask;
        if (visible) { Tween.To(this, WipeProperty, 0, .9, ArcEasing.Standard, 0, () => { if (_visible && _wipeBox is not null) { _wipeBox.OpacityMask = null; } }); }
        else { Tween.To(this, WipeProperty, 1, Tween.Exit, ArcEasing.Standard); }
    }

    private static void Show(Part part)
    {
        if (part.HideX != 0) { Tween.To(part.Move, TranslateTransform.XProperty, 0, part.Settle, ArcEasing.Enter, part.Delay); }
        if (part.HideY != 0) { Tween.To(part.Move, TranslateTransform.YProperty, 0, part.Settle, ArcEasing.Enter, part.Delay); }
        if (part.HideBlur != 0) { TextMotion.Blur(part.View, part.HideBlur, 0, part.Settle * .78, ArcEasing.Enter, part.Delay); }
        if (part.Fades) { Tween.To(part.View, OpacityProperty, 1, part.Settle * .56, ArcEasing.Standard, part.Delay); }
    }

    // Hiding is a quick fade with no stagger.
    private static void Hide(Part part)
    {
        if (part.HideX != 0) { Tween.To(part.Move, TranslateTransform.XProperty, part.HideX, Tween.Exit, ArcEasing.Standard); }
        if (part.HideY != 0) { Tween.To(part.Move, TranslateTransform.YProperty, part.HideY, Tween.Exit, ArcEasing.Standard); }
        if (part.HideBlur != 0) { TextMotion.Blur(part.View, 0, part.HideBlur, Tween.Exit, ArcEasing.Standard); }
        if (part.Fades) { Tween.To(part.View, OpacityProperty, 0, Tween.Exit, ArcEasing.Standard); }
    }

    private void Snap(bool visible)
    {
        foreach (var part in _parts)
        {
            Tween.Set(part.Move, TranslateTransform.XProperty, visible ? 0 : part.HideX);
            Tween.Set(part.Move, TranslateTransform.YProperty, visible ? 0 : part.HideY);
            if (part.Fades) { Tween.Set(part.View, OpacityProperty, visible ? 1 : 0); }
            part.View.Effect = visible || part.HideBlur == 0 ? null : new System.Windows.Media.Effects.BlurEffect { Radius = part.HideBlur * 2 };
        }
        Tween.Set(this, WipeProperty, visible ? 0 : 1);
        if (_wipeBox is not null) { _wipeBox.OpacityMask = visible ? null : _wipeMask; PaintWipe(); }
    }
}
