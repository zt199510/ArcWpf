using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ArcWpf.Controls;

// skeleton.tsx (frozen 2026-10-31): text lines and an optional avatar that pulse in a staggered wave.
// With Content, the placeholder crossfades into it while the height springs from placeholder to content.
[TemplatePart(Name = "PART_Frame", Type = typeof(Grid))]
[TemplatePart(Name = "PART_Body", Type = typeof(Grid))]
[TemplatePart(Name = "PART_Placeholder", Type = typeof(Border))]
[TemplatePart(Name = "PART_Content", Type = typeof(ContentPresenter))]
public class Skeleton : ContentControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(Skeleton), new FrameworkPropertyMetadata("Loading content"));
    public static readonly DependencyProperty LinesProperty = DependencyProperty.Register(nameof(Lines), typeof(int), typeof(Skeleton), new FrameworkPropertyMetadata(3, OnShapeChanged, (_, value) => Math.Clamp((int)value, 1, 6)));
    public static readonly DependencyProperty HasAvatarProperty = DependencyProperty.Register(nameof(HasAvatar), typeof(bool), typeof(Skeleton), new FrameworkPropertyMetadata(false, OnShapeChanged));
    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(Skeleton), new FrameworkPropertyMetadata(true, OnIsLoadingChanged));

    /// <summary>Accessible name of the loading status.</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <summary>Number of text lines, clamped to 1-6.</summary>
    public int Lines { get => (int)GetValue(LinesProperty); set => SetValue(LinesProperty, value); }
    /// <summary>Adds a round avatar placeholder.</summary>
    public bool HasAvatar { get => (bool)GetValue(HasAvatarProperty); set => SetValue(HasAvatarProperty, value); }
    /// <summary>Keeps the placeholder visible while true. Only used together with Content.</summary>
    public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>True while the placeholder is the visible state.</summary>
    public bool IsBusy => Content is null || IsLoading;

    private const double STEP = .09, PULSE = 1.8, LINE_GAP = 12, AVATAR = 44, AVATAR_GAP = 16;
    private Grid? _frame, _body;
    private Border? _placeholder;
    private ContentPresenter? _content;
    private HeightMorph? _morph;
    private readonly List<Border> _blocks = [];

    static Skeleton() { DefaultStyleKeyProperty.OverrideMetadata(typeof(Skeleton), new FrameworkPropertyMetadata(typeof(Skeleton))); }

    public Skeleton()
    {
        Loaded += (_, _) => { Show(false); Pulse(); };
        Unloaded += (_, _) => { _morph?.Stop(); StopPulse(); };
    }

    private ArcMotionLevel Level => ArcMotion.GetLevel(this);

    public override void OnApplyTemplate()
    {
        _morph?.Stop();
        base.OnApplyTemplate();
        _frame = GetTemplateChild("PART_Frame") as Grid;
        _body = GetTemplateChild("PART_Body") as Grid;
        _placeholder = GetTemplateChild("PART_Placeholder") as Border;
        _content = GetTemplateChild("PART_Content") as ContentPresenter;
        if (_content is not null) _content.RenderTransform = new TranslateTransform();
        _morph = _frame is not null && _body is not null ? new HeightMorph(_frame, _body) { Settled = Rest } : null;
        Build();
        Show(false);
    }

    private static void OnShapeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var skeleton = (Skeleton)d; skeleton.Build(); skeleton.Pulse(); }
    private static void OnIsLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Skeleton)d).Swap();

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        if ((oldContent is null) != (newContent is null)) Swap();
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ArcMotion.LevelProperty) Pulse();
    }

    // .root: avatar (control-height-md pill) and a grid of lines; the first line is 48% wide and 16 tall, the last 72%.
    private void Build()
    {
        if (_placeholder is null) return;
        StopPulse();
        _blocks.Clear();
        var row = new DockPanel { LastChildFill = true };
        if (HasAvatar)
        {
            var avatar = Block(AVATAR, AVATAR / 2);
            avatar.Width = AVATAR;
            avatar.VerticalAlignment = VerticalAlignment.Top;
            avatar.Margin = new Thickness(0, 0, AVATAR_GAP, 0);
            DockPanel.SetDock(avatar, Dock.Left);
            row.Children.Add(avatar);
        }
        var lines = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
        int count = Lines;
        for (int i = 0; i < count; i++)
        {
            double height = i == 0 ? 16 : 12;
            double share = i == count - 1 ? .72 : i == 0 ? .48 : 1;
            var line = Block(height, height / 2);
            var track = new Grid { Margin = new Thickness(0, i == 0 ? 0 : LINE_GAP, 0, 0) };
            track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(share, GridUnitType.Star) });
            track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - share, GridUnitType.Star) });
            track.Children.Add(line);
            lines.Children.Add(track);
        }
        row.Children.Add(lines);
        _placeholder.Child = row;
    }

    private Border Block(double height, double radius)
    {
        var block = new Border { Height = height, CornerRadius = new CornerRadius(radius) };
        block.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SkeletonFill");
        _blocks.Add(block);
        return block;
    }

    // A calm, finite pulse: each block dims to .52 a beat (90ms) after the one before, 11 times over about 20 seconds.
    private void Pulse()
    {
        StopPulse();
        if (!IsLoaded || !IsBusy || Level != ArcMotionLevel.Full) return;
        for (int i = 0; i < _blocks.Count; i++)
        {
            var pulse = new DoubleAnimationUsingKeyFrames { BeginTime = TimeSpan.FromSeconds(i * STEP), Duration = TimeSpan.FromSeconds(PULSE), RepeatBehavior = new RepeatBehavior(11), FillBehavior = FillBehavior.Stop };
            pulse.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            pulse.KeyFrames.Add(new EasingDoubleKeyFrame(.52, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(PULSE / 2)), ArcEasing.InOut));
            pulse.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(PULSE)), ArcEasing.InOut));
            _blocks[i].BeginAnimation(OpacityProperty, pulse);
        }
    }

    private void StopPulse() { foreach (var block in _blocks) block.BeginAnimation(OpacityProperty, null); }

    private void Swap()
    {
        if (_morph is null || !IsLoaded) { Show(false); return; }
        _morph.Morph(() => Show(true), Level == ArcMotionLevel.Full);
        Pulse();
    }

    // Placeholder out .16 (instant .12 when reduced); content rises 4px over .24 enter, or fades .12 when reduced.
    private void Show(bool animate)
    {
        if (_placeholder is null || _content is null) return;
        bool busy = IsBusy;
        bool motion = animate && Level != ArcMotionLevel.Off;
        bool full = Level == ArcMotionLevel.Full;
        UIElement incoming = busy ? _placeholder : _content, outgoing = busy ? _content : _placeholder;
        incoming.Visibility = Visibility.Visible;
        if (!motion)
        {
            incoming.BeginAnimation(OpacityProperty, null);
            incoming.Opacity = 1;
            outgoing.BeginAnimation(OpacityProperty, null);
            outgoing.Visibility = Visibility.Collapsed;
            return;
        }
        if (outgoing.Visibility == Visibility.Visible)
            TimePicker.Fly(outgoing, outgoing.Opacity, 0, 0, 0, 0, 0, full ? .16 : .12, ArcEasing.Standard, () => { if (IsBusy != busy) return; outgoing.Visibility = Visibility.Collapsed; });
        if (busy) TimePicker.Fly(incoming, 0, 1, 0, 0, 0, 0, full ? .16 : .12, ArcEasing.Standard, null);
        else TimePicker.Fly(incoming, 0, 1, full ? 4 : 0, 0, 0, 0, full ? .24 : .12, ArcEasing.Enter, null);
    }

    // Once the height is back to auto, the hidden state must not hold layout.
    private void Rest()
    {
        if (_placeholder is null || _content is null) return;
        var hidden = IsBusy ? (UIElement)_content : _placeholder;
        if (hidden.Opacity == 0 || !hidden.HasAnimatedProperties) hidden.Visibility = Visibility.Collapsed;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SkeletonPeer(this);

    // role="status" with aria-busy while the placeholder shows; the shapes themselves are not exposed.
    private sealed class SkeletonPeer(Skeleton owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetNameCore() => owner.IsBusy ? owner.Label : base.GetNameCore();
        protected override string GetItemStatusCore() => owner.IsBusy ? "busy" : string.Empty;
        protected override List<AutomationPeer>? GetChildrenCore() => owner.IsBusy ? null : base.GetChildrenCore();
    }
}
