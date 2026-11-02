using System.Collections.Generic;
using System.Windows.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class JSONViewerPage : UserControl
{
    public JSONViewerPage()
    {
        InitializeComponent();
        Viewer.Data = new Dictionary<string, object?>
        {
            ["event"] = "deployment.completed",
            ["repository"] = new Dictionary<string, object?> { ["name"] = "ArcWpf", ["private"] = false },
            ["artifacts"] = new List<object?> { "gallery.exe", "controls.dll", 42 }
        };
    }
}
