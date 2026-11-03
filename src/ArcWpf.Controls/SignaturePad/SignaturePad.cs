using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace ArcWpf.Controls;

/// <summary>Carries a finished export. Set <see cref="Handled"/> to keep the default save dialog from opening.</summary>
public sealed class SignatureExportEventArgs(string kind, string fileName, byte[] data) : EventArgs
{
    public string Kind { get; } = kind;
    public string FileName { get; } = fileName;
    public byte[] Data { get; } = data;
    public bool Handled { get; set; }
}

/// <summary>
/// A signature field with real ink. Width follows pen pressure, or speed for a mouse or finger, and the ends taper. It keeps undo and
/// redo, clears with a wipe across the pad, replays in the original rhythm, and exports a cropped PNG or SVG.
/// Keyboard: Ctrl+Z undoes, Ctrl+Shift+Z or Ctrl+Y redoes, Delete clears.
/// </summary>
[TemplatePart(Name = "PART_Root", Type = typeof(Decorator))]
public class SignaturePad : Control
{
    private const double INSTANT = .12, FAST = .16, STANDARD = .24, WIPE = .56, MORPH = .42;
    private const int HISTORY_LIMIT = 60;

    public static readonly DependencyProperty SignerProperty = DependencyProperty.Register(nameof(Signer), typeof(string), typeof(SignaturePad), new PropertyMetadata(null, (d, _) => ((SignaturePad)d).RefreshGuide()));
    public static readonly DependencyProperty HintProperty = DependencyProperty.Register(nameof(Hint), typeof(string), typeof(SignaturePad), new PropertyMetadata("Sign here", (d, _) => ((SignaturePad)d).RefreshGuide()));
    public static readonly DependencyProperty InkColorProperty = DependencyProperty.Register(nameof(InkColor), typeof(SignatureInkColor), typeof(SignaturePad), new FrameworkPropertyMetadata(SignatureInkColor.Black, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((SignaturePad)d).RefreshChoices(true)));
    public static readonly DependencyProperty InkWidthProperty = DependencyProperty.Register(nameof(InkWidth), typeof(SignatureInkWidth), typeof(SignaturePad), new FrameworkPropertyMetadata(SignatureInkWidth.Medium, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((SignaturePad)d).RefreshChoices(true)));
    public static readonly DependencyProperty FileNameProperty = DependencyProperty.Register(nameof(FileName), typeof(string), typeof(SignaturePad), new PropertyMetadata("signature"));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(SignaturePad), new PropertyMetadata("Signature pad", (d, _) => ((SignaturePad)d).RefreshPadName()));
    private static readonly DependencyProperty WipeProgressProperty = DependencyProperty.Register("WipeProgress", typeof(double), typeof(SignaturePad), new PropertyMetadata(0d, (d, _) => ((SignaturePad)d).ApplyWipe()));

    public string? Signer { get => (string?)GetValue(SignerProperty); set => SetValue(SignerProperty, value); }
    public string Hint { get => (string)GetValue(HintProperty); set => SetValue(HintProperty, value); }
    public SignatureInkColor InkColor { get => (SignatureInkColor)GetValue(InkColorProperty); set => SetValue(InkColorProperty, value); }
    public SignatureInkWidth InkWidth { get => (SignatureInkWidth)GetValue(InkWidthProperty); set => SetValue(InkWidthProperty, value); }
    public string FileName { get => (string)GetValue(FileNameProperty); set => SetValue(FileNameProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>Raised after every change to the strokes.</summary>
    public event EventHandler? StrokesChanged;
    /// <summary>Raised with the exported bytes. Unhandled, a save dialog asks where to put the file.</summary>
    public event EventHandler<SignatureExportEventArgs>? Exporting;

    public IReadOnlyList<SignatureInkStroke> Strokes => _present;
    public bool CanUndo => _past.Count > 0;
    public bool CanRedo => _future.Count > 0;

    private readonly List<List<SignatureInkStroke>> _past = [];
    private readonly List<List<SignatureInkStroke>> _future = [];
    private List<SignatureInkStroke> _present = [];
    private string? _fresh;
    private readonly Dictionary<string, Path> _paths = [];
    private readonly OwnedMotion _motion = new();
    private Decorator? _root;

    private PadSurface _pad = null!;
    private Grid _guide = null!, _ink = null!, _sweep = null!;
    private Canvas _inkCanvas = null!;
    private Path _live = null!;
    private Ellipse _nib = null!;
    private TextBlock? _signer;
    private TextBlock? _hintBlock;
    private bool _hintShown;
    private readonly RectangleGeometry _wipeClip = new();
    private SignatureChoice _colors = null!, _widths = null!;
    private ArcChromeButton _undo = null!, _redo = null!, _replayButton = null!, _clear = null!;
    private SwapGlyph _replayGlyph = null!;
    private SignatureExportButton _png = null!, _svg = null!;

    private enum Pointer { None, Mouse, Stylus }
    private Pointer _pointer;
    private SignatureInkStroke? _liveStroke;
    private long _stamp;
    private bool _wiping;
    private Action? _finishWipe;
    private int _counter;
    private readonly string _uid = Guid.NewGuid().ToString("N")[..6];

    private (double Begin, double Speed, double Total, double[] Offsets)? _replay;
    private System.Windows.Threading.DispatcherTimer? _savedTimer;

    static SignaturePad() => DefaultStyleKeyProperty.OverrideMetadata(typeof(SignaturePad), new FrameworkPropertyMetadata(typeof(SignaturePad)));

    public SignaturePad()
    {
        Unloaded += (_, _) => { StopReplay(); _finishWipe?.Invoke(); _savedTimer?.Stop(); };
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _root = GetTemplateChild("PART_Root") as Decorator;
        if (_root is null) { return; }
        _root.Child = Build();
        RefreshGuide();
        RefreshPadName();
        RefreshChrome(false);
        SyncInk();
    }

    private bool Reduced => ArcMotion.GetLevel(this) != ArcMotionLevel.Full;
    private bool Animates => IsLoaded && ArcMotion.GetLevel(this) != ArcMotionLevel.Off;

    // ---- Tree ----

    private UIElement Build()
    {
        _guide = new Grid { IsHitTestVisible = false };
        var baseline = new DashedRule { Height = 1, VerticalAlignment = VerticalAlignment.Top };
        var mark = new Border { Width = 14, Height = 14, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        var markIcon = ArcText.Icon("x", 14, Brushes.Transparent);
        markIcon.SetResourceReference(ArcDataGridIcon.StrokeProperty, "ArcWpf.Brush.TextMuted");
        mark.Child = markIcon;
        _guide.Children.Add(baseline);
        _guide.Children.Add(mark);
        _signer = ArcText.Block("", 12, 16.8, "TextMuted");
        _signer.HorizontalAlignment = HorizontalAlignment.Left;
        _signer.VerticalAlignment = VerticalAlignment.Top;
        _guide.Children.Add(_signer);
        _guide.SizeChanged += OnGuideSize;

        _inkCanvas = new Canvas { Width = SignatureInk.PadWidth, Height = SignatureInk.PadHeight };
        _live = new Path();
        _nib = new Ellipse { Opacity = .35, Visibility = Visibility.Collapsed };
        _inkCanvas.Children.Add(_live);
        _inkCanvas.Children.Add(_nib);
        // The svg stretches with preserveAspectRatio none, so strokes scale with the pad.
        var viewbox = new Viewbox { Stretch = Stretch.Fill, Child = _inkCanvas };
        _ink = new Grid { IsHitTestVisible = false };
        _ink.Children.Add(viewbox);
        _ink.SizeChanged += (_, _) => ApplyWipe();

        _sweep = BuildSweep();

        var surface = new Grid();
        surface.Children.Add(_guide);
        surface.Children.Add(_ink);
        surface.Children.Add(_sweep);
        _pad = new PadSurface(this, surface);
        _pad.PreviewMouseLeftButtonDown += OnMouseDown;
        _pad.PreviewMouseMove += OnMouseMove;
        _pad.PreviewMouseLeftButtonUp += OnMouseUp;
        _pad.LostMouseCapture += OnLostCapture;
        _pad.StylusDown += OnStylusDown;
        _pad.StylusMove += OnStylusMove;
        _pad.StylusUp += OnStylusUp;
        _pad.KeyDown += OnPadKeyDown;

        _colors = new SignatureChoice("Ink color", Enum.GetValues<SignatureInkColor>().Select(color => new SignatureChoice.Option(color, color + " ink", Swatch(color))).ToList(), value => { InkColor = (SignatureInkColor)value; Announcer.Say(this, $"{InkColor} ink selected"); }, this);
        _widths = new SignatureChoice("Stroke width", Enum.GetValues<SignatureInkWidth>().Select(width => new SignatureChoice.Option(width, width.ToString(), Dot(width))).ToList(), value => { InkWidth = (SignatureInkWidth)value; Announcer.Say(this, $"{InkWidth} nib selected"); }, this);
        var divider = new Border { Width = 1, Height = 20, Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        divider.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Border");
        var left = new StackPanel { Orientation = Orientation.Horizontal };
        left.Children.Add(_colors);
        left.Children.Add(divider);
        left.Children.Add(_widths);

        _undo = Icon("Undo", "undo-2", Undo);
        _redo = Icon("Redo", "redo-2", Redo);
        _replayGlyph = new SwapGlyph();
        _replayButton = new SignatureIconButton("Replay signature", ToggleReplay, () => _replay is not null) { Content = _replayGlyph };
        _clear = Icon("Clear signature", "eraser", Clear);
        var right = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var button in new[] { _undo, _redo, _replayButton, _clear }) { if (right.Children.Count > 0) { button.Margin = new Thickness(2, 0, 0, 0); } right.Children.Add(button); }
        var toolbar = new SpaceBetweenWrap { Margin = new Thickness(0, 10, 0, 0) };
        toolbar.Children.Add(left);
        toolbar.Children.Add(right);

        var saveAs = ArcText.Block("Save as", 12, 16.8, "TextMuted");
        saveAs.VerticalAlignment = VerticalAlignment.Center;
        saveAs.Margin = new Thickness(0, 0, 10, 0);
        _png = new SignatureExportButton("png", () => Save("png"));
        _svg = new SignatureExportButton("svg", () => Save("svg")) { Margin = new Thickness(6, 0, 0, 0) };
        var exports = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        exports.Children.Add(saveAs);
        exports.Children.Add(_png);
        exports.Children.Add(_svg);

        var column = new StackPanel();
        column.Children.Add(_pad);
        column.Children.Add(toolbar);
        column.Children.Add(exports);
        ThemeWatch.Add(column, () => { RefreshInkBrushes(); RefreshChrome(false); });
        return column;

        ArcChromeButton Icon(string label, string icon, Action run)
        {
            var button = new SignatureIconButton(label, run, () => false);
            button.Content = ArcText.Icon(icon, 18, button.Ink);
            return button;
        }
    }

    private UIElement Swatch(SignatureInkColor color)
    {
        var dot = new Ellipse { Width = 16, Height = 16, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        dot.SetResourceReference(Shape.FillProperty, "ArcWpf.Brush." + RoleOf(color));
        return dot;
    }

    private static UIElement Dot(SignatureInkWidth width)
    {
        double size = SignatureInk.SizeOf(width) + 2;
        var dot = new Ellipse { Width = size, Height = size, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        dot.SetResourceReference(Shape.FillProperty, "ArcWpf.Brush.Foreground");
        return dot;
    }

    private Grid BuildSweep()
    {
        var sweep = new Grid { IsHitTestVisible = false, Opacity = 0, Visibility = Visibility.Collapsed, RenderTransform = new TranslateTransform() };
        var soft = new Border { Width = 28, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(-28, 0, 0, 0) };
        var softEnd = new GradientStop(Colors.Transparent, 1);
        var softBrush = new LinearGradientBrush { StartPoint = new Point(0, .5), EndPoint = new Point(1, .5) };
        softBrush.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
        softBrush.GradientStops.Add(softEnd);
        soft.Background = softBrush;
        void Tint() => softEnd.Color = RoleColor("SignatureSweep");
        ThemeWatch.Add(soft, Tint);
        soft.Loaded += (_, _) => Tint();
        var edge = new Border { Width = 2, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(-1, 0, 0, 0), CornerRadius = new CornerRadius(2), Opacity = .5 };
        edge.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Foreground");
        sweep.Children.Add(soft);
        sweep.Children.Add(edge);
        sweep.SizeChanged += (_, e) => edge.Margin = new Thickness(-1, e.NewSize.Height * .08, 0, e.NewSize.Height * .08);
        return sweep;
    }

    private Color RoleColor(string role) => TryFindResource("ArcWpf.Brush." + role) is SolidColorBrush brush ? brush.Color : Colors.Transparent;
    private static string RoleOf(SignatureInkColor color) => color switch { SignatureInkColor.Blue => "SignatureInkBlue", SignatureInkColor.Violet => "SignatureInkViolet", _ => "Foreground" };

    // ---- Guide and hint ----

    private void RefreshGuide()
    {
        if (_guide is null) { return; }
        if (_signer is not null) { _signer.Text = Signer ?? ""; _signer.Visibility = string.IsNullOrEmpty(Signer) ? Visibility.Collapsed : Visibility.Visible; }
        if (_hintBlock is not null) { _hintBlock.Text = Hint; }
        RefreshHint(false);
    }

    private void OnGuideSize(object sender, SizeChangedEventArgs e)
    {
        double width = _guide.ActualWidth, height = _guide.ActualHeight;
        double inset = width * .07, line = height * .72;
        ((FrameworkElement)_guide.Children[0]).Margin = new Thickness(inset, line, inset, 0);
        ((FrameworkElement)_guide.Children[1]).Margin = new Thickness(inset, line - 20, 0, 0);
        if (_signer is not null) { _signer.Margin = new Thickness(inset, line + 8, 0, 0); }
        if (_hintBlock is not null) { PlaceHint(); }
    }

    private void PlaceHint()
    {
        if (_hintBlock is null) { return; }
        // left 50%, top 72%, translated by its own size: the text centers on the pad and rests 8 above the baseline.
        _hintBlock.Margin = new Thickness(0, _guide.ActualHeight * .72 - 8 - 25.2, 0, 0);
    }

    private void RefreshHint(bool animate)
    {
        bool show = _present.Count == 0 && _liveStroke is null && !_wiping;
        if (show == _hintShown && (_hintBlock is not null || !show)) { return; }
        _hintShown = show;
        animate = animate && Animates;
        if (show)
        {
            if (_hintBlock is not null && _hintBlock.Tag is OwnedMotion old) { old.Stop(); _guide.Children.Remove(_hintBlock); }
            var hint = ArcText.Block(Hint, 18, 25.2, "TextMuted");
            hint.HorizontalAlignment = HorizontalAlignment.Center;
            hint.VerticalAlignment = VerticalAlignment.Top;
            var motion = new OwnedMotion();
            hint.Tag = motion;
            _hintBlock = hint;
            _guide.Children.Add(hint);
            PlaceHint();
            if (!animate) { return; }
            hint.Opacity = 0;
            motion.Double(hint, OpacityProperty, 1, Reduced ? INSTANT : STANDARD, true, enter: true);
            if (Reduced) { return; }
            var rise = new TranslateTransform(0, 6);
            var scale = new ScaleTransform(.97, .97);
            hint.RenderTransformOrigin = new Point(.5, .5);
            hint.RenderTransform = new TransformGroup { Children = { scale, rise } };
            motion.Double(rise, TranslateTransform.YProperty, 0, STANDARD, true, enter: true);
            motion.Double(scale, ScaleTransform.ScaleXProperty, 1, STANDARD, true, enter: true);
            motion.Double(scale, ScaleTransform.ScaleYProperty, 1, STANDARD, true, enter: true);
            ArcEffects.BlurIn(motion, hint, 4, STANDARD);
            return;
        }
        if (_hintBlock is null) { return; }
        var leaving = _hintBlock;
        _hintBlock = null;
        if (!animate) { _guide.Children.Remove(leaving); return; }
        var exit = (OwnedMotion)leaving.Tag;
        exit.Stop();
        if (!Reduced)
        {
            var rise = new TranslateTransform();
            var scale = new ScaleTransform(1, 1);
            leaving.RenderTransformOrigin = new Point(.5, .5);
            leaving.RenderTransform = new TransformGroup { Children = { scale, rise } };
            exit.Double(rise, TranslateTransform.YProperty, 6, FAST, true);
            exit.Double(scale, ScaleTransform.ScaleXProperty, .97, FAST, true);
            exit.Double(scale, ScaleTransform.ScaleYProperty, .97, FAST, true);
            ArcEffects.BlurOut(exit, leaving, 4, FAST);
        }
        exit.Double(leaving, OpacityProperty, 0, Reduced ? INSTANT : FAST, true, completed: () => _guide.Children.Remove(leaving));
    }

    private void RefreshPadName()
    {
        if (_pad is null) { return; }
        string count = _present.Count == 0 ? "Empty." : $"{_present.Count} {(_present.Count == 1 ? "stroke" : "strokes")}.";
        AutomationProperties.SetName(_pad, $"{Label}. {count} Draw with a pointer, pen, or finger.");
        AutomationProperties.SetHelpText(_pad, "Press Control Z to undo, add Shift to redo, and Delete to clear.");
    }

    // ---- Pointer input ----

    private Point ToPad(Point inPad)
    {
        double width = Math.Max(1, _pad.ActualWidth), height = Math.Max(1, _pad.ActualHeight);
        return new Point(inPad.X / width * SignatureInk.PadWidth, inPad.Y / height * SignatureInk.PadHeight);
    }

    // The svg sits in the pad's padding box, but toPad measures against the border box, exactly as the registry does.
    private void Begin(Point at, double pressure, int timestamp)
    {
        _finishWipe?.Invoke();
        if (_replay is not null) { StopReplay(); RefreshChrome(true); }
        _pad.Focus();
        _counter++;
        var pad = ToPad(at);
        _stamp = timestamp;
        _liveStroke = new SignatureInkStroke { Id = $"{_uid}-{_counter}", Color = InkColor, Width = InkWidth, At = timestamp, Points = [new SignatureInkPoint(pad.X, pad.Y, pressure, 0)] };
        _pad.Drawing = true;
        RefreshHint(true);
        PaintLive();
    }

    private void Extend(Point at, double pressure, int timestamp)
    {
        var pad = ToPad(at);
        var points = _liveStroke!.Points;
        points.Add(new SignatureInkPoint(pad.X, pad.Y, pressure, Math.Max(points[^1].T, timestamp - _stamp)));
    }

    private void End()
    {
        var stroke = _liveStroke;
        if (stroke is null) { return; }
        _liveStroke = null;
        _pointer = Pointer.None;
        Commit([.. _present, stroke], stroke.Id);
        _live.Data = null;
        _pad.Drawing = false;
        RefreshHint(true);
        Announcer.Say(this, $"Stroke added. {_present.Count} in the signature.");
    }

    private void PaintLive()
    {
        if (_liveStroke is null) { return; }
        _live.Fill = (Brush)FindResource("ArcWpf.Brush." + RoleOf(InkColor));
        _live.Data = SignatureInk.ToGeometry(SignatureInk.OutlineToPath(SignatureInk.Outline(_liveStroke.Points, SignatureInk.OptionsFor(_liveStroke.Width, _liveStroke.Points, false))));
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_liveStroke is not null || e.StylusDevice is not null) { return; }
        e.Handled = true;
        _pointer = Pointer.Mouse;
        Begin(e.GetPosition(_pad), -1, e.Timestamp);
        _pad.CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_pointer != Pointer.Mouse || _liveStroke is null || e.StylusDevice is not null) { return; }
        var buffer = new Point[64];
        int count = Mouse.GetIntermediatePoints(_pad, buffer);
        // Oldest first: the coalesced samples the browser hands over with getCoalescedEvents.
        for (int i = count - 1; i >= 0; i--) { Extend(buffer[i], -1, e.Timestamp); }
        if (count <= 0) { Extend(e.GetPosition(_pad), -1, e.Timestamp); }
        PaintLive();
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_pointer != Pointer.Mouse) { return; }
        e.Handled = true;
        End();
        _pad.ReleaseMouseCapture();
    }

    private void OnLostCapture(object sender, MouseEventArgs e)
    {
        if (_pointer == Pointer.Mouse) { End(); }
    }

    private void OnStylusDown(object sender, StylusDownEventArgs e)
    {
        if (_liveStroke is not null) { return; }
        e.Handled = true;
        bool pen = e.StylusDevice.TabletDevice?.Type == TabletDeviceType.Stylus;
        var points = e.GetStylusPoints(_pad);
        _pointer = Pointer.Stylus;
        var first = points.Count > 0 ? points[0] : default;
        Begin(new Point(first.X, first.Y), pen && first.PressureFactor > 0 ? first.PressureFactor : -1, e.Timestamp);
        _pad.CaptureStylus();
    }

    private void OnStylusMove(object sender, StylusEventArgs e)
    {
        if (_pointer != Pointer.Stylus || _liveStroke is null) { return; }
        e.Handled = true;
        bool pen = e.StylusDevice.TabletDevice?.Type == TabletDeviceType.Stylus;
        foreach (var point in e.GetStylusPoints(_pad)) { Extend(new Point(point.X, point.Y), pen && point.PressureFactor > 0 ? point.PressureFactor : -1, e.Timestamp); }
        PaintLive();
    }

    private void OnStylusUp(object sender, StylusEventArgs e)
    {
        if (_pointer != Pointer.Stylus) { return; }
        e.Handled = true;
        End();
        _pad.ReleaseStylusCapture();
    }

    private void OnPadKeyDown(object sender, KeyEventArgs e)
    {
        bool mod = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        if (mod && e.Key == Key.Z) { e.Handled = true; if (shift) { Redo(); } else { Undo(); } }
        else if (mod && e.Key == Key.Y) { e.Handled = true; Redo(); }
        else if (!mod && e.Key is Key.Delete or Key.Back) { e.Handled = true; Clear(); }
        else if (e.Key == Key.Escape && _replay is not null) { StopReplay(); RefreshChrome(true); }
    }

    // ---- History ----

    private void Commit(List<SignatureInkStroke> next, string? fresh = null)
    {
        _past.Add(_present);
        if (_past.Count > HISTORY_LIMIT) { _past.RemoveAt(0); }
        _present = next;
        _future.Clear();
        _fresh = fresh;
        AfterChange();
    }

    private void AfterChange()
    {
        SyncInk();
        RefreshPadName();
        RefreshHint(true);
        RefreshChrome(true);
        StrokesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        if (_past.Count == 0 || _liveStroke is not null) { return; }
        _finishWipe?.Invoke();
        StopReplay();
        if (_past.Count == 0) { return; }
        _future.Insert(0, _present);
        _present = _past[^1];
        _past.RemoveAt(_past.Count - 1);
        _fresh = null;
        AfterChange();
        Announcer.Say(this, "Undone");
    }

    public void Redo()
    {
        if (_future.Count == 0 || _liveStroke is not null) { return; }
        _finishWipe?.Invoke();
        StopReplay();
        if (_future.Count == 0) { return; }
        _past.Add(_present);
        _present = _future[0];
        _future.RemoveAt(0);
        _fresh = null;
        AfterChange();
        Announcer.Say(this, "Redone");
    }

    /// <summary>Clears the signature with a wipe across the pad. Undo brings it back.</summary>
    public void Clear()
    {
        if (_present.Count == 0 || _wiping || _liveStroke is not null) { return; }
        StopReplay();
        void Finish()
        {
            if (_finishWipe is null && _wiping) { return; }
            _finishWipe = null;
            bool wiped = _wiping;
            Commit([]);
            _wiping = false;
            RefreshHint(true);
            RefreshChrome(true);
            Announcer.Say(this, "Signature cleared. Undo brings it back.");
            if (!wiped) { return; }
            // Lift the clip only once the wiped strokes have left, so they never flash back for a frame.
            _motion.Stop();
            _ink.Clip = null;
            _sweep.BeginAnimation(OpacityProperty, null);
            _sweep.Visibility = Visibility.Collapsed;
            SetValue(WipeProgressProperty, 0d);
        }
        if (!Animates || Reduced) { Finish(); return; }
        _wiping = true;
        RefreshChrome(true);
        RefreshHint(true);
        // The ink is cut away behind a squeegee edge that sweeps across the pad.
        _ink.Clip = _wipeClip;
        _sweep.Visibility = Visibility.Visible;
        SetValue(WipeProgressProperty, 0d);
        ApplyWipe();
        _finishWipe = Finish;
        _motion.Double(this, WipeProgressProperty, 1, WIPE, true, completed: () => _finishWipe?.Invoke(), ease: new KeySpline(.65, 0, .35, 1));
        var opacity = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(WIPE), FillBehavior = FillBehavior.HoldEnd };
        foreach (var (time, value) in new[] { (0d, 0d), (.12, 1d), (.85, 1d), (1d, 0d) }) { opacity.KeyFrames.Add(new LinearDoubleKeyFrame(value, KeyTime.FromPercent(time))); }
        _sweep.BeginAnimation(OpacityProperty, opacity);
    }

    private void ApplyWipe()
    {
        if (_ink is null) { return; }
        double progress = (double)GetValue(WipeProgressProperty), width = _ink.ActualWidth, height = _ink.ActualHeight;
        _wipeClip.Rect = new Rect(width * progress, 0, Math.Max(0, width * (1 - progress)), Math.Max(0, height));
        ((TranslateTransform)_sweep.RenderTransform).X = width * progress;
    }

    // ---- Ink ----

    private void RefreshInkBrushes()
    {
        foreach (var stroke in _present) { if (_paths.TryGetValue(stroke.Id, out var path)) { path.Fill = (Brush)FindResource("ArcWpf.Brush." + RoleOf(stroke.Color)); } }
    }

    private void SyncInk(double? replayAt = null)
    {
        if (_inkCanvas is null) { return; }
        var keep = _present.Select(stroke => stroke.Id).ToHashSet();
        foreach (var (id, path) in _paths.ToList())
        {
            if (keep.Contains(id)) { continue; }
            _paths.Remove(id);
            // Wiped strokes leave at once, undone ones fade.
            if (_wiping || !Animates) { _inkCanvas.Children.Remove(path); continue; }
            _motion.Double(path, OpacityProperty, 0, STANDARD, true, completed: () => _inkCanvas.Children.Remove(path));
        }
        for (int index = 0; index < _present.Count; index++)
        {
            var stroke = _present[index];
            if (!_paths.TryGetValue(stroke.Id, out var path))
            {
                path = new Path { Data = SignatureInk.ToGeometry(SignatureInk.StrokePath(stroke)), Fill = (Brush)FindResource("ArcWpf.Brush." + RoleOf(stroke.Color)) };
                _paths[stroke.Id] = path;
                _inkCanvas.Children.Insert(Math.Max(0, _inkCanvas.Children.IndexOf(_live)), path);
                if (_fresh != stroke.Id && Animates)
                {
                    path.Opacity = 0;
                    _motion.Double(path, OpacityProperty, 1, STANDARD, true);
                }
            }
        }
    }

    // ---- Replay ----

    private void ToggleReplay()
    {
        if (_replay is not null) { StopReplay(); RefreshChrome(true); Announcer.Say(this, "Replay stopped"); return; }
        if (_present.Count == 0 || _liveStroke is not null) { return; }
        _finishWipe?.Invoke();
        if (_present.Count == 0) { return; }
        // Each stroke keeps its own rhythm, and pauses between strokes are capped so the replay never stalls.
        var offsets = new double[_present.Count];
        double cursor = 0;
        for (int index = 0; index < _present.Count; index++)
        {
            var stroke = _present[index];
            if (index > 0)
            {
                var previous = _present[index - 1];
                double gap = stroke.At - (previous.At + previous.Points[^1].T);
                cursor += Math.Min(260, Math.Max(90, gap));
            }
            offsets[index] = cursor;
            cursor += stroke.Points[^1].T;
        }
        _replay = (NowMs(), Math.Max(1, cursor / 3200), cursor + 120, offsets);
        CompositionTarget.Rendering += OnReplayFrame;
        PaintReplay(0);
        RefreshChrome(true);
        Announcer.Say(this, "Replaying signature");
    }

    private static double NowMs() => System.Diagnostics.Stopwatch.GetTimestamp() * 1000d / System.Diagnostics.Stopwatch.Frequency;

    private void OnReplayFrame(object? sender, EventArgs e)
    {
        if (_replay is not { } replay) { return; }
        double at = (NowMs() - replay.Begin) * replay.Speed;
        if (at >= replay.Total)
        {
            StopReplay();
            RefreshChrome(true);
            Announcer.Say(this, "Replay finished");
            return;
        }
        PaintReplay(at);
    }

    // During replay each stroke shows only the part drawn so far, and the nib rides its newest point.
    internal void PaintReplay(double at)
    {
        if (_replay is not { } replay) { return; }
        SignatureInkPoint? nib = null;
        var nibColor = InkColor;
        for (int index = 0; index < _present.Count; index++)
        {
            var stroke = _present[index];
            if (!_paths.TryGetValue(stroke.Id, out var path)) { continue; }
            double local = at - replay.Offsets[index], lastT = stroke.Points[^1].T;
            if (local < 0) { path.Data = Geometry.Empty; continue; }
            if (local >= lastT) { path.Data = SignatureInk.ToGeometry(SignatureInk.StrokePath(stroke)); continue; }
            nib = stroke.Points.LastOrDefault(point => point.T <= local);
            nibColor = stroke.Color;
            path.Data = SignatureInk.ToGeometry(SignatureInk.StrokePath(stroke, local));
        }
        if (nib is { } tip)
        {
            double radius = SignatureInk.SizeOf(InkWidth) * .9;
            _nib.Width = _nib.Height = radius * 2;
            Canvas.SetLeft(_nib, tip.X - radius);
            Canvas.SetTop(_nib, tip.Y - radius);
            _nib.Fill = (Brush)FindResource("ArcWpf.Brush." + RoleOf(nibColor));
            _nib.Visibility = Visibility.Visible;
        }
        else { _nib.Visibility = Visibility.Collapsed; }
    }

    private void StopReplay()
    {
        if (_replay is null) { return; }
        CompositionTarget.Rendering -= OnReplayFrame;
        _replay = null;
        _nib.Visibility = Visibility.Collapsed;
        foreach (var stroke in _present) { if (_paths.TryGetValue(stroke.Id, out var path)) { path.Data = SignatureInk.ToGeometry(SignatureInk.StrokePath(stroke)); } }
    }

    // ---- Chrome ----

    private void RefreshChoices(bool animate)
    {
        _colors?.Select(InkColor, animate);
        _widths?.Select(InkWidth, animate);
    }

    private void RefreshChrome(bool animate)
    {
        if (_undo is null) { return; }
        bool empty = _present.Count == 0;
        _undo.IsEnabled = _past.Count > 0 && !_wiping;
        _redo.IsEnabled = _future.Count > 0 && !_wiping;
        _replayButton.IsEnabled = !empty && !_wiping;
        _clear.IsEnabled = !empty && !_wiping;
        bool replaying = _replay is not null;
        string label = replaying ? "Stop replay" : "Replay signature";
        AutomationProperties.SetName(_replayButton, label);
        _replayButton.ToolTip = label;
        _replayGlyph.Show(replaying ? "square" : "play", replaying ? 15 : 17, ((ArcChromeButton)_replayButton).Ink, animate && Animates, Reduced);
        foreach (var button in new[] { _undo, _redo, _replayButton, _clear }) { button.Refresh(animate); }
        _png.IsEnabled = _svg.IsEnabled = !empty;
        _png.Refresh(animate);
        _svg.Refresh(animate);
        RefreshChoices(animate);
    }

    // ---- Export ----

    internal void Save(string kind)
    {
        if (_present.Count == 0) { return; }
        _savedTimer?.Stop();
        string name = $"{FileName}.{kind}";
        string? saved;
        try
        {
            byte[] data = kind == "svg" ? System.Text.Encoding.UTF8.GetBytes(SignatureInk.ToSvg(_present)) : SignatureInk.ToPng(_present);
            var args = new SignatureExportEventArgs(kind, name, data);
            Exporting?.Invoke(this, args);
            if (!args.Handled && !SaveWithDialog(kind, name, data)) { return; }
            saved = kind;
            Announcer.Say(this, $"Saved {name}");
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.IO.IOException or UnauthorizedAccessException)
        {
            saved = "failed";
            Announcer.Say(this, "Export failed. Try again.");
        }
        _png.Show(saved, Animates, Reduced);
        _svg.Show(saved, Animates, Reduced);
        _savedTimer ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.8) };
        _savedTimer.Tick -= OnSavedTick;
        _savedTimer.Tick += OnSavedTick;
        _savedTimer.Start();
    }

    private void OnSavedTick(object? sender, EventArgs e)
    {
        _savedTimer?.Stop();
        _png.Show(null, Animates, Reduced);
        _svg.Show(null, Animates, Reduced);
    }

    // A browser downloads straight away; a desktop app asks where to put the file.
    private static bool SaveWithDialog(string kind, string name, byte[] data)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { FileName = name, DefaultExt = "." + kind, Filter = $"{kind.ToUpperInvariant()} (*.{kind})|*.{kind}" };
        if (dialog.ShowDialog() != true) { return false; }
        System.IO.File.WriteAllBytes(dialog.FileName, data);
        return true;
    }

    internal void AddStrokeForTest(IReadOnlyList<Point> padPoints, double[]? pressures = null)
    {
        _counter++;
        double at = _present.Count == 0 ? 0 : _present[^1].At + _present[^1].Points[^1].T + 200;
        var points = padPoints.Select((point, index) => new SignatureInkPoint(point.X, point.Y, pressures?[index] ?? -1, index * 16)).ToList();
        var stroke = new SignatureInkStroke { Id = $"{_uid}-{_counter}", Color = InkColor, Width = InkWidth, At = at, Points = points };
        Commit([.. _present, stroke], stroke.Id);
    }

    internal IReadOnlyList<SignatureInkStroke> StrokesForTest => _present;
    internal string StrokePathForTest(int index) => SignatureInk.StrokePath(_present[index]);
    internal bool FocusPadForTest() => _pad.Focus();
    internal bool PadHasKeyboardFocus => _pad.IsKeyboardFocused;
    internal bool IsWiping => _wiping;
    internal bool IsReplaying => _replay is not null;
    internal bool IsHintShown => _hintBlock is not null;
    internal IEnumerable<Path> InkPaths => _paths.Values;
    internal void FinishWipeForTest() => _finishWipe?.Invoke();
}

// Keeps the 600 by 260 ratio of the coordinate space, draws the border and clips the ink to the rounded inner edge.
internal sealed class PadSurface : Decorator
{
    private readonly Border _frame;
    private readonly OwnedMotion _motion = new();
    private readonly ThemedBrush _edge;
    private bool _drawing;

    public PadSurface(FrameworkElement owner, UIElement content)
    {
        _edge = new ThemedBrush(owner, _motion);
        _frame = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(26), BorderBrush = _edge.Brush, Child = new ClippedHost(25) { Child = content } };
        _frame.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
        // The pad matches :focus-visible after a pointer press too, because it takes focus from script: a 2 outline, 2 off the edge.
        var ring = new Border { Margin = new Thickness(-4), BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(30), Opacity = .72, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        ring.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.AccentStrong");
        var layers = new Grid();
        layers.Children.Add(_frame);
        layers.Children.Add(ring);
        Child = layers;
        Cursor = Cursors.Cross;
        Focusable = true;
        FocusVisualStyle = null;
        IsKeyboardFocusedChanged += (_, e) => ring.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        Stylus.SetIsPressAndHoldEnabled(this, false);
        Stylus.SetIsFlicksEnabled(this, false);
        KeyboardNavigation.SetIsTabStop(this, true);
        Loaded += (_, _) => _edge.To("Border", false);
        ThemeWatch.Add(this, () => _edge.To(_drawing ? "BorderStrong" : "Border", false));
    }

    public bool Drawing
    {
        get => _drawing;
        set { _drawing = value; _edge.To(value ? "BorderStrong" : "Border", true); }
    }

    protected override Size MeasureOverride(Size constraint)
    {
        double width = double.IsInfinity(constraint.Width) ? SignatureInk.PadWidth : constraint.Width;
        var size = new Size(width, width * SignatureInk.PadHeight / SignatureInk.PadWidth);
        Child.Measure(size);
        return size;
    }

    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new PadPeer(this);

    private sealed class PadPeer(PadSurface owner) : System.Windows.Automation.Peers.FrameworkElementAutomationPeer(owner)
    {
        protected override System.Windows.Automation.Peers.AutomationControlType GetAutomationControlTypeCore() => System.Windows.Automation.Peers.AutomationControlType.Image;
        protected override string GetLocalizedControlTypeCore() => "signature pad";
        protected override bool IsKeyboardFocusableCore() => true;
    }
}

internal sealed class ClippedHost(double radius) : Decorator
{
    protected override Size ArrangeOverride(Size arrangeSize)
    {
        Clip = new RectangleGeometry(new Rect(arrangeSize), radius, radius);
        return base.ArrangeOverride(arrangeSize);
    }
}

// CSS 1.5px dashed top border: Chromium snaps it to one device pixel and draws dashes three long.
internal sealed class DashedRule : FrameworkElement
{
    public DashedRule() => ThemeWatch.Add(this, InvalidateVisual);
    protected override void OnRender(DrawingContext context)
    {
        if (TryFindResource("ArcWpf.Brush.BorderStrong") is not Brush brush || ActualWidth <= 0) { return; }
        // Chromium stretches the gaps so a dash lands on both ends of the line.
        const double DASH = 3;
        int count = Math.Max(1, (int)Math.Round((ActualWidth + DASH) / (DASH * 2)));
        double gap = count > 1 ? (ActualWidth - count * DASH) / (count - 1) : 0;
        for (int i = 0; i < count; i++) { context.DrawRectangle(brush, null, new Rect(i * (DASH + gap), 0, DASH, 1)); }
    }
}

// flex-wrap with justify-content space-between and an 8 gap.
internal sealed class SpaceBetweenWrap : Panel
{
    protected override Size MeasureOverride(Size available)
    {
        double used = 0, height = 0, line = 0, lines = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            if (used > 0 && used + 8 + child.DesiredSize.Width > available.Width) { lines += line + 8; used = 0; line = 0; }
            used += (used > 0 ? 8 : 0) + child.DesiredSize.Width;
            line = Math.Max(line, child.DesiredSize.Height);
            height = lines + line;
        }
        return new Size(double.IsInfinity(available.Width) ? used : available.Width, height);
    }

    protected override Size ArrangeOverride(Size size)
    {
        var first = (UIElement)InternalChildren[0];
        var second = (UIElement)InternalChildren[1];
        double a = first.DesiredSize.Width, b = second.DesiredSize.Width;
        if (a + 8 + b <= size.Width)
        {
            first.Arrange(new Rect(0, 0, a, first.DesiredSize.Height));
            second.Arrange(new Rect(size.Width - b, 0, b, second.DesiredSize.Height));
            return size;
        }
        first.Arrange(new Rect(0, 0, a, first.DesiredSize.Height));
        second.Arrange(new Rect(0, first.DesiredSize.Height + 8, b, second.DesiredSize.Height));
        return size;
    }
}

internal sealed class SignatureIconButton : ArcChromeButton
{
    private readonly Func<bool> _pressed;
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly OwnedMotion _press = new();

    public SignatureIconButton(string label, Action run, Func<bool> pressed) : base(18)
    {
        _pressed = pressed;
        Width = Height = 36;
        Focusable = true;
        ToolTip = label;
        AutomationProperties.SetName(this, label);
        RenderTransformOrigin = new Point(.5, .5);
        RenderTransform = _scale;
        Roles = _ =>
        {
            if (!IsEnabled) { return (null, null, "TextMuted"); }
            if (_pressed() || IsMouseOver) { return ("SurfaceMuted", null, "Foreground"); }
            return (null, null, "TextSecondary");
        };
        Click += (_, _) => run();
        IsEnabledChanged += (_, _) => Opacity = IsEnabled ? 1 : .45;
        Opacity = .45;
    }

    protected override void OnIsPressedChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnIsPressedChanged(e);
        bool animate = ArcMotion.GetLevel(this) != ArcMotionLevel.Off;
        double target = IsPressed ? .94 : 1;
        _press.Double(_scale, ScaleTransform.ScaleXProperty, target, .12, animate);
        _press.Double(_scale, ScaleTransform.ScaleYProperty, target, .12, animate);
    }
}

// Crossfades one icon into another with a scale, on the snappy spring. Icons sit on top of each other, as with popLayout.
internal sealed class SwapGlyph : Grid
{
    private string? _name;
    private UIElement? _current;

    public void Show(string name, double size, Brush stroke, bool animate, bool reduced)
    {
        if (_name == name) { return; }
        _name = name;
        var next = ArcText.Icon(name, size, stroke);
        var previous = _current;
        _current = next;
        Children.Add(next);
        if (!animate) { if (previous is not null) { Children.Remove(previous); } return; }
        double seconds = reduced ? .12 : .26;
        var motion = new OwnedMotion();
        if (previous is not null) { Fade(previous, 0, .6, () => Children.Remove(previous)); }
        next.Opacity = 0;
        var scale = new ScaleTransform(.6, .6);
        next.RenderTransformOrigin = new Point(.5, .5);
        next.RenderTransform = scale;
        motion.Double(next, OpacityProperty, 1, seconds, true, spring: !reduced);
        motion.Double(scale, ScaleTransform.ScaleXProperty, 1, seconds, true, spring: !reduced);
        motion.Double(scale, ScaleTransform.ScaleYProperty, 1, seconds, true, spring: !reduced);

        void Fade(UIElement element, double opacity, double to, Action done)
        {
            var away = new ScaleTransform(1, 1);
            element.RenderTransformOrigin = new Point(.5, .5);
            element.RenderTransform = away;
            motion.Double(away, ScaleTransform.ScaleXProperty, to, seconds, true, spring: !reduced);
            motion.Double(away, ScaleTransform.ScaleYProperty, to, seconds, true, spring: !reduced);
            motion.Double(element, OpacityProperty, opacity, seconds, true, spring: !reduced, completed: done);
        }
    }
}

// A compact radio group with a highlight that glides to the chosen option. Arrow keys move the choice.
internal sealed class SignatureChoice : Grid
{
    public sealed record Option(object Value, string Label, UIElement Glyph);

    private readonly List<(Option Option, OptionButton Button)> _options = [];
    private readonly Border _selected;
    private readonly TranslateTransform _slide = new();
    private readonly SpringDriver _driver;
    private readonly ControlSpring _x;
    private readonly FrameworkElement _owner;
    private readonly Action<object> _change;
    private object? _value;

    public SignatureChoice(string label, IReadOnlyList<Option> options, Action<object> change, FrameworkElement owner)
    {
        _owner = owner;
        _change = change;
        _driver = new SpringDriver(() => _slide.X = _x!.Position);
        _x = _driver.Add(0, .42);
        AutomationProperties.SetName(this, label);
        _selected = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Left, RenderTransform = _slide, IsHitTestVisible = false };
        _selected.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
        _selected.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        Children.Add(_selected);
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var option in options)
        {
            var button = new OptionButton(option.Label) { Content = option.Glyph, Margin = new Thickness(row.Children.Count > 0 ? 2 : 0, 0, 0, 0) };
            button.Click += (_, _) => _change(option.Value);
            button.PreviewKeyDown += OnKey;
            _options.Add((option, button));
            row.Children.Add(button);
        }
        Children.Add(row);
        Select(options[0].Value, false);
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        int step = e.Key is Key.Right or Key.Down ? 1 : e.Key is Key.Left or Key.Up ? -1 : 0;
        if (step == 0) { return; }
        e.Handled = true;
        int index = _options.FindIndex(entry => Equals(entry.Option.Value, _value));
        var next = _options[(index + step + _options.Count) % _options.Count];
        _change(next.Option.Value);
        next.Button.Focus();
    }

    public void Select(object value, bool animate)
    {
        bool moved = !Equals(_value, value);
        _value = value;
        int index = _options.FindIndex(entry => Equals(entry.Option.Value, value));
        for (int i = 0; i < _options.Count; i++) { _options[i].Button.IsChosen = i == index; }
        double target = index * 38;
        var level = ArcMotion.GetLevel(_owner);
        if (animate && moved && _owner.IsLoaded && level == ArcMotionLevel.Full) { _driver.Animate(_x, target, .42); }
        else { _driver.Stop(); _driver.Snap(_x, target); }
    }

    internal sealed class OptionButton : System.Windows.Controls.Button
    {
        public bool IsChosen
        {
            get => _chosen;
            set
            {
                _chosen = value;
                KeyboardNavigation.SetTabIndex(this, value ? 0 : 1);
                IsTabStop = value;
                Refresh();
            }
        }
        private bool _chosen;

        public OptionButton(string label)
        {
            OverridesDefaultStyle = true;
            Width = Height = 36;
            Cursor = Cursors.Hand;
            ToolTip = label;
            AutomationProperties.SetName(this, label);
            FocusVisualStyle = ArcChromeButton.FocusFor(18);
            var frame = new FrameworkElementFactory(typeof(Grid));
            var fill = new FrameworkElementFactory(typeof(Border));
            fill.SetValue(Border.CornerRadiusProperty, new CornerRadius(18));
            fill.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(Background)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            frame.AppendChild(fill);
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            frame.AppendChild(content);
            Template = new ControlTemplate(typeof(System.Windows.Controls.Button)) { VisualTree = frame };
            Background = Brushes.Transparent;
            MouseEnter += (_, _) => Refresh();
            MouseLeave += (_, _) => Refresh();
            ThemeWatch.Add(this, Refresh);
        }

        private void Refresh()
        {
            if (IsMouseOver && !_chosen) { SetResourceReference(BackgroundProperty, "ArcWpf.Brush.SurfaceMuted"); }
            else { Background = Brushes.Transparent; }
        }
    }
}

// Save as PNG or SVG. Content sits at the top of the 36 box: the registry's grid button leaves it there, and the swap rises through it.
internal sealed class SignatureExportButton : ArcChromeButton
{
    private readonly string _kind;
    private readonly Grid _slot = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly OwnedMotion _press = new();
    private string _face = "";

    public SignatureExportButton(string kind, Action run) : base(18, 1)
    {
        _kind = kind;
        MinWidth = 76;
        Height = 36;
        Padding = new Thickness(12, 0, 12, 0);
        VerticalContentAlignment = VerticalAlignment.Stretch;
        Content = _slot;
        AutomationProperties.SetName(this, $"Download {kind.ToUpperInvariant()}");
        RenderTransformOrigin = new Point(.5, .5);
        RenderTransform = _scale;
        Roles = _ =>
        {
            if (!IsEnabled) { return ("Surface", "Border", "TextMuted"); }
            return (IsMouseOver ? "SurfaceMuted" : "Surface", "Border", "Foreground");
        };
        Click += (_, _) => run();
        Show(null, false, false);
    }

    protected override void OnIsPressedChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnIsPressedChanged(e);
        bool animate = ArcMotion.GetLevel(this) != ArcMotionLevel.Off;
        double target = IsPressed ? .97 : 1;
        _press.Double(_scale, ScaleTransform.ScaleXProperty, target, .12, animate);
        _press.Double(_scale, ScaleTransform.ScaleYProperty, target, .12, animate);
    }

    internal string Face => _face;

    public void Show(string? saved, bool animate, bool reduced)
    {
        string face = saved == _kind ? "done" : saved == "failed" ? "failed" : "idle";
        if (face == _face) { return; }
        _face = face;
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new Border { Height = 19.6, Margin = new Thickness(0, 0, 6, 0), Child = ArcText.Icon(face == "done" ? "check" : "download", 15, Ink) });
        var text = new TextBlock { Text = face == "done" ? "Saved" : _kind.ToUpperInvariant(), FontSize = 14, LineHeight = 19.6, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, Foreground = Ink };
        row.Children.Add(text);
        var motion = new OwnedMotion();
        foreach (FrameworkElement old in _slot.Children.OfType<FrameworkElement>().ToList())
        {
            if (!animate) { _slot.Children.Remove(old); continue; }
            var up = new TranslateTransform();
            old.RenderTransform = up;
            if (!reduced) { motion.Double(up, TranslateTransform.YProperty, -4, .12, true); }
            motion.Double(old, OpacityProperty, 0, .12, true, completed: () => _slot.Children.Remove(old));
        }
        _slot.Children.Add(row);
        if (!animate) { return; }
        row.Opacity = 0;
        motion.Double(row, OpacityProperty, 1, reduced ? .12 : .16, true, enter: true);
        if (reduced) { return; }
        var rise = new TranslateTransform(0, 4);
        row.RenderTransform = rise;
        motion.Double(rise, TranslateTransform.YProperty, 0, .16, true, enter: true);
        ArcEffects.BlurIn(motion, row, 2, .16);
    }
}
