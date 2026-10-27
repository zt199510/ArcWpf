using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace ArcWpf.Controls;

public enum BadgeTone { Neutral, Success, Info, Warning, Danger }
public enum BadgeSize { Small, Medium }

[TemplatePart(Name = "PART_Frame", Type = typeof(Border))]
[TemplatePart(Name = "PART_Body", Type = typeof(BadgeBody))]
[TemplatePart(Name = "PART_Label", Type = typeof(ContentPresenter))]
[TemplatePart(Name = "PART_Icon", Type = typeof(ContentPresenter))]
public class Badge : ContentControl
{
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(nameof(Tone), typeof(BadgeTone), typeof(Badge), new PropertyMetadata(BadgeTone.Neutral), v => v is BadgeTone t && Enum.IsDefined(t));
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(BadgeSize), typeof(Badge), new PropertyMetadata(BadgeSize.Medium), v => v is BadgeSize s && Enum.IsDefined(s));
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(object), typeof(Badge), new PropertyMetadata(null, IconChanged));
    public static readonly DependencyProperty IconKeyProperty = DependencyProperty.Register(nameof(IconKey), typeof(object), typeof(Badge), new PropertyMetadata(null, IconChanged));
    public static readonly DependencyProperty IconBrushProperty = DependencyProperty.Register(nameof(IconBrush), typeof(Brush), typeof(Badge), new PropertyMetadata(null));
    public Brush? IconBrush { get => (Brush?)GetValue(IconBrushProperty); set => SetValue(IconBrushProperty, value); }
    public BadgeTone Tone { get => (BadgeTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
    public BadgeSize Size { get => (BadgeSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public object? IconKey { get => GetValue(IconKeyProperty); set => SetValue(IconKeyProperty, value); }

    private Border? _frame;
    private BadgeBody? _body;
    private ContentPresenter? _label, _icon;
    private Canvas? _labelGhost, _iconGhost;
    private FrameworkElement? _iconSlot;
    private object? _shownIconKey;
    private readonly OwnedMotion _textMotion = new(), _iconMotion = new(), _colors = new();
    private SolidColorBrush? _background, _border, _foreground, _iconForeground;
    private bool _ready;
    internal bool IsMotionRunning => _textMotion.IsRunning || _iconMotion.IsRunning || _colors.IsRunning || _body?.IsRunning == true;
    private bool CanAnimate => _ready && IsLoaded && IsVisible && ArcMotion.GetLevel(this) != ArcMotionLevel.Off;
    private bool Full => ArcMotion.GetLevel(this) == ArcMotionLevel.Full;

    static Badge()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Badge), new FrameworkPropertyMetadata(typeof(Badge)));
        FocusableProperty.OverrideMetadata(typeof(Badge), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Badge), new FrameworkPropertyMetadata(false));
    }
    public Badge()
    {
        Loaded += (_, _) => { UpdateColors(false); _ready = true; };
        Unloaded += (_, _) => { _ready = false; Stop(); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) Stop(); };
    }
    public override void OnApplyTemplate()
    {
        Release(); base.OnApplyTemplate();
        _frame = GetTemplateChild("PART_Frame") as Border;
        _body = GetTemplateChild("PART_Body") as BadgeBody;
        _label = GetTemplateChild("PART_Label") as ContentPresenter;
        _icon = GetTemplateChild("PART_Icon") as ContentPresenter;
        _labelGhost = GetTemplateChild("PART_LabelGhost") as Canvas;
        _iconGhost = GetTemplateChild("PART_IconGhost") as Canvas;
        _iconSlot = GetTemplateChild("PART_IconSlot") as FrameworkElement;
        if (_frame is not null) _frame.SizeChanged += FrameSizeChanged;
        if (_label is not null) _label.Content = Content;
        UpdateIcon(false); UpdateColors(false); _ready = IsLoaded;
    }
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    { Release(); base.OnTemplateChanged(oldTemplate, newTemplate); }
    private void Release()
    {
        Stop(); if (_frame is not null) _frame.SizeChanged -= FrameSizeChanged;
        _frame = null; _body = null; _label = _icon = null; _labelGhost = _iconGhost = null; _iconSlot = null;
        _background = _border = _foreground = _iconForeground = null; _ready = false;
    }
    private void FrameSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_frame is null) return;
        _frame.CornerRadius = new CornerRadius(e.NewSize.Height / 2);
        _frame.Clip = new RectangleGeometry(new Rect(e.NewSize), e.NewSize.Height / 2, e.NewSize.Height / 2);
    }
    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        if (_label is null) return;
        bool primitive = IsText(oldContent) && IsText(newContent) && ContentTemplate is null && ContentTemplateSelector is null;
        if (CanAnimate && Full && primitive) _body?.RequestMorph();
        Swap(_label, _labelGhost, _textMotion, newContent, false, CanAnimate && primitive);
    }
    private static bool IsText(object? value) => value is null or string or char or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;
    private static void IconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Badge)d).UpdateIcon(true);
    private void UpdateIcon(bool allowMotion)
    {
        if (_icon is null) return;
        object? key = Icon is null ? null : IconKey ?? Icon.GetType();
        bool changed = !Equals(key, _shownIconKey);
        bool swap = allowMotion && CanAnimate && changed && _icon.Content is not null && Icon is not null;
        if (allowMotion && CanAnimate && Full && changed) _body?.RequestMorph();
        Swap(_icon, _iconGhost, _iconMotion, Icon, true, swap);
        _shownIconKey = key;
        if (_iconSlot is not null) _iconSlot.Visibility = Icon is null ? Visibility.Collapsed : Visibility.Visible;
    }
    private void Swap(ContentPresenter presenter, Canvas? ghosts, OwnedMotion motion, object? content, bool icon, bool animate)
    {
        // Capture before changing the presenter: visual snapshots cannot enter the automation tree.
        Image? outgoing = animate && ghosts is not null ? Snapshot(presenter) : null;
        motion.Stop(); ghosts?.Children.Clear(); Reset(presenter);
        presenter.Content = content;
        if (!animate) return;
        double enter = Full ? (icon ? .26 : .24) : .12, exit = Full ? .16 : .12;
        if (outgoing is not null && ghosts is not null)
        {
            ghosts.Children.Add(outgoing);
            AnimateSwap(outgoing, motion, false, icon, exit, () => ghosts.Children.Remove(outgoing));
        }
        presenter.Opacity = 0;
        if (Full)
        {
            presenter.RenderTransform = icon ? new ScaleTransform(.6, .6) : new TranslateTransform(0, FontSize * .3);
            presenter.Effect = new BlurEffect { Radius = icon ? 2 : 4 };
        }
        AnimateSwap(presenter, motion, true, icon, enter);
    }
    private void AnimateSwap(FrameworkElement element, OwnedMotion motion, bool entering, bool icon, double duration, Action? done = null)
    {
        element.RenderTransformOrigin = new Point(.5, .5);
        motion.Double(element, OpacityProperty, entering ? 1 : 0, duration, true, spring: icon && entering && Full, completed: done, enter: entering);
        if (!Full) return;
        if (icon)
        {
            if (element.RenderTransform is not ScaleTransform) element.RenderTransform = new ScaleTransform(1, 1);
            var scale = (ScaleTransform)element.RenderTransform;
            motion.Double(scale, ScaleTransform.ScaleXProperty, entering ? 1 : .6, duration, true, spring: entering);
            motion.Double(scale, ScaleTransform.ScaleYProperty, entering ? 1 : .6, duration, true, spring: entering);
        }
        else
        {
            if (element.RenderTransform is not TranslateTransform) element.RenderTransform = new TranslateTransform();
            motion.Double(element.RenderTransform, TranslateTransform.YProperty, entering ? 0 : -FontSize * .3, duration, true, enter: entering);
        }
        if (element.Effect is not BlurEffect) element.Effect = new BlurEffect();
        motion.Double(element.Effect, BlurEffect.RadiusProperty, entering ? 0 : 2, duration, true, spring: icon && entering, enter: entering);
    }
    private static Image? Snapshot(FrameworkElement element)
    {
        if (element.ActualWidth <= 0 || element.ActualHeight <= 0) return null;
        var dpi = VisualTreeHelper.GetDpi(element);
        var bitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(element.ActualWidth * dpi.DpiScaleX)), Math.Max(1, (int)Math.Ceiling(element.ActualHeight * dpi.DpiScaleY)), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        var drawing = new DrawingVisual();
        using (var dc = drawing.RenderOpen())
            dc.DrawRectangle(new VisualBrush(element) { Stretch = Stretch.Fill }, null, new Rect(element.RenderSize));
        bitmap.Render(drawing); bitmap.Freeze();
        return new Image { Source = bitmap, Width = element.ActualWidth, Height = element.ActualHeight, IsHitTestVisible = false };
    }
    private static void Reset(ContentPresenter? presenter)
    { if (presenter is not null) { presenter.Opacity = 1; presenter.RenderTransform = Transform.Identity; presenter.Effect = null; } }
    private void Stop()
    {
        _body?.Stop(); _textMotion.Stop(); _iconMotion.Stop(); _colors.Stop();
        _labelGhost?.Children.Clear(); _iconGhost?.Children.Clear(); Reset(_label); Reset(_icon);
    }
    private void UpdateColors(bool animate)
    {
        if (_frame is null) return;
        double duration = Full ? .24 : .12;
        Paint(Background, ref _background, b => _frame.Background = b);
        Paint(BorderBrush, ref _border, b => _frame.BorderBrush = b);
        Paint(Foreground, ref _foreground, b => _frame.SetValue(TextElement.ForegroundProperty, b));
        if (_iconSlot is not null) Paint(IconBrush ?? Foreground, ref _iconForeground, b => _iconSlot.SetValue(TextElement.ForegroundProperty, b));
        void Paint(Brush source, ref SolidColorBrush? owned, Action<Brush> apply)
        {
            if (source is not SolidColorBrush solid) { apply(source); return; }
            owned ??= new SolidColorBrush(solid.Color); apply(owned); _colors.Color(owned, solid.Color, duration, animate);
        }
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_colors is null) return;
        if (e.Property == BackgroundProperty || e.Property == ForegroundProperty || e.Property == BorderBrushProperty || e.Property == IconBrushProperty) UpdateColors(CanAnimate);
        if (e.Property == ArcMotion.LevelProperty) { Stop(); UpdateColors(false); }
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new BadgeAutomationPeer(this);
    private sealed class BadgeAutomationPeer(Badge owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(Badge);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;
        protected override string GetNameCore()
        {
            string explicitName = base.GetNameCore();
            return string.IsNullOrEmpty(explicitName) ? Convert.ToString(owner.Content, CultureInfo.CurrentCulture) ?? "" : explicitName;
        }
        protected override List<AutomationPeer>? GetChildrenCore() => null;
    }
}


