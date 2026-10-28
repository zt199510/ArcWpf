using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class SplitButtonPage : UserControl
{
    public SplitButtonPage()
    {
        InitializeComponent();
        SaveButton.Click += (_, _) => Status.Text = "已保存";
        SaveButton.Items.Add(new ArcWpf.Controls.MenuItem { Label = "另存为", Command = new SplitAction(() => Status.Text = "另存为") });
        SaveButton.Items.Add(new ArcWpf.Controls.MenuItem { Label = "导出", Command = new SplitAction(() => Status.Text = "导出") });
    }
    private sealed class SplitAction(Action action) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => action();
    }
}

