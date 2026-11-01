using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;

// The registry demo's saved colors: cobalt, tomato, moss, saffron, plum.
public partial class ColorPickerPage : UserControl
{
    public ColorPickerPage()
    {
        InitializeComponent();
        Picker.Swatches = [new("cobalt", "#2F6BFF"), new("tomato", "#E5484D"), new("moss", "#2E7D4F"), new("saffron", "#F5B83D"), new("plum", "#7B4BD1")];
    }
}
