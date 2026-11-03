using System.Windows.Controls; using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class RadioCardsPage : UserControl { public RadioCardsPage() { InitializeComponent(); Status.Text = "当前：standard"; } private void Changed(object? s, System.EventArgs e) => Status.Text = $"当前：{Main.SelectedValue}"; }
