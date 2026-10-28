using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class ActionButtonPage : UserControl
{
    public ActionButtonPage()
    {
        InitializeComponent();
        PublishButton.Action = async () => { await Task.Delay(1200); Status.Text = "保存成功，2.4 秒后复位"; };
        KeepSuccessButton.Action = async () => { await Task.Delay(900); Status.Text = "发布成功，保留成功状态（再次点击会重新执行）"; };
        FailButton.Action = async () => { await Task.Delay(900); throw new InvalidOperationException("服务器拒绝了提交"); };
        FailButton.ActionFailed += (_, e) => Status.Text = $"失败：{e.Exception.Message}";
    }
}

