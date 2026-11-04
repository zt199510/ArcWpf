using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class PromptInputPage : UserControl
{
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(40) };
    private readonly DispatcherTimer _first = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(700) };
    private readonly DispatcherTimer _upload = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(60) };
    private PromptAttachment? _uploading;
    private bool _failNext;
    private int _at;

    public PromptInputPage()
    {
        InitializeComponent();
        Composer.Commands = AiDemoData.Commands;
        Composer.Mentions = AiDemoData.Mentions;
        Composer.AttachActions = AiDemoData.AttachActions;
        Composer.Tools = [new PromptTool("web", "Web search", AiGlyphs.Globe), new PromptTool("thinking", "Extended thinking", AiGlyphs.Brain)];
        Composer.Models = AiDemoData.Models;
        Composer.Providers = AiDemoData.Providers;
        Composer.FeaturedModels = AiDemoData.Featured;
        Composer.Model = "opus-5.5";
        foreach (var (label, glyph, prompt) in new[] { ("Summarize the Q4 launch plan", AiGlyphs.FileText, "/summarize @Q4 launch plan into five bullets"), ("Draft a release note", AiGlyphs.Pencil, "Draft a release note for the checkout fix"), ("Explain the checkout retry bug", AiGlyphs.Bug, "/explain the retry bug in @checkout.tsx") })
        {
            var button = new ArcWpf.Controls.Button { Margin = new Thickness(4), Content = label, Variant = ButtonVariant.Secondary };
            button.Click += (_, _) => Composer.SetPrompt(prompt);
            Suggestions.Children.Add(button);
        }
        _first.Tick += (_, _) => { _first.Stop(); Begin(); };
        _timer.Tick += (_, _) => Tick();
        _upload.Tick += (_, _) => Upload();
        Unloaded += (_, _) => { _first.Stop(); _timer.Stop(); _upload.Stop(); };
    }

    private void OnSubmit(object? sender, PromptMessageEventArgs e)
    {
        var message = e.Message;
        Log.Text = $"Submitted: {message.Tokens.Count} tokens, {message.Attachments.Count} attachments, {message.Model}, {message.Effort}, tools [{string.Join(", ", message.Tools)}]. The reply is simulated.";
        Greeting.Visibility = Visibility.Collapsed;
        Suggestions.Visibility = Visibility.Collapsed;
        Start();
    }

    private void Start()
    {
        _at = 0;
        Composer.ErrorText = null;
        Composer.IsGenerating = true;
        Composer.Progress = 0;
        Thread.Visibility = Visibility.Visible;
        Reply.ModelName = Composer.PickedName;
        Reply.Error = null;
        Reply.Sources = AiDemoData.Sources;
        Reply.Status = StreamingStatus.Thinking;
        Reply.Text = "";
        _first.Start();
    }

    private void Begin()
    {
        if (_failNext)
        {
            _failNext = false;
            Finish(StreamingStatus.Error);
            Reply.Error = "The model is busy. Try again in a moment.";
            Composer.ErrorText = "The last message did not get a reply.";
            return;
        }
        Reply.Status = StreamingStatus.Streaming;
        _timer.Start();
    }

    private void Tick()
    {
        string script = AiDemoData.Answer;
        int size = Math.Min(script.Length - _at, Random.Shared.Next(3, 24));
        Reply.Append(script.Substring(_at, size));
        _at += size;
        Composer.Progress = _at / (double)script.Length;
        if (_at >= script.Length) { Finish(StreamingStatus.Done); }
    }

    private void Finish(StreamingStatus status)
    {
        _first.Stop();
        _timer.Stop();
        Composer.IsGenerating = false;
        Composer.Progress = null;
        Reply.Status = status;
    }

    private void OnStop(object? sender, EventArgs e) => Finish(StreamingStatus.Stopped);
    private void OnRetry(object? sender, EventArgs e) => Start();
    private void OnFailNext(object sender, RoutedEventArgs e) => _failNext = true;
    private void OnDictate(object? sender, EventArgs e) { Composer.IsDictating = !Composer.IsDictating; Log.Text = "DictationRequested: hook up your own recognizer."; }

    private void OnAttach(object? sender, PromptAttachEventArgs e)
    {
        if (e.ActionId == "upload") { return; }
        e.Handled = true;
        Log.Text = $"AttachRequested: {e.ActionId}. Replace this with your own picker.";
    }

    private void OnUpload(object sender, RoutedEventArgs e)
    {
        _uploading = new PromptAttachment("brand-guidelines.pdf") { Detail = "4.8 MB", Status = PromptAttachmentStatus.Uploading, Progress = 0 };
        Composer.Attachments.Add(_uploading);
        _upload.Start();
    }

    private void Upload()
    {
        if (_uploading is null || !Composer.Attachments.Contains(_uploading)) { _upload.Stop(); return; }
        _uploading.Progress = Math.Min(1, _uploading.Progress + .03);
        if (_uploading.Progress >= 1) { _uploading.Status = PromptAttachmentStatus.Done; _upload.Stop(); }
    }
}
