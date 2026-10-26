using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
namespace ArcWpf.Gallery.Pages;

public sealed class ButtonViewModel : INotifyPropertyChanged
{
    private bool _isSaving;
    private int _count;
    private bool _copied;
    private int _morphIndex;
    public string MorphLabel => new[] { "继续", "确认并继续下一步", "完成" }[_morphIndex];
    public ICommand MorphCommand { get; }
    public bool Copied { get => _copied; private set { _copied = value; Changed(); } }
    public ICommand CopyCommand { get; }
    private string _status = "尚未执行操作";
    public bool IsSaving { get => _isSaving; private set { _isSaving = value; Changed(); } }
    public string Status { get => _status; private set { _status = value; Changed(); } }
    public ICommand CountCommand { get; }
    public ICommand SaveCommand { get; }
    public ButtonViewModel()
    {
        CountCommand = new ActionCommand(() => Status = $"已执行 {++_count} 次操作");
        SaveCommand = new ActionCommand(Save);
        MorphCommand = new ActionCommand(() => { _morphIndex = (_morphIndex + 1) % 3; Changed(nameof(MorphLabel)); });
        CopyCommand = new ActionCommand(() =>
        {
            try
            {
                System.Windows.Clipboard.SetText("https://uiarc.dev/components/button");
                Copied = true;
                Status = "已复制 Arc Button 页面链接";
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                Copied = false;
                Status = "剪贴板暂时不可用，请重试";
            }
        });
    }
    private async void Save()
    {
        if (IsSaving) { return; }
        IsSaving = true;
        Status = "正在模拟保存…";
        try { await Task.Delay(2000); Status = "保存完成"; }
        finally { IsSaving = false; }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private sealed class ActionCommand(Action action) : ICommand
    {
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => action();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
