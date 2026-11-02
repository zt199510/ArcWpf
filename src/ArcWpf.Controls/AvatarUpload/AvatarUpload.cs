using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using System.Windows.Input;
using System.IO;
using System.Windows.Media.Imaging;

namespace ArcWpf.Controls;

public class AvatarUpload : Control
{
    public static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register(nameof(ImageSource), typeof(ImageSource), typeof(AvatarUpload), new PropertyMetadata(null, OnImageChanged));
    public static readonly DependencyProperty InitialsProperty = DependencyProperty.Register(nameof(Initials), typeof(string), typeof(AvatarUpload), new PropertyMetadata("A"));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(AvatarUpload), new PropertyMetadata("Upload avatar"));
    public static readonly DependencyProperty IsUploadingProperty = DependencyProperty.Register(nameof(IsUploading), typeof(bool), typeof(AvatarUpload), new PropertyMetadata(false));
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(nameof(Progress), typeof(double), typeof(AvatarUpload), new PropertyMetadata(0d, OnProgressChanged));
    public static readonly DependencyProperty ErrorProperty = DependencyProperty.Register(nameof(Error), typeof(string), typeof(AvatarUpload), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty HintProperty = DependencyProperty.Register(nameof(Hint), typeof(string), typeof(AvatarUpload), new PropertyMetadata("PNG, JPG or WebP · up to 5 MB"));
    public static readonly DependencyProperty AcceptProperty = DependencyProperty.Register(nameof(Accept), typeof(string), typeof(AvatarUpload), new PropertyMetadata(".png;.jpg;.jpeg;.webp"));
    public static readonly DependencyProperty MaxSizeProperty = DependencyProperty.Register(nameof(MaxSize), typeof(long), typeof(AvatarUpload), new PropertyMetadata(5 * 1024 * 1024L));
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(double), typeof(AvatarUpload), new PropertyMetadata(88d));
    public static readonly DependencyProperty OutputSizeProperty = DependencyProperty.Register(nameof(OutputSize), typeof(int), typeof(AvatarUpload), new FrameworkPropertyMetadata(512, null, CoerceOutputSize));
    private static readonly DependencyPropertyKey CroppedImagePropertyKey = DependencyProperty.RegisterReadOnly(nameof(CroppedImage), typeof(BitmapSource), typeof(AvatarUpload), new PropertyMetadata(null));
    public static readonly DependencyProperty CroppedImageProperty = CroppedImagePropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey IsCroppingPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsCropping), typeof(bool), typeof(AvatarUpload), new PropertyMetadata(false, OnCropChanged));
    public static readonly DependencyProperty IsCroppingProperty = IsCroppingPropertyKey.DependencyProperty;
    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(AvatarUpload), new FrameworkPropertyMetadata(1d, OnCropChanged, CoerceZoom));
    public static readonly DependencyProperty CropOffsetXProperty = DependencyProperty.Register(nameof(CropOffsetX), typeof(double), typeof(AvatarUpload), new FrameworkPropertyMetadata(0d, OnCropChanged, CoerceOffset));
    public static readonly DependencyProperty CropOffsetYProperty = DependencyProperty.Register(nameof(CropOffsetY), typeof(double), typeof(AvatarUpload), new FrameworkPropertyMetadata(0d, OnCropChanged, CoerceOffset));
    public ImageSource? ImageSource { get => (ImageSource?)GetValue(ImageSourceProperty); set => SetValue(ImageSourceProperty, value); }
    public string Initials { get => (string)GetValue(InitialsProperty); set => SetValue(InitialsProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public bool IsUploading { get => (bool)GetValue(IsUploadingProperty); set => SetValue(IsUploadingProperty, value); }
    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, Math.Clamp(value, 0, 100)); }
    public string Error { get => (string)GetValue(ErrorProperty); set => SetValue(ErrorProperty, value); }
    public string Hint { get => (string)GetValue(HintProperty); set => SetValue(HintProperty, value); }
    public string Accept { get => (string)GetValue(AcceptProperty); set => SetValue(AcceptProperty, value); }
    public long MaxSize { get => (long)GetValue(MaxSizeProperty); set => SetValue(MaxSizeProperty, value); }
    public double Size { get => (double)GetValue(SizeProperty); set => SetValue(SizeProperty, Math.Clamp(value, 40, 184)); }
    public int OutputSize { get => (int)GetValue(OutputSizeProperty); set => SetValue(OutputSizeProperty, value); }
    public BitmapSource? CroppedImage => (BitmapSource?)GetValue(CroppedImageProperty);
    public bool IsCropping { get => (bool)GetValue(IsCroppingProperty); private set => SetValue(IsCroppingPropertyKey, value); }
    public double Zoom { get => (double)GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }
    public double CropOffsetX { get => (double)GetValue(CropOffsetXProperty); set => SetValue(CropOffsetXProperty, value); }
    public double CropOffsetY { get => (double)GetValue(CropOffsetYProperty); set => SetValue(CropOffsetYProperty, value); }
    public event EventHandler<string>? FileSelected;
    static AvatarUpload() => DefaultStyleKeyProperty.OverrideMetadata(typeof(AvatarUpload), new FrameworkPropertyMetadata(typeof(AvatarUpload)));
    private ImageSource? _pendingImage; private string? _pendingPath; private bool _dragging; private Point _dragStart; private double _dragOriginX, _dragOriginY;
    public AvatarUpload() { AllowDrop = true; Focusable = true; Drop += OnDrop; DragOver += OnDragOver; KeyDown += OnKeyDown; }
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_Button") is System.Windows.Controls.Button button) button.Click += (_, _) => Browse();
        if (GetTemplateChild("PART_Save") is System.Windows.Controls.Button save) save.Click += (_, _) => SaveCrop();
        if (GetTemplateChild("PART_Cancel") is System.Windows.Controls.Button cancel) cancel.Click += (_, _) => CancelCrop();
        if (GetTemplateChild("PART_ZoomOut") is System.Windows.Controls.Button zoomOut) zoomOut.Click += (_, _) => Zoom -= .1;
        if (GetTemplateChild("PART_ZoomIn") is System.Windows.Controls.Button zoomIn) zoomIn.Click += (_, _) => Zoom += .1;
        if (GetTemplateChild("PART_Zoom") is Slider slider) slider.ValueChanged += (_, _) => Zoom = slider.Value;
        if (GetTemplateChild("PART_CropStage") is UIElement stage) { stage.MouseLeftButtonDown += OnCropMouseDown; stage.MouseMove += OnCropMouseMove; stage.MouseLeftButtonUp += OnCropMouseUp; stage.KeyDown += OnKeyDown; }
        UpdateFallbackVisual(); UpdateProgressRing(); UpdateCropVisual();
    }
    private static void OnImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((AvatarUpload)d).UpdateFallbackVisual();
    private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((AvatarUpload)d).UpdateProgressRing();
    private static object CoerceOutputSize(DependencyObject d, object value) => Math.Clamp((int)value, 32, 4096);
    private void UpdateFallbackVisual()
    {
        if (GetTemplateChild("PART_Button") is not DependencyObject root) return;
        void Walk(DependencyObject node)
        {
            if (node is TextBlock text && text.Name == "InitialsText") text.Visibility = ImageSource is null ? Visibility.Visible : Visibility.Collapsed;
            for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(node); i++) Walk(System.Windows.Media.VisualTreeHelper.GetChild(node, i));
        }
        Walk(root);
    }
    private void Browse()
    {
        var dialog = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.webp;*.gif" };
        if (dialog.ShowDialog() != true) return;
        TrySelect(dialog.FileName);
    }
    private void OnDragOver(object sender, DragEventArgs e) { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; }
    private void OnDrop(object sender, DragEventArgs e) { if (e.Data.GetData(DataFormats.FileDrop) is string[] files) { var path = files.FirstOrDefault(IsImageFile); if (path is not null) TrySelect(path); } e.Handled = true; }
    private static bool IsImageFile(string path) => new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    private void TrySelect(string path)
    {
        if (!IsImageFile(path)) { Error = "Select an image file"; return; }
        try {
            if (MaxSize > 0 && new FileInfo(path).Length > MaxSize) { Error = $"Image must be smaller than {FileDropzone.FormatFileSize(MaxSize)}"; return; }
            var image = new System.Windows.Media.Imaging.BitmapImage(new Uri(path)); image.Freeze(); _pendingImage = image; _pendingPath = path; Error = string.Empty; Zoom = 1; CropOffsetX = 0; CropOffsetY = 0; SetValue(IsCroppingPropertyKey, true); UpdateCropVisual();
        } catch (Exception error) { Error = error.Message; }
    }

    private void UpdateProgressRing()
    {
        if (GetTemplateChild("PART_ProgressRing") is System.Windows.Shapes.Ellipse ring) ring.StrokeDashOffset = Math.Clamp(100 - Progress, 0, 100);
    }
    public void SaveCrop()
    {
        if (!IsCropping || _pendingImage is null || _pendingPath is null) return;
        try
        {
            if (_pendingImage is not BitmapSource pending) throw new InvalidOperationException("The selected image cannot be cropped.");
            var cropped = CreateCropBitmap(pending);
            SetValue(CroppedImagePropertyKey, cropped);
            ImageSource = cropped;
            var path = EncodeCrop(cropped, _pendingPath);
            SetValue(IsCroppingPropertyKey, false); _pendingImage = null; _pendingPath = null; FileSelected?.Invoke(this, path); UpdateCropVisual();
        }
        catch (Exception error) { Error = error.Message; }
    }
    public void CancelCrop() { _pendingImage = null; _pendingPath = null; SetValue(IsCroppingPropertyKey, false); Zoom = 1; CropOffsetX = 0; CropOffsetY = 0; UpdateCropVisual(); }
    private static void OnCropChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((AvatarUpload)d).UpdateCropVisual();
    private static object CoerceZoom(DependencyObject d, object value) => Math.Clamp((double)value, 1, 3);
    private static object CoerceOffset(DependencyObject d, object value) => Math.Clamp((double)value, -.25, .25);
    private void UpdateCropVisual()
    {
        if (GetTemplateChild("PART_CropImage") is System.Windows.Controls.Image image)
        {
            image.Source = _pendingImage;
            image.RenderTransform = new TransformGroup { Children = { new ScaleTransform(Zoom, Zoom), new TranslateTransform(CropOffsetX * 248, CropOffsetY * 248) } };
        }
        if (GetTemplateChild("PART_Zoom") is Slider slider && Math.Abs(slider.Value - Zoom) > .001) slider.Value = Zoom;
    }
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsCropping) { if (e.Key is Key.Enter or Key.Space) { Browse(); e.Handled = true; } return; }
        var step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? .1 : .02;
        switch (e.Key) { case Key.Left: CropOffsetX -= step; break; case Key.Right: CropOffsetX += step; break; case Key.Up: CropOffsetY -= step; break; case Key.Down: CropOffsetY += step; break; case Key.Add: case Key.OemPlus: Zoom += .1; break; case Key.Subtract: case Key.OemMinus: Zoom -= .1; break; case Key.D0: Zoom = 1; CropOffsetX = CropOffsetY = 0; break; case Key.Enter: SaveCrop(); break; case Key.Escape: CancelCrop(); break; default: return; }
        e.Handled = true;
    }
    private void OnCropMouseDown(object sender, MouseButtonEventArgs e) { if (!IsCropping) return; _dragging = true; _dragStart = e.GetPosition((IInputElement)sender); _dragOriginX = CropOffsetX; _dragOriginY = CropOffsetY; ((UIElement)sender).CaptureMouse(); e.Handled = true; }
    private void OnCropMouseMove(object sender, MouseEventArgs e) { if (!_dragging) return; var point = e.GetPosition((IInputElement)sender); CropOffsetX = _dragOriginX + (point.X - _dragStart.X) / 248; CropOffsetY = _dragOriginY + (point.Y - _dragStart.Y) / 248; }
    private void OnCropMouseUp(object sender, MouseButtonEventArgs e) { _dragging = false; ((UIElement)sender).ReleaseMouseCapture(); }
    private BitmapSource CreateCropBitmap(BitmapSource source)
    {
        var output = Math.Clamp(OutputSize, 32, 4096);
        var width = source.PixelWidth; var height = source.PixelHeight; var scale = Math.Max(248d / width, 248d / height) * Zoom;
        var cropWidth = Math.Min(width, 248d / scale); var cropHeight = Math.Min(height, 248d / scale);
        var centerX = width / 2d - CropOffsetX * 248d / scale; var centerY = height / 2d - CropOffsetY * 248d / scale;
        var x = Math.Clamp(centerX - cropWidth / 2d, 0, Math.Max(0, width - cropWidth)); var y = Math.Clamp(centerY - cropHeight / 2d, 0, Math.Max(0, height - cropHeight));
        var rect = new Int32Rect((int)Math.Round(x), (int)Math.Round(y), Math.Max(1, (int)Math.Round(cropWidth)), Math.Max(1, (int)Math.Round(cropHeight)));
        var cropped = new CroppedBitmap(source, rect); cropped.Freeze();
        var visual = new DrawingVisual(); using (var context = visual.RenderOpen()) context.DrawImage(cropped, new Rect(0, 0, output, output));
        var rendered = new RenderTargetBitmap(output, output, 96, 96, PixelFormats.Pbgra32); rendered.Render(visual); rendered.Freeze(); return rendered;
    }
    private static string EncodeCrop(BitmapSource bitmap, string originalPath)
    {
        var extension = string.Equals(Path.GetExtension(originalPath), ".png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
        var path = Path.Combine(Path.GetTempPath(), $"arc-avatar-{Guid.NewGuid():N}{extension}"); using var stream = File.Create(path);
        BitmapEncoder encoder = extension == ".png" ? new PngBitmapEncoder() : new JpegBitmapEncoder { QualityLevel = 92 }; encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(stream); return path;
    }
}
