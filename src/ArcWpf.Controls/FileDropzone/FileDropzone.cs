using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ArcWpf.Controls;

public sealed class FileDropzoneItem : INotifyPropertyChanged
{
    private string _name = string.Empty, _status = string.Empty, _error = string.Empty, _preview = string.Empty, _filePath = string.Empty; private long _size; private double _progress; private bool _retryable = true; private ImageSource? _previewSource;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get => _name; set { if (Set(ref _name, value)) OnPropertyChanged(nameof(IconData)); } }
    public long Size { get => _size; set { if (Set(ref _size, value)) OnPropertyChanged(nameof(FormattedSize)); } }
    public string FormattedSize => FileDropzone.FormatFileSize(Size);
    /// <summary>可选的图片缩略图 URI；无法加载时模板会回退到类型图标。</summary>
    public string Preview { get => _preview; set { if (Set(ref _preview, value)) OnPropertyChanged(nameof(Thumbnail)); } }
    /// <summary>WPF 文件选择器返回的本地路径。上传回调可据此读取文件。</summary>
    public string FilePath { get => _filePath; set => Set(ref _filePath, value); }
    /// <summary>A thumbnail already in memory. It wins over <see cref="Preview"/>.</summary>
    public ImageSource? PreviewSource { get => _previewSource; set { if (Set(ref _previewSource, value)) OnPropertyChanged(nameof(Thumbnail)); } }
    public ImageSource? Thumbnail
    {
        get
        {
            if (_previewSource is not null) return _previewSource;
            if (string.IsNullOrWhiteSpace(_preview)) return null;
            try { return new BitmapImage(new Uri(_preview, UriKind.RelativeOrAbsolute)); }
            catch (Exception error) when (error is UriFormatException or IOException or NotSupportedException) { return null; }
        }
    }
    /// <summary>Lucide path data for this file's type, shown when there is no thumbnail.</summary>
    public string IconData => Path.GetExtension(_name).ToLowerInvariant() switch
    {
        ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif" or ".svg" => FileGlyphs.Image,
        ".mov" or ".mp4" or ".webm" or ".avi" or ".mkv" => FileGlyphs.Video,
        ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => FileGlyphs.Archive,
        _ => FileGlyphs.Text,
    };
    public string StatusText => _status == "failed" && !string.IsNullOrEmpty(_error) ? _error : DisplayStatus;
    internal bool IsFresh { get; set; }
    public bool Retryable { get => _retryable; set => Set(ref _retryable, value); }
    public string Status { get => _status; set { if (Set(ref _status, value)) { OnPropertyChanged(nameof(DisplayStatus)); OnPropertyChanged(nameof(StatusText)); } } }
    public string DisplayStatus => _status switch { "uploading" => "Uploading", "uploaded" => "Uploaded", "failed" => "Failed", "cancelled" => "Cancelled", "selected" => "Selected", _ => string.Empty };
    public double Progress { get => _progress; set => Set(ref _progress, value); }
    public string Error { get => _error; set { if (Set(ref _error, value)) OnPropertyChanged(nameof(StatusText)); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null) { if (EqualityComparer<T>.Default.Equals(field, value)) return false; field = value; OnPropertyChanged(name); return true; }
    private void OnPropertyChanged(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
public delegate Task FileDropzoneUpload(FileDropzoneItem item, Action<double> onProgress, CancellationToken cancellationToken);

public class FileDropzone : Control
{
    public static readonly DependencyProperty AcceptProperty = DependencyProperty.Register(nameof(Accept), typeof(string), typeof(FileDropzone), new PropertyMetadata("*.*"));
    public static readonly DependencyProperty MultipleProperty = DependencyProperty.Register(nameof(Multiple), typeof(bool), typeof(FileDropzone), new PropertyMetadata(true));
    public static readonly DependencyProperty MaxFilesProperty = DependencyProperty.Register(nameof(MaxFiles), typeof(int), typeof(FileDropzone), new PropertyMetadata(10));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(FileDropzone), new PropertyMetadata("Drop files here"));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(FileDropzone), new PropertyMetadata("or click to browse"));
    public static readonly DependencyProperty NoteProperty = DependencyProperty.Register(nameof(Note), typeof(string), typeof(FileDropzone), new PropertyMetadata(null));
    public static readonly DependencyProperty DropLabelProperty = DependencyProperty.Register(nameof(DropLabel), typeof(string), typeof(FileDropzone), new PropertyMetadata("Drop to add files"));
    public static readonly DependencyProperty MaxSizeProperty = DependencyProperty.Register(nameof(MaxSize), typeof(long), typeof(FileDropzone), new PropertyMetadata(0L));
    public static readonly DependencyProperty ListPlacementProperty = DependencyProperty.Register(nameof(ListPlacement), typeof(string), typeof(FileDropzone), new PropertyMetadata("below", OnLayoutPropertyChanged));
    public static readonly DependencyProperty CompactAtProperty = DependencyProperty.Register(nameof(CompactAt), typeof(int), typeof(FileDropzone), new PropertyMetadata(0, OnLayoutPropertyChanged));
    private static readonly DependencyPropertyKey IsListInsidePropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsListInside), typeof(bool), typeof(FileDropzone), new PropertyMetadata(false));
    public static readonly DependencyProperty IsListInsideProperty = IsListInsidePropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey IsCompactPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsCompact), typeof(bool), typeof(FileDropzone), new PropertyMetadata(false));
    public static readonly DependencyProperty IsCompactProperty = IsCompactPropertyKey.DependencyProperty;
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(nameof(Items), typeof(ObservableCollection<FileDropzoneItem>), typeof(FileDropzone), new PropertyMetadata(null, OnItemsChanged));
    private static readonly DependencyPropertyKey IsDraggingPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsDragging), typeof(bool), typeof(FileDropzone), new PropertyMetadata(false));
    public static readonly DependencyProperty IsDraggingProperty = IsDraggingPropertyKey.DependencyProperty;
    public static readonly DependencyProperty UploadProperty = DependencyProperty.Register(nameof(Upload), typeof(FileDropzoneUpload), typeof(FileDropzone), new PropertyMetadata(null));
    public string Accept { get => (string)GetValue(AcceptProperty); set => SetValue(AcceptProperty, value); }
    public bool Multiple { get => (bool)GetValue(MultipleProperty); set => SetValue(MultipleProperty, value); }
    public int MaxFiles { get => (int)GetValue(MaxFilesProperty); set => SetValue(MaxFilesProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public string? Note { get => (string?)GetValue(NoteProperty); set => SetValue(NoteProperty, value); }
    public string DropLabel { get => (string)GetValue(DropLabelProperty); set => SetValue(DropLabelProperty, value); }
    public long MaxSize { get => (long)GetValue(MaxSizeProperty); set => SetValue(MaxSizeProperty, value); }
    public string ListPlacement { get => (string)GetValue(ListPlacementProperty); set => SetValue(ListPlacementProperty, value); }
    public int CompactAt { get => (int)GetValue(CompactAtProperty); set => SetValue(CompactAtProperty, value); }
    public bool IsListInside => (bool)GetValue(IsListInsideProperty);
    public bool IsCompact => (bool)GetValue(IsCompactProperty);
    public ObservableCollection<FileDropzoneItem> Items { get => (ObservableCollection<FileDropzoneItem>)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    public bool IsDragging { get => (bool)GetValue(IsDraggingProperty); private set { if (value) PrepareWash(); SetValue(IsDraggingPropertyKey, value); UpdateSheets(); } }
    public FileDropzoneUpload? Upload { get => (FileDropzoneUpload?)GetValue(UploadProperty); set => SetValue(UploadProperty, value); }
    public event EventHandler<IReadOnlyList<string>>? FilesSelected;
    static FileDropzone() => DefaultStyleKeyProperty.OverrideMetadata(typeof(FileDropzone), new FrameworkPropertyMetadata(typeof(FileDropzone)));
    private Border? _surface, _wash;
    private (RotateTransform Turn, TranslateTransform Move)?[]? _sheets;
    public FileDropzone() { Items = new ObservableCollection<FileDropzoneItem>(); AllowDrop = true; Focusable = true; Drop += OnDrop; DragEnter += (_, e) => IsDragging = e.Data.GetDataPresent(DataFormats.FileDrop); DragLeave += (_, _) => IsDragging = false; DragOver += (_, e) => { IsDragging = e.Data.GetDataPresent(DataFormats.FileDrop); e.Effects = DragDropEffects.Copy; }; KeyDown += (_, e) => { if (e.Key is System.Windows.Input.Key.Enter or System.Windows.Input.Key.Space) { Browse(); e.Handled = true; } else if (e.Key == System.Windows.Input.Key.V && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0) { PasteClipboard(); e.Handled = true; } }; AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, new RoutedEventHandler(OnItemButtonClick)); UpdateLayoutFlags(); }
    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((FileDropzone)d).UpdateLayoutFlags();
    private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var zone = (FileDropzone)d;
        if (e.OldValue is ObservableCollection<FileDropzoneItem> oldItems) oldItems.CollectionChanged -= zone.OnItemsCollectionChanged;
        if (e.NewValue is ObservableCollection<FileDropzoneItem> newItems) newItems.CollectionChanged += zone.OnItemsCollectionChanged;
        zone.UpdateLayoutFlags();
    }
    private void OnItemsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => UpdateLayoutFlags();
    private void UpdateLayoutFlags() { SetValue(IsListInsidePropertyKey, string.Equals(ListPlacement, "inside", StringComparison.OrdinalIgnoreCase)); SetValue(IsCompactPropertyKey, CompactAt > 0 && Items is not null && Items.Count >= CompactAt); }
    private void OnItemButtonClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not System.Windows.Controls.Button button || button.Tag is not FileDropzoneItem item) return;
        if (button.Name == "PART_Retry") RetryItem(item); else if (button.Name == "PART_Cancel") CancelUpload(item); else if (button.Name == "PART_Remove") RemoveItem(item);
        e.Handled = true;
    }
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_Browse") is System.Windows.Controls.Button browse) browse.Click += (_, _) => Browse();
        _surface = GetTemplateChild("DropSurface") as Border;
        _wash = GetTemplateChild("DragWash") as Border;
        _sheets = [Sheet("PART_SheetBack"), Sheet("PART_SheetSide"), Sheet("PART_SheetFront")];
        if (_surface is not null)
        {
            _surface.MouseEnter += (_, _) => UpdateSheets();
            _surface.MouseLeave += (_, _) => UpdateSheets();
            _surface.RenderTransformOrigin = new Point(.5, .5);
            _surface.RenderTransform = new ScaleTransform(1, 1);
        }
        UpdateSheets(false);
    }

    private (RotateTransform Turn, TranslateTransform Move)? Sheet(string name)
    {
        if (GetTemplateChild(name) is not FrameworkElement sheet) return null;
        var turn = new RotateTransform();
        var move = new TranslateTransform();
        sheet.RenderTransformOrigin = new Point(.5, 1);
        sheet.RenderTransform = new TransformGroup { Children = { turn, move } };
        return (turn, move);
    }

    // The three sheets are the target's one illustration: they fan on hover and spread wide under a drag.
    private void UpdateSheets(bool animate = true)
    {
        if (_sheets is null) return;
        bool over = _surface?.IsMouseOver == true;
        (double R, double X, double Y)[] pose = IsDragging ? [(-22, -20, -2), (22, 20, -2), (0, 0, -8)]
            : over ? [(-15, -13, 2), (15, 13, 2), (0, 0, -2)]
            : [(-10, -9, 2), (10, 9, 2), (0, 0, 0)];
        bool full = animate && IsLoaded && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        for (int i = 0; i < _sheets.Length; i++)
        {
            if (_sheets[i] is not { } sheet) continue;
            if (full)
            {
                Tween.Spring(sheet.Turn, RotateTransform.AngleProperty, pose[i].R, Tween.Snappy);
                Tween.Spring(sheet.Move, TranslateTransform.XProperty, pose[i].X, Tween.Snappy);
                Tween.Spring(sheet.Move, TranslateTransform.YProperty, pose[i].Y, Tween.Snappy);
            }
            else
            {
                Tween.Set(sheet.Turn, RotateTransform.AngleProperty, pose[i].R);
                Tween.Set(sheet.Move, TranslateTransform.XProperty, pose[i].X);
                Tween.Set(sheet.Move, TranslateTransform.YProperty, pose[i].Y);
            }
        }
        UpdateBreath(full);
    }

    // The wash takes the current accent and follows the pointer, so it is built here rather than in the template.
    private void PrepareWash()
    {
        if (_wash is null || TryFindResource("ArcWpf.Brush.Accent") is not SolidColorBrush { Color: var accent }) return;
        _wash.Background = new RadialGradientBrush(new GradientStopCollection { new(Color.FromArgb(24, accent.R, accent.G, accent.B), 0), new(Color.FromArgb(0, accent.R, accent.G, accent.B), 1) })
        {
            MappingMode = BrushMappingMode.Absolute, RadiusX = 220, RadiusY = 220, Center = new Point(ActualWidth / 2, 100), GradientOrigin = new Point(ActualWidth / 2, 100),
        };
    }

    private void UpdateBreath(bool full)
    {
        if (_surface?.RenderTransform is not ScaleTransform scale) return;
        if (full && IsDragging)
        {
            var breathe = new DoubleAnimation(1, 1.012, TimeSpan.FromSeconds(1.1)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ArcEasing.InOut };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, breathe);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, breathe);
        }
        else
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        }
    }

    protected override void OnDragOver(DragEventArgs e)
    {
        base.OnDragOver(e);
        if (_wash?.Background is RadialGradientBrush { IsFrozen: false } wash)
        {
            var point = e.GetPosition(_wash);
            wash.Center = point;
            wash.GradientOrigin = point;
        }
    }

    private void OnDrop(object sender, DragEventArgs e) { IsDragging = false; if (e.Data.GetDataPresent(DataFormats.FileDrop)) AddFiles((string[])e.Data.GetData(DataFormats.FileDrop)); }
    private void Browse()
    {
        var dialog = new OpenFileDialog { Multiselect = Multiple, Filter = string.IsNullOrWhiteSpace(Accept) || Accept == "*.*" ? "All files|*.*" : $"Files|{Accept}" };
        if (dialog.ShowDialog() == true) AddFiles(dialog.FileNames);
    }
    private void PasteClipboard()
    {
        try
        {
            if (Clipboard.ContainsFileDropList()) { AddFiles(Clipboard.GetFileDropList().Cast<string>()); return; }
            if (!Clipboard.ContainsImage()) return;
            var path = Path.Combine(Path.GetTempPath(), $"arc-drop-{Guid.NewGuid():N}.png");
            using var stream = File.Create(path); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(Clipboard.GetImage())); encoder.Save(stream); AddFiles([path]);
        }
        catch { /* 剪贴板可能在其他进程锁定，保持输入不被打断。 */ }
    }
    private void AddFiles(IEnumerable<string> paths)
    {
        var selected = paths.Where(File.Exists).Take(Math.Max(0, MaxFiles - Items.Count)).ToArray();
        foreach (var path in selected)
        {
            var info = new FileInfo(path); var item = new FileDropzoneItem { Name = info.Name, Size = info.Length, FilePath = path, Status = "selected", Retryable = true, IsFresh = true };
            if (MaxSize > 0 && info.Length > MaxSize) { item.Status = "failed"; item.Error = $"File exceeds {FormatFileSize(MaxSize)}"; }
            Items.Add(item);
            if (Upload is not null && item.Status != "failed") _ = UploadItemAsync(item);
        }
        if (selected.Length > 0) FilesSelected?.Invoke(this, selected);
        UpdateLayoutFlags();
    }
    private readonly Dictionary<string, CancellationTokenSource> _uploads = new();
    public void RemoveItem(FileDropzoneItem item) { CancelUpload(item); Items.Remove(item); UpdateLayoutFlags(); }
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

    public static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{Math.Round(bytes / 1024d):0} KB";
        var mb = bytes / (1024d * 1024d);
        return mb >= 10 ? $"{Math.Round(mb):0} MB" : $"{mb:0.#} MB";
    }
}

internal static class FileGlyphs
{
    private const string Sheet = "M6,22 a2,2,0,0,1,-2,-2 V4 a2,2,0,0,1,2,-2 h8 a2.4,2.4,0,0,1,1.704,.706 l3.588,3.588 A2.4,2.4,0,0,1,20,8 v12 a2,2,0,0,1,-2,2 Z M14,2 v5 a1,1,0,0,0,1,1 h5";
    public const string Text = Sheet + " M10,9 H8 M16,13 H8 M16,17 H8";
    public const string Video = Sheet + " M15.033,13.44 a.647,.647,0,0,1,0,1.12 l-4.065,2.352 a.645,.645,0,0,1,-.968,-.56 v-4.704 a.645,.645,0,0,1,.967,-.56 Z";
    public const string Image = Sheet + " M10,10.5 a1.5,1.5,0,1,1,-3,0 a1.5,1.5,0,1,1,3,0 M20,17 l-1.296,-1.296 a2.41,2.41,0,0,0,-3.408,0 L9,22";
    public const string Archive = Sheet + " M10,10 h.01 M10,14 h.01 M10,18 h.01";
}

/// <summary>A file row: lands from above when it is new, and shakes when its upload fails.</summary>
internal sealed class FileDropzoneRow : Border
{
    private FileDropzoneItem? _item;

    public FileDropzoneRow()
    {
        DataContextChanged += (_, e) =>
        {
            if (_item is not null) _item.PropertyChanged -= OnChanged;
            _item = e.NewValue as FileDropzoneItem;
            if (_item is not null) _item.PropertyChanged += OnChanged;
        };
        Loaded += (_, _) =>
        {
            if (_item is not { IsFresh: true }) return;
            _item.IsFresh = false;
            if (ArcMotion.GetLevel(this) != ArcMotionLevel.Full) return;
            var move = new TranslateTransform(0, -22);
            var scale = new ScaleTransform(.94, .94);
            RenderTransformOrigin = new Point(.5, .5);
            RenderTransform = new TransformGroup { Children = { scale, move } };
            Opacity = 0;
            Tween.Spring(move, TranslateTransform.YProperty, 0, .42);
            Tween.Spring(scale, ScaleTransform.ScaleXProperty, 1, .42);
            Tween.Spring(scale, ScaleTransform.ScaleYProperty, 1, .42);
            Tween.To(this, OpacityProperty, 1, Tween.Standard, ArcEasing.Enter);
        };
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FileDropzoneItem.Status) || _item?.Status != "failed" || ArcMotion.GetLevel(this) != ArcMotionLevel.Full) return;
        var shake = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(.42) };
        double[] stops = [0, -7, 6, -4, 3, -1.5, 0];
        for (int i = 0; i < stops.Length; i++) shake.KeyFrames.Add(new LinearDoubleKeyFrame(stops[i], KeyTime.FromPercent(i / 6d)));
        var move = new TranslateTransform();
        RenderTransform = move;
        move.BeginAnimation(TranslateTransform.XProperty, shake);
    }
}
