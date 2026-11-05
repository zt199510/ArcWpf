using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcWpf.Controls;

public enum ArtifactKind { Code, Json, Markdown, File }

public sealed class ArtifactItem
{
    /// <summary>The tab's label.</summary>
    public string Title { get; init; } = "";
    public ArtifactKind Kind { get; init; }
    /// <summary>The source, JSON or Markdown. For a file, a text excerpt to show under its details.</summary>
    public string? Content { get; init; }
    public string? FileName { get; init; }
    public string? Language { get; init; }
    /// <summary>For a file: "2.4 MB · PDF".</summary>
    public string? Detail { get; init; }
    public string? FilePath { get; init; }
    public ImageSource? Preview { get; init; }
}

public sealed class ArtifactEventArgs(ArtifactItem item) : EventArgs
{
    public ArtifactItem Item { get; } = item;
}

/// <summary>
/// Tabbed results of a run: code, JSON, Markdown and files. Arc has no such component; this composes Arc's Tabs, CodeBlock and JSONViewer
/// with the reply renderer, under one head that matches CodeBlock's.
/// </summary>
[TemplatePart(Name = "PART_Root", Type = typeof(Decorator))]
public class ArtifactPreview : Control
{
    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(ArtifactPreview), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((ArtifactPreview)d).OnSelected((int)e.NewValue)));
    public static readonly DependencyProperty BodyMaxHeightProperty = DependencyProperty.Register(nameof(BodyMaxHeight), typeof(double), typeof(ArtifactPreview), new FrameworkPropertyMetadata(360d, (d, _) => ((ArtifactPreview)d).Rebuild()));
    public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(nameof(EmptyText), typeof(string), typeof(ArtifactPreview), new FrameworkPropertyMetadata("No results yet", (d, _) => ((ArtifactPreview)d).Rebuild()));

    public int SelectedIndex { get => (int)GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    /// <summary>Tall JSON and Markdown scroll inside this height; code collapses to the matching number of lines.</summary>
    public double BodyMaxHeight { get => (double)GetValue(BodyMaxHeightProperty); set => SetValue(BodyMaxHeightProperty, value); }
    public string EmptyText { get => (string)GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }
    public ObservableCollection<ArtifactItem> Items { get; } = [];

    public event EventHandler<ArtifactEventArgs>? OpenRequested;
    public event EventHandler<ArtifactEventArgs>? DownloadRequested;

    private readonly Tabs _tabs = new();
    private readonly Grid _host = new();
    private readonly Border _empty = new() { Height = 120, CornerRadius = new CornerRadius(24), BorderThickness = new Thickness(1) };
    private readonly TextBlock _emptyText = AiUi.Line("", 14, 19.6, "TextMuted");
    private readonly List<FrameworkElement> _bodies = [];
    private Decorator? _root;
    private bool _syncing;

    internal Tabs TabsForTest => _tabs;
    internal FrameworkElement BodyForTest(int index) => _bodies[index];
    internal bool EmptyShownForTest => _empty.Visibility == Visibility.Visible;

    static ArtifactPreview() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ArtifactPreview), new FrameworkPropertyMetadata(typeof(ArtifactPreview)));

    public ArtifactPreview()
    {
        _empty.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderSubtle");
        _emptyText.HorizontalAlignment = HorizontalAlignment.Center;
        _empty.Child = _emptyText;
        _host.Children.Add(_tabs);
        _host.Children.Add(_empty);
        AutomationProperties.SetName(_tabs, "Results");
        _tabs.SelectionChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.OriginalSource, _tabs) || _syncing) { return; }
            _syncing = true;
            SetCurrentValue(SelectedIndexProperty, _tabs.SelectedIndex);
            _syncing = false;
        };
        Items.CollectionChanged += (_, _) => Rebuild();
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (_root is not null) { _root.Child = null; }
        _root = GetTemplateChild("PART_Root") as Decorator;
        if (_root is null) { return; }
        _root.Child = _host;
        Rebuild();
    }

    private void OnSelected(int index)
    {
        if (_syncing || _tabs.Items.Count == 0) { return; }
        _syncing = true;
        _tabs.SelectedIndex = Math.Clamp(index, 0, _tabs.Items.Count - 1);
        _syncing = false;
    }

    private void Rebuild()
    {
        if (_root is null) { return; }
        _syncing = true;
        _tabs.Items.Clear();
        _bodies.Clear();
        foreach (var item in Items)
        {
            var body = Body(item);
            // A TabControl aligns its content by the selected TabItem's alignment, which here centres the header label; hold the width instead.
            body.SetBinding(WidthProperty, new System.Windows.Data.Binding(nameof(ActualWidth)) { Source = _tabs });
            body.FocusVisualStyle = null;
            _bodies.Add(body);
            var tab = new TabItem { Header = string.IsNullOrEmpty(item.Title) ? item.FileName ?? item.Kind.ToString() : item.Title, Content = body };
            _tabs.Items.Add(tab);
        }
        if (Items.Count > 0) { _tabs.SelectedIndex = Math.Clamp(SelectedIndex, 0, Items.Count - 1); }
        _syncing = false;
        _emptyText.Text = EmptyText;
        _tabs.Visibility = Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _empty.Visibility = Items.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private FrameworkElement Body(ArtifactItem item) => item.Kind switch
    {
        ArtifactKind.Code => Code(item),
        ArtifactKind.Json => Json(item),
        ArtifactKind.Markdown => Markdown(item),
        _ => FileResult(item),
    };

    private CodeBlock Code(ArtifactItem item) => new()
    {
        Code = item.Content ?? "",
        Filename = item.FileName ?? item.Title,
        Language = item.Language ?? "",
        // code-block lines are 23.8 tall inside 20 of padding above and below.
        MaxLines = Math.Max(1, (int)((BodyMaxHeight - 40 - 52) / 23.8)),
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };

    private FrameworkElement Json(ArtifactItem item)
    {
        object? data;
        try
        {
            using var document = JsonDocument.Parse(item.Content ?? "null");
            data = document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            // A result that is not valid JSON still shows what arrived, with the reason in place of the tree.
            var failed = new StackPanel();
            var note = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var icon = AiUi.Glyph(Lucide.CircleAlert, 16, "Danger");
            icon.Margin = new Thickness(0, 0, 6, 0);
            note.Children.Add(icon);
            note.Children.Add(AiUi.Line("Invalid JSON: " + ex.Message.Split('.')[0], 12, 16.8, "Danger"));
            failed.Children.Add(note);
            failed.Children.Add(Code(new ArtifactItem { Title = item.Title, FileName = item.FileName, Language = "json", Content = item.Content }));
            return failed;
        }
        // JSONViewer brings its own frame, search and copy actions.
        return new JSONViewer { Data = data, RootName = System.IO.Path.GetFileNameWithoutExtension(item.FileName ?? item.Title), DefaultExpandDepth = 2, Label = item.Title, MaxHeight = BodyMaxHeight };
    }

    private FrameworkElement Markdown(ArtifactItem item)
    {
        var view = new StreamingMessage { Text = item.Content ?? "", Status = StreamingStatus.Done, CanRetry = false, HidesFoot = true, Margin = new Thickness(20) };
        var scroll = new OverlayScrollViewer { Content = view, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = BodyMaxHeight, Focusable = false };
        return Framed(item, scroll, item.FileName ?? item.Title, "md", item.Content ?? "");
    }

    private static Style? _copyStyle;
    private static Style CopyStyle => _copyStyle ??= (Style)new ResourceDictionary { Source = new Uri("/ArcWpf.Controls;component/CodeBlock/Themes/CodeBlockTheme.xaml", UriKind.Relative) }["ArcWpf.CodeBlock.CopyButton"];

    // The same head CodeBlock draws, for the kinds that do not come with their own.
    private static Border Framed(ArtifactItem item, FrameworkElement body, string name, string language, string copy)
    {
        var column = new DockPanel { LastChildFill = true };
        var head = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(16, 0, 12, 0), Height = 52 };
        head.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderSubtle");
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new StackPanel { Orientation = Orientation.Horizontal };
        var icon = AiUi.Glyph(Lucide.FileText, 16, "TextSecondary");
        icon.Margin = new Thickness(0, 0, 8, 0);
        title.Children.Add(icon);
        title.Children.Add(AiUi.Line(name, 14, 19.6, "Foreground", true));
        var dot = AiUi.Line("·", 12, 16.8, "TextMuted", false, false);
        dot.Margin = new Thickness(8, 0, 8, 0);
        title.Children.Add(dot);
        title.Children.Add(AiUi.Mono(language, 12, 16.8, "TextMuted"));
        row.Children.Add(title);
        var action = new CopyButton { Style = CopyStyle, Value = copy, Content = "Copy", Label = "Copy", CopiedLabel = "Copied", VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(action, 1);
        row.Children.Add(action);
        head.Child = row;
        DockPanel.SetDock(head, Dock.Top);
        column.Children.Add(head);
        column.Children.Add(body);
        var frame = new Border { CornerRadius = new CornerRadius(24), BorderThickness = new Thickness(1), Child = column, ClipToBounds = false };
        frame.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderSubtle");
        frame.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
        AutomationProperties.SetName(frame, item.Title);
        return frame;
    }

    private Border FileResult(ArtifactItem item)
    {
        var column = new StackPanel();
        var grid = new Grid { Margin = new Thickness(16) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var tile = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 0, 12, 0) };
        tile.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
        tile.Child = AiUi.Glyph(Lucide.FileText, 18, "TextSecondary");
        grid.Children.Add(tile);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(AiUi.Line(item.FileName ?? item.Title, 14, 19.6, "Foreground", true));
        if (!string.IsNullOrEmpty(item.Detail)) { text.Children.Add(AiUi.Line(item.Detail, 12, 16.8, "TextMuted")); }
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var open = AiUi.Outline("Open", Lucide.ExternalLink, 32);
        AutomationProperties.SetName(open, "Open " + (item.FileName ?? item.Title));
        open.Click += (_, _) => OpenRequested?.Invoke(this, new ArtifactEventArgs(item));
        actions.Children.Add(open);
        var download = AiUi.Outline("Download", Lucide.Download, 32);
        download.Margin = new Thickness(6, 0, 0, 0);
        AutomationProperties.SetName(download, "Download " + (item.FileName ?? item.Title));
        download.Click += (_, _) => DownloadRequested?.Invoke(this, new ArtifactEventArgs(item));
        actions.Children.Add(download);
        Grid.SetColumn(actions, 2);
        grid.Children.Add(actions);
        column.Children.Add(grid);
        if (item.Preview is { } image)
        {
            var picture = new Image { Source = image, Stretch = Stretch.Uniform, MaxHeight = BodyMaxHeight, Margin = new Thickness(16, 0, 16, 16), HorizontalAlignment = HorizontalAlignment.Center };
            AutomationProperties.SetName(picture, "Preview of " + (item.FileName ?? item.Title));
            column.Children.Add(picture);
        }
        else if (!string.IsNullOrEmpty(item.Content))
        {
            var excerpt = new TextBlock { Text = item.Content, FontSize = 13, LineHeight = 20.8, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, Margin = new Thickness(16) };
            excerpt.SetResourceReference(TextBlock.FontFamilyProperty, "ArcWpf.Font.Mono");
            excerpt.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextSecondary");
            var scroll = new OverlayScrollViewer { Content = excerpt, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = BodyMaxHeight, Focusable = false };
            var rule = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Child = scroll };
            rule.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderSubtle");
            column.Children.Add(rule);
        }
        var frame = new Border { CornerRadius = new CornerRadius(24), BorderThickness = new Thickness(1), Child = column };
        frame.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderSubtle");
        frame.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
        AutomationProperties.SetName(frame, item.Title);
        return frame;
    }
}
