using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public enum DrawerSide { Left, Right, Top, Bottom }
public enum DrawerCloseReason { Programmatic, Escape, Overlay, CloseButton, HostUnavailable, Replaced, TemplateChanged }

public sealed class DrawerClosingEventArgs(DrawerCloseReason reason) : CancelEventArgs
{
    public DrawerCloseReason Reason { get; } = reason;
}

[TemplatePart(Name = "PART_Surface", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Overlay", Type = typeof(Border))]
[TemplatePart(Name = "PART_CloseButton", Type = typeof(ButtonBase))]
public class Drawer : ContentControl
{
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(Drawer), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OpenChanged));
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(Drawer), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(Drawer), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty SideProperty = DependencyProperty.Register(nameof(Side), typeof(DrawerSide), typeof(Drawer), new FrameworkPropertyMetadata(DrawerSide.Right, SideChanged));
    public static readonly DependencyProperty CloseOnEscapeProperty = DependencyProperty.Register(nameof(CloseOnEscape), typeof(bool), typeof(Drawer), new FrameworkPropertyMetadata(true));
    public static readonly DependencyProperty CloseOnOverlayClickProperty = DependencyProperty.Register(nameof(CloseOnOverlayClick), typeof(bool), typeof(Drawer), new FrameworkPropertyMetadata(true));
    public static readonly DependencyProperty InitialFocusElementProperty = DependencyProperty.Register(nameof(InitialFocusElement), typeof(IInputElement), typeof(Drawer), new FrameworkPropertyMetadata(null));
    private static readonly DependencyPropertyKey IsPresentedPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsPresented), typeof(bool), typeof(Drawer), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsPresentedProperty = IsPresentedPropertyKey.DependencyProperty;

    public static readonly RoutedUICommand CloseCommand = new("Close drawer", nameof(CloseCommand), typeof(Drawer));
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public DrawerSide Side { get => (DrawerSide)GetValue(SideProperty); set => SetValue(SideProperty, value); }
    public bool CloseOnEscape { get => (bool)GetValue(CloseOnEscapeProperty); set => SetValue(CloseOnEscapeProperty, value); }
    public bool CloseOnOverlayClick { get => (bool)GetValue(CloseOnOverlayClickProperty); set => SetValue(CloseOnOverlayClickProperty, value); }
    public IInputElement? InitialFocusElement { get => (IInputElement?)GetValue(InitialFocusElementProperty); set => SetValue(InitialFocusElementProperty, value); }
    public bool IsPresented => (bool)GetValue(IsPresentedProperty);

    public event EventHandler<DrawerClosingEventArgs>? Closing;
    public event EventHandler? Opened;
    public event EventHandler? Closed;

    private DrawerHost? _host;
    private FrameworkElement? _surface;
    private Border? _overlay;
    private ButtonBase? _close;
    private TranslateTransform? _move;
    private readonly OwnedMotion _motion = new();
    private bool _setting;
    private bool _requesting;
    private int _version;
    private IInputElement? _restoreFocus;
    private DispatcherOperation? _focus;
    internal bool IsMotionRunning => _motion.IsRunning;

    static Drawer() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Drawer), new FrameworkPropertyMetadata(typeof(Drawer)));

    public Drawer()
    {
        CommandBindings.Add(new CommandBinding(CloseCommand, (_, e) => { Close(); e.Handled = true; }, (_, e) => { e.CanExecute = IsOpen; e.Handled = true; }));
        Loaded += (_, _) => Synchronize();
        Unloaded += (_, _) => RequestClose(DrawerCloseReason.HostUnavailable, true);
    }

    public void Show(FrameworkElement origin)
    {
        var host = DrawerHost.Find(origin) ?? throw new InvalidOperationException("Place a DrawerHost around the window content before showing a Drawer.");
        if (_host is not null && !ReferenceEquals(_host, host)) throw new InvalidOperationException("A Drawer belongs to one DrawerHost at a time.");
        host.SetCurrentValue(DrawerHost.DrawerProperty, this);
        SetCurrentValue(IsOpenProperty, true);
        Synchronize();
    }

    public void Close() => RequestClose(DrawerCloseReason.Programmatic);
    internal void Attach(DrawerHost? host) { _host = host; if (host is not null) Synchronize(); }

    private static void OpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var drawer = (Drawer)d;
        if (drawer._setting) return;
        if ((bool)e.NewValue) drawer.Synchronize(); else drawer.RequestClose(DrawerCloseReason.Programmatic);
    }

    private static void SideChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var drawer = (Drawer)d;
        if (drawer.IsPresented) drawer.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => drawer.Transition(drawer.IsOpen, false)));
    }

    private void SetOpen(bool value)
    {
        _setting = true;
        try { SetCurrentValue(IsOpenProperty, value); }
        finally { _setting = false; }
    }

    internal void Synchronize()
    {
        if (!IsOpen || _host is null) return;
        ApplyTemplate();
        if (_surface is null) return;
        if (IsPresented && _surface.IsHitTestVisible) return;
        bool fresh = !IsPresented;
        if (fresh) _restoreFocus = Keyboard.FocusedElement;
        SetValue(IsPresentedPropertyKey, true);
        _host.UpdateModal();
        if (fresh)
        {
            _surface.Opacity = 0;
            if (_overlay is not null) _overlay.Opacity = 0;
            if (_move is not null) SetOffset(StartOffset());
        }
        Transition(true, _host.IsLoaded && _host.IsVisible);
        QueueFocus();
        if (fresh) Opened?.Invoke(this, EventArgs.Empty);
    }

    internal void RequestClose(DrawerCloseReason reason, bool force = false)
    {
        if (_requesting) { if (force) { SetOpen(false); FinishClose(false); } return; }
        if (!IsOpen && !IsPresented) return;
        if (!force && !IsOpen && IsPresented && _surface?.IsHitTestVisible == false) return;
        _requesting = true;
        try
        {
            if (!force)
            {
                var args = new DrawerClosingEventArgs(reason);
                Closing?.Invoke(this, args);
                if (args.Cancel) { SetOpen(true); return; }
            }
            SetOpen(false);
            if (force || !IsPresented) { FinishClose(!force); return; }
            Transition(false, IsLoaded && IsVisible);
        }
        finally { _requesting = false; }
    }

    internal void Transition(bool open, bool animate)
    {
        int token = ++_version;
        if (_surface is null) { if (!open) FinishClose(true); return; }
        _surface.IsHitTestVisible = open;
        var level = ArcMotion.GetLevel(this);
        bool motion = animate && level != ArcMotionLevel.Off;
        bool full = level == ArcMotionLevel.Full;
        double fade = full ? .16 : .12;
        if (_move is not null)
        {
            var distance = StartOffset();
            bool horizontal = Side is DrawerSide.Left or DrawerSide.Right;
            var property = horizontal ? TranslateTransform.XProperty : TranslateTransform.YProperty;
            if (horizontal) _move.Y = 0; else _move.X = 0;
            _motion.Double(_move, property, open ? 0 : distance, open ? .4 : .16, motion && full, spring: open);
        }
        if (_overlay is not null) _motion.Double(_overlay, OpacityProperty, open ? 1 : 0, full && open ? .24 : fade, motion, enter: open);
        _motion.Double(_surface, OpacityProperty, open ? 1 : 0, fade, motion, enter: open, completed: () => { if (!open && token == _version && !IsOpen) FinishClose(true); });
    }

    private double StartOffset()
    {
        double size = Side is DrawerSide.Left or DrawerSide.Right ? _surface?.ActualWidth ?? 0 : _surface?.ActualHeight ?? 0;
        if (size <= 0) size = Side is DrawerSide.Left or DrawerSide.Right ? 480 : 512;
        return (Side is DrawerSide.Left or DrawerSide.Top) ? -size : size;
    }

    private void SetOffset(double value)
    {
        if (_move is null) return;
        if (Side is DrawerSide.Left or DrawerSide.Right) _move.X = value; else _move.Y = value;
    }

    private void FinishClose(bool restore)
    {
        ++_version;
        _motion.Stop();
        _focus?.Abort(); _focus = null;
        bool was = IsPresented;
        SetValue(IsPresentedPropertyKey, false);
        _host?.UpdateModal();
        if (_surface is not null) { _surface.Opacity = 0; _surface.IsHitTestVisible = false; SetOffset(0); }
        if (_overlay is not null) _overlay.Opacity = 0;
        var target = _restoreFocus; _restoreFocus = null;
        if (restore && target is UIElement { IsVisible: true, IsEnabled: true } element) element.Focus();
        if (was) Closed?.Invoke(this, EventArgs.Empty);
    }

    private void QueueFocus()
    {
        _focus?.Abort();
        _focus = Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => { _focus = null; if (IsOpen && IsPresented) FocusInitial(); }));
    }

    internal void FocusInitial()
    {
        if (InitialFocusElement is DependencyObject candidate && DrawerHost.Contains(this, candidate) && InitialFocusElement.Focus()) return;
        if (FocusTargets().FirstOrDefault() is { } first && first.Focus()) return;
        _surface?.Focus();
    }

    private List<UIElement> FocusTargets()
    {
        var list = new List<UIElement>();
        void Walk(DependencyObject node)
        {
            if (node is UIElement element && (!element.IsVisible || !element.IsEnabled)) return;
            if (node is UIElement focus && !ReferenceEquals(node, _surface) && focus.Focusable && KeyboardNavigation.GetIsTabStop(focus)) list.Add(focus);
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) Walk(VisualTreeHelper.GetChild(node, i));
        }
        if (_surface is not null) Walk(_surface);
        return list.OrderBy(KeyboardNavigation.GetTabIndex).ToList();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Handled) return;
        if (e.Key != Key.Tab || _surface is null || !IsOpen) return;
        var targets = FocusTargets();
        if (targets.Count == 0) { e.Handled = true; _surface.Focus(); return; }
        var current = Keyboard.FocusedElement as DependencyObject;
        int index = targets.FindIndex(item => DrawerHost.Contains(item, current));
        bool backwards = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        if (index < 0) { e.Handled = true; targets[backwards ? ^1 : 0].Focus(); return; }
        int next = backwards ? index - 1 : index + 1;
        if (next < 0) next = targets.Count - 1;
        if (next >= targets.Count) next = 0;
        e.Handled = true; targets[next].Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && IsOpen && CloseOnEscape)
        {
            RequestClose(DrawerCloseReason.Escape);
            e.Handled = true;
        }
    }

    private void Outside(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && IsOpen && CloseOnOverlayClick) { RequestClose(DrawerCloseReason.Overlay); e.Handled = true; }
    }

    private void CloseClicked(object sender, RoutedEventArgs e) { RequestClose(DrawerCloseReason.CloseButton); e.Handled = true; }

    public override void OnApplyTemplate()
    {
        ReleaseParts(); base.OnApplyTemplate();
        _surface = GetTemplateChild("PART_Surface") as FrameworkElement;
        _overlay = GetTemplateChild("PART_Overlay") as Border;
        _close = GetTemplateChild("PART_CloseButton") as ButtonBase;
        if (_overlay is not null) _overlay.MouseLeftButtonDown += Outside;
        if (_close is not null) _close.Click += CloseClicked;
        if (_surface is not null)
        {
            _move = new TranslateTransform();
            _surface.RenderTransform = _move;
            _surface.RenderTransformOrigin = new Point(.5, .5);
        }
        if (IsOpen && _host is not null) Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(Synchronize));
    }

    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    {
        if (IsPresented) RequestClose(DrawerCloseReason.TemplateChanged, true);
        ReleaseParts(); base.OnTemplateChanged(oldTemplate, newTemplate);
    }

    private void ReleaseParts()
    {
        _motion.Stop();
        if (_overlay is not null) _overlay.MouseLeftButtonDown -= Outside;
        if (_close is not null) _close.Click -= CloseClicked;
        _surface = null; _overlay = null; _close = null; _move = null;
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ArcMotion.LevelProperty && IsPresented) Transition(IsOpen, false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new DrawerPeer(this);

    private sealed class DrawerPeer(Drawer owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(Drawer);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override bool IsDialogCore() => true;
        protected override string GetNameCore() => string.IsNullOrEmpty(AutomationProperties.GetName(owner)) ? owner.Title : base.GetNameCore();
        protected override string GetHelpTextCore() => string.IsNullOrEmpty(AutomationProperties.GetHelpText(owner)) ? owner.Description : base.GetHelpTextCore();
    }
}

[TemplatePart(Name = "PART_Background", Type = typeof(ContentPresenter))]
[TemplatePart(Name = "PART_Drawer", Type = typeof(ContentPresenter))]
public class DrawerHost : ContentControl
{
    public static readonly DependencyProperty DrawerProperty = DependencyProperty.Register(nameof(Drawer), typeof(Drawer), typeof(DrawerHost), new FrameworkPropertyMetadata(null, Changed));
    public Drawer? Drawer { get => (Drawer?)GetValue(DrawerProperty); set => SetValue(DrawerProperty, value); }
    private ContentPresenter? _background;
    private bool _redirecting;

    static DrawerHost() => DefaultStyleKeyProperty.OverrideMetadata(typeof(DrawerHost), new FrameworkPropertyMetadata(typeof(DrawerHost)));

    public DrawerHost()
    {
        Loaded += (_, _) => { Drawer?.Attach(this); Drawer?.Synchronize(); };
        Unloaded += (_, _) => Drawer?.RequestClose(DrawerCloseReason.HostUnavailable, true);
        PreviewGotKeyboardFocus += GuardFocus;
    }

    protected override IEnumerator LogicalChildren
    {
        get
        {
            var children = new List<object>(); var original = base.LogicalChildren;
            while (original.MoveNext()) children.Add(original.Current);
            if (Drawer is not null && !children.Contains(Drawer)) children.Add(Drawer);
            return children.GetEnumerator();
        }
    }

    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var host = (DrawerHost)d;
        if (e.OldValue is Drawer old) { old.RequestClose(DrawerCloseReason.Replaced, true); old.Attach(null); host.RemoveLogicalChild(old); }
        if (e.NewValue is Drawer next) { host.AddLogicalChild(next); next.Attach(host); }
        host.UpdateModal();
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate(); _background = GetTemplateChild("PART_Background") as ContentPresenter; UpdateModal(); Drawer?.Synchronize();
    }

    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    {
        if (Drawer?.IsPresented == true) Drawer.RequestClose(DrawerCloseReason.TemplateChanged, true);
        base.OnTemplateChanged(oldTemplate, newTemplate);
    }

    internal void UpdateModal()
    {
        if (_background is null) return;
        bool modal = Drawer?.IsPresented == true;
        _background.IsEnabled = !modal; _background.IsHitTestVisible = !modal;
        _background.Effect = modal ? new System.Windows.Media.Effects.BlurEffect { Radius = 7 } : null;
    }

    private void GuardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_redirecting || Drawer is not { IsPresented: true } drawer || Contains(drawer, e.NewFocus as DependencyObject)) return;
        e.Handled = true; _redirecting = true; try { drawer.FocusInitial(); } finally { _redirecting = false; }
    }

    internal static bool Contains(DependencyObject ancestor, DependencyObject? child)
    {
        for (var node = child; node is not null; node = GetParentNode(node)) if (ReferenceEquals(node, ancestor)) return true;
        return false;
    }

    internal static DependencyObject? GetParentNode(DependencyObject node) => node is Visual ? VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);

    internal static DrawerHost? Find(DependencyObject origin)
    {
        for (DependencyObject? node = origin; node is not null; node = GetParentNode(node)) if (node is DrawerHost host) return host;
        return null;
    }
}
