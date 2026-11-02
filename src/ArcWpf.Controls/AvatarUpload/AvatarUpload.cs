using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace ArcWpf.Controls;

public class AvatarUpload : Control
{
    public static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register(nameof(ImageSource), typeof(ImageSource), typeof(AvatarUpload), new PropertyMetadata(null, OnImageChanged));
    public static readonly DependencyProperty InitialsProperty = DependencyProperty.Register(nameof(Initials), typeof(string), typeof(AvatarUpload), new PropertyMetadata("A"));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(AvatarUpload), new PropertyMetadata("Upload avatar"));
    public static readonly DependencyProperty IsUploadingProperty = DependencyProperty.Register(nameof(IsUploading), typeof(bool), typeof(AvatarUpload), new PropertyMetadata(false));
    public ImageSource? ImageSource { get => (ImageSource?)GetValue(ImageSourceProperty); set => SetValue(ImageSourceProperty, value); }
    public string Initials { get => (string)GetValue(InitialsProperty); set => SetValue(InitialsProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public bool IsUploading { get => (bool)GetValue(IsUploadingProperty); set => SetValue(IsUploadingProperty, value); }
    public event EventHandler<string>? FileSelected;
    static AvatarUpload() => DefaultStyleKeyProperty.OverrideMetadata(typeof(AvatarUpload), new FrameworkPropertyMetadata(typeof(AvatarUpload)));
    public override void OnApplyTemplate() { base.OnApplyTemplate(); if (GetTemplateChild("PART_Button") is Button button) button.Click += (_, _) => Browse(); UpdateFallbackVisual(); }
    private static void OnImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((AvatarUpload)d).UpdateFallbackVisual();
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
        try { ImageSource = new System.Windows.Media.Imaging.BitmapImage(new Uri(dialog.FileName)); FileSelected?.Invoke(this, dialog.FileName); } catch { }
    }
}
