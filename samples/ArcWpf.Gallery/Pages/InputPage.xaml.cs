using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class InputPage : UserControl
{
    public InputPage() { InitializeComponent(); DataContext = new InputViewModel(); }
}

public sealed class InputViewModel : INotifyPropertyChanged, IDataErrorInfo
{
    private string _email = "", _name = "", _validatedText = "";
    private string _projectName = "Arc launch";
    public string ProjectName { get => _projectName; set { _projectName = value; Changed(); } }
    public string Email { get => _email; set { _email = value; Changed(); Changed(nameof(EmailError)); } }
    public string EmailError => Email.Length > 0 && (!Email.Contains('@') || Email.EndsWith('@')) ? "请输入包含有效域名的邮箱地址。" : "";
    public string Name { get => _name; set { _name = value; Changed(); Changed(nameof(NameSummary)); } }
    public string NameSummary => $"已提交到数据源：{Name}";
    public string ValidatedText { get => _validatedText; set { _validatedText = value; Changed(); } }
    public string Error => "";
    public string this[string columnName] => columnName == nameof(ValidatedText) && ValidatedText.Length < 3 ? "至少输入三个字符。" : "";
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
