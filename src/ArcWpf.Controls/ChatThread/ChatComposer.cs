using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace ArcWpf.Controls;

/// <summary>The message box on its own: autosizing text, attachments by button, paste, or drop, and a send button that morphs when there is something to send.</summary>
public class ChatComposer : Control
{
    private const double MaxFieldHeight = 6 * 22 + 18, GrowSeconds = Tween.Smooth, PopSeconds = .3;
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp"];

    public static readonly DependencyProperty PlaceholderProperty = Prop(nameof(Placeholder), "Message");
    public static readonly DependencyProperty AllowAttachmentsProperty = Prop(nameof(AllowAttachments), true);
    public static readonly DependencyProperty AcceptProperty = Prop<string?>(nameof(Accept), null);
    public static readonly DependencyProperty LabelsProperty = Prop<ChatComposerLabels>(nameof(Labels), new ChatComposerLabels());
    private static DependencyProperty Prop<T>(string name, T value) => DependencyProperty.Register(name, typeof(T), typeof(ChatComposer), new PropertyMetadata(value, (d, _) => ((ChatComposer)d).Refresh(false)));

    private readonly OwnedMotion _motion = new();
    private readonly SpringDriver _driver;
    private readonly ControlSpring _fieldHeight;
    private readonly List<string> _files = [];
    private readonly Dictionary<string, FrameworkElement> _pending = [];
    private Decorator? _root;
    private Border? _frame, _box;
    private ThemedBrush? _frameFill;
    private Grid? _field;
    private TextBox? _text;
    private TextBlock? _placeholder, _mirror;
    private ArcChromeButton? _attach, _send;
    private Grid? _sendGlyphs;
    private SpringHeightBox? _trayClip;
    private WrapPanel? _tray;
    private bool _dragging, _measured;

    static ChatComposer() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ChatComposer), new FrameworkPropertyMetadata(typeof(ChatComposer)));

    public ChatComposer()
    {
        _driver = new SpringDriver(() => { if (_field is not null) { _field.Height = _fieldHeight!.Position; } });
        _fieldHeight = _driver.Add(40, GrowSeconds);
        AllowDrop = true;
        Unloaded += (_, _) => { _driver.Finish(); _motion.Stop(); };
    }

    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public bool AllowAttachments { get => (bool)GetValue(AllowAttachmentsProperty); set => SetValue(AllowAttachmentsProperty, value); }
    /// <summary>The filter the attach dialog offers, as for <see cref="FileDialog.Filter"/>.</summary>
    public string? Accept { get => (string?)GetValue(AcceptProperty); set => SetValue(AcceptProperty, value); }
    public ChatComposerLabels Labels { get => (ChatComposerLabels)GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }
    public string Text { get => _text?.Text ?? string.Empty; set { if (_text is not null) { _text.Text = value; } } }
    public IReadOnlyList<string> Files => _files;
    private bool Ready => IsEnabled && (Text.Trim().Length > 0 || _files.Count > 0);
    private bool Full => IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;

    public event EventHandler<ChatDraftEventArgs>? Sent;

    internal TextBox? TextForTest => _text;
    internal ArcChromeButton? SendButtonForTest => _send;
    internal double FieldHeightTargetForTest => _fieldHeight.Target;
    internal bool IsDraggingForTest => _dragging;
    internal void FinishMotionForTest() => _driver.Finish();
    internal void AddFilesForTest(params string[] files) => Add(files);

    public new bool Focus() => _text?.Focus() ?? base.Focus();

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _root = GetTemplateChild("PART_Root") as Decorator;
        if (_root is null) { return; }
        _frameFill = new ThemedBrush(this, _motion);
        _text = ThreadUi.PlainTextBox();
        _text.FontSize = 16;
        _text.AcceptsReturn = true;
        _text.TextWrapping = TextWrapping.Wrap;
        _text.Padding = new Thickness(16, 9, 6, 9);
        TextBlock.SetLineHeight(_text, 22);
        TextBlock.SetLineStackingStrategy(_text, LineStackingStrategy.BlockLineHeight);
        TextColoring.SetColorEmoji(_text, true);
        _text.TextChanged += (_, _) => { Refresh(true); FitField(); };
        _text.PreviewKeyDown += OnFieldKey;
        _text.GotKeyboardFocus += (_, _) => RefreshBox(true);
        _text.LostKeyboardFocus += (_, _) => RefreshBox(true);
        DataObject.AddPastingHandler(_text, OnPaste);
        _placeholder = new TextBlock { FontSize = 16, LineHeight = 22, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, Margin = new Thickness(16, 9, 6, 9), IsHitTestVisible = false };
        _placeholder.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
        _mirror = new TextBlock { FontSize = 16, LineHeight = 22, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16, 9, 6, 9) };
        _field = new Grid { Height = 40, ClipToBounds = true, Children = { _placeholder, _text } };
        _field.SizeChanged += (_, e) => { if (e.WidthChanged) { FitField(); } };

        _send = new ArcChromeButton(16) { Width = 32, Height = 32, Margin = new Thickness(0, 0, 4, 4), VerticalAlignment = VerticalAlignment.Bottom, ClipToBounds = true };
        _send.Roles = _ => Ready ? ("Accent", null, "AccentForeground") : (null, null, "TextMuted");
        _send.Click += (_, _) => Send();
        _sendGlyphs = new Grid { Width = 16, Height = 16 };
        _sendGlyphs.Children.Add(SendGlyph());
        _send.Content = _sendGlyphs;
        ThreadUi.PressScale(_send, .92);

        var inside = new DockPanel();
        DockPanel.SetDock(_send, Dock.Right);
        inside.Children.Add(_send);
        inside.Children.Add(_field);
        _box = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(21), Child = inside };
        _box.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");

        _attach = new ArcChromeButton(20) { Width = 40, Height = 40, Margin = new Thickness(0, 0, 6, 1), VerticalAlignment = VerticalAlignment.Bottom };
        _attach.Roles = button => (button.IsMouseOver ? "SurfaceMuted" : null, null, button.IsMouseOver ? "Foreground" : "TextSecondary");
        _attach.Content = ArcText.Icon("paperclip", 18, _attach.Ink);
        _attach.Click += (_, _) => Browse();
        ThreadUi.PressScale(_attach, .94);

        var row = new DockPanel();
        DockPanel.SetDock(_attach, Dock.Left);
        row.Children.Add(_attach);
        row.Children.Add(_box);

        _tray = new WrapPanel { Margin = new Thickness(4, 8, 8, 10) };
        _trayClip = new SpringHeightBox(GrowSeconds) { Child = _tray, Visibility = Visibility.Collapsed };
        _frame = new Border { Padding = new Thickness(12, 10, 12, 12), BorderThickness = new Thickness(0, 1, 0, 0), Background = _frameFill.Brush, Child = new StackPanel { Children = { _trayClip, row } } };
        _frame.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderSubtle");
        _root.Child = _frame;
        ThemeWatch.Add(this, () => { RefreshBox(false); _send.Refresh(false); });
        Refresh(false);
        RefreshBox(false);
    }

    private ArcDataGridIcon SendGlyph()
    {
        var glyph = ArcText.Icon("arrow-up", 16, _send!.Ink, 2.25);
        glyph.RenderTransform = new TranslateTransform();
        return glyph;
    }

    private void Refresh(bool animate)
    {
        if (_text is null || _send is null || _attach is null || _placeholder is null) { return; }
        _placeholder.Text = Placeholder;
        _placeholder.Visibility = _text.Text.Length > 0 ? Visibility.Collapsed : Visibility.Visible;
        AutomationProperties.SetName(_text, Placeholder);
        AutomationProperties.SetName(_send, Labels.Send);
        AutomationProperties.SetName(_attach, Labels.Attach);
        _attach.Visibility = AllowAttachments ? Visibility.Visible : Visibility.Collapsed;
        _send.Cursor = Ready ? Cursors.Hand : Cursors.Arrow;
        _send.Refresh(animate && IsLoaded);
    }

    private void RefreshBox(bool animate)
    {
        if (_box is null || _frameFill is null || _text is null) { return; }
        _box.SetResourceReference(Border.BorderBrushProperty, _dragging ? "ArcWpf.Brush.Accent" : _text.IsKeyboardFocused ? "ArcWpf.Brush.BorderStrong" : "ArcWpf.Brush.Border");
        _frameFill.To(_dragging ? "AccentSubtle" : null, animate);
    }

    // The field grows to its text, up to six lines, and the send button rides the bottom edge.
    private void FitField()
    {
        if (_text is null || _mirror is null || _field is null || _field.ActualWidth <= 0) { return; }
        _mirror.FontFamily = _text.FontFamily;
        _mirror.Text = _text.Text + "​";
        _mirror.Measure(new Size(Math.Max(0, _field.ActualWidth - 22), double.PositiveInfinity));
        double target = Math.Min(MaxFieldHeight, Math.Max(40, _mirror.DesiredSize.Height));
        if (!_measured || !Full) { _measured = true; _driver.Snap(_fieldHeight, target); return; }
        if (Math.Abs(_fieldHeight.Target - target) > .01) { _driver.Animate(_fieldHeight, target, GrowSeconds); }
    }

    private void OnFieldKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && !e.IsRepeat && e.ImeProcessedKey == Key.None)
        {
            e.Handled = true;
            Send();
        }
    }

    private void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (!AllowAttachments || !e.DataObject.GetDataPresent(DataFormats.FileDrop)) { return; }
        e.CancelCommand();
        Add(e.DataObject.GetData(DataFormats.FileDrop) as string[]);
    }

    protected override void OnDragOver(DragEventArgs e)
    {
        base.OnDragOver(e);
        if (!AllowAttachments || !e.Data.GetDataPresent(DataFormats.FileDrop)) { return; }
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
        if (!_dragging) { _dragging = true; RefreshBox(true); }
    }

    protected override void OnDragLeave(DragEventArgs e)
    {
        base.OnDragLeave(e);
        if (_dragging) { _dragging = false; RefreshBox(true); }
    }

    protected override void OnDrop(DragEventArgs e)
    {
        base.OnDrop(e);
        _dragging = false;
        RefreshBox(true);
        if (!AllowAttachments) { return; }
        e.Handled = true;
        Add(e.Data.GetData(DataFormats.FileDrop) as string[]);
    }

    private void Browse()
    {
        var dialog = new OpenFileDialog { Multiselect = true };
        if (!string.IsNullOrEmpty(Accept)) { dialog.Filter = Accept; }
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) { Add(dialog.FileNames); }
    }

    private void Add(string[]? list)
    {
        if (!AllowAttachments || list is null || _tray is null || _trayClip is null) { return; }
        bool wasEmpty = _files.Count == 0;
        var next = list.Take(10).Where(path => !_files.Contains(path)).ToList();
        if (next.Count == 0) { return; }
        foreach (string path in next)
        {
            _files.Add(path);
            var item = BuildPending(path);
            _pending[path] = item;
            _tray.Children.Add(item);
            if (Full) { PopIn(item); }
        }
        if (wasEmpty)
        {
            _trayClip.Visibility = Visibility.Visible;
            if (Full) { _trayClip.Reveal(); _trayClip.Opacity = 0; Tween.To(_trayClip, OpacityProperty, 1, Tween.Fast, ArcEasing.Standard); }
        }
        else if (Full) { _trayClip.MorphNext(); }
        Refresh(true);
    }

    private void PopIn(FrameworkElement item)
    {
        var scale = new ScaleTransform(.9, .9);
        item.RenderTransform = scale;
        item.RenderTransformOrigin = new Point(.5, .5);
        Tween.Spring(scale, ScaleTransform.ScaleXProperty, 1, PopSeconds);
        Tween.Spring(scale, ScaleTransform.ScaleYProperty, 1, PopSeconds);
        item.Opacity = 0;
        Tween.Spring(item, OpacityProperty, 1, PopSeconds);
        ArcEffects.BlurIn(_motion, item, 4, PopSeconds);
    }

    private FrameworkElement BuildPending(string path)
    {
        string name = Path.GetFileName(path);
        FrameworkElement face;
        var image = ImageExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()) ? TryLoad(path) : null;
        if (image is not null)
        {
            face = new Border { Width = 56, Height = 56, CornerRadius = new CornerRadius(12), Background = new ImageBrush(image) { Stretch = Stretch.UniformToFill } };
        }
        else
        {
            var glyph = ArcText.Icon("file-text", 16, (Brush)FindResource("ArcWpf.Brush.TextSecondary"));
            glyph.Margin = new Thickness(0, 0, 8, 0);
            var title = new TextBlock { Text = name, FontSize = 12, FontWeight = FontWeights.Medium, TextTrimming = TextTrimming.CharacterEllipsis };
            title.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.Foreground");
            long bytes = 0;
            try { bytes = new FileInfo(path).Length; } catch (IOException) { } catch (UnauthorizedAccessException) { }
            var size = new TextBlock { Text = ChatRules.FormatBytes(bytes), FontSize = 12 };
            size.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
            var copy = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { title, size } };
            DockPanel.SetDock(glyph, Dock.Left);
            var frame = new Border { Width = 168, Height = 56, Padding = new Thickness(12, 0, 12, 0), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Child = new DockPanel { Children = { glyph, copy } } };
            frame.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
            frame.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
            face = frame;
        }
        var remove = new ArcChromeButton(10, 2) { Width = 20, Height = 20, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -6, -6, 0) };
        remove.Roles = _ => ("Foreground", "Surface", "Background");
        remove.Content = ArcText.Icon("x", 12, remove.Ink, 2.25);
        AutomationProperties.SetName(remove, Labels.RemoveFile(name));
        remove.Click += (_, _) => Remove(path);
        return new Grid { Margin = new Thickness(0, 0, 8, 0), Children = { face, remove } };
    }

    private static BitmapImage? TryLoad(string path)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelHeight = 112;
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception error) when (error is IOException or NotSupportedException or UriFormatException or UnauthorizedAccessException or InvalidOperationException) { return null; }
    }

    private void Remove(string path)
    {
        if (!_pending.TryGetValue(path, out var item) || _tray is null) { return; }
        _pending.Remove(path);
        _files.Remove(path);
        _tray.Children.Remove(item);
        Refresh(true);
        if (_files.Count == 0) { CollapseTray(); } else if (Full) { _trayClip!.MorphNext(); }
        _text?.Focus();
    }

    private void CollapseTray()
    {
        if (_trayClip is null) { return; }
        _trayClip.Collapse(() => _trayClip.Visibility = Visibility.Collapsed, Full);
        if (Full) { Tween.To(_trayClip, OpacityProperty, 0, Tween.Fast, ArcEasing.Standard); }
    }

    public void Send()
    {
        if (!Ready || _text is null) { return; }
        var draft = new ChatDraft(_text.Text.Trim(), _files.ToList());
        _files.Clear();
        _pending.Clear();
        _tray?.Children.Clear();
        if (_trayClip is { Visibility: Visibility.Visible }) { CollapseTray(); }
        _text.Text = string.Empty;
        FlySendGlyph();
        Sent?.Invoke(this, new ChatDraftEventArgs(draft));
        _text.Focus();
    }

    // The old arrow leaves upward as a new one comes up from below.
    private void FlySendGlyph()
    {
        if (_sendGlyphs is null || !Full) { return; }
        var old = _sendGlyphs.Children.OfType<ArcDataGridIcon>().LastOrDefault();
        var next = SendGlyph();
        var nextMove = (TranslateTransform)next.RenderTransform;
        nextMove.Y = 14;
        next.Opacity = 0;
        _sendGlyphs.Children.Add(next);
        Tween.Spring(nextMove, TranslateTransform.YProperty, 0, Tween.Snappy);
        Tween.Spring(next, OpacityProperty, 1, Tween.Snappy);
        if (old is null) { return; }
        Tween.To((TranslateTransform)old.RenderTransform, TranslateTransform.YProperty, -18, Tween.Fast, ArcEasing.Standard);
        Tween.To(old, OpacityProperty, 0, Tween.Fast, ArcEasing.Standard, done: () => _sendGlyphs.Children.Remove(old));
    }
}
