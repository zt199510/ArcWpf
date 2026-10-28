using System.Windows.Documents;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ArcWpf.Controls;

// All animated properties belong to these private visuals, never consumer bindings.
internal sealed class PresenceRoll : Grid
{
    private readonly TextBlock _current = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly PresenceDecorativeText _old = new() { VerticalAlignment = VerticalAlignment.Center, Opacity = 0 };
    private readonly TranslateTransform _currentMove = new();
    private readonly TranslateTransform _oldMove = new();
    private readonly OwnedMotion _motion = new();
    internal bool IsRunning => _motion.IsRunning;
    public PresenceRoll()
    {
        ClipToBounds = true; Height = 20;
        _current.RenderTransform = _currentMove; _old.RenderTransform = _oldMove;

        Children.Add(_old); Children.Add(_current);
    }
    internal void Set(string text, bool animate)
    {
        if (_current.Text == text) { if (!animate) Stop(); return; }
        var previous = _current.Text; _motion.Stop(); _old.Text = previous; _current.Text = text;
        AutomationProperties.SetName(this, text);
        if (!animate || previous.Length == 0) { _currentMove.Y = 0; _old.Opacity = 0; return; }
        bool up = !int.TryParse(previous.TrimStart('+'), out int before) || !int.TryParse(text.TrimStart('+'), out int after) || after >= before;
        _currentMove.Y = up ? 20 : -20; _oldMove.Y = 0; _old.Opacity = 1;
        _motion.Double(_currentMove, TranslateTransform.YProperty, 0, .18, true);
        _motion.Double(_oldMove, TranslateTransform.YProperty, up ? -20 : 20, .18, true);
        _motion.Double(_old, OpacityProperty, 0, .18, true);
    }
    internal void Stop() { _motion.Stop(); _currentMove.Y = 0; _oldMove.Y = 0; _old.Opacity = 0; }
}

internal sealed class PresenceFace : Grid
{
    private static readonly DependencyProperty StatusBrushProperty = DependencyProperty.Register("StatusBrush", typeof(Brush), typeof(PresenceFace), new PropertyMetadata(null, (d, _) => ((PresenceFace)d).UpdateStatusBrush()));
    private void UpdateStatusBrush()
    {
        if (GetValue(StatusBrushProperty) is SolidColorBrush brush) _motion.Color(_statusBrush, brush.Color, .2, Animate);
    }
    private readonly PresenceStack _owner;
    private readonly bool _rosterFace;
    private double FaceDiameter => _rosterFace ? 32 : _owner.Diameter;
    private bool Animate => _owner.Animate && (!_rosterFace || _owner.IsOpen);
    private readonly Avatar _avatar = new() { Status = AvatarStatus.None, BorderThickness = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
    private readonly Border _ring = new() { BorderThickness = new Thickness(2), Margin = new Thickness(-2), IsHitTestVisible = false };
    private readonly Border _counter = new() { BorderThickness = new Thickness(1) };
    private readonly PresenceRoll _number = new() { HorizontalAlignment = HorizontalAlignment.Center, Width = 30 };
    private readonly TranslateTransform _numberMove = new();
    private readonly Border _status = new() { Height = 10, Width = 10, CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(2), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
    private readonly StackPanel _dots = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly TranslateTransform _move = new();
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly OwnedMotion _motion = new();
    private readonly List<TranslateTransform> _bounces = [];
    private PresenceStatus? _lastStatus;
    private readonly SolidColorBrush _statusBrush = new();
    private bool _typing;
    internal bool Exiting;
    internal bool IsRunning => _motion.IsRunning || _typing || _number.IsRunning || _scale.HasAnimatedProperties;
    internal double X => _move.X;
    public PresenceFace(PresenceStack owner, bool rosterFace = false)
    {
        _owner = owner; _rosterFace = rosterFace; RenderTransformOrigin = new Point(.5, .6);
        var transforms = new TransformGroup(); transforms.Children.Add(_scale); transforms.Children.Add(_move); RenderTransform = transforms;
        _counter.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
        foreach (TextBlock text in _number.Children) text.TextAlignment = TextAlignment.Center;
        _status.Background = _statusBrush;
        _number.RenderTransform = _numberMove; _counter.Child = _number;
        _avatar.SetResourceReference(Control.ForegroundProperty, "ArcWpf.Brush.TextSecondary");
        _counter.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        _number.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.TextSecondary");
        Children.Add(_ring); Children.Add(_avatar); Children.Add(_counter); Children.Add(_status);
        for (int i = 0; i < 3; i++)
        {
            var move = new TranslateTransform(); _bounces.Add(move);
            var dot = new Border { Width = 3, Height = 3, CornerRadius = new CornerRadius(1.5), Margin = new Thickness(1, 0, 1, 0), RenderTransform = move };
            dot.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Background"); _dots.Children.Add(dot);
        }
        _status.Child = _dots;
        Unloaded += (_, _) => Stop();
    }
    internal void Update(PresencePerson? person)
    {
        Width = Height = FaceDiameter; _avatar.Size = _owner.Size; _avatar.Width = _avatar.Height = FaceDiameter;
        _counter.CornerRadius = new CornerRadius(FaceDiameter / 2);
        _ring.BorderBrush = _status.BorderBrush = _owner.RingBrush;
        if (_rosterFace) _status.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.SurfaceRaised");
        _ring.CornerRadius = new CornerRadius(FaceDiameter / 2 + 2);
        _ring.Visibility = _rosterFace ? Visibility.Collapsed : Visibility.Visible;
        _avatar.FontSize = FaceDiameter * .36;
        TextElement.SetFontSize(_number, FaceDiameter * .34);
        _avatar.Visibility = person is null ? Visibility.Collapsed : Visibility.Visible;
        _counter.Visibility = person is null ? Visibility.Visible : Visibility.Collapsed;
        _status.Visibility = person is null ? Visibility.Collapsed : Visibility.Visible;
        IsHitTestVisible = true;
        if (person is null) { _number.Set("+" + _owner.OverflowCount, Animate); StopTyping(); return; }
        _avatar.DisplayName = person.Name; _avatar.Source = person.Source;
        _motion.Double(_avatar, OpacityProperty, person.Status == PresenceStatus.Idle ? .5 : 1, .2, Animate);
        bool typing = person.Status == PresenceStatus.Typing;
        string key = person.Status == PresenceStatus.Active ? "ArcWpf.Brush.AvatarOnline" : typing ? "ArcWpf.Brush.Foreground" : "ArcWpf.Brush.BorderStrong";
        SetResourceReference(StatusBrushProperty, key);
        UpdateStatusBrush();
        double dotSize = _rosterFace ? 9 : _owner.Size == AvatarSize.Small ? 8 : _owner.Size == AvatarSize.Large ? 12 : 10;
        _motion.Double(_status, WidthProperty, (typing ? dotSize * 2.1 : dotSize) + 4, .2, Animate, true);
        _motion.Double(_status, HeightProperty, (typing ? dotSize + 2 : dotSize) + 4, .2, Animate, true);
        _status.CornerRadius = new CornerRadius((typing ? dotSize + 6 : dotSize + 4) / 2);
        _status.Margin = new Thickness(0, 0, typing ? -7 : -3, -3);
        foreach (Border dot in _dots.Children) dot.Width = dot.Height = !_rosterFace && _owner.Size == AvatarSize.Small ? 2 : 3;
        _dots.Opacity = typing ? 1 : 0;
        if (!typing || !Animate) StopTyping();
        else if (!_typing || _lastStatus != person.Status) StartTyping();
        _lastStatus = person.Status;
    }
    internal void Enter()
    {
        if (!Animate) return;
        // Independently chosen overshoot; public docs specify .4 initial scale only.
        foreach (var property in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty })
        {
            var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(.32), FillBehavior = FillBehavior.Stop };
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(.4, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new SplineDoubleKeyFrame(1.06, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(.22)), new KeySpline(.16, 1, .3, 1)));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(.32))));
            animation.Completed += (_, _) => _scale.BeginAnimation(property, null);
            _scale.BeginAnimation(property, animation);
        }
    }
    internal void Show() { _motion.Double(this, OpacityProperty, 1, .16, Animate); _motion.Double(_scale, ScaleTransform.ScaleXProperty, 1, .24, Animate); _motion.Double(_scale, ScaleTransform.ScaleYProperty, 1, .24, Animate); }
    internal void Move(double x, double y, bool fan)
    {
        _motion.Double(_move, TranslateTransform.XProperty, x, .32, Animate, true);
        _motion.Double(_move, TranslateTransform.YProperty, y, .2, Animate, true);
        _motion.Double(_numberMove, TranslateTransform.XProperty, fan || _owner.Max == 0 ? 0 : Math.Round(FaceDiameter / 6), .2, Animate, true);
    }
    internal void Exit(Action completed)
    {
        StopTyping();
        _motion.Double(_scale, ScaleTransform.ScaleXProperty, .4, .16, Animate);
        _motion.Double(_scale, ScaleTransform.ScaleYProperty, .4, .16, Animate);
        _motion.Double(this, OpacityProperty, 0, .16, Animate, completed: completed);
    }
    private void StartTyping()
    {
        StopTyping(); _typing = true;
        for (int i = 0; i < _bounces.Count; i++)
        {
            var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(1.1), RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromSeconds(i * .14) };
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
            animation.KeyFrames.Add(new SplineDoubleKeyFrame(-2, KeyTime.FromPercent(.3), new KeySpline(.4, 0, .6, 1)));
            animation.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromPercent(.6), new KeySpline(.4, 0, .6, 1)));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
            _bounces[i].BeginAnimation(TranslateTransform.YProperty, animation);
        }
    }
    private void StopTyping() { _typing = false; foreach (var move in _bounces) { move.BeginAnimation(TranslateTransform.YProperty, null); move.Y = 0; } }
    internal void Stop() { _motion.Stop(); _number.Stop(); StopTyping(); _scale.BeginAnimation(ScaleTransform.ScaleXProperty, null); _scale.BeginAnimation(ScaleTransform.ScaleYProperty, null); }
}

internal sealed class PresenceRow : Grid
{
    private readonly PresenceStack _owner;
    private readonly OwnedMotion _motion = new();
    private readonly PresenceFace _avatar;
    private readonly TextBlock _name = new() { FontSize = 14, FontWeight = FontWeights.Medium, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly PresenceRoll _detail = new();
    
    private readonly Grid _layout = new() { Margin = new Thickness(10, 8, 10, 8) };
    private readonly System.Windows.Controls.Button _button = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(0) };
    private PresencePerson? _person;
    internal bool Exiting;
    internal bool IsRunning => _motion.IsRunning || _detail.IsRunning || _avatar.IsRunning;
    public PresenceRow(PresenceStack owner)
    {
        _owner = owner; _avatar = new PresenceFace(owner, true) { Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center }; Height = 56; ClipToBounds = true;
        _layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); _layout.ColumnDefinitions.Add(new ColumnDefinition());
        _layout.Children.Add(_avatar);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(text, 1);
        text.Children.Add(_name); text.Children.Add(_detail); _layout.Children.Add(text);
        TextElement.SetFontSize(_detail, 12);
        _detail.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.TextMuted");

        _button.SetResourceReference(StyleProperty, new ComponentResourceKey(typeof(PresenceStack), "PersonButtonStyle"));
        _button.Click += (_, _) => { if (_person is not null) _owner.SelectPerson(_person); };
        Unloaded += (_, _) => Stop();
    }
    internal void Update(PresencePerson person)
    {
        bool returning = Exiting; Exiting = false; IsHitTestVisible = true; _person = person;
        _avatar.Update(person);
        _name.Text = _owner.PersonName(person); _detail.Set(person.Detail ?? PresenceStack.StatusText(person), _owner.Animate);
        if (_owner.IsPersonSelectionEnabled && !Children.Contains(_button)) { Children.Clear(); _button.Content = _layout; Children.Add(_button); }
        else if (!_owner.IsPersonSelectionEnabled && !Children.Contains(_layout)) { Children.Clear(); _button.Content = null; Children.Add(_layout); }
        AutomationProperties.SetName(_button, $"{_owner.PersonName(person)}, {PresenceStack.StatusText(person)}, {person.Detail}");
        if (returning) { _motion.Double(this, HeightProperty, 56, .24, _owner.Animate, true); _motion.Double(this, OpacityProperty, 1, .16, _owner.Animate); }
    }
    internal void Enter() { if (_owner.Animate) Height = 0; _motion.Double(this, HeightProperty, 56, .24, _owner.Animate, true); }
    internal void Exit(Action completed) { if (Exiting) return; Exiting = true; IsHitTestVisible = false; _motion.Double(this, HeightProperty, 0, .16, _owner.Animate); _motion.Double(this, OpacityProperty, 0, .16, _owner.Animate, completed: completed); }
    internal void Stop() { _motion.Stop(); _detail.Stop(); _avatar.Stop(); }
}



internal sealed class PresenceRoster : StackPanel
{
    protected override AutomationPeer OnCreateAutomationPeer() => new RosterPeer(this);
    private sealed class RosterPeer(PresenceRoster owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => "PresenceRosterDialog";
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;
        protected override string GetLocalizedControlTypeCore() => "dialog";
    }
}

internal sealed class PresenceDecorativeText : TextBlock
{
    protected override AutomationPeer OnCreateAutomationPeer() => null!;
}
