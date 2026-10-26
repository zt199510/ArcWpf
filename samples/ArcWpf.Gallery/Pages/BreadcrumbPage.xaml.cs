using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class BreadcrumbPage : UserControl
{
    public BreadcrumbPage()
    {
        InitializeComponent();
        Path.Items.Add(new BreadcrumbItem { Label = "Workspace", Command = new PageCommand(() => Result.Text = "已选择 Workspace") });
        Path.Items.Add(new BreadcrumbItem { Label = "Settings", Command = new PageCommand(() => Result.Text = "已选择 Settings") });
        Path.Items.Add(new BreadcrumbItem { Label = "Billing" });
        CompactPath.Items.Add(new BreadcrumbItem { Label = "Workspace", Command = new PageCommand(() => { }) });
        CompactPath.Items.Add(new BreadcrumbItem { Label = "Archived projects", IsEnabled = false, Command = new PageCommand(() => { }) });
        CompactPath.Items.Add(new BreadcrumbItem { Label = "Billing" });
    }

    private void OnItemInvoked(object sender, BreadcrumbItemInvokedEventArgs e) => Result.Text = $"已选择 {e.Item.Label}";

    private sealed class PageCommand(Action action) : ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => action();
    }
}
