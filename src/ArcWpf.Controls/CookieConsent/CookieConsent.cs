using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public sealed class CookieCategory
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Required { get; set; }
    public bool DefaultEnabled { get; set; }
    /// <summary>The switch value being edited in the panel. It does not touch the saved consent until saved.</summary>
    public bool DraftEnabled { get; set; }
}

public enum CookieConsentAction { AcceptAll, RejectAll, Save, Reset }
public enum CookieConsentPosition { Bottom, BottomLeft, BottomRight }
public sealed class CookieConsentChangedEventArgs(CookieConsentAction action, IReadOnlyDictionary<string, bool>? value) : EventArgs
{
    public CookieConsentAction Action { get; } = action;
    public IReadOnlyDictionary<string, bool>? Value { get; } = value;
}

internal enum CookieMode { None, Bar, Panel, Chip }

/// <summary>A consent bar that morphs into a preferences panel and, once a choice is saved, folds into a small button that reopens it. One surface springs between all three shapes.</summary>
[TemplatePart(Name = "PART_Layer", Type = typeof(Grid))]
public class CookieConsent : Control
{
    private const double BarMax = 660, PanelMax = 420, Chip = 44, StackBelow = 560;

    public static readonly DependencyProperty CategoriesProperty = DependencyProperty.Register(nameof(Categories), typeof(ObservableCollection<CookieCategory>), typeof(CookieConsent), new FrameworkPropertyMetadata(null, OnCategoriesChanged));
    public static readonly DependencyProperty ConsentProperty = DependencyProperty.Register(nameof(Consent), typeof(IReadOnlyDictionary<string, bool>), typeof(CookieConsent), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((CookieConsent)d).UpdateMode()));
    public static readonly DependencyProperty StorageKeyProperty = DependencyProperty.Register(nameof(StorageKey), typeof(string), typeof(CookieConsent), new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty VersionProperty = DependencyProperty.Register(nameof(Version), typeof(string), typeof(CookieConsent), new FrameworkPropertyMetadata("1"));
    public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(nameof(Position), typeof(CookieConsentPosition), typeof(CookieConsent), new FrameworkPropertyMetadata(CookieConsentPosition.Bottom, (d, _) => ((CookieConsent)d).Reshape()));
    public static readonly DependencyProperty ShowReopenButtonProperty = DependencyProperty.Register(nameof(ShowReopenButton), typeof(bool), typeof(CookieConsent), new FrameworkPropertyMetadata(true, (d, _) => ((CookieConsent)d).UpdateMode()));
    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(nameof(Message), typeof(string), typeof(CookieConsent), new FrameworkPropertyMetadata("We use essential cookies to run this site. With your OK, we'd also use optional ones to remember settings and learn what helps.", (d, _) => ((CookieConsent)d).RebuildFace()));
    public static readonly DependencyProperty PreferencesTitleProperty = DependencyProperty.Register(nameof(PreferencesTitle), typeof(string), typeof(CookieConsent), new FrameworkPropertyMetadata("Cookie preferences", (d, _) => ((CookieConsent)d).RebuildFace()));

    public ObservableCollection<CookieCategory> Categories { get => (ObservableCollection<CookieCategory>)GetValue(CategoriesProperty); set => SetValue(CategoriesProperty, value); }
    public IReadOnlyDictionary<string, bool>? Consent { get => (IReadOnlyDictionary<string, bool>?)GetValue(ConsentProperty); set => SetValue(ConsentProperty, value); }
    public string? StorageKey { get => (string?)GetValue(StorageKeyProperty); set => SetValue(StorageKeyProperty, value); }
    public string Version { get => (string)GetValue(VersionProperty); set => SetValue(VersionProperty, value); }
    public CookieConsentPosition Position { get => (CookieConsentPosition)GetValue(PositionProperty); set => SetValue(PositionProperty, value); }
    public bool ShowReopenButton { get => (bool)GetValue(ShowReopenButtonProperty); set => SetValue(ShowReopenButtonProperty, value); }
    public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
    public string PreferencesTitle { get => (string)GetValue(PreferencesTitleProperty); set => SetValue(PreferencesTitleProperty, value); }
    public bool IsDecided => Consent is not null;
    public event EventHandler<CookieConsentChangedEventArgs>? ConsentChanged;

    public static readonly IReadOnlyList<CookieCategory> DefaultCategories =
    [
        new() { Id = "necessary", Label = "Strictly necessary", Description = "Sign-in, security, and your cart. The site can't work without these.", Required = true },
        new() { Id = "preferences", Label = "Preferences", Description = "Remember your language, region, and display settings." },
        new() { Id = "analytics", Label = "Analytics", Description = "Count visits and see which pages help, only in aggregate." },
        new() { Id = "marketing", Label = "Marketing", Description = "Measure campaigns and show relevant ads on other sites." },
    ];

    private readonly SpringDriver _driver;
    private readonly ControlSpring _x, _w, _h, _r, _riseY, _riseScale;
    private readonly OwnedMotion _surfaceMotion = new();
    private readonly CookieSurface _surface = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Visibility = Visibility.Collapsed, RenderTransformOrigin = new Point(.5, 1) };
    private readonly Border _shadow = new() { Margin = new Thickness(-1) };
    private readonly Border _fill = new();
    private readonly CookieFaceHost _faces = new();
    private readonly RectangleGeometry _clip = new();
    private readonly TranslateTransform _position = new(), _rise = new(), _leave = new();
    private readonly ScaleTransform _riseScaleTransform = new(), _leaveScale = new();
    private CookieFace? _face;
    private Grid? _layer;
    private bool _panelOpen, _ready, _stacked;
    private int _faceLayout;
    private ObservableCollection<CookieCategory>? _observed;
    private double _shapeW, _shapeH;
    private CookieMode _mode, _focusTarget;

    internal CookieMode Mode => _mode;
    internal Rect SurfaceBox => new(_x.Position, 0, _w.Position, _h.Position);
    internal double SurfaceRadius => _r.Position;
    internal double GrowTuning { get; private set; }
    internal FrameworkElement? CurrentFace => _face;
    internal void AdvanceMotion(double seconds) => _driver.Advance(seconds);

    static CookieConsent() => DefaultStyleKeyProperty.OverrideMetadata(typeof(CookieConsent), new FrameworkPropertyMetadata(typeof(CookieConsent)));

    public CookieConsent()
    {
        _driver = new SpringDriver(ApplySurface);
        _x = _driver.Add(0, ArcSpring.Of(.46));
        _w = _driver.Add(0, ArcSpring.Of(.46));
        _h = _driver.Add(0, ArcSpring.Of(.46));
        _r = _driver.Add(22, ArcSpring.Of(.46));
        _riseY = _driver.Add(0, ArcSpring.Of(.5));
        _riseScale = _driver.Add(1, ArcSpring.Of(.5));
        _shadow.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Border");
        _shadow.SetResourceReference(EffectProperty, "ArcWpf.Shadow.Floating");
        _fill.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceRaised");
        _faces.Clip = _clip;
        _surface.Children.Add(_shadow);
        _surface.Children.Add(_fill);
        _surface.Children.Add(_faces);
        var transforms = new TransformGroup();
        foreach (var transform in new Transform[] { _riseScaleTransform, _leaveScale, _rise, _leave, _position }) { transforms.Children.Add(transform); }
        _surface.RenderTransform = transforms;
        AutomationProperties.SetName(this, "Cookie consent");
        Categories = new ObservableCollection<CookieCategory>(DefaultCategories.Select(c => new CookieCategory { Id = c.Id, Label = c.Label, Description = c.Description, Required = c.Required, DefaultEnabled = c.DefaultEnabled }));
        Loaded += (_, _) => { LoadStored(); UpdateMode(); };
        Unloaded += (_, _) => _driver.Finish();
    }

    private bool Reduced => ArcMotion.GetLevel(this) != ArcMotionLevel.Full;

    private static void OnCategoriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var consent = (CookieConsent)d;
        if (consent._observed is not null) { consent._observed.CollectionChanged -= consent.OnCategoryChange; }
        consent._observed = e.NewValue as ObservableCollection<CookieCategory>;
        if (consent._observed is not null) { consent._observed.CollectionChanged += consent.OnCategoryChange; }
        consent.RebuildFace();
    }

    private void OnCategoryChange(object? sender, NotifyCollectionChangedEventArgs e) => RebuildFace();

    public override void OnApplyTemplate()
    {
        _layer?.Children.Remove(_surface);
        base.OnApplyTemplate();
        if (_layer is not null) { _layer.SizeChanged -= OnLayerSize; }
        _layer = GetTemplateChild("PART_Layer") as Grid;
        if (_layer is null) { return; }
        _layer.Children.Add(_surface);
        _layer.SizeChanged += OnLayerSize;
        UpdateMode();
    }

    private void OnLayerSize(object sender, SizeChangedEventArgs e)
    {
        if (_face is not null && FaceLayout() != _faceLayout) { RebuildFace(); return; }
        Reshape();
    }

    private double LayerWidth => _layer?.ActualWidth ?? 0;

    // Each face picks its arrangement from its width, so crossing a breakpoint rebuilds it.
    private int FaceLayout() => _mode switch
    {
        CookieMode.Bar => Math.Min(LayerWidth, BarMax) is var bar && bar >= StackBelow ? 0 : bar > 300 ? 1 : 2,
        CookieMode.Panel => Math.Min(LayerWidth, PanelMax) - 36 >= 340 ? 0 : 1,
        _ => 0,
    };

    // ---- Public actions ----
    public void OpenPreferences()
    {
        foreach (var category in Categories) { category.DraftEnabled = category.Required || (Consent is not null && Consent.TryGetValue(category.Id, out var on) ? on : category.DefaultEnabled); }
        _focusTarget = CookieMode.Panel;
        _panelOpen = true;
        UpdateMode();
    }

    public void ClosePreferences() => Close(false);

    private void Close(bool focus)
    {
        _focusTarget = focus ? (IsDecided ? CookieMode.Chip : CookieMode.Bar) : CookieMode.None;
        _panelOpen = false;
        UpdateMode();
    }

    public void Reset()
    {
        _panelOpen = false;
        _focusTarget = CookieMode.None;
        Consent = null;
        DeleteStored();
        ConsentChanged?.Invoke(this, new CookieConsentChangedEventArgs(CookieConsentAction.Reset, null));
    }

    public void CommitForTest(bool acceptAll) => Choose(acceptAll ? CookieConsentAction.AcceptAll : CookieConsentAction.RejectAll);

    private void Choose(CookieConsentAction action)
    {
        var value = Categories.ToDictionary(c => c.Id, c => c.Required || (action == CookieConsentAction.Save ? c.DraftEnabled : action == CookieConsentAction.AcceptAll));
        // Keep focus on the page: the choice folds into the reopen button, which takes focus when it exists.
        _focusTarget = ShowReopenButton ? CookieMode.Chip : CookieMode.None;
        _panelOpen = false;
        Consent = value;
        SaveStored(value);
        ConsentChanged?.Invoke(this, new CookieConsentChangedEventArgs(action, value));
    }

    // ---- Shape ----
    private void UpdateMode()
    {
        var next = !IsLoaded && _layer is null ? CookieMode.None : _panelOpen ? CookieMode.Panel : !IsDecided ? CookieMode.Bar : ShowReopenButton ? CookieMode.Chip : CookieMode.None;
        if (_layer is null) { _mode = next; return; }
        if (next == _mode && _face is not null) { Reshape(); FocusTarget(); return; }
        var previous = _mode;
        _mode = next;
        SwapFace(previous != CookieMode.None);
        if (next == CookieMode.None) { LeaveSurface(); _ready = false; }
        else if (previous == CookieMode.None || _surface.Visibility != Visibility.Visible) { EnterSurface(); }
        Reshape();
        FocusTarget();
    }

    private void EnterSurface()
    {
        _surfaceMotion.Stop();
        _surface.Visibility = Visibility.Visible;
        _leave.Y = 0; _leaveScale.ScaleX = _leaveScale.ScaleY = 1;
        _surface.Opacity = 0;
        if (Reduced || !IsLoaded) { _surfaceMotion.Double(_surface, OpacityProperty, 1, .16, IsLoaded); return; }
        _riseY.Snap(24); _riseScale.Snap(.98);
        _driver.Animate(_riseY, 0, ArcSpring.Of(.5));
        _driver.Animate(_riseScale, 1, ArcSpring.Of(.5));
        _surfaceMotion.Double(_surface, OpacityProperty, 1, .24, true, enter: true);
    }

    private void LeaveSurface()
    {
        if (_surface.Visibility != Visibility.Visible) { return; }
        void Hide() { if (_mode == CookieMode.None) { _surface.Visibility = Visibility.Collapsed; } }
        if (!IsLoaded) { Hide(); return; }
        if (Reduced) { _surfaceMotion.Double(_surface, OpacityProperty, 0, .12, true, completed: Hide); return; }
        _surfaceMotion.Double(_surface, OpacityProperty, 0, .18, true, completed: Hide);
        _surfaceMotion.Double(_leave, TranslateTransform.YProperty, 12, .18, true);
        _surfaceMotion.Double(_leaveScale, ScaleTransform.ScaleXProperty, .96, .18, true);
        _surfaceMotion.Double(_leaveScale, ScaleTransform.ScaleYProperty, .96, .18, true);
    }

    private (double Width, double Height, double X, double Radius) Target()
    {
        var align = Position switch { CookieConsentPosition.BottomLeft => CookieFaceSide.Left, CookieConsentPosition.BottomRight => CookieFaceSide.Right, _ => CookieFaceSide.Center };
        var chip = align == CookieFaceSide.Right ? CookieFaceSide.Right : CookieFaceSide.Left;
        double layer = LayerWidth;
        double width = _mode switch { CookieMode.Bar => Math.Min(layer, BarMax), CookieMode.Panel => Math.Min(layer, PanelMax), _ => Chip };
        double height = _mode == CookieMode.Chip ? Chip : MeasuredHeight(width);
        // Reopened preferences grow out of the corner button.
        var side = _mode == CookieMode.Chip ? chip : _mode == CookieMode.Panel && IsDecided && ShowReopenButton ? chip : align;
        if (_face is not null) { _face.Side = side; _face.Width = width; }
        double x = side == CookieFaceSide.Left ? 0 : side == CookieFaceSide.Right ? layer - width : (layer - width) / 2;
        return (width, height, x, _mode == CookieMode.Chip ? Chip / 2 : _mode == CookieMode.Panel ? 26 : 22);
    }

    private double MeasuredHeight(double width)
    {
        if (_face is null || width <= 0) { return 0; }
        _face.Width = width;
        _face.Measure(new Size(width, double.PositiveInfinity));
        return _face.DesiredSize.Height;
    }

    private void Reshape()
    {
        if (_layer is null || _mode == CookieMode.None || LayerWidth <= 0) { return; }
        var next = Target();
        if (next.Height <= 0) { return; }
        if (!_ready || Reduced || !IsLoaded)
        {
            _x.Snap(next.X); _w.Snap(next.Width); _h.Snap(next.Height); _r.Snap(next.Radius);
            ApplySurface();
            (_shapeW, _shapeH, _ready) = (next.Width, next.Height, true);
            return;
        }
        double tuning = next.Width * next.Height >= _shapeW * _shapeH ? ArcSpring.Of(.46) : ArcSpring.Of(.38);
        GrowTuning = tuning;
        (_shapeW, _shapeH) = (next.Width, next.Height);
        _driver.Animate(_x, next.X, tuning);
        _driver.Animate(_w, next.Width, tuning);
        _driver.Animate(_h, next.Height, tuning);
        _driver.Animate(_r, next.Radius, tuning);
    }

    private void ApplySurface()
    {
        double width = Math.Max(0, _w.Position), height = Math.Max(0, _h.Position), radius = Math.Max(0, _r.Position);
        _surface.Width = width;
        _surface.Height = height;
        _position.X = _x.Position;
        _rise.Y = _riseY.Position;
        _riseScaleTransform.ScaleX = _riseScaleTransform.ScaleY = _riseScale.Position;
        _shadow.CornerRadius = new CornerRadius(radius + 1);
        _fill.CornerRadius = new CornerRadius(radius);
        _clip.Rect = new Rect(0, 0, width, height);
        _clip.RadiusX = _clip.RadiusY = radius;
    }

    // ---- Faces ----
    private void RebuildFace()
    {
        if (_layer is null || _face is null) { return; }
        SwapFace(false);
        Reshape();
    }

    private void SwapFace(bool animate)
    {
        animate = animate && IsLoaded;
        var old = _face;
        if (old is not null) { if (animate) { old.Leave(Reduced, () => _faces.Children.Remove(old)); } else { _faces.Children.Remove(old); } }
        _faceLayout = FaceLayout();
        _face = _mode switch { CookieMode.Bar => BuildBar(), CookieMode.Panel => BuildPanel(), CookieMode.Chip => BuildChip(), _ => null };
        if (_face is null) { return; }
        _faces.Children.Add(_face);
        if (animate) { _face.Enter(Reduced); }
    }

    private void FocusTarget()
    {
        if (_focusTarget != _mode || _face is null) { return; }
        var target = _face.Autofocus;
        _focusTarget = CookieMode.None;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => target?.Focus());
    }

    private Button Action(string text, ButtonVariant variant, CookieConsentAction? action, Action? click = null)
    {
        var button = new Button { Content = text, Size = ControlSize.Small, Variant = variant, HorizontalAlignment = HorizontalAlignment.Stretch };
        button.Click += (_, _) => { if (action is { } chosen) { Choose(chosen); } else { click?.Invoke(); } };
        return button;
    }

    private CookieFace BuildBar()
    {
        double width = Math.Min(LayerWidth, BarMax);
        _stacked = width < StackBelow;
        var message = ArcText.Block(Message, 14, 19.6, "TextSecondary");
        message.TextWrapping = TextWrapping.Wrap;
        var customize = Action("Customize", ButtonVariant.Ghost, null, OpenPreferences);
        var reject = Action("Reject all", ButtonVariant.Secondary, CookieConsentAction.RejectAll);
        var accept = Action("Accept all", ButtonVariant.Secondary, CookieConsentAction.AcceptAll);
        var actions = new Grid();
        void Column(GridLength size) => actions.ColumnDefinitions.Add(new ColumnDefinition { Width = size });
        void Place(FrameworkElement element, int column, int row = 0, double left = 6) { Grid.SetColumn(element, column); Grid.SetRow(element, row); if (column > 0) { element.Margin = new Thickness(left, 0, 0, 0); } actions.Children.Add(element); }
        Grid inner;
        if (_stacked && width <= 300)
        {
            // Below about 340 px the three buttons no longer fit one row, so Customize takes the row under them.
            Column(new GridLength(1, GridUnitType.Star)); Column(new GridLength(1, GridUnitType.Star));
            actions.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            actions.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Place(reject, 0); Place(accept, 1);
            customize.Margin = new Thickness(0, 6, 0, 0);
            Grid.SetRow(customize, 1); Grid.SetColumnSpan(customize, 2);
            actions.Children.Add(customize);
        }
        else if (_stacked)
        {
            Column(GridLength.Auto); Column(new GridLength(1, GridUnitType.Star)); Column(new GridLength(1, GridUnitType.Star));
            Place(customize, 0); Place(reject, 1); Place(accept, 2);
        }
        else
        {
            Column(GridLength.Auto); Column(GridLength.Auto); Column(GridLength.Auto);
            Place(customize, 0); Place(reject, 1); Place(accept, 2);
        }
        inner = new Grid();
        if (_stacked)
        {
            inner.Margin = new Thickness(0, 6, 8, 0);
            inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            actions.Margin = new Thickness(0, 12, 0, 0);
            Grid.SetRow(actions, 1);
        }
        else
        {
            inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            message.VerticalAlignment = VerticalAlignment.Center;
            actions.Margin = new Thickness(16, 0, 0, 0);
            actions.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(actions, 1);
        }
        inner.Children.Add(message);
        inner.Children.Add(actions);
        var face = new CookieFace(CookieMode.Bar) { Padding = new Thickness(18, 10, 10, 10), Child = inner, Autofocus = customize };
        AutomationProperties.SetName(face, PreferencesTitle);
        return face;
    }

    private CookieFace BuildPanel()
    {
        double width = Math.Min(LayerWidth, PanelMax);
        var title = ArcText.Block(PreferencesTitle, 16, 22.4, "Foreground", true);
        title.VerticalAlignment = VerticalAlignment.Center;
        title.Focusable = true;
        title.FocusVisualStyle = null;
        var close = new ArcChromeButton(16) { Width = 32, Height = 32, Margin = new Thickness(0, 0, -8, 0), Roles = b => (b.IsMouseOver || b.IsPressed ? "SurfaceMuted" : null, null, b.IsMouseOver || b.IsPressed ? "Foreground" : "TextSecondary") };
        close.Content = ArcText.Icon("x", 16, close.Ink);
        AutomationProperties.SetName(close, "Close preferences");
        close.Click += (_, _) => Close(true);
        var head = new DockPanel { Margin = new Thickness(0, -4, 0, -6) };
        DockPanel.SetDock(close, Dock.Right);
        head.Children.Add(close);
        head.Children.Add(title);
        var description = ArcText.Block("Choose which optional cookies we can use. You can change this any time.", 14, 19.6, "TextSecondary");
        description.TextWrapping = TextWrapping.Wrap;
        description.Margin = new Thickness(0, 12, 0, 0);

        var rows = new StackPanel();
        for (int i = 0; i < Categories.Count; i++) { rows.Children.Add(CategoryRow(Categories[i], i == 0)); }
        double viewport = Window.GetWindow(this)?.ActualHeight ?? 0;
        var list = new ScrollArea { Content = rows, MaxHeight = viewport > 0 ? Math.Min(viewport * .46, 300) : 300, Margin = new Thickness(-6, 12, -6, 0), Fade = 0 };
        AutomationProperties.SetName(list, "Cookie categories");

        var reject = Action("Reject all", ButtonVariant.Secondary, CookieConsentAction.RejectAll);
        var accept = Action("Accept all", ButtonVariant.Secondary, CookieConsentAction.AcceptAll);
        var save = Action("Save choices", ButtonVariant.Primary, CookieConsentAction.Save);
        var optional = Categories.Where(c => !c.Required).ToList();
        AutomationProperties.SetHelpText(save, $"{optional.Count(c => c.DraftEnabled)} of {optional.Count} optional categories allowed");
        var actions = new Grid();
        bool wide = width - 36 >= 340;
        if (wide)
        {
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            accept.Margin = save.Margin = new Thickness(6, 0, 0, 0);
            Grid.SetColumn(accept, 1);
            Grid.SetColumn(save, 2);
        }
        else
        {
            actions.ColumnDefinitions.Add(new ColumnDefinition());
            actions.ColumnDefinitions.Add(new ColumnDefinition());
            actions.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            actions.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            accept.Margin = new Thickness(6, 0, 0, 0);
            save.Margin = new Thickness(0, 6, 0, 0);
            Grid.SetColumn(accept, 1);
            Grid.SetRow(save, 1);
            Grid.SetColumnSpan(save, 2);
        }
        actions.Children.Add(reject);
        actions.Children.Add(accept);
        actions.Children.Add(save);
        var footer = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 8, 0, 0), Margin = new Thickness(0, 12, 0, 0), Child = actions };
        footer.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderSubtle");

        var stack = new StackPanel();
        foreach (var child in new UIElement[] { head, description, list, footer }) { stack.Children.Add(child); }
        var face = new CookieFace(CookieMode.Panel) { Padding = new Thickness(18), Child = stack, Autofocus = title };
        AutomationProperties.SetName(face, PreferencesTitle);
        face.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(true); } };
        return face;
    }

    private Border CategoryRow(CookieCategory category, bool first)
    {
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var label = ArcText.Block(category.Label, 14, 19.6, "Foreground", true);
        text.Children.Add(label);
        if (category.Description is not null)
        {
            var hint = ArcText.Block(category.Description, 12, 16.8, "TextMuted");
            hint.TextWrapping = TextWrapping.Wrap;
            hint.Margin = new Thickness(0, 2, 0, 0);
            text.Children.Add(hint);
        }
        FrameworkElement end;
        if (category.Required) { end = ArcText.Block("Always on", 12, 16.8, "TextMuted"); }
        else
        {
            var toggle = new Switch { IsChecked = category.DraftEnabled };
            AutomationProperties.SetName(toggle, category.Label);
            toggle.Checked += (_, _) => category.DraftEnabled = true;
            toggle.Unchecked += (_, _) => category.DraftEnabled = false;
            label.Cursor = Cursors.Hand;
            label.MouseLeftButtonUp += (_, _) => toggle.IsChecked = !toggle.IsChecked;
            end = toggle;
        }
        end.VerticalAlignment = VerticalAlignment.Center;
        end.Margin = new Thickness(16, 0, 0, 0);
        var row = new DockPanel();
        DockPanel.SetDock(end, Dock.Right);
        row.Children.Add(end);
        row.Children.Add(text);
        var frame = new Border { Padding = new Thickness(6, 10, 6, 10), BorderThickness = new Thickness(0, first ? 0 : 1, 0, 0), Child = row };
        frame.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderSubtle");
        return frame;
    }

    private CookieFace BuildChip()
    {
        var chip = new ArcChromeButton(22) { Width = Chip, Height = Chip, Roles = b => (b.IsMouseOver || b.IsPressed ? "SurfaceMuted" : null, null, "Foreground") };
        chip.Content = ArcText.Icon("cookie", 20, chip.Ink);
        AutomationProperties.SetName(chip, "Cookie preferences");
        chip.Click += (_, _) => OpenPreferences();
        return new CookieFace(CookieMode.Chip) { Child = chip, Height = Chip, Autofocus = chip };
    }

    // ---- Storage ----
    private string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArcWpf", (StorageKey ?? "cookie-consent").Replace('/', '_') + ".json");
    private void LoadStored()
    {
        if (string.IsNullOrWhiteSpace(StorageKey) || Consent is not null) { return; }
        try
        {
            if (!File.Exists(FilePath)) { return; }
            var data = JsonSerializer.Deserialize<StoredConsent>(File.ReadAllText(FilePath));
            if (data?.Version == Version) { Consent = data.Value; }
        }
        catch { /* Storage can be blocked or stale; the bar simply asks again. */ }
    }

    private void SaveStored(IReadOnlyDictionary<string, bool> value)
    {
        if (string.IsNullOrWhiteSpace(StorageKey)) { return; }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new StoredConsent(value, Version, DateTime.UtcNow)));
        }
        catch { /* The event still reports the choice. */ }
    }

    private void DeleteStored()
    {
        if (string.IsNullOrWhiteSpace(StorageKey)) { return; }
        try { if (File.Exists(FilePath)) { File.Delete(FilePath); } }
        catch { /* Nothing further to forget. */ }
    }

    private sealed record StoredConsent(IReadOnlyDictionary<string, bool> Value, string Version, DateTime UpdatedAt);
}

/// <summary>Overflows its layer upward, as the zero-height CSS layer does, so the panel and its shadow must not be cut to the layout slot.</summary>
internal sealed class CookieSurface : Grid
{
    protected override Geometry? GetLayoutClip(Size layoutSlotSize) => null;
}

internal enum CookieFaceSide { Left, Center, Right }

/// <summary>One of the surface's faces. It sits on the bottom edge at its final width, so the surface grows upward into it.</summary>
internal sealed class CookieFace : Border
{
    private readonly OwnedMotion _motion = new();
    private readonly TranslateTransform _move = new();
    private SpringDriver? _driver;
    public CookieMode Mode { get; }
    public CookieFaceSide Side { get; set; }
    public UIElement? Autofocus { get; set; }
    public CookieFace(CookieMode mode) { Mode = mode; RenderTransform = _move; }

    public void Enter(bool reduced)
    {
        Opacity = 0;
        if (reduced) { _motion.Double(this, OpacityProperty, 1, .16, true); return; }
        _move.Y = 10;
        ControlSpring? spring = null;
        _driver = new SpringDriver(() => _move.Y = spring!.Position);
        spring = _driver.Add(10, ArcSpring.Of(.42));
        _driver.Animate(spring, 0, ArcSpring.Of(.42));
        _motion.Double(this, OpacityProperty, 1, .22, true, delay: .04, enter: true);
        ArcEffects.BlurIn(_motion, this, 4, .26, .04);
    }

    public void Leave(bool reduced, Action done)
    {
        IsHitTestVisible = false;
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.None);
        _driver?.Stop();
        if (reduced) { _motion.Double(this, OpacityProperty, 0, .1, true, completed: done); return; }
        _motion.Double(this, OpacityProperty, 0, .12, true, completed: done);
        _motion.Double(_move, TranslateTransform.YProperty, 4, .12, true);
        ArcEffects.BlurOut(_motion, this, 4, .12);
    }
}

internal sealed class CookieFaceHost : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        // A face has no width until the layer has been measured.
        foreach (FrameworkElement child in InternalChildren) { child.Measure(new Size(double.IsNaN(child.Width) ? 0 : child.Width, double.PositiveInfinity)); }
        return new Size();
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (CookieFace face in InternalChildren)
        {
            double width = double.IsNaN(face.Width) ? 0 : face.Width, x = face.Side == CookieFaceSide.Left ? 0 : face.Side == CookieFaceSide.Right ? finalSize.Width - width : (finalSize.Width - width) / 2;
            face.Arrange(new Rect(x, finalSize.Height - face.DesiredSize.Height, width, face.DesiredSize.Height));
        }
        return finalSize;
    }
}
