using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;

public partial class AvatarPage : UserControl, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private string _personName = "Maya Chen";
    private ImageSource? _currentImage;
    public string PersonName { get => _personName; set { _personName = value; Notify(nameof(PersonName)); } }
    public AvatarStatus Presence { get; private set; } = AvatarStatus.Online;
    public ImageSource Portrait { get; } = CreatePortrait();
    public ImageSource? CurrentImage { get => _currentImage; set { _currentImage = value; Notify(nameof(CurrentImage)); } }
    public AvatarPage() { InitializeComponent(); DataContext = this; }
    private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private void CycleStatus(object sender, RoutedEventArgs e) { Presence = (AvatarStatus)(((int)Presence + 1) % 3); Notify(nameof(Presence)); }
    private void TogglePhoto(object sender, RoutedEventArgs e) => CurrentImage = CurrentImage is null ? Portrait : null;
    private void LoadAddress(object sender, RoutedEventArgs e)
    {
        try
        {
            var image = new BitmapImage(); image.BeginInit(); image.UriSource = new Uri(ImageAddress.Text, UriKind.Absolute); image.DecodePixelWidth = 176; image.EndInit(); CurrentImage = image;
            LoadMessage.Text = "已交给 WPF 加载；失败时头像显示首字母。";
        }
        catch (Exception error) when (error is UriFormatException or System.IO.IOException or NotSupportedException or ArgumentException)
        { CurrentImage = null; LoadMessage.Text = "地址或图片无法加载：" + error.Message; }
    }
    // Original geometric illustration: no downloaded portrait or third-party image asset.
    public static ImageSource CreatePortrait()
    {
        var drawing = new DrawingGroup(); using (var dc = drawing.Open())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(210, 224, 215)), null, new Rect(0, 0, 100, 100));
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(58, 86, 75)), null, new Point(50, 100), 38, 42);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(62, 47, 43)), null, new Point(50, 38), 24, 29);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(217, 166, 134)), null, new Point(50, 42), 18, 24);
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(62, 47, 43)), null, Geometry.Parse("M28,35 Q30,4 54,10 Q76,15 73,40 Q55,31 45,23 Q41,33 28,35Z"));
        }
        drawing.Freeze(); var result = new DrawingImage(drawing); result.Freeze(); return result;
    }
}
