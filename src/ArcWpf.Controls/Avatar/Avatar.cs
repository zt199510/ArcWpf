using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace ArcWpf.Controls;

public enum AvatarSize { Small, Medium, Large, ExtraLarge }
public enum AvatarStatus { None, Online, Offline }

[TemplatePart(Name = "PART_Face", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Image", Type = typeof(Image))]
[TemplatePart(Name = "PART_Initials", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_Status", Type = typeof(Border))]
[TemplatePart(Name = "PART_StatusExit", Type = typeof(Border))]
public class Avatar : Control
{
    public static readonly DependencyProperty DisplayNameProperty = DependencyProperty.Register(nameof(DisplayName), typeof(string), typeof(Avatar), new PropertyMetadata("", NameChanged));
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(ImageSource), typeof(Avatar), new PropertyMetadata(null, SourceChanged));
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(AvatarSize), typeof(Avatar), new PropertyMetadata(AvatarSize.Medium), v => v is AvatarSize s && Enum.IsDefined(s));
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(nameof(Status), typeof(AvatarStatus), typeof(Avatar), new PropertyMetadata(AvatarStatus.None, (d, _) => ((Avatar)d).UpdateStatus(true)), v => v is AvatarStatus s && Enum.IsDefined(s));
    private static readonly DependencyPropertyKey InitialsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Initials), typeof(string), typeof(Avatar), new PropertyMetadata(""));
    public static readonly DependencyProperty InitialsProperty = InitialsPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey IsImageLoadingPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsImageLoading), typeof(bool), typeof(Avatar), new PropertyMetadata(false));
    public static readonly DependencyProperty IsImageLoadingProperty = IsImageLoadingPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey HasImageErrorPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasImageError), typeof(bool), typeof(Avatar), new PropertyMetadata(false));
    public static readonly DependencyProperty HasImageErrorProperty = HasImageErrorPropertyKey.DependencyProperty;
    public string DisplayName { get => (string)GetValue(DisplayNameProperty); set => SetValue(DisplayNameProperty, value); }
    public ImageSource? Source { get => (ImageSource?)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    public AvatarSize Size { get => (AvatarSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public AvatarStatus Status { get => (AvatarStatus)GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
    public string Initials => (string)GetValue(InitialsProperty);
    public bool IsImageLoading => (bool)GetValue(IsImageLoadingProperty);
    public bool HasImageError => (bool)GetValue(HasImageErrorProperty);

    private FrameworkElement? _face, _layout;
    private Image? _image;
    private TextBlock? _initials;
    private Border? _status, _exit;
    private BitmapSource? _observed;
    private ImageSource? _failedSource;
    private readonly OwnedMotion _photoMotion = new(), _statusMotion = new();
    private AvatarStatus _shownStatus;
    internal bool IsMotionRunning => _photoMotion.IsRunning || _statusMotion.IsRunning;
    private bool Animate => IsLoaded && IsVisible && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;

    static Avatar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Avatar), new FrameworkPropertyMetadata(typeof(Avatar)));
        FocusableProperty.OverrideMetadata(typeof(Avatar), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Avatar), new FrameworkPropertyMetadata(false));
    }
    public Avatar()
    {
        Loaded += (_, _) => { Observe(); UpdatePhoto(false); UpdateStatus(false); };
        Unloaded += (_, _) => { Unobserve(); Stop(); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) Stop(); else { UpdatePhoto(false); UpdateStatus(false); } };
    }
    private static void NameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var parts = ((string?)e.NewValue ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Take(2);
        ((Avatar)d).SetValue(InitialsPropertyKey, string.Concat(parts.Select(p => StringInfo.GetNextTextElement(p).ToUpperInvariant())));
    }
    private static void SourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var avatar = (Avatar)d; avatar._failedSource = null; avatar.Unobserve(); avatar.Observe(); avatar.UpdatePhoto(false);
    }
    public override void OnApplyTemplate()
    {
        Release(); base.OnApplyTemplate();
        _layout = GetTemplateChild("PART_Layout") as FrameworkElement; UpdateDiameter();
        _face = GetTemplateChild("PART_Face") as FrameworkElement;
        _image = GetTemplateChild("PART_Image") as Image;
        _initials = GetTemplateChild("PART_Initials") as TextBlock;
        _status = GetTemplateChild("PART_Status") as Border;
        _exit = GetTemplateChild("PART_StatusExit") as Border;
        if (_face is not null) _face.SizeChanged += FaceSizeChanged;
        if (_image is not null) _image.ImageFailed += ImageFailed;
        Observe(); UpdatePhoto(false); UpdateStatus(false);
    }
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    { Release(); base.OnTemplateChanged(oldTemplate, newTemplate); }
    private void Release()
    {
        Unobserve(); Stop();
        if (_face is not null) _face.SizeChanged -= FaceSizeChanged;
        if (_image is not null) _image.ImageFailed -= ImageFailed;
        _layout = null; _face = null; _image = null; _initials = null; _status = _exit = null; _shownStatus = AvatarStatus.None;
    }
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    { base.OnRenderSizeChanged(sizeInfo); UpdateDiameter(); }
    private void UpdateDiameter()
    {
        if (_layout is null) return;
        double diameter = Math.Min(ActualWidth, ActualHeight);
        _layout.Width = _layout.Height = diameter > 0 ? diameter : double.NaN;
    }
    private void FaceSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_face is not null) _face.Clip = new EllipseGeometry(new Point(e.NewSize.Width / 2, e.NewSize.Height / 2), Math.Min(e.NewSize.Width, e.NewSize.Height) / 2, Math.Min(e.NewSize.Width, e.NewSize.Height) / 2);
    }
    private void Observe()
    {
        if (!IsLoaded || _observed is not null || Source is not BitmapSource bitmap || bitmap.IsFrozen) return;
        _observed = bitmap; bitmap.DownloadCompleted += DownloadCompleted; bitmap.DownloadFailed += DownloadFailed; bitmap.DecodeFailed += DownloadFailed;
    }
    private void Unobserve()
    {
        if (_observed is null) return;
        _observed.DownloadCompleted -= DownloadCompleted; _observed.DownloadFailed -= DownloadFailed; _observed.DecodeFailed -= DownloadFailed; _observed = null;
    }
    private void DownloadCompleted(object? sender, EventArgs e)
    { if (ReferenceEquals(sender, Source)) UpdatePhoto(true); }
    private void DownloadFailed(object? sender, ExceptionEventArgs e)
    { if (ReferenceEquals(sender, Source)) Fail(); }
    private void ImageFailed(object? sender, ExceptionRoutedEventArgs e)
    { if (_image?.Source is not null && ReferenceEquals(_image.Source, Source)) Fail(); }
    private void Fail() { _failedSource = Source; UpdatePhoto(true); }
    private void UpdatePhoto(bool transition)
    {
        bool failed = Source is not null && ReferenceEquals(_failedSource, Source);
        bool loading = !failed && Source is BitmapSource { IsDownloading: true };
        SetValue(HasImageErrorPropertyKey, failed); SetValue(IsImageLoadingPropertyKey, loading);
        _photoMotion.Stop();
        if (_image is not null)
        {
            _image.Source = failed ? null : Source;
            // Setting Source may synchronously raise ImageFailed; do not overwrite its fallback state.
            if (!failed && Source is not null && ReferenceEquals(_failedSource, Source)) return;
            _image.Visibility = Source is null || failed ? Visibility.Collapsed : Visibility.Visible;
            _image.Effect = null; _image.Opacity = loading ? 0 : 1;
            if (transition && Animate && !failed && Source is not null && !loading)
            {
                _image.Opacity = 0; var blur = new BlurEffect { Radius = 4 }; _image.Effect = blur;
                _photoMotion.Double(_image, OpacityProperty, 1, .24, true, enter: true);
                _photoMotion.Double(blur, BlurEffect.RadiusProperty, 0, .24, true, enter: true);
            }
        }
        if (_initials is not null)
        {
            _initials.Visibility = Source is null || failed ? Visibility.Visible : Visibility.Collapsed;
            _initials.Opacity = 1;
            if (transition && Animate && failed) { _initials.Opacity = 0; _photoMotion.Double(_initials, OpacityProperty, 1, .24, true, enter: true); }
        }
    }
    private void UpdateStatus(bool transition)
    {
        if (_status is null) return;
        bool animate = transition && Animate && _shownStatus != Status;
        double currentOpacity = _status.Opacity;
        double currentScale = (_status.RenderTransform as ScaleTransform)?.ScaleX ?? 1;
        _statusMotion.Stop();
        if (_exit is not null)
        {
            _exit.Visibility = Visibility.Collapsed;
            if (animate && _shownStatus != AvatarStatus.None)
            {
                SetDotBrush(_exit, _shownStatus); _exit.Visibility = Visibility.Visible; _exit.Opacity = currentOpacity;
                _exit.RenderTransform = new ScaleTransform(currentScale, currentScale);
                DotMotion(_exit, 0, .6, .16, false, () => _exit.Visibility = Visibility.Collapsed);
            }
        }
        _shownStatus = Status;
        _status.Visibility = Status == AvatarStatus.None ? Visibility.Collapsed : Visibility.Visible;
        SetDotBrush(_status, Status);
        _status.Opacity = animate ? 0 : 1; _status.RenderTransform = new ScaleTransform(animate ? .6 : 1, animate ? .6 : 1);
        if (animate && Status != AvatarStatus.None) DotMotion(_status, 1, 1, .26, true);
    }
    private static void SetDotBrush(Border dot, AvatarStatus status) => dot.SetResourceReference(Border.BackgroundProperty, status == AvatarStatus.Online ? "ArcWpf.Brush.AvatarOnline" : "ArcWpf.Brush.TextMuted");
    private void DotMotion(Border dot, double opacity, double scale, double duration, bool spring, Action? done = null)
    {
        _statusMotion.Double(dot, OpacityProperty, opacity, duration, true, spring, done);
        _statusMotion.Double(dot.RenderTransform, ScaleTransform.ScaleXProperty, scale, duration, true, spring);
        _statusMotion.Double(dot.RenderTransform, ScaleTransform.ScaleYProperty, scale, duration, true, spring);
    }
    private void Stop()
    {
        _photoMotion.Stop(); _statusMotion.Stop();
        if (_exit is not null) _exit.Visibility = Visibility.Collapsed;
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_photoMotion is not null && e.Property == ArcMotion.LevelProperty) { Stop(); UpdatePhoto(false); UpdateStatus(false); }
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new AvatarAutomationPeer(this);
    private sealed class AvatarAutomationPeer(Avatar owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(Avatar);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;
        protected override string GetNameCore()
        {
            string name = base.GetNameCore();
            return !string.IsNullOrEmpty(name) ? name : (owner.DisplayName ?? "") + (owner.Status == AvatarStatus.None ? "" : $", {owner.Status.ToString().ToLowerInvariant()}");
        }
        protected override List<AutomationPeer>? GetChildrenCore() => null;
    }
}
