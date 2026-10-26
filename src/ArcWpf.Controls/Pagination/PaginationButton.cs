using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ArcWpf.Controls;

// Native Button keeps mouse capture, Tab, Enter/Space and Invoke automation.
internal sealed class PaginationButton : System.Windows.Controls.Button
{
    private static readonly DependencyProperty CurrentBrushProperty = DependencyProperty.Register("CurrentBrush", typeof(Brush), typeof(PaginationButton), new PropertyMetadata(null, ResourceChanged));
    private static readonly DependencyProperty HoverBrushProperty = DependencyProperty.Register("HoverBrush", typeof(Brush), typeof(PaginationButton), new PropertyMetadata(null, ResourceChanged));
    private readonly Pagination _owner;
    private readonly OwnedMotion _motion = new();
    private readonly TranslateTransform _position = new();
    private readonly ScaleTransform _scale = new(1, 1);
    private Border? _surface;
    private ContentPresenter? _presenter;
    private SolidColorBrush? _background, _foreground;
    private bool _selected;
    internal int Number { get; }
    internal PaginationChangeOrigin Origin { get; }
    internal bool IsAnimating => _motion.IsRunning;
    internal Brush DisplayForeground => _foreground ?? Foreground;
    private bool Full => IsLoaded && IsVisible && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;

    internal PaginationButton(Pagination owner, int number, PaginationChangeOrigin origin)
    {
        _owner = owner; Number = number; Origin = origin;
        SetResourceReference(StyleProperty, "ArcWpf.Pagination.Button");
        SetResourceReference(CurrentBrushProperty, "ArcWpf.Brush.Foreground");
        SetResourceReference(HoverBrushProperty, "ArcWpf.Brush.SurfaceMuted");
        RenderTransformOrigin = new Point(.5, .5);
        var transform = new TransformGroup(); transform.Children.Add(_scale); transform.Children.Add(_position); RenderTransform = transform;
        if (origin == PaginationChangeOrigin.PageButton) Content = number.ToString(CultureInfo.CurrentCulture);
        else Content = new Path
        {
            Width = 16, Height = 16, StrokeThickness = 1.5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            Data = Geometry.Parse(origin == PaginationChangeOrigin.Previous ? "M 10,4 L 6,8 L 10,12" : "M 6,4 L 10,8 L 6,12")
        };
        AutomationProperties.SetName(this, origin == PaginationChangeOrigin.Previous ? "Previous page" : origin == PaginationChangeOrigin.Next ? "Next page" : $"Page {number}");
        Loaded += (_, _) => UpdateAppearance(false);
        Unloaded += (_, _) => Stop();
    }
    protected override void OnClick()
    {
        base.OnClick();
        int target = Origin == PaginationChangeOrigin.Previous ? _owner.Page - 1 : Origin == PaginationChangeOrigin.Next ? (_owner.Page == int.MaxValue ? _owner.Page : _owner.Page + 1) : Number;
        _owner.Request(target, Origin);
    }
    internal void SetSelected(bool selected)
    {
        if (_selected == selected) return;
        _selected = selected;
        AutomationProperties.SetItemStatus(this, selected ? "Current page" : string.Empty);
        UpdateAppearance(true);
    }
    private static void ResourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((PaginationButton)d).UpdateAppearance(false);
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_motion is null) return;
        if (e.Property == IsMouseOverProperty || e.Property == IsPressedProperty || e.Property == IsEnabledProperty || e.Property == ForegroundProperty || e.Property == BackgroundProperty)
            UpdateAppearance(true);
        if (e.Property == ArcMotion.LevelProperty) Settle();
    }
    public override void OnApplyTemplate()
    {
        _motion.Stop(); base.OnApplyTemplate();
        _surface = GetTemplateChild("Surface") as Border;
        _presenter = GetTemplateChild("Presenter") as ContentPresenter;
        _background = _foreground = null;
        UpdateAppearance(false);
    }
    private void UpdateAppearance(bool animate)
    {
        if (_surface is null || _presenter is null) return;
        bool moving = animate && Full;
        bool hover = IsMouseOver && IsEnabled;
        Paint(_surface, Border.BackgroundProperty, hover && !_selected ? (Brush?)GetValue(HoverBrushProperty) : Background, ref _background, moving);
        Paint(_presenter, TextElement.ForegroundProperty, hover || _selected ? (Brush?)GetValue(CurrentBrushProperty) : Foreground, ref _foreground, moving);
        if (Content is Path arrow) arrow.Stroke = _foreground ?? Foreground;
        _motion.Double(_surface, OpacityProperty, IsEnabled ? 1 : .4, .16, moving);
        double scale = Origin != PaginationChangeOrigin.PageButton && IsPressed && IsEnabled && Full ? .96 : 1;
        _motion.Double(_scale, ScaleTransform.ScaleXProperty, scale, IsPressed ? .1 : .58, moving, spring: !IsPressed);
        _motion.Double(_scale, ScaleTransform.ScaleYProperty, scale, IsPressed ? .1 : .58, moving, spring: !IsPressed);
    }
    private void Paint(DependencyObject target, DependencyProperty property, Brush? source, ref SolidColorBrush? owned, bool animate)
    {
        if (source is not SolidColorBrush color) { target.SetValue(property, source); owned = null; return; }
        if (owned is null) { owned = new SolidColorBrush(color.Color); target.SetValue(property, owned); }
        owned.Opacity = color.Opacity;
        _motion.Color(owned, color.Color, .16, animate);
    }
    internal Point LayoutPosition(Visual relative)
    {
        var position = TranslatePoint(new Point(), (UIElement)relative);
        return new Point(position.X - _position.X, position.Y - _position.Y);
    }
    internal void MoveFrom(double x, double y)
    {
        // Capture current screen position before layout, then animate only the private translation.
        _motion.Double(_position, TranslateTransform.XProperty, x, 0, false);
        _motion.Double(_position, TranslateTransform.YProperty, y, 0, false);
        _motion.Double(_position, TranslateTransform.XProperty, 0, .4, true, spring: true);
        _motion.Double(_position, TranslateTransform.YProperty, 0, .4, true, spring: true);
        _motion.Double(this, OpacityProperty, 1, .24, true, enter: true);
    }
    internal void Enter(double x)
    {
        _motion.Double(this, OpacityProperty, 0, 0, false);
        MoveFrom(x, 0);
    }
    internal void Stop() { _motion.Stop(); }
    internal void Settle()
    {
        Stop(); _position.X = _position.Y = 0; Opacity = 1;
        UpdateAppearance(false);
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new ButtonAutomationPeer(this);
}
