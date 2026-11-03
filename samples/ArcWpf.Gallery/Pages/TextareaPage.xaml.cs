using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Media;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class TextareaPage : UserControl
{
    private static readonly Brush Tag = Frozen(0x25, 0x63, 0xEB), Mention = Frozen(0xDB, 0x27, 0x77);

    public TextareaPage()
    {
        InitializeComponent();
        TextColoring.SetSegmenter(Colored, text => Regex.Matches(text, @"[#@]\w+").Select(match => new TextSegment(match.Index, match.Length, match.Value[0] == '#' ? Tag : Mention)));
    }

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
