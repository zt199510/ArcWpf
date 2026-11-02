using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using System.Windows.Input;
using System.IO;

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
    public event EventHandler<string>? FileSelected;
    static AvatarUpload() => DefaultStyleKeyProperty.OverrideMetadata(typeof(AvatarUpload), new FrameworkPropertyMetadata(typeof(AvatarUpload)));
    public AvatarUpload() { AllowDrop = true; Focusable = true; Drop += OnDrop; DragOver += OnDragOver; KeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Space) { Browse(); e.Handled = true; } }; }
    public override void OnApplyTemplate() { base.OnApplyTemplate(); if (GetTemplateChild("PART_Button") is System.Windows.Controls.Button button) button.Click += (_, _) => Browse(); UpdateFallbackVisual(); UpdateProgressRing(); }
    private static void OnImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((AvatarUpload)d).UpdateFallbackVisual();
    private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((AvatarUpload)d).UpdateProgressRing();
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
            ImageSource = new System.Windows.Media.Imaging.BitmapImage(new Uri(path)); Error = string.Empty; FileSelected?.Invoke(this, path);
        } catch (Exception error) { Error = error.Message; }
    }

    private void UpdateProgressRing()
    {
        if (GetTemplateChild("PART_ProgressRing") is System.Windows.Shapes.Ellipse ring) ring.StrokeDashOffset = Math.Clamp(100 - Progress, 0, 100);
    }
}
