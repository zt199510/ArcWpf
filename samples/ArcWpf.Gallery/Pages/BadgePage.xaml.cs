using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;

public partial class BadgePage : UserControl, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private bool _live, _alternate;
    private int _step;
    public string Status => _live ? "Live" : "Draft";
    public BadgeTone Tone => _live ? BadgeTone.Success : BadgeTone.Neutral;
    public object? Icon { get; private set; }
    public int Count { get; private set; } = 9;
    public BadgePage() { InitializeComponent(); DataContext = this; }
    private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private void ToggleLive(object sender, RoutedEventArgs e)
    { _live = !_live; Icon = _live ? Glyph(false) : null; Notify(nameof(Status)); Notify(nameof(Tone)); Notify(nameof(Icon)); }
    private void Cycle(object sender, RoutedEventArgs e)
    { _step = (_step + 1) % 3; Morph.Content = new[] { "Ready", "Deploying changes", "Live" }[_step]; Morph.Tone = new[] { BadgeTone.Info, BadgeTone.Warning, BadgeTone.Success }[_step]; }
    private void Increment(object sender, RoutedEventArgs e) { Count++; Notify(nameof(Count)); }
    private void SwapIcon(object sender, RoutedEventArgs e)
    { _alternate = !_alternate; Morph.Icon = Glyph(_alternate); Morph.IconKey = _alternate ? "warning" : "check"; }
    private void ToggleIcon(object sender, RoutedEventArgs e) => Morph.Icon = Morph.Icon is null ? Glyph(_alternate) : null;
    private static Path Glyph(bool alternate)
    {
        var path = new Path { Data = Geometry.Parse(alternate ? "M6,1 L11,11 H1 Z M6,4 V7 M6,8.5 V9" : "M1,6 L4.5,9.5 L11,2.5"), Width = 12, Height = 12, StrokeThickness = 1.5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
        path.SetBinding(Shape.StrokeProperty, new System.Windows.Data.Binding("(TextElement.Foreground)") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.Self) });
        return path;
    }
}
