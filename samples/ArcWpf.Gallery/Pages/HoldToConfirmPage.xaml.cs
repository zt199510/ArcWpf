using System.Windows;
using System.Windows.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class HoldToConfirmPage : UserControl
{
    public HoldToConfirmPage() { InitializeComponent(); }
    private void HoldChanged(object sender, bool holding) => Status.Text = holding ? "继续按住..." : "已取消或等待操作";
    private void DeleteConfirmed(object sender, RoutedEventArgs e) => Status.Text = "项目已删除";
    private void ArchiveConfirmed(object sender, RoutedEventArgs e) => Status.Text = "项目已归档";
}
