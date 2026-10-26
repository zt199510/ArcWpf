using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace ArcWpf.Gallery.Pages;
public partial class CheckBoxPage : UserControl
{
    private readonly CheckBoxModel _model = new();
    public CheckBoxPage() { InitializeComponent(); DataContext = _model; }
    private void SetMixed(object sender, RoutedEventArgs e) => _model.Selection = null;
}
public sealed class CheckBoxModel : INotifyPropertyChanged
{
    private bool _includeArchived = true;
    private bool? _selection;
    private string _status = "", _commandStatus = "尚未执行命令";
    private int _count;
    public CheckBoxModel() => SelectionCommand = new DemoCommand(value => CommandStatus = $"{value} · 已执行 {++_count} 次");
    public bool IncludeArchived { get => _includeArchived; set { if(Set(ref _includeArchived,value)) Status=value?"Archived projects included":"Archived projects hidden"; } }
    public bool? Selection { get => _selection; set { if(Set(ref _selection,value)) Changed(nameof(SelectionSummary)); } }
    public string SelectionSummary => Selection switch { true => "选中", false => "未选中", null => "不确定" };
    public string Status { get => _status; private set => Set(ref _status,value); }
    public string CommandStatus { get => _commandStatus; private set => Set(ref _commandStatus,value); }
    public ICommand SelectionCommand { get; }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed(string name) => PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(name));
    private bool Set<T>(ref T field,T value,[CallerMemberName] string name="") { if(EqualityComparer<T>.Default.Equals(field,value))return false; field=value; Changed(name); return true; }
    private sealed class DemoCommand(Action<object?> execute) : ICommand
    {
        public bool CanExecute(object? parameter)=>true;
        public void Execute(object? parameter)=>execute(parameter);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
