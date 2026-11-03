using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcWpf.Controls;

// Shared pieces of ChatThread, CommentThread and ConfirmPopover.
internal static class ThreadUi
{
    private static ControlTemplate? _plainBox;

    /// <summary>A native TextBox without chrome: the frame around it is the caller's, and text starts at its Padding.</summary>
    public static TextBox PlainTextBox()
    {
        var box = new TextBox { OverridesDefaultStyle = true, Template = _plainBox ??= BuildPlainBox(), Background = Brushes.Transparent, BorderThickness = new Thickness(0), FocusVisualStyle = null };
        box.SetResourceReference(Control.ForegroundProperty, "ArcWpf.Brush.Foreground");
        box.SetResourceReference(TextBox.CaretBrushProperty, "ArcWpf.Brush.Foreground");
        box.SetResourceReference(TextBox.SelectionBrushProperty, "ArcWpf.Brush.EditorSelection");
        return box;
    }

    private static ControlTemplate BuildPlainBox()
    {
        var host = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
        host.SetValue(ScrollViewer.FocusableProperty, false);
        host.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
        host.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
        // TextBoxBase applies Padding to this host itself; TextBoxView keeps 2 DIPs of its own on each side, taken back here so Padding is the whole inset.
        host.SetValue(FrameworkElement.MarginProperty, new Thickness(-2, 0, -2, 0));
        var template = new ControlTemplate(typeof(TextBox)) { VisualTree = host };
        template.Seal();
        return template;
    }

    /// <summary>Arc's plain avatar on the Avatar control: no border, initials at 38% of the size.</summary>
    public static Avatar Face(string name, ImageSource? image, double size, double? fontSize = null)
    {
        var avatar = new Avatar { DisplayName = name, Source = image, Width = size, Height = size, FontSize = fontSize ?? size * .38, BorderThickness = new Thickness(0), IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Top };
        avatar.SetResourceReference(Control.ForegroundProperty, "ArcWpf.Brush.TextSecondary");
        return avatar;
    }

    /// <summary>box-shadow: 0 0 0 2px surface around a stacked avatar.</summary>
    public static Border Ringed(UIElement face, double size)
    {
        var ring = new Border { Width = size + 4, Height = size + 4, CornerRadius = new CornerRadius(size / 2 + 2), Padding = new Thickness(2), Child = face };
        ring.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
        return ring;
    }

    /// <summary>CSS :focus-visible: keyboard focus that arrived from the keyboard, not from a press or an opened picker.</summary>
    public static bool FocusVisible(UIElement element) => element.IsKeyboardFocused && System.Windows.Input.InputManager.Current.MostRecentInputDevice is System.Windows.Input.KeyboardDevice;

    public static string Initials(string name) => string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(part => char.ToUpperInvariant(part[0])));

    public static void PressScale(FrameworkElement element, double down, double seconds = Tween.Instant)
    {
        var scale = new ScaleTransform();
        element.RenderTransformOrigin = new Point(.5, .5);
        element.RenderTransform = scale;
        void To(double value)
        {
            bool animate = element.IsLoaded && Tween.Level(element) == ArcMotionLevel.Full;
            foreach (var property in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty })
            {
                if (animate) { Tween.To(scale, property, value, seconds, ArcEasing.Standard); } else { Tween.Set(scale, property, value); }
            }
        }
        element.PreviewMouseLeftButtonDown += (_, _) => { if (element.IsEnabled) { To(down); } };
        element.PreviewMouseLeftButtonUp += (_, _) => To(1);
        element.MouseLeave += (_, _) => To(1);
    }
}

/// <summary>
/// Follows its content's height. A height change snaps unless a morph is asked for, then it springs. Reveal and Collapse grow from and to a true zero,
/// which is what motion's height "auto" does for rows, replies and clips. The content keeps its full height and is clipped.
/// </summary>
internal sealed class SpringHeightBox : Decorator
{
    private readonly SpringDriver _driver;
    private readonly ControlSpring _height;
    private readonly double _visual;
    private bool _measured, _morph, _running, _collapsing;
    private Action? _collapsed;
    private double _content;

    public SpringHeightBox(double visualDuration)
    {
        _visual = visualDuration;
        _driver = new SpringDriver(() => InvalidateMeasure()) { Settled = OnSettled };
        _height = _driver.Add(0, visualDuration);
        ClipToBounds = true;
        Unloaded += (_, _) => { if (_running) { _driver.Finish(); } };
    }

    public double VisualDuration => _visual;
    public bool IsCollapsing => _collapsing;
    public bool IsMoving => _running;

    /// <summary>The next height change springs.</summary>
    public void MorphNext() { _morph = true; InvalidateMeasure(); }

    public void Reveal()
    {
        _morph = false;
        _measured = true;
        _height.Snap(0);
        _running = true;
        InvalidateMeasure();
        Dispatcher.BeginInvoke(() =>
        {
            UpdateLayout();
            if (_content > 0 && !_collapsing) { _driver.Animate(_height, _content, _visual); } else if (!_collapsing) { _running = false; }
        }, System.Windows.Threading.DispatcherPriority.Render);
    }

    public void Collapse(Action done, bool animate)
    {
        _collapsed = done;
        _collapsing = true;
        if (!animate || !IsLoaded || ActualHeight <= 0) { Finish(); return; }
        _height.Snap(ActualHeight);
        _running = true;
        _driver.Animate(_height, 0, _visual);
    }

    public void Cancel()
    {
        _collapsing = false;
        _collapsed = null;
        _running = true;
        _driver.Animate(_height, _content, _visual);
    }

    private void Finish() { var done = _collapsed; _collapsed = null; _collapsing = false; _running = false; done?.Invoke(); }

    private void OnSettled()
    {
        if (_collapsing) { Finish(); return; }
        _running = false;
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size constraint)
    {
        if (Child is null) { return new Size(); }
        Child.Measure(new Size(constraint.Width, double.PositiveInfinity));
        var desired = Child.DesiredSize;
        bool changed = Math.Abs(desired.Height - _content) > .01;
        _content = desired.Height;
        if (!_measured) { _measured = true; _height.Snap(_content); return desired; }
        if (_collapsing) { return new Size(desired.Width, _height.Position); }
        if (changed)
        {
            bool spring = (_morph || _running) && IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;
            _morph = false;
            if (spring) { _running = true; _driver.Animate(_height, _content, _visual); }
            else { _running = false; _driver.Stop(); _height.Snap(_content); }
        }
        else if (!_running) { _morph = _morph && false; _height.Snap(_content); }
        return new Size(desired.Width, Math.Min(constraint.Height, Math.Max(0, _height.Position)));
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        Child?.Arrange(new Rect(0, 0, arrangeSize.Width, Math.Max(_content, arrangeSize.Height)));
        return arrangeSize;
    }
}

/// <summary>A count that rolls up or down when it changes.</summary>
internal sealed class ThreadCount : Grid
{
    private readonly double _distance;
    private readonly bool _percent;
    private TextBlock? _shown;
    private int _value = int.MinValue;

    public ThreadCount(double distance, bool percentOfHeight)
    {
        _distance = distance;
        _percent = percentOfHeight;
        ClipToBounds = true;
    }

    public int Value => _value;

    public void Set(int value)
    {
        if (value == _value) { return; }
        int direction = value > _value ? 1 : -1;
        bool first = _value == int.MinValue;
        _value = value;
        var next = new TextBlock { Text = value.ToString(CultureInfo.InvariantCulture), HorizontalAlignment = HorizontalAlignment.Center };
        var old = _shown;
        _shown = next;
        Children.Add(next);
        if (first || old is null || !IsLoaded || Tween.Level(this) == ArcMotionLevel.Off)
        {
            if (old is not null) { Children.Remove(old); }
            return;
        }
        bool full = Tween.Level(this) == ArcMotionLevel.Full;
        double travel = !full ? 0 : _percent ? ActualHeight * _distance : _distance;
        var nextMove = new TranslateTransform(0, direction * travel);
        var oldMove = new TranslateTransform();
        next.RenderTransform = nextMove;
        old.RenderTransform = oldMove;
        next.Opacity = 0;
        Tween.Spring(nextMove, TranslateTransform.YProperty, 0, Tween.Snappy);
        Tween.Spring(next, OpacityProperty, 1, Tween.Snappy);
        Tween.Spring(oldMove, TranslateTransform.YProperty, -direction * travel, Tween.Snappy);
        Tween.Spring(old, OpacityProperty, 0, Tween.Snappy, done: () => Children.Remove(old));
    }
}
