using System.Collections.ObjectModel;
using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class ArcNextBatchPage : UserControl
{
    public ArcNextBatchPage()
    {
        InitializeComponent();
        Announcements.Messages = new ObservableCollection<string> { "ArcWpf 组件库新增 8 个控件", "支持明暗主题与键盘交互" };
        Countdown.Target = DateTime.UtcNow.AddDays(2).AddHours(4).AddMinutes(18);
        Timeline.Events = new ObservableCollection<TimelineEvent> { new() { Actor = "ArcWpf", Title = "来源冻结", Meta = "AnnouncementBar · Countdown", At = DateTime.Now.AddMinutes(-8) }, new() { Actor = "Gallery", Title = "新增组件演示", Meta = "FileDropzone · MentionInput", At = DateTime.Now.AddHours(-2) } };
        Mention.People = new ObservableCollection<MentionPerson> { new() { Name = "Ada Lovelace", Role = "Engineer" }, new() { Name = "Grace Hopper", Role = "Researcher" } };
    }
}
