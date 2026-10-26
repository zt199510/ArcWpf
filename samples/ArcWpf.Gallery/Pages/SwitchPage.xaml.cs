using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;

namespace ArcWpf.Gallery.Pages;

public partial class SwitchPage : UserControl
{
    public SwitchPage() { InitializeComponent(); DataContext = new SwitchViewModel(); }
}

public sealed class SwitchViewModel : INotifyPropertyChanged
{
    private bool _notifications = true;
    private bool _changed;
    private int _commands;
    public bool Notifications
    {
        get => _notifications;
        set { if (_notifications == value) { return; } _notifications = value; _changed = true; Changed(); Changed(nameof(Summary)); Changed(nameof(PreviewStatus)); }
    }
    public string PreviewStatus => _changed ? (Notifications ? "Weekly digest 已开启" : "Weekly digest 已关闭") : string.Empty;
    public string Summary => $"通知：{(Notifications ? "开启" : "关闭")} · 用户命令执行 {_commands} 次";
    public ICommand CountCommand { get; }
    public ICommand ChangeCommand { get; }
    public SwitchViewModel()
    {
        CountCommand = new Callback(() => { _commands++; Changed(nameof(Summary)); });
        ChangeCommand = new Callback(() => Notifications = !Notifications);
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private sealed class Callback(Action action) : ICommand
    {
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => action();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}

