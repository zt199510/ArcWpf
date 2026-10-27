using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class OTPInputPage : UserControl
{
    private readonly CodeModel _model=new();
    private int _completions;
    public OTPInputPage() { InitializeComponent(); DataContext=_model; }
    private void Fill(object sender,RoutedEventArgs e) => _model.Code="123456";
    private void Clear(object sender,RoutedEventArgs e) => Editable.Clear();
    private void ToggleError(object sender,RoutedEventArgs e) => Editable.ErrorMessage=Editable.ErrorMessage.Length==0?"验证码不正确，请重试。":"";
    private void ToggleDescription(object sender,RoutedEventArgs e) => Editable.Description=Editable.Description.Length==0?"输入六位数字，或直接粘贴验证码。":"";
    private void CodeCompleted(object sender,RoutedEventArgs e) => Status.Text=$"已完成输入 {_completions+=1} 次（此示例不提交服务器）";
    private sealed class CodeModel : INotifyPropertyChanged
    { private string _code=""; public string Code { get=>_code; set { _code=value; PropertyChanged?.Invoke(this,new(nameof(Code))); } } public event PropertyChangedEventHandler? PropertyChanged; }
}
