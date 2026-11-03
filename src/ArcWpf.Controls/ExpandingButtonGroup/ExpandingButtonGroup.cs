using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public enum ExpandingButtonGroupSize { Small, Medium }
public enum ExpandingButtonGroupTone { Neutral, Danger }

public sealed class ExpandingButtonGroupItem
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public string Icon { get; init; } = "";
    /// <summary>Past tense word shown in place after the action. Leave null for actions that open something.</summary>
    public string? DoneLabel { get; init; }
    public ExpandingButtonGroupTone Tone { get; init; }
    /// <summary>Dimmed and inert, but it stays focusable and still reveals its label.</summary>
    public bool IsDisabled { get; init; }
    /// <summary>Runs the action. Return false, or fault, to skip the confirmation; the button stays busy until the task ends.</summary>
    public Func<Task<bool>>? Run { get; init; }
}

/// <summary>A compact pill of icon buttons: one action always shows its label, and the one you point at or focus takes it over.</summary>
public class ExpandingButtonGroup : Control
{
    public static readonly DependencyProperty ItemsProperty = Prop(nameof(Items), (IReadOnlyList<ExpandingButtonGroupItem>)[]);
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ExpandingButtonGroup), new PropertyMetadata("", (d, e) => AutomationProperties.SetName(d, (string)e.NewValue)));
    public static readonly DependencyProperty SizeProperty = Prop(nameof(Size), ExpandingButtonGroupSize.Medium);
    public static readonly DependencyProperty DefaultExpandedProperty = Prop<string?>(nameof(DefaultExpanded), null);
    public static readonly DependencyProperty ConfirmDurationProperty = DependencyProperty.Register(nameof(ConfirmDuration), typeof(TimeSpan), typeof(ExpandingButtonGroup), new PropertyMetadata(TimeSpan.FromMilliseconds(1400)));

    private static DependencyProperty Prop<T>(string name, T fallback) => DependencyProperty.Register(name, typeof(T), typeof(ExpandingButtonGroup), new PropertyMetadata(fallback, (d, _) => ((ExpandingButtonGroup)d).Build()));

    public IReadOnlyList<ExpandingButtonGroupItem> Items { get => (IReadOnlyList<ExpandingButtonGroupItem>)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public ExpandingButtonGroupSize Size { get => (ExpandingButtonGroupSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public string? DefaultExpanded { get => (string?)GetValue(DefaultExpandedProperty); set => SetValue(DefaultExpandedProperty, value); }
    public TimeSpan ConfirmDuration { get => (TimeSpan)GetValue(ConfirmDurationProperty); set => SetValue(ConfirmDurationProperty, value); }

    /// <summary>Raised with the item id whenever an action runs, before its own Run.</summary>
    public event EventHandler<string>? ItemInvoked;

    /// <summary>Pixels the pointer must travel into a neighbouring action before the expansion moves to it.</summary>
    private const double Hysteresis = 4;

    private readonly List<ItemView> _views = [];
    private readonly DispatcherTimer _confirm = new();
    private Decorator? _host;
    private StackPanel? _row;
    private TextBlock? _status;
    private string? _intent, _focused, _pointerOver, _keyFocus, _touchOpen, _done, _busy;
    private Point? _pointerAt;
    private (string Id, bool Touch, bool WasExpanded)? _press;
    private int _seq;
    private ExpandingButtonGroupItem? _confirming;
    private Window? _touchWindow;

    static ExpandingButtonGroup() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ExpandingButtonGroup), new FrameworkPropertyMetadata(typeof(ExpandingButtonGroup)));

    public ExpandingButtonGroup()
    {
        _confirm.Tick += (_, _) => EndConfirm();
        Unloaded += (_, _) => { _confirm.Stop(); DropTouchWatch(); foreach (var view in _views) { view.Stop(); } };
        ThemeWatch.Add(this, () => { foreach (var view in _views) { view.Sync(false); } });
    }

    private bool Has(string? id) => id is not null && (Items ?? []).Any(item => item.Id == id);
    private string? RestId => Has(DefaultExpanded) ? DefaultExpanded : Items?.FirstOrDefault(item => !item.IsDisabled)?.Id ?? Items?.FirstOrDefault()?.Id;
    internal string? ExpandedId => Has(_intent) ? _intent : RestId;
    private string? TabStop => Has(_focused) ? _focused : RestId;

    internal string? DoneIdForTest => _done;
    internal string? BusyIdForTest => _busy;
    internal double ItemWidthTargetForTest(string id) => _views.First(v => v.Item.Id == id).WidthTarget;
    internal double ItemWidthForTest(string id) => _views.First(v => v.Item.Id == id).Button.Width;
    internal ToolbarButton ButtonForTest(string id) => _views.First(v => v.Item.Id == id).Button;
    internal string WordForTest(string id) => _views.First(v => v.Item.Id == id).Word;
    internal void PointerAtForTest(string id, double insideX) => PointerAt(_views.First(v => v.Item.Id == id), insideX, new Point(insideX, 0));
    internal void PointerLeaveForTest() => PointerLeave();
    internal void TapForTest(string id) { var view = _views.First(v => v.Item.Id == id); _press = (id, true, ExpandedId == id); OnItemClick(view.Item); }
    internal void FinishMotionForTest() { foreach (var view in _views) { view.Finish(); } }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _host = GetTemplateChild("PART_Root") as Decorator;
        Build();
    }

    private void Build()
    {
        if (_host is null) { return; }
        foreach (var view in _views) { view.Stop(); }
        _views.Clear();
        bool small = Size == ExpandingButtonGroupSize.Small;
        _row = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var item in Items ?? [])
        {
            var view = new ItemView(this, item, small);
            view.Button.Margin = new Thickness(_views.Count == 0 ? 0 : 2, 0, 0, 0);
            _views.Add(view);
            _row.Children.Add(view.Button);
        }
        _status = new TextBlock { Width = 0, Height = 0, Opacity = 0 };
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        var tray = new Border { BorderThickness = new Thickness(1), Padding = new Thickness(3), Child = new Grid { Children = { _status, _row } }, HorizontalAlignment = HorizontalAlignment.Left };
        tray.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        tray.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
        tray.CornerRadius = new CornerRadius((small ? 28 : 36) / 2d + 4);
        tray.MouseMove += OnTrayMove;
        tray.MouseLeave += (_, e) => { if (e.StylusDevice is null) { PointerLeave(); } };
        tray.PreviewKeyDown += OnTrayKey;
        tray.LostKeyboardFocus += (_, _) => { if (!tray.IsKeyboardFocusWithin) { _keyFocus = null; _touchOpen = null; SetIntent(_pointerOver ?? _done); } };
        _host.Child = tray;
        SyncAll();
    }

    private void SetIntent(string? id) { _intent = id; SyncAll(); }

    private void SyncAll()
    {
        string? expanded = ExpandedId, stop = TabStop;
        foreach (var view in _views) { view.Button.IsTabStop = view.Item.Id == stop; view.Sync(IsLoaded); }
        if (_intent is not null && _touchOpen == _intent) { WatchTouchOutside(); } else { DropTouchWatch(); }
    }

    // A tap outside closes a label opened by a first tap.
    private void WatchTouchOutside()
    {
        if (_touchWindow is not null) { return; }
        _touchWindow = Window.GetWindow(this);
        if (_touchWindow is not null) { _touchWindow.PreviewMouseDown += OnWindowDown; }
    }

    private void DropTouchWatch()
    {
        if (_touchWindow is not null) { _touchWindow.PreviewMouseDown -= OnWindowDown; _touchWindow = null; }
    }

    private void OnWindowDown(object sender, MouseButtonEventArgs e)
    {
        if (Outside.IsWithin(e.OriginalSource, this)) { return; }
        _touchOpen = null;
        SetIntent(_done ?? _keyFocus);
    }

    private void Confirm(ExpandingButtonGroupItem item)
    {
        _confirm.Stop();
        _seq++;
        _done = item.Id;
        _confirming = item;
        // A zero width space alternates so the same word is announced again on a repeat action.
        ArcEffects.Announce(_status, item.DoneLabel + (_seq % 2 == 1 ? "" : "​"));
        _confirm.Interval = ConfirmDuration;
        _confirm.Start();
        SyncAll();
    }

    private void EndConfirm()
    {
        _confirm.Stop();
        var item = _confirming;
        _confirming = null;
        _done = null;
        // Collapse unless the pointer or the keyboard is still on it.
        if (item is not null && _pointerOver != item.Id && _keyFocus != item.Id)
        {
            if (_touchOpen == item.Id) { _touchOpen = null; }
            if (_intent == item.Id) { _intent = _pointerOver ?? _keyFocus; }
        }
        SyncAll();
    }

    private async void Activate(ExpandingButtonGroupItem item)
    {
        if (item.IsDisabled || _busy == item.Id || _done == item.Id) { return; }
        SetIntent(item.Id);
        ItemInvoked?.Invoke(this, item.Id);
        bool result = true;
        try
        {
            if (item.Run?.Invoke() is { } pending)
            {
                if (!pending.IsCompleted) { _busy = item.Id; SyncAll(); }
                result = await pending;
            }
        }
        catch (Exception) { result = false; }
        finally { if (_busy == item.Id) { _busy = null; SyncAll(); } }
        if (!IsLoaded || !result || item.DoneLabel is null) { return; }
        Confirm(item);
    }

    private void OnItemClick(ExpandingButtonGroupItem item)
    {
        var last = _press;
        _press = null;
        // Touch has no hover: the first tap opens the label, the second runs it. An item already showing its label runs at once.
        if (last is { Touch: true, WasExpanded: false } && last.Value.Id == item.Id)
        {
            _touchOpen = item.Id;
            SetIntent(item.Id);
            return;
        }
        if (last?.Touch == true) { _touchOpen = item.Id; }
        Activate(item);
    }

    // Hover follows real pointer movement only: a button that slides under a resting pointer must not take over.
    private void OnTrayMove(object sender, MouseEventArgs e)
    {
        if (e.StylusDevice is not null) { return; }
        var view = _views.FirstOrDefault(v => v.Button.IsMouseOver);
        var screen = e.GetPosition(this);
        if (_pointerAt is { } at && Math.Abs(at.X - screen.X) < 1 && Math.Abs(at.Y - screen.Y) < 1) { return; }
        _pointerAt = screen;
        if (view is not null) { PointerAt(view, e.GetPosition(view.Button).X, screen); }
    }

    private void PointerAt(ItemView view, double insideX, Point at)
    {
        _pointerAt = at;
        if (view.Item.Id == _pointerOver) { return; }
        // Hysteresis: resting on a boundary between two actions never flips between them.
        if (_pointerOver is not null && (insideX < Hysteresis || insideX > view.Button.ActualWidth - Hysteresis)) { return; }
        _pointerOver = view.Item.Id;
        SetIntent(view.Item.Id);
    }

    private void PointerLeave()
    {
        _pointerAt = null;
        _pointerOver = null;
        SetIntent(_done ?? _keyFocus);
    }

    private void OnTrayKey(object sender, KeyEventArgs e)
    {
        int index = _views.FindIndex(v => v.Button.IsKeyboardFocused);
        if (index < 0) { return; }
        int target = ToolbarKeys.Next(_views.Select(_ => true).ToList(), index, e.Key, false);
        if (target < 0) { return; }
        e.Handled = true;
        _views[target].Button.Focus();
    }

    private sealed class ItemView
    {
        private readonly ExpandingButtonGroup _owner;
        private readonly OwnedMotion _motion = new();
        private readonly SpringDriver _driver;
        private readonly ControlSpring _width;
        private readonly ThemedBrush _fill;
        private readonly double _height, _icon, _start, _gap, _end, _fontSize;
        private readonly Canvas _clip = new();
        private readonly Grid _iconBox = new(), _label = new();
        private readonly ScaleTransform _iconPress = new();
        private readonly TranslateTransform _labelMove = new();
        private readonly RectangleGeometry _shape = new();
        private readonly double _textWidth;
        private FrameworkElement? _glyph;
        private TextBlock? _word;
        private bool? _expanded;
        private bool _showingDone, _busy, _settled;

        public ExpandingButtonGroupItem Item { get; }
        public ToolbarButton Button { get; }
        public double WidthTarget { get; private set; }
        public string Word => _word?.Text ?? "";

        public ItemView(ExpandingButtonGroup owner, ExpandingButtonGroupItem item, bool small)
        {
            _owner = owner;
            Item = item;
            (_height, _icon, _start, _gap, _end, _fontSize) = small ? (28d, 14d, 7d, 5d, 11d, 12d) : (36d, 16d, 10d, 7d, 14d, 14d);
            Button = new ToolbarButton(_height / 2) { Height = _height, MinWidth = _height, Roles = Roles, Padding = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
            _fill = new ThemedBrush(Button, _motion);
            Button.Background = _fill.Brush;
            Button.IsInert = item.IsDisabled;
            Button.Opacity = item.IsDisabled ? .5 : 1;
            AutomationProperties.SetName(Button, item.Label);

            _textWidth = Math.Max(Measure(item.Label), item.DoneLabel is null ? 0 : Measure(item.DoneLabel));
            _driver = new SpringDriver(Apply);
            _width = _driver.Add(_height, ArcSpring.Morph);

            _iconBox.Width = _iconBox.Height = _icon;
            _iconBox.RenderTransformOrigin = new Point(.5, .5);
            _iconBox.RenderTransform = _iconPress;
            Canvas.SetLeft(_iconBox, _start);
            Canvas.SetTop(_iconBox, (_height - _icon) / 2);
            _label.RenderTransform = _labelMove;
            Canvas.SetLeft(_label, _start + _icon + _gap);
            Canvas.SetTop(_label, (_height - _fontSize) / 2);
            _label.Opacity = 0;
            _clip.Children.Add(_iconBox);
            _clip.Children.Add(_label);
            _clip.Clip = _shape;
            Button.Content = _clip;

            _glyph = Glyph();
            _iconBox.Children.Add(_glyph);
            _word = WordBlock(item.Label);
            _label.Children.Add(_word);

            Button.PreviewMouseLeftButtonDown += (_, e) => _owner._press = (Item.Id, e.StylusDevice is not null, _owner.ExpandedId == Item.Id);
            Button.Click += (_, _) => _owner.OnItemClick(Item);
            Button.PressedChanged += OnPressed;
            Button.GotKeyboardFocus += (_, _) =>
            {
                _owner._focused = Item.Id;
                // Only keyboard focus expands; a mouse click or a tap already has its own path.
                if (ThreadUi.FocusVisible(Button)) { _owner._keyFocus = Item.Id; _owner.SetIntent(Item.Id); } else { _owner.SyncAll(); }
            };
            Button.Loaded += (_, _) => Sync(false);
        }

        private bool Full => Button.IsLoaded && Tween.Level(Button) == ArcMotionLevel.Full;
        private bool Off => !Button.IsLoaded || Tween.Level(Button) == ArcMotionLevel.Off;

        public void Stop() { _driver.Stop(); _motion.Stop(); }
        public void Finish() => _driver.Finish();

        private double Measure(string text)
        {
            var block = WordBlock(text);
            block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return block.DesiredSize.Width;
        }

        private TextBlock WordBlock(string text)
        {
            var block = ArcText.Block(text, _fontSize, double.NaN, medium: true);
            block.Foreground = Button.Ink;
            return block;
        }

        private FrameworkElement Glyph() => Center(ArcText.Icon(Item.Icon, _icon, Button.Ink));
        private static FrameworkElement Center(FrameworkElement element) { element.HorizontalAlignment = HorizontalAlignment.Center; element.VerticalAlignment = VerticalAlignment.Center; return element; }

        private (string?, string?, string?) Roles(ArcChromeButton _)
        {
            bool expanded = _owner.ExpandedId == Item.Id;
            string ink = Item.IsDisabled ? expanded ? "TextSecondary" : "TextMuted" : !expanded ? "TextSecondary" : Item.Tone == ExpandingButtonGroupTone.Danger ? "ArcDanger" : "Foreground";
            return (null, null, ink);
        }

        public void Sync(bool animate)
        {
            bool expanded = _owner.ExpandedId == Item.Id, done = _owner._done == Item.Id && Item.DoneLabel is not null;
            Button.Refresh(animate);
            bool danger = Item.Tone == ExpandingButtonGroupTone.Danger;
            string? fill = done && !danger ? "ExpandDone" : !expanded ? null : Item.IsDisabled ? "SurfaceMuted" : danger ? "ExpandDanger" : "SurfaceMuted";
            _fill.To(fill, animate, Tween.Standard);
            UpdateWidth(expanded);
            UpdateLabel(expanded, animate);
            UpdateConfirmation(done, animate);
            UpdateBusy(_owner._busy == Item.Id);
        }

        private void UpdateWidth(bool expanded)
        {
            WidthTarget = expanded ? _start + _icon + _gap + _textWidth + _end : _height;
            if (!_settled || !Full) { _driver.Snap(_width, WidthTarget); _settled = Button.IsLoaded; return; }
            _driver.Animate(_width, WidthTarget, ArcSpring.Morph);
        }

        private void Apply()
        {
            double w = Math.Max(_height, _width.Position);
            Button.Width = w;
            _shape.Rect = new Rect(0, 0, w, _height);
            _shape.RadiusX = _shape.RadiusY = _height / 2;
        }

        // The label slides out from behind the icon as the width opens, and tucks back a little faster than it came.
        private void UpdateLabel(bool expanded, bool animate)
        {
            if (_expanded == expanded) { return; }
            _expanded = expanded;
            if (!animate || Off)
            {
                Tween.Set(_label, UIElement.OpacityProperty, expanded ? 1 : 0);
                Tween.Set(_labelMove, TranslateTransform.XProperty, !expanded && !Off ? -6 : 0);
                return;
            }
            if (!Full)
            {
                Tween.To(_label, UIElement.OpacityProperty, expanded ? 1 : 0, Tween.Instant, null);
                return;
            }
            if (expanded)
            {
                Tween.To(_label, UIElement.OpacityProperty, 1, Tween.Standard, ArcEasing.Enter, .04);
                Tween.To(_labelMove, TranslateTransform.XProperty, 0, Tween.Standard, ArcEasing.Enter, .04);
                ArcEffects.BlurIn(_motion, _label, 2, Tween.Standard, .04);
            }
            else
            {
                Tween.To(_label, UIElement.OpacityProperty, 0, Tween.Instant, ArcEasing.Standard);
                Tween.To(_labelMove, TranslateTransform.XProperty, -6, Tween.Instant, ArcEasing.Standard);
                ArcEffects.BlurOut(_motion, _label, 2, Tween.Instant);
            }
        }

        private void UpdateConfirmation(bool done, bool animate)
        {
            if (_showingDone == done) { return; }
            _showingDone = done;
            bool moving = animate && Full;
            var oldGlyph = _glyph;
            var oldWord = _word;
            _glyph = done ? DrawnCheck(moving) : Glyph();
            _word = WordBlock(done ? Item.DoneLabel! : Item.Label);
            _iconBox.Children.Add(_glyph);
            _label.Children.Add(_word);
            if (!moving)
            {
                if (oldGlyph is not null) { _iconBox.Children.Remove(oldGlyph); }
                if (oldWord is not null) { _label.Children.Remove(oldWord); }
                return;
            }
            // Scale rides the spring; opacity and blur tween so blur never overshoots below zero.
            var scale = new ScaleTransform(.55, .55);
            _glyph.RenderTransformOrigin = new Point(.5, .5);
            _glyph.RenderTransform = scale;
            _glyph.Opacity = 0;
            Tween.Spring(scale, ScaleTransform.ScaleXProperty, 1, Tween.Snappy);
            Tween.Spring(scale, ScaleTransform.ScaleYProperty, 1, Tween.Snappy);
            Tween.To(_glyph, UIElement.OpacityProperty, 1, Tween.Fast, ArcEasing.Enter);
            ArcEffects.BlurIn(_motion, _glyph, 2, Tween.Fast);
            if (oldGlyph is not null)
            {
                var out1 = new ScaleTransform();
                oldGlyph.RenderTransformOrigin = new Point(.5, .5);
                oldGlyph.RenderTransform = out1;
                Tween.To(out1, ScaleTransform.ScaleXProperty, .55, Tween.Fast, ArcEasing.Standard);
                Tween.To(out1, ScaleTransform.ScaleYProperty, .55, Tween.Fast, ArcEasing.Standard);
                Tween.To(oldGlyph, UIElement.OpacityProperty, 0, Tween.Fast, ArcEasing.Standard, done: () => _iconBox.Children.Remove(oldGlyph));
            }
            Tween.Present(_word, true, _fontSize * .5, 4, Tween.Standard, ArcEasing.Enter);
            if (oldWord is not null) { Tween.Present(oldWord, false, _fontSize * -.4, 2, Tween.Fast, ArcEasing.Standard, done: () => _label.Children.Remove(oldWord)); }
        }

        // The confirmation tick draws itself from its short stroke, in the success color.
        private FrameworkElement DrawnCheck(bool draw)
        {
            const double length = 22.63;
            var stroke = new SolidColorBrush(Tween.Resolve(Button, Item.Tone == ExpandingButtonGroupTone.Danger ? "ArcWpf.Brush.ArcDanger" : "ArcWpf.Brush.ArcSuccess"));
            var path = new Path { Data = Geometry.Parse("M4 12.5l5 5L20 6.5"), Stroke = stroke, StrokeThickness = 2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round };
            path.StrokeDashArray = [length / 2, length / 2];
            path.StrokeDashOffset = draw ? length / 2 : 0;
            if (draw)
            {
                path.Opacity = 0;
                Tween.To(path, Shape.StrokeDashOffsetProperty, 0, Tween.Standard, ArcEasing.Enter, .06);
                Tween.To(path, UIElement.OpacityProperty, 1, .05, null, .06);
            }
            return new Viewbox { Width = _icon, Height = _icon, Child = new Canvas { Width = 24, Height = 24, Children = { path } } };
        }

        private void UpdateBusy(bool busy)
        {
            if (_busy == busy) { return; }
            _busy = busy;
            Button.Cursor = busy ? Cursors.Wait : Item.IsDisabled ? Cursors.No : Cursors.Hand;
            if (!busy) { Tween.Set(_iconBox, UIElement.OpacityProperty, 1); return; }
            if (!Full) { Tween.Set(_iconBox, UIElement.OpacityProperty, .6); return; }
            _iconBox.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, .45, TimeSpan.FromSeconds(.55)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ArcEasing.InOut });
        }

        // The box never scales: a press answers with the icon.
        public void OnPressed(bool pressed)
        {
            if (Item.IsDisabled || !Full) { return; }
            double to = pressed ? .86 : 1, seconds = pressed ? .09 : .32;
            var ease = pressed ? ArcEasing.Standard : Tween.SpringToken;
            Tween.To(_iconPress, ScaleTransform.ScaleXProperty, to, seconds, ease);
            Tween.To(_iconPress, ScaleTransform.ScaleYProperty, to, seconds, ease);
        }
    }
}
