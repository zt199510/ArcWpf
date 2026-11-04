using System.Windows.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class ModelPickerPage : UserControl
{
    public ModelPickerPage()
    {
        InitializeComponent();
        Picker.Models = AiDemoData.Models;
        Picker.Providers = AiDemoData.Providers;
        Picker.Featured = AiDemoData.Featured;
        Picker.Value = "opus-5.5";
    }
}
