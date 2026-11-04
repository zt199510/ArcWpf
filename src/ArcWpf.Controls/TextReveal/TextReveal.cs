using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcWpf.Controls;

public enum TextRevealKind { Heading, Paragraph }

// text-reveal.tsx (frozen 2026-11-03): a short headline or sentence reveals once, as it appears: each word rises out of its own clip while it sharpens from a soft blur.
public class TextReveal : Control
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(TextReveal), new FrameworkPropertyMetadata("", (d, _) => ((TextReveal)d).Rebuild()));
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(TextRevealKind), typeof(TextReveal), new FrameworkPropertyMetadata(TextRevealKind.Heading));
    public static readonly DependencyProperty DelayProperty = DependencyProperty.Register(nameof(Delay), typeof(double), typeof(TextReveal), new FrameworkPropertyMetadata(0d));
    public static readonly DependencyProperty TrackingProperty = DependencyProperty.Register(nameof(Tracking), typeof(double), typeof(TextReveal), new FrameworkPropertyMetadata(0d, (d, _) => ((TextReveal)d).Rebuild()));
    public static readonly DependencyProperty LineRatioProperty = DependencyProperty.Register(nameof(LineRatio), typeof(double), typeof(TextReveal), new FrameworkPropertyMetadata(1.4, (d, _) => ((TextReveal)d).Rebuild()));
    public static readonly DependencyProperty MaxCharactersProperty = DependencyProperty.Register(nameof(MaxCharacters), typeof(double), typeof(TextReveal), new FrameworkPropertyMetadata(24d, (d, _) => ((TextReveal)d).Rebuild()));

    /// <summary>The text; a line feed is a deliberate break.</summary>
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    /// <summary>A heading blurs more than a paragraph, and is exposed as a heading.</summary>
    public TextRevealKind Kind { get => (TextRevealKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    /// <summary>Seconds to wait before the first word rises.</summary>
    public double Delay { get => (double)GetValue(DelayProperty); set => SetValue(DelayProperty, value); }
    /// <summary>Letter spacing in em.</summary>
    public double Tracking { get => (double)GetValue(TrackingProperty); set => SetValue(TrackingProperty, value); }
    /// <summary>Line height as a multiple of the font size.</summary>
    public double LineRatio { get => (double)GetValue(LineRatioProperty); set => SetValue(LineRatioProperty, value); }
    /// <summary>The longest line in widths of the digit 0, CSS max-width: 24ch. Zero or less removes the limit; MaxWidth still applies.</summary>
    public double MaxCharacters { get => (double)GetValue(MaxCharactersProperty); set => SetValue(MaxCharactersProperty, value); }

    private const double MaxStagger = Tween.Considered, WordStagger = .04, Rise = .62, RiseSeconds = .76, FadeSeconds = .44, FocusSeconds = .58;

    private sealed record Word(Border Clip, TrackedText View, TranslateTransform Move, LinearGradientBrush Mask, double Delay);

    private readonly List<Word> _words = [];
    private Decorator? _host;
    private WordFlow? _flow;
    private bool _played;

    static TextReveal() => DefaultStyleKeyProperty.OverrideMetadata(typeof(TextReveal), new FrameworkPropertyMetadata(typeof(TextReveal)));

    public TextReveal()
    {
        AutomationProperties.SetHeadingLevel(this, AutomationHeadingLevel.Level2);
        Loaded += (_, _) => { if (!_played) { Replay(); } };
    }

    internal int WordCountForTest => _words.Count;
    internal int LineCountForTest => _flow?.LineCountForTest ?? 0;
    internal double DelayForTest(int word) => _words[word].Delay;
    internal double OpacityForTest(int word) => _words[word].View.Opacity;
    internal double RiseForTest(int word) => _words[word].Move.Y;
    internal bool IsBlurredForTest(int word) => _words[word].View.Effect is not null;
    internal bool IsMaskedForTest(int word) => _words[word].Clip.OpacityMask is not null;
    internal Point WordAtForTest(int word) => _words[word].View.TranslatePoint(new Point(), this);
    internal void FinishMotionForTest() { foreach (var word in _words) { Land(word); } }

    protected override AutomationPeer OnCreateAutomationPeer() => new ArcTextPeer(this, () => (Text ?? "").Replace('\n', ' '));

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
        if (e.Property == KindProperty) { AutomationProperties.SetHeadingLevel(this, Kind == TextRevealKind.Heading ? AutomationHeadingLevel.Level2 : AutomationHeadingLevel.None); }
    }

    private double ZeroWidth() => new FormattedText("0", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(FontFamily, FontStyle, FontWeight, FontStretch), FontSize, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip).WidthIncludingTrailingWhitespace + Tracking * FontSize;

    private static string[][] Split(string text) => text.Split('\n').Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToArray();

    private void Rebuild()
    {
        if (_host is null) { return; }
        _words.Clear();
        double em = FontSize, line = LineRatio * em;
        double limit = MaxCharacters > 0 ? MaxCharacters * ZeroWidth() : double.PositiveInfinity;
        _flow = new WordFlow { Balance = true, LineHeight = line, MaxLine = limit };
        _host.Child = _flow;
        var lines = Split(Text ?? "");
        int count = lines.Sum(words => words.Length);
        double step = Math.Min(WordStagger, MaxStagger / Math.Max(count, 1));
        int position = 0;
        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            for (int wordIndex = 0; wordIndex < lines[lineIndex].Length; wordIndex++)
            {
                var view = new TrackedText { Text = lines[lineIndex][wordIndex], Tracking = Tracking, LineRatio = LineRatio };
                var move = new TranslateTransform();
                view.RenderTransform = move;
                // The clip feathers its bottom edge, below the descenders, so a blurred word surfaces softly instead of being cut by a hard line.
                var box = line + .34 * em;
                var mask = new LinearGradientBrush { MappingMode = BrushMappingMode.Absolute, StartPoint = new Point(0, box - .18 * em), EndPoint = new Point(0, box), GradientStops = { new GradientStop(Colors.White, 0), new GradientStop(Colors.Transparent, 1) } };
                var clip = new Border { Padding = new Thickness(.16 * em, .1 * em, .16 * em, .24 * em), Margin = new Thickness(-.16 * em, -.1 * em, -.16 * em, -.24 * em), Child = view, Background = Brushes.Transparent, IsHitTestVisible = false };
                WordFlow.SetBreakBefore(clip, wordIndex == 0 && lineIndex > 0);
                _flow.Children.Add(clip);
                _words.Add(new Word(clip, view, move, mask, Delay + position * step + lineIndex * step));
                position++;
            }
        }
        if (_words.Count > 0) { _flow.Gap = _words[0].View.SpaceWidth; }
        if (IsLoaded) { Replay(); }
    }

    /// <summary>Runs the entrance again, the equivalent of changing the React key.</summary>
    public void Replay()
    {
        _played = true;
        var level = Tween.Level(this);
        double blur = Kind == TextRevealKind.Paragraph ? 4 : 8;
        foreach (var word in _words)
        {
            Land(word);
            if (level == ArcMotionLevel.Off) { continue; }
            var view = word.View;
            // The delay holds the hidden start, then lets go: a resting word keeps no mask, transform or blur.
            view.Opacity = 0;
            if (level == ArcMotionLevel.Reduced) { Tween.To(view, OpacityProperty, 1, Tween.Fast, ArcEasing.Standard); continue; }
            word.Clip.OpacityMask = word.Mask;
            word.Move.Y = Rise * FontSize;
            Tween.To(word.Move, TranslateTransform.YProperty, 0, RiseSeconds, ArcEasing.Enter, word.Delay, () => word.Clip.OpacityMask = null);
            Tween.To(view, OpacityProperty, 1, FadeSeconds, ArcEasing.Standard, word.Delay);
            TextMotion.Blur(view, blur, 0, FocusSeconds, ArcEasing.Enter, word.Delay);
        }
    }

    private static void Land(Word word)
    {
        Tween.Set(word.View, OpacityProperty, 1);
        Tween.Set(word.Move, TranslateTransform.YProperty, 0);
        word.View.Effect = null;
        word.Clip.OpacityMask = null;
    }
}
