using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

public enum PaginationChangeOrigin { Programmatic, PageCount, PageButton, Previous, Next }

public sealed class PaginationPageChangedEventArgs(int oldPage, int newPage, PaginationChangeOrigin origin, RoutedEvent routedEvent)
    : RoutedPropertyChangedEventArgs<int>(oldPage, newPage, routedEvent)
{
    public PaginationChangeOrigin Origin { get; } = origin;
    protected override void InvokeEventHandler(Delegate genericHandler, object genericTarget) => ((EventHandler<PaginationPageChangedEventArgs>)genericHandler)(genericTarget, this);
}

[TemplatePart(Name = "PART_PagesHost", Type = typeof(Panel))]
[TemplatePart(Name = "PART_Mark", Type = typeof(Border))]
[TemplatePart(Name = "PART_ExitLayer", Type = typeof(Canvas))]
public class Pagination : Control
{
    public static readonly DependencyProperty PageProperty = DependencyProperty.Register(nameof(Page), typeof(int), typeof(Pagination),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, PageChangedCore,
            (d, value) => ((Pagination)d).PageCount == 0 ? 0 : Math.Clamp((int)value, 1, ((Pagination)d).PageCount)));
    public static readonly DependencyProperty PageCountProperty = DependencyProperty.Register(nameof(PageCount), typeof(int), typeof(Pagination),
        new FrameworkPropertyMetadata(0, CountChanged, (_, value) => Math.Max(0, (int)value)));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(Pagination), new PropertyMetadata("Pagination"));
    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(Pagination), new PropertyMetadata(null, CommandChanged));
    public static readonly RoutedEvent PageChangedEvent = EventManager.RegisterRoutedEvent(nameof(PageChanged), RoutingStrategy.Bubble, typeof(EventHandler<PaginationPageChangedEventArgs>), typeof(Pagination));
    public int Page { get => (int)GetValue(PageProperty); set => SetValue(PageProperty, value); }
    public int PageCount { get => (int)GetValue(PageCountProperty); set => SetValue(PageCountProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public event EventHandler<PaginationPageChangedEventArgs> PageChanged { add => AddHandler(PageChangedEvent, value); remove => RemoveHandler(PageChangedEvent, value); }

    private Panel? _host;
    private Canvas? _exitLayer;
    private Border? _mark;
    private readonly TranslateTransform _markPosition = new();
    private readonly OwnedMotion _motion = new();
    private readonly Dictionary<int, PaginationButton> _pages = new();
    private readonly List<PageExit> _exits = new();
    private PaginationButton? _previous, _next;
    private PaginationChangeOrigin _origin;
    private int _start = 1;
    private bool _ready, _updating;
    internal bool IsAnimating => _motion.IsRunning || Buttons.Any(b => b.IsAnimating) || _exits.Any(b => b.IsAnimating);
    private IEnumerable<PaginationButton> Buttons => _pages.Values.Concat(new[] { _previous, _next }.OfType<PaginationButton>());
    private bool Full => IsLoaded && IsVisible && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;

    static Pagination() { DefaultStyleKeyProperty.OverrideMetadata(typeof(Pagination), new FrameworkPropertyMetadata(typeof(Pagination))); }
    public Pagination()
    {
        CoerceValue(PageProperty);
        Loaded += (_, _) => Refresh(false);
        Unloaded += (_, _) => Settle();
        IsVisibleChanged += (_, _) => { if (!IsVisible) Settle(); };
        SizeChanged += (_, _) => { if (!_updating) { Settle(); PlaceMark(false); } };
    }
    private static void PageChangedCore(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var owner = (Pagination)d;
        owner.Refresh(true);
        owner.RaiseEvent(new PaginationPageChangedEventArgs((int)e.OldValue, (int)e.NewValue, owner._origin, PageChangedEvent));
    }
    private static void CountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var owner = (Pagination)d;
        var old = owner._origin; owner._origin = PaginationChangeOrigin.PageCount;
        try { owner.CoerceValue(PageProperty); owner.Refresh(false); }
        finally { owner._origin = old; }
    }
    private static void CommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var owner = (Pagination)d;
        if (e.OldValue is ICommand old) CanExecuteChangedEventManager.RemoveHandler(old, owner.CommandAvailabilityChanged);
        if (e.NewValue is ICommand next) CanExecuteChangedEventManager.AddHandler(next, owner.CommandAvailabilityChanged);
        owner.UpdateButtons();
    }
    private void CommandAvailabilityChanged(object? sender, EventArgs e) => UpdateButtons();
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ArcMotion.LevelProperty && _motion is not null) Settle();
    }
    internal void Request(int target, PaginationChangeOrigin origin)
    {
        if (!IsEnabled || PageCount == 0 || target < 1 || target > PageCount || target == Page || Command?.CanExecute(target) == false) return;
        var command = Command;
        var old = _origin; _origin = origin;
        try { SetCurrentValue(PageProperty, target); }
        finally { _origin = old; }
        if (Page == target && command?.CanExecute(target) == true) command.Execute(target);
    }
    public override void OnApplyTemplate()
    {
        ReleaseTemplate(); base.OnApplyTemplate();
        _host = GetTemplateChild("PART_PagesHost") as Panel;
        _exitLayer = GetTemplateChild("PART_ExitLayer") as Canvas;
        _mark = GetTemplateChild("PART_Mark") as Border;
        if (_mark is not null) _mark.RenderTransform = _markPosition;
        Refresh(false);
    }
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate) { ReleaseTemplate(); base.OnTemplateChanged(oldTemplate, newTemplate); }
    private void ReleaseTemplate()
    {
        _motion?.Stop();
        foreach (var button in Buttons.ToArray()) button.Stop();
        ClearExits(); _host?.Children.Clear(); _pages.Clear();
        _previous = _next = null; _host = null; _exitLayer = null; _mark = null; _ready = false;
    }
    private void ClearExits()
    {
        foreach (var button in _exits) button.Stop();
        _exits.Clear(); _exitLayer?.Children.Clear();
    }
    private PaginationButton CreateButton(int number, PaginationChangeOrigin origin) => new(this, number, origin);
    private void Refresh(bool animate)
    {
        if (_host is null || _updating) return;
        _updating = true;
        try
        {
            bool moving = animate && Full && _ready;
            ClearExits();
            var oldPositions = _pages.ToDictionary(pair => pair.Key, pair => pair.Value.TranslatePoint(new Point(), _host));
            bool pageHadFocus = _pages.Values.Any(b => b.IsKeyboardFocusWithin);
            int start = PageCount == 0 ? 1 : (int)Math.Clamp((long)Page - 2, 1, Math.Max(1L, (long)PageCount - 4));
            int shift = start - _start;
            var visible = Enumerable.Range(start, Math.Min(5, PageCount)).ToHashSet();
            foreach (var number in _pages.Keys.Where(n => !visible.Contains(n)).ToArray())
            {
                var button = _pages[number]; _pages.Remove(number); _host.Children.Remove(button);
                if (moving && _exitLayer is not null)
                {
                    var position = oldPositions[number]; button.Stop();
                    // A noninteractive visual copy avoids Button unload/template notifications cancelling exit clocks.
                    var ghost = new PageExit(button);
                    _exitLayer.Children.Add(ghost); Canvas.SetLeft(ghost, position.X); Canvas.SetTop(ghost, position.Y);
                    _exits.Add(ghost);
                    ghost.Exit(-shift * (button.ActualWidth + 4), () => { _exitLayer?.Children.Remove(ghost); _exits.Remove(ghost); });
                }
                else button.Stop();
            }
            if (PageCount == 0)
            {
                foreach (var button in Buttons) button.Stop();
                _host.Children.Clear(); _previous = _next = null; _ready = false; _motion.Stop();
                if (_mark is not null) _mark.Visibility = Visibility.Collapsed;
                _start = start; return;
            }
            _previous ??= CreateButton(0, PaginationChangeOrigin.Previous);
            _next ??= CreateButton(0, PaginationChangeOrigin.Next);
            // Keep surviving buttons attached so pointer capture and keyboard focus survive a window shift.
            if (!_host.Children.Contains(_previous)) _host.Children.Insert(0, _previous);
            int index = 1;
            foreach (int number in visible.Order())
            {
                if (!_pages.TryGetValue(number, out var button)) _pages[number] = button = CreateButton(number, PaginationChangeOrigin.PageButton);
                if (_host.Children.IndexOf(button) != index) { _host.Children.Remove(button); _host.Children.Insert(index, button); }
                index++;
            }
            if (!_host.Children.Contains(_next)) _host.Children.Add(_next);
            UpdateButtons(); _host.UpdateLayout();
            foreach (var (number, button) in _pages)
            {
                var final = button.LayoutPosition(_host);
                if (moving && oldPositions.TryGetValue(number, out var oldPosition)) button.MoveFrom(oldPosition.X - final.X, oldPosition.Y - final.Y);
                else if (moving && shift != 0) button.Enter(shift * (button.ActualWidth + 4));
                else button.Settle();
            }
            PlaceMark(moving);
            if (pageHadFocus && !_pages.Values.Any(b => b.IsKeyboardFocusWithin) && _pages.TryGetValue(Page, out var current)) current.Focus();
            _start = start;
        }
        finally { _updating = false; }
    }
    private void UpdateButtons()
    {
        foreach (var button in Buttons)
        {
            int target = button.Origin == PaginationChangeOrigin.Previous ? Page - 1 : button.Origin == PaginationChangeOrigin.Next ? (Page == int.MaxValue ? Page : Page + 1) : button.Number;
            button.IsEnabled = PageCount > 0 && target >= 1 && target <= PageCount &&
                !(button.Origin == PaginationChangeOrigin.Next && Page >= PageCount) && Command?.CanExecute(target) != false;
            button.SetSelected(button.Origin == PaginationChangeOrigin.PageButton && button.Number == Page);
        }
    }
    private void PlaceMark(bool animate)
    {
        if (_host is null || _mark is null || !_pages.TryGetValue(Page, out var button)) return;
        var position = button.LayoutPosition(_host);
        _mark.Visibility = Visibility.Visible;
        _mark.Width = button.ActualWidth; _mark.Height = button.ActualHeight;
        _motion.Double(_markPosition, TranslateTransform.XProperty, position.X, .42, animate, spring: true);
        _motion.Double(_markPosition, TranslateTransform.YProperty, position.Y, .42, animate, spring: true);
        _ready = true;
    }
    private void Settle()
    {
        _motion.Stop(); ClearExits();
        foreach (var button in Buttons) button.Settle();
        PlaceMark(false);
    }
    private sealed class PageExit : Border
    {
        private readonly OwnedMotion _motion = new();
        private readonly TranslateTransform _position = new();
        internal bool IsAnimating => _motion.IsRunning;
        internal PageExit(PaginationButton button)
        {
            Width = button.ActualWidth; Height = button.ActualHeight;
            Opacity = button.Opacity; RenderTransform = _position; IsHitTestVisible = false;
            Child = new TextBlock { Text = button.Content?.ToString(), FontFamily = button.FontFamily,
                FontSize = button.FontSize, Foreground = button.DisplayForeground.CloneCurrentValue(),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        }
        internal void Exit(double x, Action completed)
        {
            _motion.Double(_position, TranslateTransform.XProperty, x, .4, true, spring: true, completed: completed);
            _motion.Double(this, OpacityProperty, 0, .12, true);
        }
        internal void Stop() => _motion.Stop();
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new PaginationPeer(this);
    private sealed class PaginationPeer(Pagination owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(Pagination);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetNameCore() => string.IsNullOrEmpty(base.GetNameCore()) ? owner.Label : base.GetNameCore();
    }
}
