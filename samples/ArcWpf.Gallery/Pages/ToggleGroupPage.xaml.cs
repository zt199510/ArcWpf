using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class ToggleGroupPage : UserControl
{
    public ToggleGroupPage()
    {
        InitializeComponent();
        BoldItem.Icon = "M6,12 h9 a4,4,0,0,1,0,8 H7 a1,1,0,0,1,-1,-1 V5 a1,1,0,0,1,1,-1 h7 a4,4,0,0,1,0,8";
        ItalicItem.Icon = "M19,4 H10 M14,20 H5 M15,4 L9,20";
        UnderlineItem.Icon = "M6,4 v6 a6,6,0,0,0,12,0 V4 M4,20 H20";
        LeftItem.Icon = "M21,5 H3 M15,12 H3 M17,19 H3";
        CenterItem.Icon = "M21,5 H3 M17,12 H7 M19,19 H5";
        RightItem.Icon = "M21,5 H3 M21,12 H9 M21,19 H7";
        JustifyItem.Icon = "M3,5 h18 M3,12 h18 M3,19 h18";
        Loaded += (_, _) => Apply();
    }
    private void StyleChanged(object sender, SelectionChangedEventArgs e) { if (IsLoaded) Apply(); }
    private void Apply()
    {
        Sample.FontWeight = BoldItem.IsSelected ? FontWeights.SemiBold : FontWeights.Normal;
        Sample.FontStyle = ItalicItem.IsSelected ? FontStyles.Italic : FontStyles.Normal;
        Sample.TextDecorations = UnderlineItem.IsSelected ? TextDecorations.Underline : null;
        Sample.TextAlignment = CenterItem.IsSelected ? TextAlignment.Center : RightItem.IsSelected ? TextAlignment.Right : JustifyItem.IsSelected ? TextAlignment.Justify : TextAlignment.Left;
    }
}
