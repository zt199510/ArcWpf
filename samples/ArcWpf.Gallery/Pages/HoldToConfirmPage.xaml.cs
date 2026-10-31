using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class HoldToConfirmPage : UserControl
{
    public HoldToConfirmPage() { InitializeComponent(); }
    private void HoldChanged(object? sender, bool holding) { if (!Delete.IsConfirmed) Hint.Text = holding ? "Keep holding" : "Hold for 1.2 seconds, or hold Space"; }
    private void DeleteConfirmed(object sender, RoutedEventArgs e) { Hint.Text = "Project deleted"; Reset.Visibility = Visibility.Visible; Status.Text = "项目已删除"; }
    private void ExtensionConfirmed(object sender, RoutedEventArgs e) { Status.Text = $"{((HoldToConfirm)sender).ConfirmedLabel}：点击 Reset 可恢复全部按钮"; Reset.Visibility = Visibility.Visible; }
    // Confirmed=false resets a self-managed button; clearing it hands the state back to the button.
    private void ResetClick(object sender, RoutedEventArgs e)
    {
        foreach (var button in new[] { Delete, Danger, Neutral }) { button.Confirmed = false; button.ClearValue(HoldToConfirm.ConfirmedProperty); }
        Hint.Text = "Hold for 1.2 seconds, or hold Space"; Reset.Visibility = Visibility.Collapsed; Status.Text = "已重置";
    }
}
