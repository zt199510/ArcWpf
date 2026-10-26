using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;

public partial class PopoverPage : UserControl
{
    private readonly PopoverModel _model = new();
    public PopoverPage() { InitializeComponent(); DataContext = _model; }
    private void ToggleShare(object sender, RoutedEventArgs e) => _model.ShareOpen = !_model.ShareOpen;
    private void ToggleEditor(object sender, RoutedEventArgs e) => _model.EditorOpen = !_model.EditorOpen;
    private void CloseEditor(object sender, RoutedEventArgs e) => EditorPanel.Close();
    private void PanelOpened(object sender, RoutedEventArgs e) => EventStatus.Text = "Opened · 已打开";
    private void PanelClosed(object? sender, PopoverClosedEventArgs e) => EventStatus.Text = $"Closed · {e.Reason}";
}
public sealed class PopoverModel : INotifyPropertyChanged
{
    private bool _shareOpen, _editorOpen;
    private string _projectName = "Arc launch";
    private PopoverSide _side = PopoverSide.Bottom;
    private PopoverAlignment _alignment = PopoverAlignment.Start;
    public Array Sides => Enum.GetValues<PopoverSide>();
    public Array Alignments => Enum.GetValues<PopoverAlignment>();
    public bool ShareOpen { get => _shareOpen; set => Set(ref _shareOpen, value); }
    public bool EditorOpen { get => _editorOpen; set => Set(ref _editorOpen, value); }
    public string ProjectName { get => _projectName; set => Set(ref _projectName, value); }
    public PopoverSide Side { get => _side; set => Set(ref _side, value); }
    public PopoverAlignment Alignment { get => _alignment; set => Set(ref _alignment, value); }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
