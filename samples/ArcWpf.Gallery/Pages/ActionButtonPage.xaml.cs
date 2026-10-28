using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class ActionButtonPage : UserControl
{
    public ActionButtonPage()
    {
        InitializeComponent();
        PublishButton.Action = async () => { await Task.Delay(1200); Status.Text = "发布成功"; };
        KeepSuccessButton.Action = async () => { await Task.Delay(900); Status.Text = "提交成功，保留成功状态"; };
        FailButton.Action = async () => { await Task.Delay(900); throw new InvalidOperationException("模拟失败"); };
        FailButton.ActionFailed += (_, e) => Status.Text = $"失败：{e.Exception.Message}";
    }
}

