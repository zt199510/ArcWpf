using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace ArcWpf.Controls;

/// <summary>
/// A top-level window drawn in Arc's surfaces: its own title bar with slots for content and actions, and caption
/// buttons in Arc's chrome. WindowChrome keeps the native frame, so dragging, snapping, resizing, the system menu
/// and Windows 11 snap layouts behave as they do on any Windows window.
/// </summary>
[TemplatePart(Name = "PART_Root", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_TitleBar", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_CaptionButtons", Type = typeof(Panel))]
public class ArcWindow : Window
{
    public static readonly DependencyProperty TitleBarHeightProperty = DependencyProperty.Register(nameof(TitleBarHeight), typeof(double), typeof(ArcWindow), new FrameworkPropertyMetadata(40d, (d, _) => ((ArcWindow)d).UpdateChrome()), height => height is double value && double.IsFinite(value) && value >= 0);
    public static readonly DependencyProperty TitleBarContentProperty = DependencyProperty.Register(nameof(TitleBarContent), typeof(object), typeof(ArcWindow));
    public static readonly DependencyProperty TitleBarActionsProperty = DependencyProperty.Register(nameof(TitleBarActions), typeof(object), typeof(ArcWindow));
    public static readonly DependencyProperty ShowTitleProperty = DependencyProperty.Register(nameof(ShowTitle), typeof(bool), typeof(ArcWindow), new FrameworkPropertyMetadata(true));

    private const int WM_NCHITTEST = 0x0084, WM_NCMOUSEMOVE = 0x00A0, WM_NCLBUTTONDOWN = 0x00A1, WM_NCLBUTTONUP = 0x00A2, WM_NCLBUTTONDBLCLK = 0x00A3, WM_NCMOUSELEAVE = 0x02A2, WM_MOUSEMOVE = 0x0200;
    internal const int HTMAXBUTTON = 9;
    private FrameworkElement? _root;
    private Panel? _captions;
    private CaptionButton? _minimize, _maximize, _close;
    private HwndSource? _source;

    /// <summary>Height of the title bar, which is also the drag area WindowChrome hands to the system.</summary>
    public double TitleBarHeight { get => (double)GetValue(TitleBarHeightProperty); set => SetValue(TitleBarHeightProperty, value); }
    /// <summary>Shown after the title: tabs, a breadcrumb, a search field. It takes input; its empty space still drags the window.</summary>
    public object? TitleBarContent { get => GetValue(TitleBarContentProperty); set => SetValue(TitleBarContentProperty, value); }
    /// <summary>Shown just before the caption buttons, for a few window-level actions.</summary>
    public object? TitleBarActions { get => GetValue(TitleBarActionsProperty); set => SetValue(TitleBarActionsProperty, value); }
    public bool ShowTitle { get => (bool)GetValue(ShowTitleProperty); set => SetValue(ShowTitleProperty, value); }

    internal System.Windows.Controls.Button? MinimizeButton => _minimize;
    internal System.Windows.Controls.Button? MaximizeButton => _maximize;
    internal System.Windows.Controls.Button? CloseButton => _close;

    static ArcWindow() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ArcWindow), new FrameworkPropertyMetadata(typeof(ArcWindow)));

    public ArcWindow()
    {
        CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, (_, _) => SystemCommands.MinimizeWindow(this), (_, e) => e.CanExecute = ResizeMode != ResizeMode.NoResize));
        CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand, (_, _) => SystemCommands.MaximizeWindow(this), (_, e) => e.CanExecute = CanResize));
        CommandBindings.Add(new CommandBinding(SystemCommands.RestoreWindowCommand, (_, _) => SystemCommands.RestoreWindow(this), (_, e) => e.CanExecute = CanResize));
        CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (_, _) => SystemCommands.CloseWindow(this)));
        UpdateChrome();
        ThemeWatch.Add(this, ApplyFrameColors);
    }

    private bool CanResize => ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _root = GetTemplateChild("PART_Root") as FrameworkElement;
        _captions = GetTemplateChild("PART_CaptionButtons") as Panel;
        BuildCaptions();
        UpdateState();
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ResizeModeProperty) { UpdateChrome(); BuildCaptions(); }
        if (e.Property == WindowStateProperty) UpdateState();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _source?.AddHook(Hook);
        ApplyFrameColors();
    }

    // Caption ink dims with the window, as the title does.
    protected override void OnActivated(EventArgs e) { base.OnActivated(e); RefreshCaptions(); }
    protected override void OnDeactivated(EventArgs e) { base.OnDeactivated(e); RefreshCaptions(); }
    private void RefreshCaptions() { foreach (var button in (CaptionButton?[])[_minimize, _maximize, _close]) button?.Refresh(true); }

    protected override void OnClosed(EventArgs e)
    {
        _source?.RemoveHook(Hook);
        _source = null;
        base.OnClosed(e);
    }

    private void UpdateChrome()
    {
        // GlassFrameThickness -1 lets DWM keep the shadow and, on Windows 11, the rounded corners; the template paints over the glass.
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = TitleBarHeight,
            ResizeBorderThickness = CanResize ? SystemParameters.WindowResizeBorderThickness : new Thickness(0),
            GlassFrameThickness = new Thickness(-1),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false,
            NonClientFrameEdges = NonClientFrameEdges.None,
        });
    }

    private void BuildCaptions()
    {
        if (_captions is null) return;
        _captions.Children.Clear();
        _minimize = _maximize = null;
        if (ResizeMode != ResizeMode.NoResize)
        {
            _minimize = Caption("Minimize", Lucide.Minus, SystemCommands.MinimizeWindowCommand);
            // CanMinimize keeps a disabled maximize button, as a native frame does.
            _maximize = Caption("Maximize", Lucide.Square, SystemCommands.MaximizeWindowCommand);
            _maximize.IsEnabled = CanResize;
        }
        _close = Caption("Close", Lucide.X, SystemCommands.CloseWindowCommand, close: true);
        UpdateState();
    }

    private CaptionButton Caption(string name, string glyph, RoutedCommand command, bool close = false)
    {
        // The target first: setting the command queries it at once, and without a target the query never reaches the window.
        var button = new CaptionButton(close) { CommandTarget = this, Command = command, Content = Lucide.Glyph(glyph, 16, 1.5) };
        AutomationProperties.SetName(button, name);
        _captions!.Children.Add(button);
        return button;
    }

    private void UpdateState()
    {
        bool maximized = WindowState == WindowState.Maximized;
        // A maximized chrome window spills past the monitor by its resize border; the root pulls the content back in.
        if (_root is not null) _root.Margin = maximized ? SystemParameters.WindowResizeBorderThickness : new Thickness(0);
        if (_maximize is null) return;
        _maximize.Command = maximized ? SystemCommands.RestoreWindowCommand : SystemCommands.MaximizeWindowCommand;
        _maximize.Content = Lucide.Glyph(maximized ? Lucide.Copy : Lucide.Square, 16, 1.5);
        AutomationProperties.SetName(_maximize, maximized ? "Restore" : "Maximize");
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) => NonClient(msg, wParam, lParam, ref handled);

    /// <summary>
    /// Windows 11 offers snap layouts only over a frame button the system can see, so the maximize button answers the
    /// hit test as HTMAXBUTTON and takes its hover and click from the non-client messages that follow.
    /// </summary>
    internal IntPtr NonClient(int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_NCHITTEST:
                bool over = OverMaximize(lParam);
                _maximize?.SetHot(over);
                if (over) { handled = true; return HTMAXBUTTON; }
                break;
            case WM_NCMOUSEMOVE when _maximize is not null:
                _maximize.SetHot(wParam.ToInt32() == HTMAXBUTTON);
                break;
            case WM_NCLBUTTONDOWN or WM_NCLBUTTONDBLCLK when wParam.ToInt32() == HTMAXBUTTON && _maximize is { IsEnabled: true }:
                // Left to the system, the press would track and paint a classic frame button.
                _maximize.SetDown(true);
                handled = true;
                break;
            case WM_NCLBUTTONUP when wParam.ToInt32() == HTMAXBUTTON && _maximize is { IsEnabled: true }:
                bool pressed = _maximize.IsDown;
                _maximize.SetDown(false);
                handled = true;
                if (pressed && _maximize.Command is RoutedCommand command && command.CanExecute(null, this)) command.Execute(null, this);
                break;
            case WM_NCMOUSELEAVE or WM_MOUSEMOVE when _maximize is not null:
                _maximize.SetHot(false); _maximize.SetDown(false);
                break;
        }
        return IntPtr.Zero;
    }

    private bool OverMaximize(IntPtr lParam)
    {
        if (_maximize is not { IsVisible: true, IsEnabled: true } button || PresentationSource.FromVisual(button) is null) return false;
        long value = lParam.ToInt64();
        var screen = new Point((short)(value & 0xFFFF), (short)(value >> 16 & 0xFFFF));
        var local = button.PointFromScreen(screen);
        return local.X >= 0 && local.Y >= 0 && local.X < button.ActualWidth && local.Y < button.ActualHeight;
    }

    /// <summary>Lets DWM draw the frame to match: dark frame in the dark theme, rounded corners, the theme's border color.</summary>
    private void ApplyFrameColors()
    {
        if (_source is null) return;
        var hwnd = _source.Handle;
        var background = TryFindResource("ArcWpf.Brush.Background") is SolidColorBrush fill ? fill.Color : Colors.White;
        int dark = (background.R * .2126 + background.G * .7152 + background.B * .0722) < 128 ? 1 : 0;
        Dwm.Set(hwnd, Dwm.UseImmersiveDarkMode, dark);
        Dwm.Set(hwnd, Dwm.WindowCornerPreference, Dwm.Round);
        if (TryFindResource("ArcWpf.Brush.Border") is SolidColorBrush border) Dwm.Set(hwnd, Dwm.BorderColor, border.Color.R | border.Color.G << 8 | border.Color.B << 16);
    }

    /// <summary>A caption button: square, flush with the title bar, with Arc's hover fill; close turns danger red.</summary>
    internal sealed class CaptionButton : ArcChromeButton
    {
        private bool _hot;
        public bool IsDown { get; private set; }
        public bool IsHot => _hot || IsMouseOver;

        public CaptionButton(bool close) : base(0)
        {
            Width = 46;
            Focusable = false;
            IsTabStop = false;
            Cursor = Cursors.Arrow;
            VerticalAlignment = VerticalAlignment.Stretch;
            WindowChrome.SetIsHitTestVisibleInChrome(this, true);
            Roles = button =>
            {
                var caption = (CaptionButton)button;
                bool down = caption.IsPressed || caption.IsDown, hot = caption.IsHot;
                if (!caption.IsEnabled) return (null, null, "TextMuted");
                if (close && (hot || down)) return (down ? "WindowClosePressed" : "WindowCloseHover", null, "WindowCloseInk");
                return (down ? "WindowCaptionPressed" : hot ? "SurfaceMuted" : null, null, IsActiveWindow(caption) ? "Foreground" : "TextMuted");
            };
        }

        private static bool IsActiveWindow(DependencyObject button) => GetWindow(button)?.IsActive ?? true;

        public void SetHot(bool hot) { if (_hot == hot) return; _hot = hot; Refresh(true); }
        public void SetDown(bool down) { if (IsDown == down) return; IsDown = down; Refresh(true); }
    }

    private static class Dwm
    {
        public const int UseImmersiveDarkMode = 20, WindowCornerPreference = 33, BorderColor = 34, Round = 2;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        // Older Windows and other hosts reject attributes they don't know; the frame then stays the system's.
        public static void Set(IntPtr hwnd, int attribute, int value)
        {
            try { DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int)); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
    }
}
