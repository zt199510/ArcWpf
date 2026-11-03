using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class DropdownMenuPage : UserControl
{
    public DropdownMenuPage()
    {
        InitializeComponent();
        Menu.Icon = Glyph("ellipsis");
        Menu.Items.Add(new ArcWpf.Controls.MenuItem { Label = "Edit project", Icon = Glyph("pencil") });
        Menu.Items.Add(new ArcWpf.Controls.MenuItem { Label = "Download brief", Icon = Glyph("download") });
        Menu.Items.Add(new ArcWpf.Controls.MenuItem { Label = "Archive", Icon = Glyph("archive"), SeparatorBefore = true });
    }

    private static ArcDataGridIcon Glyph(string name)
    {
        var glyph = new ArcDataGridIcon { Icon = name, Size = 15, StrokeThickness = 1.75 };
        glyph.SetResourceReference(ArcDataGridIcon.StrokeProperty, "ArcWpf.Brush.TextSecondary");
        return glyph;
    }
}
