using System.Windows.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class SignaturePadPage : UserControl
{
    public SignaturePadPage() => InitializeComponent();
    private void OnStrokes(object? sender, EventArgs e) => Adopt.IsEnabled = Pad.Strokes.Count > 0;
}
