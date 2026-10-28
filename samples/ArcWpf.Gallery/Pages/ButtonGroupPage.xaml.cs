using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class ButtonGroupPage : UserControl
{
    private readonly DispatcherTimer _shareReset = new() { Interval = TimeSpan.FromMilliseconds(1800) };
    private readonly DispatcherTimer _duplicateReset = new() { Interval = TimeSpan.FromMilliseconds(1800) };
    private readonly DispatcherTimer _saveReset = new() { Interval = TimeSpan.FromMilliseconds(1800) };
    private int _zoom = 100;

    public ButtonGroupPage()
    {
        InitializeComponent();
        // Usage example menu: Pin to sidebar, Delete (destructive).
        DocumentActions.MenuItems.Add(new ArcWpf.Controls.MenuItem { Label = "Pin to sidebar", Icon = MenuIcon("Icon.Pin", "ArcWpf.Brush.TextSecondary") });
        DocumentActions.MenuItems.Add(new ArcWpf.Controls.MenuItem { Label = "Delete", Icon = MenuIcon("Icon.Trash", "ArcWpf.Brush.Danger"), IsDestructive = true });
        _shareReset.Tick += (_, _) => { _shareReset.Stop(); Share.Label = "Share"; Share.Icon = Resources["Icon.Link"]; };
        _duplicateReset.Tick += (_, _) => { _duplicateReset.Stop(); Duplicate.Label = "Duplicate"; };
        _saveReset.Tick += (_, _) => { _saveReset.Stop(); Save.Label = "Save"; Save.Icon = Resources["Icon.Save"]; };
        Unloaded += (_, _) => { _shareReset.Stop(); _duplicateReset.Stop(); _saveReset.Stop(); };
    }

    private FrameworkElement MenuIcon(string geometry, string brush)
    {
        var path = new Path { Data = (Geometry)Resources[geometry], StrokeThickness = 1.75, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round };
        path.SetResourceReference(Shape.StrokeProperty, brush);
        return new Viewbox { Width = 16, Height = 16, Child = new Canvas { Width = 24, Height = 24, Children = { path } } };
    }

    private void OnShare(object sender, RoutedEventArgs e)
    {
        Share.Label = "Copied";
        Share.Icon = Resources["Icon.Check"];
        Status.Text = "链接已复制";
        _shareReset.Stop(); _shareReset.Start();
    }

    private void OnDuplicate(object sender, RoutedEventArgs e)
    {
        Duplicate.Label = "Duplicated";
        Status.Text = "已创建副本";
        _duplicateReset.Stop(); _duplicateReset.Start();
    }

    private void OnArchive(object sender, RoutedEventArgs e)
    {
        Archive.Label = Archive.Label == "Archive" ? "Restore" : "Archive";
        Status.Text = Archive.Label == "Restore" ? "已归档" : "已恢复";
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        Save.Label = "Saved";
        Save.Icon = Resources["Icon.Check"];
        Status.Text = "已保存";
        _saveReset.Stop(); _saveReset.Start();
    }

    private void OnPreview(object sender, RoutedEventArgs e) => Preview.Label = Preview.Label == "Preview" ? "Edit" : "Preview";

    private void OnZoomOut(object sender, RoutedEventArgs e) => SetZoom(_zoom - 10);
    private void OnZoomIn(object sender, RoutedEventArgs e) => SetZoom(_zoom + 10);
    private void OnZoomReset(object sender, RoutedEventArgs e) => SetZoom(100);

    private void SetZoom(int value)
    {
        _zoom = Math.Clamp(value, 10, 400);
        // Content is a live value: it updates without the label crossfade, and the label stays the accessible name.
        ZoomReset.Content = _zoom.ToString(CultureInfo.InvariantCulture) + "%";
        ZoomReset.Label = $"Zoom {_zoom}%, reset to 100%";
    }

    private void OnUnreserved(object sender, RoutedEventArgs e) => Unreserved.Label = Unreserved.Label == "同步" ? "已同步到云端" : "同步";

    private void OnMenuItemInvoked(object? sender, MenuItemInvokedEventArgs e) => Status.Text = $"菜单：{e.Item.Label}";
}
