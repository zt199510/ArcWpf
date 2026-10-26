using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class ToastStackPage : UserControl
{
    private string? _updatable;
    public ToastStackPage() => InitializeComponent();
    private void ShowSuccess(object sender, RoutedEventArgs e) => Stack.Show(new ToastStackOptions { Title = "Changes saved", Description = "Synced to all devices.", Type = ToastStackType.Success, Duration = 4500 });
    private void ShowWarning(object sender, RoutedEventArgs e) => Stack.Show(new ToastStackOptions { Title = "Review required", Description = "Some fields need your attention.", Type = ToastStackType.Warning });
    private void ShowLoading(object sender, RoutedEventArgs e) => Stack.Show(new ToastStackOptions { Id = "loading", Title = "Uploading", Description = "This notification stays until it is updated.", Type = ToastStackType.Loading });
    private void UpdateToast(object sender, RoutedEventArgs e)
    {
        _updatable ??= Stack.Show(new ToastStackOptions { Id = "sync", Title = "Saving", Description = "Working…", Type = ToastStackType.Loading });
        Stack.Update(_updatable, new ToastStackPatch { Type = ToastStackType.Success, Title = "Saved", Description = "The update is complete.", Duration = 3000 });
    }
    private void DismissAll(object sender, RoutedEventArgs e) => Stack.Dismiss();
}
