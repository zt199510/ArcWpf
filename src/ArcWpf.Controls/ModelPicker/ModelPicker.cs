using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NativeButton = System.Windows.Controls.Button;

namespace ArcWpf.Controls;

public enum ModelEffort { Low, Medium, High, ExtraHigh, Max }

/// <summary>One model in the catalog. Only models with Efforts show the Effort row.</summary>
public sealed record PickerModel(string Id, string Name, string Provider)
{
    /// <summary>The name a compact chip shows; defaults to Name.</summary>
    public string? Short { get; init; }
    /// <summary>A few words on what the model is for: the only line under the name in the menu.</summary>
    public string? Blurb { get; init; }
    /// <summary>The longer sentence in the review under the pill.</summary>
    public string? Description { get; init; }
    public string? Context { get; init; }
    /// <summary>USD per 1M tokens.</summary>
    public double? InputPrice { get; init; }
    public double? OutputPrice { get; init; }
    public AiMark? Mark { get; init; }
    public IReadOnlyList<ModelEffort>? Efforts { get; init; }
    public ModelEffort? DefaultEffort { get; init; }
}

public sealed record PickerProvider(string Id, string Name, AiMark? Mark = null);

public static class ModelEffortText
{
    public static string Label(ModelEffort effort) => effort switch { ModelEffort.Low => "Low", ModelEffort.Medium => "Medium", ModelEffort.High => "High", ModelEffort.ExtraHigh => "Extra high", _ => "Max" };
    public static string Note(ModelEffort effort) => effort switch { ModelEffort.Low => "Quickest answers", ModelEffort.Medium => "Balanced speed and depth", ModelEffort.High => "Thinks longer", ModelEffort.ExtraHigh => "Thinks much longer", _ => "Deepest thinking, slowest" };
    /// <summary>A level the model does not accept snaps to the nearest one it does.</summary>
    public static ModelEffort Snap(ModelEffort effort, IReadOnlyList<ModelEffort> levels) => levels.OrderBy(level => Math.Abs((int)level - (int)effort)).ThenByDescending(level => (int)level <= (int)effort).First();
}

public sealed class ModelChosenEventArgs(PickerModel model) : EventArgs { public PickerModel Model { get; } = model; }

/// <summary>A model picker in one quiet pill: featured models with a one line note each, a reasoning effort, and every other model by provider.</summary>
public class ModelPicker : Control
{
    public static readonly DependencyProperty ModelsProperty = Prop(nameof(Models), typeof(IReadOnlyList<PickerModel>), null);
    public static readonly DependencyProperty ProvidersProperty = Prop(nameof(Providers), typeof(IReadOnlyList<PickerProvider>), null);
    public static readonly DependencyProperty FeaturedProperty = Prop(nameof(Featured), typeof(IReadOnlyList<string>), null);
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(ModelPicker), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ModelPicker)d).OnChoice()));
    public static readonly DependencyProperty EffortProperty = DependencyProperty.Register(nameof(Effort), typeof(ModelEffort?), typeof(ModelPicker), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ModelPicker)d).OnChoice()));
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(ModelPicker), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ModelPicker)d).OnOpen()));
    public static readonly DependencyProperty ShowSummaryProperty = Prop(nameof(ShowSummary), typeof(bool), true);
    public static readonly DependencyProperty IsCompactProperty = Prop(nameof(IsCompact), typeof(bool), false);
    public static readonly DependencyProperty OpensUpwardProperty = DependencyProperty.Register(nameof(OpensUpward), typeof(bool), typeof(ModelPicker), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty NoteProperty = Prop(nameof(Note), typeof(string), "Prices per million tokens");

    private static DependencyProperty Prop(string name, Type type, object? fallback) => DependencyProperty.Register(name, type, typeof(ModelPicker), new FrameworkPropertyMetadata(fallback, (d, _) => ((ModelPicker)d).Refresh()));

    public IReadOnlyList<PickerModel>? Models { get => (IReadOnlyList<PickerModel>?)GetValue(ModelsProperty); set => SetValue(ModelsProperty, value); }
    /// <summary>Providers in the order More models groups them.</summary>
    public IReadOnlyList<PickerProvider>? Providers { get => (IReadOnlyList<PickerProvider>?)GetValue(ProvidersProperty); set => SetValue(ProvidersProperty, value); }
    /// <summary>Model ids listed at the top of the menu, in order; the first four when left out.</summary>
    public IReadOnlyList<string>? Featured { get => (IReadOnlyList<string>?)GetValue(FeaturedProperty); set => SetValue(FeaturedProperty, value); }
    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public ModelEffort? Effort { get => (ModelEffort?)GetValue(EffortProperty); set => SetValue(EffortProperty, value); }
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    /// <summary>The review under the pill. Turn it off to use the pill alone, for example inside a composer toolbar.</summary>
    public bool ShowSummary { get => (bool)GetValue(ShowSummaryProperty); set => SetValue(ShowSummaryProperty, value); }
    /// <summary>A borderless chip with the short name, for toolbars.</summary>
    public bool IsCompact { get => (bool)GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }
    public bool OpensUpward { get => (bool)GetValue(OpensUpwardProperty); set => SetValue(OpensUpwardProperty, value); }
    public string? Note { get => (string?)GetValue(NoteProperty); set => SetValue(NoteProperty, value); }

    private int _fold;
    // A narrow toolbar: 1 drops the effort from the chip, 2 keeps only the mark.
    internal int FoldLevel { get => _fold; set { if (_fold != value) { _fold = value; _pillKey = null; Refresh(); } } }

    public event EventHandler<ModelChosenEventArgs>? ModelChosen;
    /// <summary>The effort changed, including when a new model snapped it to a level it accepts.</summary>
    public event EventHandler? EffortChanged;

    private const double Gutter = 24, MenuWidth = 304, SubWidth = 260, SubGap = 4, PageBreak = 640, RowHeight = 54, ExtraHeight = 40;
    private enum Sub { None, Effort, More }

    private ToolbarButton? _pill;
    private readonly Decorator _pillMark = new() { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
    private readonly FadeSwap _pillText = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly SpringWidthBox _pillWidth = new();
    private readonly TurningChevron _chevron = new();
    private readonly Popup _popup = new() { AllowsTransparency = true, StaysOpen = false, Placement = PlacementMode.Custom };
    private readonly Grid _stage = new() { Margin = new Thickness(Gutter) };
    private readonly Border _menu = new() { Width = MenuWidth, CornerRadius = new CornerRadius(26), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Border _sub = new() { Width = SubWidth, CornerRadius = new CornerRadius(26), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(MenuWidth + SubGap, 0, 0, 0), Visibility = Visibility.Hidden };
    private readonly ScaleTransform _menuScale = new(1, 1);
    private readonly TranslateTransform _menuShift = new();
    private readonly Grid _pageHost = new();
    private readonly StackPanel _rows = new() { Margin = new Thickness(6) };
    private readonly StackPanel _subEffort = new() { Margin = new Thickness(6) };
    private readonly StackPanel _subMore = new() { Margin = new Thickness(6) };
    private readonly Border _highlight = new() { CornerRadius = new CornerRadius(18), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false, Opacity = 0, Width = 290 };
    private readonly TranslateTransform _highlightAt = new();
    private readonly StackPanel _summary = new();
    private readonly FadeSwap _description = new();
    private readonly Grid _cells = new();
    private readonly List<(FadeSwap Slot, string Key)> _cellSlots = [];
    private readonly string?[] _cellShown = new string?[4];
    private readonly TextBlock _note = AiUi.Line("", 12, 16.8, "TextMuted");
    private readonly FadeSwap _confirm = new() { Height = 18, Margin = new Thickness(0, 6, 0, 0) };
    private readonly DispatcherTimer _confirmClear = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromSeconds(2.4) };
    private readonly DispatcherTimer _subOpen = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(120) };
    private Sub _pending, _openSub;
    private bool _pages, _built, _settling, _highlightPlaced;
    private string? _shownModel, _pillKey, _descriptionKey;
    private ModelEffort? _shownEffort;
    private AiMark? _pillShownMark;
    private bool _hasPillMark;

    static ModelPicker() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ModelPicker), new FrameworkPropertyMetadata(typeof(ModelPicker)));

    public ModelPicker()
    {
        foreach (var surface in new[] { _menu, _sub })
        {
            surface.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceRaised");
            surface.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
            surface.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 36, ShadowDepth = 14, Direction = 270, Opacity = .11, Color = Colors.Black };
        }
        _highlight.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
        _highlight.RenderTransform = _highlightAt;
        _pageHost.Children.Add(_highlight);
        _pageHost.Children.Add(_rows);
        _menu.Child = _pageHost;
        _menu.RenderTransformOrigin = new Point(.5, 0);
        var group = new TransformGroup();
        group.Children.Add(_menuScale);
        group.Children.Add(_menuShift);
        _menu.RenderTransform = group;
        var subHost = new Grid();
        subHost.Children.Add(_subEffort);
        subHost.Children.Add(_subMore);
        _sub.Child = subHost;
        _stage.Children.Add(_menu);
        _stage.Children.Add(_sub);
        _popup.Child = _stage;
        _popup.CustomPopupPlacementCallback = Place;
        // A press on the pill while open only closes it, instead of closing and reopening.
        _popup.Opened += (_, _) => { if (_pill is not null) { _pill.IsHitTestVisible = false; } };
        _popup.Closed += (_, _) => { _subOpen.Stop(); ShowSub(Sub.None); if (IsOpen) { SetCurrentValue(IsOpenProperty, false); } Dispatcher.BeginInvoke(() => { if (_pill is not null) { _pill.IsHitTestVisible = true; } }, DispatcherPriority.Input); };
        _stage.PreviewKeyDown += OnMenuKey;
        _confirmClear.Tick += (_, _) => { _confirmClear.Stop(); _confirm.Set(new Decorator()); };
        _subOpen.Tick += (_, _) => { _subOpen.Stop(); ShowSub(_pending); };
        Unloaded += (_, _) => { _confirmClear.Stop(); _subOpen.Stop(); _popup.IsOpen = false; };
    }

    internal IReadOnlyList<PickerModel> Catalog => Models ?? [];
    internal PickerModel? Chosen => Catalog.FirstOrDefault(model => model.Id == Value) ?? FeaturedModels.FirstOrDefault();
    private IReadOnlyList<PickerModel> FeaturedModels => Featured is { } ids ? [.. ids.Select(id => Catalog.FirstOrDefault(model => model.Id == id)).OfType<PickerModel>()] : [.. Catalog.Take(4)];
    private IReadOnlyList<PickerModel> OtherModels => [.. Catalog.Where(model => !FeaturedModels.Contains(model))];
    internal ModelEffort? ShownEffort => Chosen is { Efforts.Count: > 0 } model ? ModelEffortText.Snap(Effort ?? model.DefaultEffort ?? model.Efforts![0], model.Efforts!) : null;

    internal string PillNameForTest => Chosen is { } model ? IsCompact ? model.Short ?? model.Name : model.Name : "";
    internal string PillAutomationForTest => _pill is null ? "" : AutomationProperties.GetName(_pill);
    internal NativeButton? PillForTest => _pill;
    internal bool PopupOpenForTest => _popup.IsOpen;
    internal string[] RowLabelsForTest => [.. _rows.Children.OfType<FrameworkElement>().Select(row => row.Tag as string ?? "").Where(tag => tag.Length > 0)];
    internal string[] SubLabelsForTest => [.. Current(_openSub).Children.OfType<FrameworkElement>().Select(row => row.Tag as string ?? "").Where(tag => tag.Length > 0)];
    internal string OpenSubForTest => _openSub.ToString();
    internal bool PagesForTest => _pages;
    internal NativeButton RowForTest(string tag) => _rows.Children.OfType<NativeButton>().First(row => (row.Tag as string) == tag);
    internal NativeButton SubRowForTest(string tag) => Current(_openSub).Children.OfType<NativeButton>().First(row => (row.Tag as string) == tag);
    internal void OpenSubForTest2(string name) => ShowSub(Enum.Parse<Sub>(name));
    internal bool KeyHandledForTest(Key key) => HandleKey(key);
    internal string DescriptionForTest => _descriptionKey ?? "";
    internal string CellForTest(int index) => _cellShown[index] ?? "";
    internal FrameworkElement SummaryForTest => _summary;
    internal bool SummaryShownForTest => _summary.Visibility == Visibility.Visible;
    internal double HighlightYForTest => _highlightAt.Y;
    internal double HighlightOpacityForTest => _highlight.Opacity;
    internal Border MenuForTest => _menu;
    internal string ConfirmForTest => _confirm.Current is TextBlock text ? text.Text : "";
    internal void HoverRowForTest(FrameworkElement row) => MoveHighlight(row, true);

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_Root") is not Decorator host) { return; }
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(_pillMark);
        _pillWidth.Child = _pillText;
        _pillWidth.Margin = new Thickness(8, 0, 8, 0);
        row.Children.Add(_pillWidth);
        row.Children.Add(_chevron);
        _pill = new ToolbarButton(20, 1) { Content = row, HorizontalAlignment = HorizontalAlignment.Center, Roles = self => (IsCompact ? self.IsMouseOver || IsOpen ? "SurfaceMuted" : null : self.IsMouseOver ? "SurfaceMuted" : "Surface", IsCompact ? null : "Border", "Foreground") };
        _pill.Click += (_, _) => SetCurrentValue(IsOpenProperty, !IsOpen);
        _pill.PreviewKeyDown += (_, e) => { if (e.Key == Key.Down && !IsOpen) { e.Handled = true; SetCurrentValue(IsOpenProperty, true); } };
        _popup.PlacementTarget = _pill;

        _description.HorizontalAlignment = HorizontalAlignment.Center;
        var descriptionSlot = new Grid { Height = 48, MaxWidth = 380 };
        descriptionSlot.Children.Add(_description);
        var well = new Border { CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1), Margin = new Thickness(0, 15, 0, 0), Child = _cells };
        well.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderSubtle");
        string[] labels = ["Context", "Input", "Output", "Effort"];
        for (int index = 0; index < 4; index++)
        {
            var cell = new StackPanel { Margin = new Thickness(8, 11, 8, 11) };
            var label = AiUi.Line(labels[index], 12, 16.8, "TextMuted");
            label.HorizontalAlignment = HorizontalAlignment.Center;
            var slot = new FadeSwap { Height = 22, HorizontalAlignment = HorizontalAlignment.Center };
            cell.Children.Add(label);
            cell.Children.Add(slot);
            _cells.Children.Add(cell);
            _cellSlots.Add((slot, labels[index]));
        }
        _note.HorizontalAlignment = HorizontalAlignment.Center;
        _note.Margin = new Thickness(0, 12, 0, 0);
        _confirm.HorizontalAlignment = HorizontalAlignment.Center;
        _summary.Margin = new Thickness(0, 24, 0, 0);
        _summary.HorizontalAlignment = HorizontalAlignment.Center;
        _summary.Width = 440;
        _summary.Children.Add(descriptionSlot);
        _summary.Children.Add(well);
        _summary.Children.Add(_note);
        _summary.Children.Add(_confirm);
        var column = new StackPanel();
        column.Children.Add(_pill);
        column.Children.Add(_summary);
        host.Child = column;
        SizeChanged += (_, e) => { if (e.WidthChanged) { LayoutCells(); } };
        _built = true;
        Refresh();
        LayoutCells();
    }

    // The review's four values fold into a two by two grid under 400.
    private void LayoutCells()
    {
        double room = ActualWidth > 0 ? ActualWidth : 440;
        _summary.Width = Math.Min(440, room);
        bool fold = room < 400;
        _cells.ColumnDefinitions.Clear();
        _cells.RowDefinitions.Clear();
        int columns = fold ? 2 : 4;
        for (int index = 0; index < columns; index++) { _cells.ColumnDefinitions.Add(new ColumnDefinition()); }
        for (int index = 0; index < (fold ? 2 : 1); index++) { _cells.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); }
        for (int index = 0; index < _cells.Children.Count; index++)
        {
            Grid.SetColumn(_cells.Children[index], index % columns);
            Grid.SetRow(_cells.Children[index], index / columns);
        }
    }

    private CustomPopupPlacement[] Place(Size popup, Size target, Point offset)
    {
        double scale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1;
        double gutter = Gutter * scale, gap = 8 * scale, menu = MenuWidth * scale;
        double x = (target.Width - menu) / 2 - gutter;
        double below = target.Height + gap - gutter, above = -popup.Height - gutter - gap; // The size given here leaves out the gutter margin.
        return OpensUpward
            ? [new(new Point(x, above), PopupPrimaryAxis.Horizontal), new(new Point(x, below), PopupPrimaryAxis.Horizontal)]
            : [new(new Point(x, below), PopupPrimaryAxis.Horizontal), new(new Point(x, above), PopupPrimaryAxis.Horizontal)];
    }

    private void OnChoice()
    {
        if (!_built) { return; }
        var model = Chosen;
        // A level the new model does not accept snaps to the nearest one it does.
        if (!_settling && Effort is { } wanted && model?.Efforts is { Count: > 0 } levels && !levels.Contains(wanted))
        {
            _settling = true;
            SetCurrentValue(EffortProperty, ModelEffortText.Snap(wanted, levels));
            _settling = false;
            EffortChanged?.Invoke(this, EventArgs.Empty);
        }
        Refresh();
        if (_shownModel is not null && (_shownModel != model?.Id || _shownEffort != ShownEffort) && model is not null)
        {
            string words = ShownEffort is { } effort ? $"{model.Name}, {ModelEffortText.Label(effort).ToLowerInvariant()} effort" : model.Name;
            Announcer.Say(this, $"Model: {words}");
            if (ShowSummary)
            {
                var line = AiUi.Line($"Now using {words}", 12, 16.8, "TextSecondary");
                _confirm.Set(line);
                _confirmClear.Stop();
                _confirmClear.Start();
            }
        }
        _shownModel = model?.Id;
        _shownEffort = ShownEffort;
    }

    private void Refresh()
    {
        if (!_built || _pill is null) { return; }
        var model = Chosen;
        if (model is null) { return; }
        _shownModel ??= model.Id;
        _shownEffort ??= ShownEffort;
        _pill.Height = IsCompact ? 36 : 40;
        _pill.BorderThickness = new Thickness(IsCompact ? 0 : 1);
        _pill.Padding = new Thickness(IsCompact ? 10 : 13, 0, IsCompact ? 10 : 12, 0);
        _pill.Refresh(false);
        if (!_hasPillMark || _pillShownMark != model.Mark)
        {
            bool hadMark = _hasPillMark;
            _hasPillMark = true;
            _pillShownMark = model.Mark;
            _pillMark.Child = AiUi.Mark(model.Mark, 16, model.Name);
            if (hadMark && AiUi.Moving(this)) { _pillMark.Child.Opacity = 0; Tween.To(_pillMark.Child, OpacityProperty, 1, .16, ArcEasing.Enter); }
        }
        string name = IsCompact ? model.Short ?? model.Name : model.Name;
        string? effort = ShownEffort is { } level ? ModelEffortText.Label(level) : null;
        string? shownEffort = _fold > 0 ? null : effort;
        _pillWidth.Visibility = _fold > 1 ? Visibility.Collapsed : Visibility.Visible;
        string key = name + "|" + shownEffort;
        if (key != _pillKey)
        {
            _pillKey = key;
            var text = new StackPanel { Orientation = Orientation.Horizontal };
            text.Children.Add(AiUi.Line(name, 14, 19.6, "Foreground", true, false));
            if (shownEffort is not null) { var quiet = AiUi.Line(shownEffort, 14, 19.6, "TextMuted", false, false); quiet.Margin = new Thickness(6, 0, 0, 0); text.Children.Add(quiet); }
            _pillText.Set(text);
        }
        AutomationProperties.SetName(_pill, $"Model: {model.Name}{(effort is null ? "" : $", {effort.ToLowerInvariant()} effort")}. Change model");
        Summary(model, effort);
        if (_popup.IsOpen) { BuildMenu(); }
    }

    private void Summary(PickerModel model, string? effort)
    {
        _summary.Visibility = ShowSummary ? Visibility.Visible : Visibility.Collapsed;
        if (!ShowSummary) { return; }
        string description = model.Description ?? model.Blurb ?? "";
        if (description != _descriptionKey)
        {
            _descriptionKey = description;
            var view = AiUi.Wrapped(description, 14, 21, "TextSecondary");
            view.TextAlignment = TextAlignment.Center;
            _description.Set(view);
        }
        string?[] values = [model.Context ?? "–", Price(model.InputPrice), Price(model.OutputPrice), effort ?? "–"];
        for (int index = 0; index < 4; index++)
        {
            if (_cellShown[index] == values[index]) { continue; }
            _cellShown[index] = values[index];
            var view = AiUi.Line(values[index] ?? "–", 14, 22, "Foreground", true, false);
            _cellSlots[index].Slot.Set(view);
        }
        _note.Text = Note ?? "";
        _note.Visibility = string.IsNullOrEmpty(Note) ? Visibility.Collapsed : Visibility.Visible;
    }

    private static string Price(double? price) => price is { } value ? "$" + value.ToString("0.00", CultureInfo.InvariantCulture) : "–";

    private void OnOpen()
    {
        if (!_built) { return; }
        _chevron.Point(IsOpen);
        _pill?.Refresh(true);
        if (!IsOpen) { if (_popup.IsOpen) { _popup.IsOpen = false; } return; }
        var window = Window.GetWindow(this);
        _pages = window is not null && window.ActualWidth > 0 && window.ActualWidth < PageBreak;
        _stage.Width = _pages ? MenuWidth : MenuWidth + SubGap + SubWidth;
        _highlightPlaced = false;
        _highlight.Opacity = 0;
        BuildMenu();
        Subs();
        _menu.VerticalAlignment = OpensUpward ? VerticalAlignment.Bottom : VerticalAlignment.Top;
        _sub.VerticalAlignment = OpensUpward ? VerticalAlignment.Bottom : VerticalAlignment.Top;
        // The submenu holds its place while hidden, so the popup keeps one size and its placement stays put.
        _sub.Visibility = _pages ? Visibility.Collapsed : Visibility.Hidden;
        _popup.IsOpen = true;
        _menu.RenderTransformOrigin = new Point(.5, OpensUpward ? 1 : 0);
        if (AiUi.Moving(this))
        {
            _menu.Opacity = 0;
            _menuScale.ScaleX = _menuScale.ScaleY = .97;
            _menuShift.Y = OpensUpward ? 4 : -4;
            Tween.To(_menu, OpacityProperty, 1, .12, ArcEasing.Standard);
            Tween.To(_menuScale, ScaleTransform.ScaleXProperty, 1, .16, ArcEasing.Enter);
            Tween.To(_menuScale, ScaleTransform.ScaleYProperty, 1, .16, ArcEasing.Enter);
            Tween.To(_menuShift, TranslateTransform.YProperty, 0, .16, ArcEasing.Enter);
        }
        else { Tween.Set(_menu, OpacityProperty, 1); }
        Dispatcher.BeginInvoke(() => { if (IsOpen) { var current = _rows.Children.OfType<NativeButton>().FirstOrDefault(row => (row.Tag as string) == Chosen?.Id) ?? _rows.Children.OfType<NativeButton>().FirstOrDefault(); current?.Focus(); if (current is not null) { MoveHighlight(current, false); } } }, DispatcherPriority.Input);
    }

    private void BuildMenu()
    {
        _rows.Children.Clear();
        var chosen = Chosen;
        foreach (var model in FeaturedModels) { _rows.Children.Add(ModelRow(model, chosen, _rows)); }
        bool effort = chosen?.Efforts is { Count: > 0 }, more = OtherModels.Count > 0;
        if (effort || more) { _rows.Children.Add(Divider()); }
        if (effort) { _rows.Children.Add(ExtraRow("Effort", Lucide.Gauge, ShownEffort is { } level ? ModelEffortText.Label(level) : "", Sub.Effort)); }
        if (more) { _rows.Children.Add(ExtraRow("More models", Lucide.Layers, "", Sub.More)); }
    }

    private static Border Divider()
    {
        var line = new Border { Height = 1, Margin = new Thickness(10, 6, 10, 6) };
        line.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.BorderSubtle");
        return line;
    }

    private void Subs()
    {
        _subEffort.Children.Clear();
        _subMore.Children.Clear();
        var chosen = Chosen;
        if (chosen?.Efforts is { Count: > 0 } levels)
        {
            foreach (var level in levels) { _subEffort.Children.Add(EffortRow(level, chosen)); }
        }
        var others = OtherModels;
        var order = Providers?.Select(provider => provider.Id).ToList() ?? others.Select(model => model.Provider).Distinct().ToList();
        foreach (string provider in order)
        {
            var group = others.Where(model => model.Provider == provider).ToList();
            if (group.Count == 0) { continue; }
            var label = AiUi.Line(Providers?.FirstOrDefault(item => item.Id == provider)?.Name ?? provider, 12, 16.8, "TextMuted");
            label.Margin = new Thickness(10, 6, 10, 4);
            _subMore.Children.Add(label);
            foreach (var model in group) { _subMore.Children.Add(ModelRow(model, chosen, _subMore, Providers?.FirstOrDefault(item => item.Id == provider)?.Mark)); }
        }
    }

    private ToolbarButton Row(string tag, UIElement content, double height, Action click, Panel owner)
    {
        var row = new ToolbarButton(18) { Tag = tag, Height = height, Padding = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, Content = content, Roles = _ => (ReferenceEquals(owner, _rows) ? null : "Transparent", null, "Foreground"), FocusVisualStyle = null };
        row.Click += (_, _) => click();
        row.MouseEnter += (_, _) =>
        {
            if (ReferenceEquals(owner, _rows))
            {
                MoveHighlight(row, true);
                // Leaving a submenu row waits a moment, so a diagonal pointer path does not close it.
                if ((string)row.Tag is not ("Effort" or "More models")) { Pend(Sub.None); }
            }
        };
        row.GotKeyboardFocus += (_, _) => { if (ReferenceEquals(owner, _rows)) { MoveHighlight(row, false); } };
        return row;
    }

    private ToolbarButton ModelRow(PickerModel model, PickerModel? chosen, Panel owner, AiMark? fallback = null)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var mark = AiUi.Mark(model.Mark ?? fallback, 18, model.Name);
        mark.HorizontalAlignment = HorizontalAlignment.Left;
        mark.VerticalAlignment = VerticalAlignment.Center;
        mark.Margin = new Thickness(10, 0, 0, 0);
        grid.Children.Add(mark);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(AiUi.Line(model.Name, 14, 18.2, "Foreground", true));
        if (!string.IsNullOrEmpty(model.Blurb)) { var blurb = AiUi.Line(model.Blurb, 12, 16.2, "TextSecondary"); blurb.Margin = new Thickness(0, 1, 0, 0); text.Children.Add(blurb); }
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        bool on = model.Id == chosen?.Id;
        if (on)
        {
            var check = AiUi.Glyph(Lucide.Check, 16, "Foreground");
            check.Margin = new Thickness(0, 0, 10, 0);
            Grid.SetColumn(check, 2);
            grid.Children.Add(check);
        }
        var row = Row(model.Id, grid, RowHeight, () => Pick(model), owner);
        AutomationProperties.SetName(row, $"{model.Name}{(on ? ", checked" : "")}");
        return row;
    }

    private ToolbarButton ExtraRow(string label, string glyph, string value, Sub sub)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
        var icon = AiUi.Glyph(glyph, 16, "TextSecondary");
        icon.HorizontalAlignment = HorizontalAlignment.Left;
        icon.Margin = new Thickness(11, 0, 0, 0);
        grid.Children.Add(icon);
        var text = AiUi.Line(label, 14, 19.6);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        if (value.Length > 0) { var quiet = AiUi.Line(value, 14, 19.6, "TextMuted", false, false); Grid.SetColumn(quiet, 2); grid.Children.Add(quiet); }
        var chevron = AiUi.Glyph(Lucide.ChevronRight, 14, "TextMuted");
        Grid.SetColumn(chevron, 3);
        grid.Children.Add(chevron);
        var row = Row(label, grid, ExtraHeight, () => ShowSub(sub), _rows);
        row.MouseEnter += (_, _) => Pend(sub);
        AutomationProperties.SetName(row, value.Length > 0 ? $"{label}: {value}" : label);
        return row;
    }

    private ToolbarButton EffortRow(ModelEffort level, PickerModel model)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        var head = new StackPanel { Orientation = Orientation.Horizontal };
        head.Children.Add(AiUi.Line(ModelEffortText.Label(level), 14, 18.2, "Foreground", true, false));
        if (level == model.DefaultEffort) { var note = AiUi.Line("Default", 12, 18.2, "TextMuted", false, false); note.Margin = new Thickness(6, 0, 0, 0); head.Children.Add(note); }
        text.Children.Add(head);
        text.Children.Add(AiUi.Line(ModelEffortText.Note(level), 12, 16.2, "TextSecondary"));
        grid.Children.Add(text);
        bool on = level == ShownEffort;
        if (on)
        {
            var check = AiUi.Glyph(Lucide.Check, 16, "Foreground");
            check.Margin = new Thickness(0, 0, 10, 0);
            Grid.SetColumn(check, 1);
            grid.Children.Add(check);
        }
        var row = Row(ModelEffortText.Label(level), grid, 50, () => PickEffort(level), _subEffort);
        AutomationProperties.SetName(row, $"{ModelEffortText.Label(level)}{(on ? ", checked" : "")}");
        return row;
    }

    private void Pend(Sub sub)
    {
        if (_pages) { return; }
        _pending = sub;
        _subOpen.Stop();
        if (sub != _openSub) { _subOpen.Start(); }
    }

    private void ShowSub(Sub sub)
    {
        _openSub = sub;
        _subEffort.Visibility = sub == Sub.Effort ? Visibility.Visible : Visibility.Hidden;
        _subMore.Visibility = sub == Sub.More ? Visibility.Visible : Visibility.Hidden;
        if (_pages) { Page(sub); return; }
        var want = sub == Sub.None ? Visibility.Hidden : Visibility.Visible;
        if (_sub.Visibility != want)
        {
            _sub.Visibility = want;
            if (want == Visibility.Visible && AiUi.Moving(this)) { _sub.Opacity = 0; Tween.To(_sub, OpacityProperty, 1, .12, ArcEasing.Standard); }
            else { Tween.Set(_sub, OpacityProperty, 1); }
        }
    }

    // Below 640 the submenus become pages inside the same menu with a Back row.
    private void Page(Sub sub)
    {
        _pageHost.Children.Clear();
        _highlight.Opacity = 0;
        _highlightPlaced = false;
        if (sub == Sub.None)
        {
            _pageHost.Children.Add(_highlight);
            _pageHost.Children.Add(_rows);
            Slide(1);
            return;
        }
        var page = new StackPanel();
        var back = new Grid();
        back.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        back.ColumnDefinitions.Add(new ColumnDefinition());
        var icon = AiUi.Glyph(Lucide.ChevronLeft, 16, "TextSecondary");
        back.Children.Add(icon);
        var label = AiUi.Line(sub == Sub.Effort ? "Effort" : "More models", 14, 19.6, "Foreground", true);
        Grid.SetColumn(label, 1);
        back.Children.Add(label);
        var row = new ToolbarButton(18) { Tag = "Back", Height = ExtraHeight, Margin = new Thickness(6, 6, 6, 0), Padding = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, Content = back, Roles = self => (self.IsMouseOver ? "SurfaceMuted" : null, null, "Foreground") };
        row.Click += (_, _) => ShowSub(Sub.None);
        AutomationProperties.SetName(row, "Back");
        page.Children.Add(row);
        var body = sub == Sub.Effort ? _subEffort : _subMore;
        (VisualTreeHelper.GetParent(body) as Panel)?.Children.Remove(body);
        (body.Parent as Panel)?.Children.Remove(body);
        body.Visibility = Visibility.Visible;
        page.Children.Add(body);
        _pageHost.Children.Add(page);
        Slide(-1);
        Dispatcher.BeginInvoke(() => row.Focus(), DispatcherPriority.Input);
    }

    private void Slide(int direction)
    {
        if (!AiUi.Moving(this)) { return; }
        foreach (FrameworkElement child in _pageHost.Children)
        {
            if (ReferenceEquals(child, _highlight)) { continue; }
            var shift = new TranslateTransform(8 * -direction, 0);
            child.RenderTransform = shift;
            child.Opacity = 0;
            Tween.To(child, OpacityProperty, 1, .16, ArcEasing.Enter);
            Tween.To(shift, TranslateTransform.XProperty, 0, .16, ArcEasing.Enter);
        }
    }

    // One highlight glides between rows for the pointer and jumps for the keyboard.
    private void MoveHighlight(FrameworkElement row, bool glide)
    {
        if (!_rows.IsAncestorOf(row) || !_pageHost.Children.Contains(_rows)) { return; }
        var at = row.TranslatePoint(new Point(), _pageHost);
        _highlight.Height = row.ActualHeight > 0 ? row.ActualHeight : row.Height;
        _highlight.Margin = new Thickness(6, 0, 0, 0);
        if (glide && _highlightPlaced && AiUi.Moving(this)) { Tween.Spring(_highlightAt, TranslateTransform.YProperty, at.Y, Tween.Snappy); }
        else { Tween.Set(_highlightAt, TranslateTransform.YProperty, at.Y); }
        _highlightPlaced = true;
        Tween.Set(_highlight, OpacityProperty, 1);
    }

    private void Pick(PickerModel model)
    {
        SetCurrentValue(ValueProperty, model.Id);
        ModelChosen?.Invoke(this, new ModelChosenEventArgs(model));
        Close();
    }

    private void PickEffort(ModelEffort level)
    {
        bool changed = Effort != level;
        SetCurrentValue(EffortProperty, level);
        if (changed) { EffortChanged?.Invoke(this, EventArgs.Empty); }
        Close();
    }

    private void Close()
    {
        SetCurrentValue(IsOpenProperty, false);
        _pill?.Focus();
    }

    private void OnMenuKey(object sender, KeyEventArgs e) { if (HandleKey(e.Key)) { e.Handled = true; } }

    private StackPanel Current(Sub sub) => sub switch { Sub.Effort => _subEffort, Sub.More => _subMore, _ => _rows };

    private bool HandleKey(Key key)
    {
        var rows = Current(_openSub).Children.OfType<NativeButton>().ToList();
        int at = rows.FindIndex(row => row.IsKeyboardFocused);
        switch (key)
        {
            case Key.Down: Step(rows, at, 1); return true;
            case Key.Up: Step(rows, at, -1); return true;
            case Key.Right:
                if (_openSub == Sub.None && at >= 0 && (rows[at].Tag as string) is "Effort" or "More models")
                {
                    ShowSub((rows[at].Tag as string) == "Effort" ? Sub.Effort : Sub.More);
                    var first = Current(_openSub).Children.OfType<NativeButton>().FirstOrDefault();
                    first?.Focus();
                    return true;
                }
                return false;
            case Key.Left:
                if (_openSub != Sub.None)
                {
                    var from = _openSub == Sub.Effort ? "Effort" : "More models";
                    ShowSub(Sub.None);
                    _rows.Children.OfType<NativeButton>().FirstOrDefault(row => (row.Tag as string) == from)?.Focus();
                    return true;
                }
                return false;
            case Key.Escape: Close(); return true;
            case Key.Enter or Key.Space:
                if (at >= 0) { rows[at].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); return true; }
                return false;
            default:
                // Type a letter to jump to the next model whose name starts with it.
                if (key is >= Key.A and <= Key.Z)
                {
                    char letter = (char)('a' + (key - Key.A));
                    var models = rows.Where(row => AutomationProperties.GetName(row).StartsWith(letter.ToString(), StringComparison.OrdinalIgnoreCase)).ToList();
                    if (models.Count > 0) { (models.FirstOrDefault(row => rows.IndexOf(row) > at) ?? models[0]).Focus(); return true; }
                }
                return false;
        }
    }

    private static void Step(List<NativeButton> rows, int at, int by)
    {
        if (rows.Count == 0) { return; }
        rows[((at < 0 ? (by > 0 ? -1 : 0) : at) + by + rows.Count) % rows.Count].Focus();
    }
}
