using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ArcWpf.Controls;

public enum AttachmentTrayLayout { List, Chips }

public sealed class AttachmentEventArgs(PromptAttachment item) : EventArgs
{
    public PromptAttachment Item { get; } = item;
}

/// <summary>
/// The files going along with a message: a thumbnail or icon, upload progress, retry after a failure, and remove.
/// List follows Arc's file-upload rows, Chips the composer's row of chips. The caller runs the uploads and updates each <see cref="PromptAttachment"/>.
/// </summary>
[TemplatePart(Name = "PART_Root", Type = typeof(Decorator))]
public class AttachmentTray : Control
{
    public static readonly DependencyProperty LayoutProperty = DependencyProperty.Register(nameof(Layout), typeof(AttachmentTrayLayout), typeof(AttachmentTray), new FrameworkPropertyMetadata(AttachmentTrayLayout.List, (d, _) => ((AttachmentTray)d).Rebuild()));

    public AttachmentTrayLayout Layout { get => (AttachmentTrayLayout)GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }
    public ObservableCollection<PromptAttachment> Items { get; } = [];

    public event EventHandler<AttachmentEventArgs>? Removed;
    /// <summary>The attachment is back to uploading at 0%; start its upload again.</summary>
    public event EventHandler<AttachmentEventArgs>? RetryRequested;
    public event EventHandler<AttachmentEventArgs>? PreviewRequested;

    private readonly StackPanel _rows = new();
    private readonly StackPanel _chips = new() { Orientation = Orientation.Horizontal };
    private readonly ScrollViewer _chipScroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false };
    private readonly Dictionary<PromptAttachment, Entry> _entries = [];
    private Decorator? _root;

    internal IReadOnlyList<PromptAttachment> ShownForTest => [.. _entries.Keys];
    internal ToolbarButton RemoveForTest(PromptAttachment item) => _entries[item].Remove;
    internal ToolbarButton RetryForTest(PromptAttachment item) => _entries[item].Retry;
    internal bool RetryShownForTest(PromptAttachment item) => _entries[item].Retry.Visibility == Visibility.Visible;
    internal string MetaForTest(PromptAttachment item) => _entries[item].MetaText;
    internal bool BarShownForTest(PromptAttachment item) => _entries[item].BarShown;
    internal bool CheckShownForTest(PromptAttachment item) => _entries[item].CheckShown;
    internal double CheckDashOffsetForTest(PromptAttachment item) => _entries[item].CheckDashOffset;
    internal double ShownProgressForTest(PromptAttachment item) => _entries[item].ShownProgress;
    internal FrameworkElement? ThumbnailForTest(PromptAttachment item) => _entries[item].Thumbnail;
    internal FrameworkElement EntryForTest(PromptAttachment item) => _entries[item].Box;
    internal ScrollViewer ChipScrollForTest => _chipScroll;

    static AttachmentTray() => DefaultStyleKeyProperty.OverrideMetadata(typeof(AttachmentTray), new FrameworkPropertyMetadata(typeof(AttachmentTray)));

    public AttachmentTray()
    {
        _chipScroll.Content = _chips;
        // A plain wheel walks the row sideways when the chips overflow.
        _chipScroll.PreviewMouseWheel += (_, e) =>
        {
            if (_chipScroll.ScrollableWidth <= 0) { return; }
            _chipScroll.ScrollToHorizontalOffset(_chipScroll.HorizontalOffset - e.Delta / 2d);
            e.Handled = true;
        };
        Items.CollectionChanged += OnItems;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (_root is not null) { _root.Child = null; }
        _root = GetTemplateChild("PART_Root") as Decorator;
        Rebuild();
    }

    private void Rebuild()
    {
        if (_root is null) { return; }
        foreach (var entry in _entries.Values) { entry.Detach(); }
        _entries.Clear();
        _rows.Children.Clear();
        _chips.Children.Clear();
        foreach (var item in Items) { Place(item, -1, false); }
        _root.Child = Layout == AttachmentTrayLayout.Chips ? _chipScroll : _rows;
    }

    private void OnItems(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_root is null) { return; }
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                int at = e.NewStartingIndex;
                foreach (PromptAttachment item in e.NewItems!) { Place(item, at++, true); }
                Announcer.Say(this, e.NewItems.Count == 1 ? "1 file added." : $"{e.NewItems.Count} files added.");
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (PromptAttachment item in e.OldItems!) { Fold(item); }
                break;
            default:
                Rebuild();
                break;
        }
    }

    private Panel Host => Layout == AttachmentTrayLayout.Chips ? _chips : _rows;

    private void Place(PromptAttachment item, int index, bool animate)
    {
        var entry = new Entry(this, item, Layout == AttachmentTrayLayout.Chips);
        _entries[item] = entry;
        // A row still folding away keeps its slot until it is gone, so the new entry counts from the live ones.
        int slot = index < 0 ? Host.Children.Count : SlotOf(index);
        Host.Children.Insert(slot, entry.Box);
        if (animate && AiUi.Moving(this)) { entry.Enter(); }
    }

    private int SlotOf(int index)
    {
        int live = 0;
        for (int slot = 0; slot < Host.Children.Count; slot++)
        {
            if (Host.Children[slot] is SpringHeightBox { IsCollapsing: true } or SpringWidthFold { IsFolding: true }) { continue; }
            if (live == index) { return slot; }
            live++;
        }
        return Host.Children.Count;
    }

    private void Fold(PromptAttachment item)
    {
        if (!_entries.Remove(item, out var entry)) { return; }
        entry.Detach();
        var host = Host;
        entry.Leave(() => host.Children.Remove(entry.Box), AiUi.Moving(this));
    }

    private void RemoveItem(PromptAttachment item, ToolbarButton pressed)
    {
        int index = Items.IndexOf(item);
        if (index < 0) { return; }
        // Focus goes to the neighbouring remove button, or on to whatever comes next when none is left.
        if (pressed.IsKeyboardFocused)
        {
            var neighbour = index + 1 < Items.Count ? Items[index + 1] : index > 0 ? Items[index - 1] : null;
            if (neighbour is not null) { _entries[neighbour].Remove.Focus(); }
            else { pressed.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous)); }
        }
        Items.RemoveAt(index);
        Announcer.Say(this, $"{item.Name} removed.");
        Removed?.Invoke(this, new AttachmentEventArgs(item));
    }

    private void RetryItem(PromptAttachment item)
    {
        item.Error = null;
        item.Progress = 0;
        item.Status = PromptAttachmentStatus.Uploading;
        RetryRequested?.Invoke(this, new AttachmentEventArgs(item));
    }

    private static ImageSource? Thumb(PromptAttachment item)
    {
        if (item.Preview is not null) { return item.Preview; }
        if (item.FilePath is not { } path || !IsImage(path) || !System.IO.File.Exists(path)) { return null; }
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 96;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is NotSupportedException or System.IO.IOException or UriFormatException or ArgumentException) { return null; }
    }

    private static bool IsImage(string path) => System.IO.Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp";

    // One attachment on screen. The same parts are laid out as a file row or a chip.
    private sealed class Entry
    {
        private readonly AttachmentTray _tray;
        private readonly PromptAttachment _item;
        private readonly bool _chip;
        private readonly SpringDriver _driver;
        private readonly ControlSpring _progress;
        private readonly TextBlock _meta;
        private readonly ScaleTransform _fill = new(0, 1);
        private readonly SpringHeightBox? _barBox;
        private readonly Border? _bar;
        private readonly AiRing? _ring;
        private readonly Decorator _check = new() { Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        private readonly Path _checkPath = new() { Data = Geometry.Parse("M4 12.5 L9 17.5 L20 6.5"), Width = 24, Height = 24, StrokeThickness = 2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, StrokeDashArray = [12, 12] };
        private PromptAttachmentStatus _shown;
        private bool _landed;

        public FrameworkElement Box { get; }
        public ToolbarButton Remove { get; }
        public ToolbarButton Retry { get; }
        public FrameworkElement? Thumbnail { get; }
        public string MetaText => _meta.Text;
        public bool BarShown => _barBox is not null && _bar!.Visibility == Visibility.Visible;
        public bool CheckShown => _check.Visibility == Visibility.Visible;
        public double CheckDashOffset => _checkPath.StrokeDashOffset;
        public double ShownProgress => _progress.Position;

        public Entry(AttachmentTray tray, PromptAttachment item, bool chip)
        {
            (_tray, _item, _chip) = (tray, item, chip);
            _driver = new SpringDriver(Apply) { Settled = OnSettled };
            _progress = _driver.Add(item.Status == PromptAttachmentStatus.Done ? 1 : Math.Clamp(item.Progress, 0, 1), Tween.Smooth);
            _shown = item.Status;
            _landed = item.Status == PromptAttachmentStatus.Done;
            double hit = chip ? 24 : 26;
            Remove = AiUi.Icon(Lucide.X, hit, 14, $"Remove {item.Name}", hit / 2);
            Remove.Click += (_, _) => tray.RemoveItem(item, Remove);
            ThreadUi.PressScale(Remove, .96);
            Retry = AiUi.Icon(Lucide.RotateCcw, hit, 14, $"Retry {item.Name}", hit / 2);
            Retry.Click += (_, _) => tray.RetryItem(item);
            ThreadUi.PressScale(Retry, .96);
            _checkPath.SetResourceReference(Shape.StrokeProperty, "ArcWpf.Brush.Success");
            _check.Child = new Viewbox { Width = 14, Height = 14, Child = _checkPath, IsHitTestVisible = false };
            Thumbnail = Thumb(item) is { } source ? MakeThumbnail(source, chip ? 28 : 36) : null;
            _meta = AiUi.Line("", 12, 16.8, "TextMuted");
            var name = AiUi.Line(item.Name, chip ? 12 : 14, chip ? 16.8 : 19.6, "Foreground", true);
            if (chip)
            {
                _ring = new AiRing(16, 1.75);
                Box = BuildChip(name);
            }
            else
            {
                _bar = new Border { Height = 3, Margin = new Thickness(0, 5, 0, 1), CornerRadius = new CornerRadius(1.5), ClipToBounds = true };
                _bar.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Border");
                var fill = new Border { CornerRadius = new CornerRadius(1.5), RenderTransform = _fill };
                fill.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Accent");
                _bar.Child = fill;
                _barBox = new SpringHeightBox(Tween.Smooth) { Child = _bar };
                Box = BuildRow(name);
            }
            item.PropertyChanged += OnChanged;
            Sync(false);
            Apply();
        }

        public void Detach() { _item.PropertyChanged -= OnChanged; _driver.Stop(); }

        public void Enter()
        {
            if (Box is SpringHeightBox box) { box.Reveal(); Box.Opacity = 0; Tween.To(Box, UIElement.OpacityProperty, 1, Tween.Standard, ArcEasing.Enter); }
            else if (Box is SpringWidthFold fold) { fold.Open(); }
        }

        public void Leave(Action done, bool animate)
        {
            Box.IsHitTestVisible = false;
            if (Box is SpringHeightBox box)
            {
                if (animate) { Tween.To(Box, UIElement.OpacityProperty, 0, Tween.Fast, ArcEasing.Standard); }
                box.Collapse(done, animate);
            }
            else if (Box is SpringWidthFold fold) { fold.Fold(done, animate); }
        }

        private FrameworkElement BuildRow(TextBlock name)
        {
            // file-upload.module.css .fileItem: margin-top 6, padding 10 10 10 12, gap 12, radius 18.
            var card = new Border { Margin = new Thickness(0, 6, 0, 0), Padding = new Thickness(12, 10, 10, 10), CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(1) };
            card.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
            card.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderSubtle");
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            var lead = Thumbnail ?? AiUi.Glyph(_item.PastedText is null ? Lucide.FileText : Lucide.Layers, 16, "TextSecondary");
            var leadSlot = new Grid { Width = Thumbnail is null ? 24 : 36, Height = Thumbnail is null ? 24 : 36, VerticalAlignment = VerticalAlignment.Center };
            leadSlot.Children.Add(lead);
            grid.Children.Add(leadSlot);
            var copy = new StackPanel { Margin = new Thickness(12, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            copy.Children.Add(name);
            _meta.Margin = new Thickness(0, 2, 0, 0);
            copy.Children.Add(_meta);
            copy.Children.Add(_barBox!);
            Grid.SetColumn(copy, 1);
            grid.Children.Add(copy);
            var slot = new Grid { VerticalAlignment = VerticalAlignment.Center };
            slot.Children.Add(_check);
            slot.Children.Add(Retry);
            Grid.SetColumn(slot, 2);
            grid.Children.Add(slot);
            var removeSlot = new Decorator { Margin = new Thickness(12, 0, 0, 0), Child = Remove, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(removeSlot, 3);
            grid.Children.Add(removeSlot);
            card.Child = grid;
            return new SpringHeightBox(Tween.Smooth) { Child = card };
        }

        private FrameworkElement BuildChip(TextBlock name)
        {
            var chip = new Border { Height = 44, CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1), Padding = new Thickness(Thumbnail is null ? 10 : 7, 0, 6, 0) };
            chip.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var lead = Thumbnail ?? AiUi.Glyph(_item.PastedText is null ? Lucide.FileText : Lucide.Layers, 16, "TextSecondary");
            lead.Margin = new Thickness(0, 0, 8, 0);
            row.Children.Add(lead);
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, MaxWidth = 170 };
            text.Children.Add(name);
            text.Children.Add(_meta);
            row.Children.Add(text);
            var slot = new Grid { Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            slot.Children.Add(_check);
            slot.Children.Add(Retry);
            row.Children.Add(slot);
            Remove.Margin = new Thickness(2, 0, 0, 0);
            row.Children.Add(Remove);
            chip.Child = row;
            var fold = new SpringWidthFold { Child = chip, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Top };
            return fold;
        }

        private FrameworkElement MakeThumbnail(ImageSource source, double size)
        {
            var button = new ToolbarButton(8) { Width = size, Height = size, Padding = new Thickness(0), VerticalAlignment = VerticalAlignment.Center, Roles = _ => (null, null, "Foreground") };
            var face = new Border { Width = size, Height = size, CornerRadius = new CornerRadius(8), Background = new ImageBrush(source) { Stretch = Stretch.UniformToFill } };
            button.Content = face;
            AutomationProperties.SetName(button, $"Preview {_item.Name}");
            button.Click += (_, _) => _tray.PreviewRequested?.Invoke(_tray, new AttachmentEventArgs(_item));
            return button;
        }

        private void OnChanged(object? sender, PropertyChangedEventArgs e) => Sync(true);

        private void Sync(bool animate)
        {
            bool moving = animate && AiUi.Moving(_tray);
            var status = _item.Status;
            bool changed = status != _shown;
            _shown = status;
            if (changed) { Say(status); }
            double target = status == PromptAttachmentStatus.Done ? 1 : Math.Clamp(_item.Progress, 0, 1);
            // A retry starts over from an empty bar rather than sliding back along it.
            if (changed && status == PromptAttachmentStatus.Uploading) { _driver.Stop(); _progress.Snap(target); _landed = false; }
            else if (moving && status != PromptAttachmentStatus.Error) { if (changed && status == PromptAttachmentStatus.Done) { _landed = false; } _driver.Animate(_progress, target, Tween.Smooth); }
            else if (status != PromptAttachmentStatus.Error) { _driver.Stop(); _progress.Snap(target); _landed = status == PromptAttachmentStatus.Done; }
            Chrome(animate);
            Apply();
        }

        private void Say(PromptAttachmentStatus status)
        {
            if (status == PromptAttachmentStatus.Done) { Announcer.Say(_tray, $"{_item.Name} uploaded."); }
            else if (status == PromptAttachmentStatus.Error) { Announcer.Say(_tray, $"{_item.Name} failed. {_item.Error}"); }
        }

        private void OnSettled()
        {
            if (_item.Status == PromptAttachmentStatus.Done && !_landed) { _landed = true; Chrome(true); }
            Apply();
        }

        private void Apply()
        {
            double value = Math.Clamp(_progress.Position, 0, 1);
            _fill.ScaleX = value;
            if (_ring is not null) { _ring.Value = value; }
            // The label turns as soon as the count reads 100%, without waiting out the spring's last fraction.
            if (_item.Status == PromptAttachmentStatus.Done && !_landed && value >= .995) { _landed = true; Chrome(true); }
            Meta();
        }

        private void Meta()
        {
            string? phase = _item.Status switch
            {
                PromptAttachmentStatus.Uploading => $"Uploading {Math.Round(Math.Clamp(_progress.Position, 0, 1) * 100):0}%",
                PromptAttachmentStatus.Error => string.IsNullOrEmpty(_item.Error) ? "Upload failed" : _item.Error,
                _ => _chip ? null : _landed ? "Uploaded" : $"Uploading {Math.Round(Math.Clamp(_progress.Position, 0, 1) * 100):0}%",
            };
            _meta.Text = string.IsNullOrEmpty(_item.Detail) ? phase ?? "" : phase is null ? _item.Detail : $"{_item.Detail}  ·  {phase}";
            _meta.Visibility = _meta.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            _meta.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush." + (_item.Status == PromptAttachmentStatus.Error ? "Danger" : "TextMuted"));
        }

        private void Chrome(bool animate)
        {
            bool moving = animate && AiUi.Moving(_tray);
            var status = _item.Status;
            Retry.Visibility = status == PromptAttachmentStatus.Error ? Visibility.Visible : Visibility.Collapsed;
            bool uploading = status == PromptAttachmentStatus.Uploading || (status == PromptAttachmentStatus.Done && !_landed);
            if (_bar is not null)
            {
                // The bar fills on the spring, then folds away once the file lands.
                _barBox!.MorphNext();
                _bar.Visibility = uploading ? Visibility.Visible : Visibility.Collapsed;
            }
            bool ready = status == PromptAttachmentStatus.Done && _landed;
            if (ready && _check.Visibility != Visibility.Visible)
            {
                _check.Visibility = Visibility.Visible;
                if (moving)
                {
                    Tween.Set(_checkPath, Shape.StrokeDashOffsetProperty, 12d);
                    PopIn(_check);
                    Tween.To(_checkPath, Shape.StrokeDashOffsetProperty, 0, Tween.Standard, ArcEasing.Enter, .08);
                }
                else
                {
                    Tween.Set(_checkPath, Shape.StrokeDashOffsetProperty, 0d);
                    Tween.Set(_check, UIElement.OpacityProperty, 1d);
                }
            }
            else if (!ready) { _check.Visibility = Visibility.Collapsed; }
            if (_ring is not null)
            {
                // A running upload's control is its ring; pressing it cancels.
                AutomationProperties.SetName(Remove, uploading ? "Cancel upload" : $"Remove {_item.Name}");
                if (uploading && Remove.Content != _ring) { Remove.Content = _ring; }
                else if (!uploading && Remove.Content == _ring) { Remove.Content = Lucide.Glyph(Lucide.X, 14); }
            }
        }

        private static void PopIn(FrameworkElement element)
        {
            var scale = new ScaleTransform(.6, .6);
            element.RenderTransform = scale;
            element.RenderTransformOrigin = new Point(.5, .5);
            element.Opacity = 0;
            Tween.To(element, UIElement.OpacityProperty, 1, Tween.Fast, ArcEasing.Enter);
            Tween.Spring(scale, ScaleTransform.ScaleXProperty, 1, Tween.Snappy);
            Tween.Spring(scale, ScaleTransform.ScaleYProperty, 1, Tween.Snappy);
        }
    }
}

// A chip in the sideways row opens and closes its width on the same spring as the list rows' height.
internal sealed class SpringWidthFold : Decorator
{
    private readonly SpringDriver _driver;
    private readonly ControlSpring _width;
    private bool _folding;
    private Action? _done;

    public SpringWidthFold()
    {
        _driver = new SpringDriver(InvalidateMeasure) { Settled = OnSettled };
        _width = _driver.Add(1, Tween.Smooth);
        ClipToBounds = true;
    }

    public bool IsFolding => _folding;

    public void Open()
    {
        _width.Snap(0);
        Opacity = 0;
        Tween.To(this, OpacityProperty, 1, Tween.Standard, ArcEasing.Enter);
        _driver.Animate(_width, 1, Tween.Smooth);
    }

    public void Fold(Action done, bool animate)
    {
        _done = done;
        _folding = true;
        if (!animate || !IsLoaded) { Finish(); return; }
        Tween.To(this, OpacityProperty, 0, Tween.Fast, ArcEasing.Standard);
        _driver.Animate(_width, 0, Tween.Smooth);
    }

    private void OnSettled() { if (_folding) { Finish(); } }

    private void Finish() { var done = _done; _done = null; _folding = false; _driver.Stop(); done?.Invoke(); }

    protected override Size MeasureOverride(Size constraint)
    {
        if (Child is null) { return new Size(); }
        Child.Measure(new Size(double.PositiveInfinity, constraint.Height));
        return new Size(Child.DesiredSize.Width * Math.Clamp(_width.Position, 0, 1), Child.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        Child?.Arrange(new Rect(0, 0, Child.DesiredSize.Width, arrangeSize.Height));
        return arrangeSize;
    }
}
