using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace ArcWpf.Controls;

// empty-state.tsx (frozen 2026-10-31): icon, title, description and an optional action. Keep it mounted and change
// its properties to morph between states: the copy rolls in place while the height springs, a new icon pops in.
[TemplatePart(Name = "PART_Icon", Type = typeof(Border))]
[TemplatePart(Name = "PART_Glyphs", Type = typeof(Grid))]
[TemplatePart(Name = "PART_Frame", Type = typeof(Border))]
[TemplatePart(Name = "PART_Copy", Type = typeof(StackPanel))]
[TemplatePart(Name = "PART_Title", Type = typeof(Grid))]
[TemplatePart(Name = "PART_Description", Type = typeof(Grid))]
public class EmptyState : Control
{
    // lucide folder, the source's default glyph.
    public static readonly Geometry FolderIcon = Frozen("M20,20 a2,2 0 0 0 2,-2 V8 a2,2 0 0 0 -2,-2 h-7.9 a2,2 0 0 1 -1.69,-0.9 L9.6,3.9 A2,2 0 0 0 7.93,3 H4 a2,2 0 0 0 -2,2 v13 a2,2 0 0 0 2,2 Z");

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(EmptyState), new FrameworkPropertyMetadata(string.Empty, OnCopyChanged));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(EmptyState), new FrameworkPropertyMetadata(string.Empty, OnCopyChanged));
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(object), typeof(EmptyState), new FrameworkPropertyMetadata(null, OnIconChanged));
    public static readonly DependencyProperty IconStrokeThicknessProperty = DependencyProperty.Register(nameof(IconStrokeThickness), typeof(double), typeof(EmptyState), new FrameworkPropertyMetadata(1.5));
    public static readonly DependencyProperty ActionProperty = DependencyProperty.Register(nameof(Action), typeof(object), typeof(EmptyState));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(EmptyState));

    /// <summary>Headline. A new title rises in while the old one leaves.</summary>
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    /// <summary>One or two sentences on why it is empty and what to do.</summary>
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    /// <summary>Leading icon: a 24-unit Geometry (drawn as a stroke) or any element. Null shows the folder glyph.</summary>
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    /// <summary>Stroke width for Geometry icons (lucide strokeWidth).</summary>
    public double IconStrokeThickness { get => (double)GetValue(IconStrokeThicknessProperty); set => SetValue(IconStrokeThicknessProperty, value); }
    /// <summary>Call to action, usually a button.</summary>
    public object? Action { get => GetValue(ActionProperty); set => SetValue(ActionProperty, value); }
    /// <summary>Accessible name for the region.</summary>
    public string? Label { get => (string?)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    private const double SNAPPY = .26, TITLE_SIZE = 16, TEXT_SIZE = 14, LEADING = 1.4;
    private Border? _icon;
    private Grid? _glyphs, _title, _description;
    private Canvas? _titleLeaving, _descriptionLeaving;
    private TextBlock? _titleText, _descriptionText;
    private FrameworkElement? _glyph;
    private HeightMorph? _morph;
    private bool _settledIn;

    static EmptyState() { DefaultStyleKeyProperty.OverrideMetadata(typeof(EmptyState), new FrameworkPropertyMetadata(typeof(EmptyState))); }

    public EmptyState() { Loaded += (_, _) => SettleIn(); Unloaded += (_, _) => _morph?.Stop(); }

    private ArcMotionLevel Level => ArcMotion.GetLevel(this);

    public override void OnApplyTemplate()
    {
        _morph?.Stop();
        base.OnApplyTemplate();
        _icon = GetTemplateChild("PART_Icon") as Border;
        _glyphs = GetTemplateChild("PART_Glyphs") as Grid;
        _title = GetTemplateChild("PART_Title") as Grid;
        _description = GetTemplateChild("PART_Description") as Grid;
        var frame = GetTemplateChild("PART_Frame") as FrameworkElement;
        var copy = GetTemplateChild("PART_Copy") as FrameworkElement;
        _morph = frame is not null && copy is not null ? new HeightMorph(frame, copy) : null;
        _titleLeaving = Leaving(_title);
        _descriptionLeaving = Leaving(_description);
        _titleText = Line(_title, Title, true);
        _descriptionText = Line(_description, Description, false);
        _glyphs?.Children.Clear();
        _glyph = _glyphs is null ? null : Glyph();
        if (_glyph is not null) _glyphs!.Children.Add(_glyph);
    }

    // The icon settles in once when the state first appears (scale .92, considered, ease-enter); later changes crossfade.
    private void SettleIn()
    {
        if (_settledIn || _icon is null) return;
        _settledIn = true;
        if (Level != ArcMotionLevel.Full) return;
        var scale = new ScaleTransform(.92, .92);
        _icon.RenderTransformOrigin = new Point(.5, .5);
        _icon.RenderTransform = scale;
        var duration = TimeSpan.FromSeconds(.48);
        _icon.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ArcEasing.Enter, FillBehavior = FillBehavior.Stop });
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(.92, 1, duration) { EasingFunction = ArcEasing.Enter });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(.92, 1, duration) { EasingFunction = ArcEasing.Enter });
    }

    private static void OnCopyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var state = (EmptyState)d;
        bool title = e.Property == TitleProperty;
        state.Roll(title ? state._title : state._description, title ? state._titleLeaving : state._descriptionLeaving, title, (string)e.NewValue ?? string.Empty);
    }

    // The new line rises from .3em with a soft blur over .24 enter; the old one leaves upward (.16, subtle blur),
    // out of flow so it never holds space. The frame height springs to fit (smooth). Reduced fades .12 and snaps.
    private void Roll(Grid? slot, Canvas? leaving, bool title, string text)
    {
        if (slot is null || leaving is null) return;
        var old = title ? _titleText : _descriptionText;
        if (old?.Text == text) return;
        bool motion = IsLoaded && Level != ArcMotionLevel.Off;
        bool full = Level == ArcMotionLevel.Full;
        TextBlock? fresh = null;
        _morph?.Morph(() =>
        {
            if (old is not null)
            {
                slot.Children.Remove(old);
                if (motion) { old.Width = slot.ActualWidth; leaving.Children.Add(old); }
            }
            fresh = Line(slot, text, title);
        }, motion && full);
        if (title) _titleText = fresh; else _descriptionText = fresh;
        if (!motion || fresh is null) return;
        double em = title ? TITLE_SIZE : TEXT_SIZE;
        if (full)
        {
            TimePicker.Fly(fresh, 0, 1, .3 * em, 0, 4, 0, .24, ArcEasing.Enter, null);
            if (old is not null) TimePicker.Fly(old, old.Opacity, 0, 0, -.3 * em, 0, 2, .16, ArcEasing.Standard, () => leaving.Children.Remove(old));
        }
        else
        {
            TimePicker.Fly(fresh, 0, 1, 0, 0, 0, 0, .12, ArcEasing.Enter, null);
            if (old is not null) TimePicker.Fly(old, old.Opacity, 0, 0, 0, 0, 0, .12, ArcEasing.Standard, () => leaving.Children.Remove(old));
        }
    }

    private static Canvas? Leaving(Grid? slot)
    {
        if (slot is null) return null;
        slot.Children.Clear();
        var layer = new Canvas { IsHitTestVisible = false };
        slot.Children.Add(layer);
        return layer;
    }

    // h3: text-base 500; p: text-sm, text-secondary. Both use leading-body 1.4.
    private static TextBlock Line(Grid? slot, string text, bool title)
    {
        double size = title ? TITLE_SIZE : TEXT_SIZE;
        var line = new TextBlock
        {
            Text = text, FontSize = size, LineHeight = size * LEADING, LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, RenderTransform = new TranslateTransform(),
        };
        if (title) line.FontWeight = FontWeights.Medium;
        else line.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextSecondary");
        slot?.Children.Add(line);
        return line;
    }

    // A different icon pops in with the snappy spring from scale .6 and a subtle blur; the old one shrinks out (.16).
    private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((EmptyState)d).SwapIcon();

    private void SwapIcon()
    {
        if (_glyphs is null) return;
        var old = _glyph;
        var fresh = Glyph();
        _glyph = fresh;
        _glyphs.Children.Add(fresh);
        bool motion = IsLoaded && Level != ArcMotionLevel.Off;
        if (old is null) return;
        if (!motion) { _glyphs.Children.Remove(old); return; }
        if (Level == ArcMotionLevel.Full)
        {
            Pop(fresh, 0, 1, .6, 1, 2, 0, ArcEasing.Spring(SNAPPY, .6), .6, null);
            Pop(old, old.Opacity, 0, 1, .6, 0, 2, ArcEasing.Standard, .16, () => _glyphs.Children.Remove(old));
        }
        else
        {
            Pop(fresh, 0, 1, 1, 1, 0, 0, ArcEasing.Enter, .12, null);
            Pop(old, old.Opacity, 0, 1, 1, 0, 0, ArcEasing.Standard, .12, () => _glyphs.Children.Remove(old));
        }
    }

    private static void Pop(FrameworkElement element, double fromOpacity, double toOpacity, double fromScale, double toScale, double fromBlur, double toBlur, IEasingFunction ease, double seconds, System.Action? done)
    {
        var duration = TimeSpan.FromSeconds(seconds);
        TimePicker.Fly(element, fromOpacity, toOpacity, 0, 0, fromBlur, toBlur, seconds, ease, done);
        if (fromScale == toScale || element.RenderTransform is not ScaleTransform scale) return;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(fromScale, toScale, duration) { EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(fromScale, toScale, duration) { EasingFunction = ease });
    }

    // Geometry icons are drawn on lucide's 24-unit grid in text-secondary; other content is hosted as is.
    private FrameworkElement Glyph()
    {
        FrameworkElement host;
        if (Icon is Geometry or null)
        {
            var path = new Path { Data = Icon as Geometry ?? FolderIcon, Width = 24, Height = 24, Stretch = Stretch.None, StrokeThickness = IconStrokeThickness, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round };
            path.SetResourceReference(Shape.StrokeProperty, "ArcWpf.Brush.TextSecondary");
            host = path;
        }
        else host = new ContentPresenter { Content = Icon };
        host.HorizontalAlignment = HorizontalAlignment.Center;
        host.VerticalAlignment = VerticalAlignment.Center;
        host.RenderTransformOrigin = new Point(.5, .5);
        host.RenderTransform = new ScaleTransform(1, 1);
        return host;
    }

    private static Geometry Frozen(string data)
    {
        var geometry = Geometry.Parse(data);
        geometry.Freeze();
        return geometry;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new EmptyStatePeer(this);

    // A section named by Label; the icon is decorative, outgoing copies are not exposed.
    private sealed class EmptyStatePeer(EmptyState owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetNameCore() => owner.Label ?? base.GetNameCore();
        protected override string GetHelpTextCore() => owner.Description;
    }
}
