using System.Threading.Tasks;
using System.Windows.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class InlineEditPage : UserControl
{
    public InlineEditPage() { InitializeComponent(); NameEdit.SaveAsync = Save; NameEdit.Validate = ValidateName; DescriptionEdit.SaveAsync = Save; }
    private static string? ValidateName(string value) => string.IsNullOrWhiteSpace(value) ? "Name can’t be empty" : null;
    // A short network-like delay so the saving state is visible; the switch makes the next save fail once.
    private async Task Save(string value)
    {
        await Task.Delay(700);
        if (FailNext.IsChecked == true) { FailNext.IsChecked = false; throw new InvalidOperationException("Network error"); }
        Activity.Text = $"Last edited by you, just now";
    }
}
