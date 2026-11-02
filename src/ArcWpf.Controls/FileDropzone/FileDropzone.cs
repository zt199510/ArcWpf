using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ArcWpf.Controls;

public sealed class FileDropzoneItem : INotifyPropertyChanged
{
    private string _name = string.Empty, _status = string.Empty, _error = string.Empty; private long _size; private double _progress;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get => _name; set => Set(ref _name, value); }
    public long Size { get => _size; set => Set(ref _size, value); }
    public string Status { get => _status; set => Set(ref _status, value); }
    public double Progress { get => _progress; set => Set(ref _progress, value); }
    public string Error { get => _error; set => Set(ref _error, value); }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null) { if (EqualityComparer<T>.Default.Equals(field, value)) return; field = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }
}
public delegate Task FileDropzoneUpload(FileDropzoneItem item, Action<double> onProgress, CancellationToken cancellationToken);

public class FileDropzone : Control
{
    public static readonly DependencyProperty AcceptProperty = DependencyProperty.Register(nameof(Accept), typeof(string), typeof(FileDropzone), new PropertyMetadata("*.*"));
    public static readonly DependencyProperty MultipleProperty = DependencyProperty.Register(nameof(Multiple), typeof(bool), typeof(FileDropzone), new PropertyMetadata(true));
    public static readonly DependencyProperty MaxFilesProperty = DependencyProperty.Register(nameof(MaxFiles), typeof(int), typeof(FileDropzone), new PropertyMetadata(10));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(FileDropzone), new PropertyMetadata("Drop files here"));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(FileDropzone), new PropertyMetadata("or click to browse"));
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(nameof(Items), typeof(ObservableCollection<FileDropzoneItem>), typeof(FileDropzone), new PropertyMetadata(null));
    private static readonly DependencyPropertyKey IsDraggingPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsDragging), typeof(bool), typeof(FileDropzone), new PropertyMetadata(false));
    public static readonly DependencyProperty IsDraggingProperty = IsDraggingPropertyKey.DependencyProperty;
    public static readonly DependencyProperty UploadProperty = DependencyProperty.Register(nameof(Upload), typeof(FileDropzoneUpload), typeof(FileDropzone), new PropertyMetadata(null));
    public string Accept { get => (string)GetValue(AcceptProperty); set => SetValue(AcceptProperty, value); }
    public bool Multiple { get => (bool)GetValue(MultipleProperty); set => SetValue(MultipleProperty, value); }
    public int MaxFiles { get => (int)GetValue(MaxFilesProperty); set => SetValue(MaxFilesProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public ObservableCollection<FileDropzoneItem> Items { get => (ObservableCollection<FileDropzoneItem>)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    public bool IsDragging { get => (bool)GetValue(IsDraggingProperty); private set => SetValue(IsDraggingPropertyKey, value); }
    public FileDropzoneUpload? Upload { get => (FileDropzoneUpload?)GetValue(UploadProperty); set => SetValue(UploadProperty, value); }
    public event EventHandler<IReadOnlyList<string>>? FilesSelected;
    static FileDropzone() => DefaultStyleKeyProperty.OverrideMetadata(typeof(FileDropzone), new FrameworkPropertyMetadata(typeof(FileDropzone)));
    public FileDropzone() { Items = new ObservableCollection<FileDropzoneItem>(); AllowDrop = true; Focusable = true; Drop += OnDrop; DragEnter += (_, e) => IsDragging = e.Data.GetDataPresent(DataFormats.FileDrop); DragLeave += (_, _) => IsDragging = false; DragOver += (_, e) => { IsDragging = e.Data.GetDataPresent(DataFormats.FileDrop); e.Effects = DragDropEffects.Copy; }; KeyDown += (_, e) => { if (e.Key is System.Windows.Input.Key.Enter or System.Windows.Input.Key.Space) { Browse(); e.Handled = true; } }; AddHandler(Button.ClickEvent, new RoutedEventHandler(OnItemButtonClick)); }
    private void OnItemButtonClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not Button button || button.Tag is not FileDropzoneItem item) return;
        if (button.Name == "PART_Retry") RetryItem(item); else if (button.Name == "PART_Cancel") CancelUpload(item); else if (button.Name == "PART_Remove") RemoveItem(item);
        e.Handled = true;
    }
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_Browse") is Button browse) browse.Click += (_, _) => Browse();
        if (GetTemplateChild("DropSurface") is Border surface) surface.MouseLeftButtonUp += (_, e) => { if (e.OriginalSource is not Button) Browse(); Focus(); };
    }
    private void OnDrop(object sender, DragEventArgs e) { IsDragging = false; if (e.Data.GetDataPresent(DataFormats.FileDrop)) AddFiles((string[])e.Data.GetData(DataFormats.FileDrop)); }
    private void Browse()
    {
        var dialog = new OpenFileDialog { Multiselect = Multiple, Filter = string.IsNullOrWhiteSpace(Accept) || Accept == "*.*" ? "All files|*.*" : $"Files|{Accept}" };
        if (dialog.ShowDialog() == true) AddFiles(dialog.FileNames);
    }
    private void AddFiles(IEnumerable<string> paths)
    {
        var selected = paths.Where(File.Exists).Take(Math.Max(0, MaxFiles - Items.Count)).ToArray();
        foreach (var path in selected)
        {
            var info = new FileInfo(path); var item = new FileDropzoneItem { Name = info.Name, Size = info.Length, Status = "selected" }; Items.Add(item);
            if (Upload is not null) _ = UploadItemAsync(item);
        }
        if (selected.Length > 0) FilesSelected?.Invoke(this, selected);
    }
    private readonly Dictionary<string, CancellationTokenSource> _uploads = new();
    public void RemoveItem(FileDropzoneItem item) { CancelUpload(item); Items.Remove(item); }
    public void RetryItem(FileDropzoneItem item) { if (Items.Contains(item) && Upload is not null) { CancelUpload(item); _ = UploadItemAsync(item); } }
    public void CancelUpload(FileDropzoneItem item) { if (_uploads.Remove(item.Id, out var cts)) cts.Cancel(); }
    private async Task UploadItemAsync(FileDropzoneItem item)
    {
        if (Upload is null) return;
        CancelUpload(item); using var cts = new CancellationTokenSource(); _uploads[item.Id] = cts;
        item.Status = "uploading"; item.Progress = 0;
        try { await Upload(item, percent => { item.Progress = Math.Clamp(percent, 0, 100); }, cts.Token); item.Progress = 100; item.Status = "uploaded"; item.Error = string.Empty; }
        catch (OperationCanceledException) { item.Status = "cancelled"; }
        catch (Exception error) { item.Status = "failed"; item.Error = error.Message; }
        finally { _uploads.Remove(item.Id); }
    }
}
