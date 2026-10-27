using System.Windows;
using System.Windows.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class PasswordInputPage : UserControl
{
    private bool _longDescription;
    public PasswordInputPage()
    {
        InitializeComponent();
        Narrow.Password = "demo-long-password-12345"; Disabled.Password = "demo-only";
    }
    private void PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (Status is null) return;
        using var password = Editable.SecurePassword;
        Status.Text = password.Length == 0 ? "尚未输入" : $"已输入 {password.Length} 个字符";
    }
    private void FillDemo(object sender, RoutedEventArgs e) => Editable.Password = "Arc-demo-2026!";
    private void Clear(object sender, RoutedEventArgs e) => Editable.Clear();
    private void ChangeDescription(object sender, RoutedEventArgs e)
    {
        _longDescription = !_longDescription;
        Editable.Description = _longDescription ? "这是一段较长的说明，用于检查说明行换行、高度过渡以及修改说明后输入框和眼睛按钮的位置是否保持稳定。" : "只在点击眼睛时显示密码。";
    }
    private void ChangeError(object sender, RoutedEventArgs e) => Editable.ErrorMessage = Editable.HasError ? "" : "密码不正确，请检查后再试。";
}
