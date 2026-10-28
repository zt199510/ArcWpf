using System.Windows.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class CopyButtonPage : UserControl
{
    public CopyButtonPage()
    {
        InitializeComponent();
        CopyLink.Click += (_, _) => Status.Text = "链接已复制";
        CopyCode.Click += (_, _) => Status.Text = "代码已复制";
        CopyLink.CopyFailed += (_, e) => Status.Text = $"复制失败：{e.Exception.Message}";
    }
}
