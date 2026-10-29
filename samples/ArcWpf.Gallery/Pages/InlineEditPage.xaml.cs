using System.Threading.Tasks;
using System.Windows.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class InlineEditPage : UserControl
{
    public InlineEditPage() { InitializeComponent(); NameEdit.SaveAsync = SaveName; NameEdit.Validate = ValidateName; DescriptionEdit.SaveAsync = SaveDescription; }
    private string? ValidateName(string value) => string.IsNullOrWhiteSpace(value) ? "Name cannot be empty" : null;
    private async Task SaveName(string value) { await Task.Delay(220); Status.Text = $"名称已保存：{value}"; }
    private async Task SaveDescription(string value) { await Task.Delay(180); Status.Text = "描述已保存"; }
}
