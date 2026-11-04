using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class StreamingMessagePage : UserControl
{
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(40) };
    private readonly DispatcherTimer _first = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(700) };
    private string _script = AiDemoData.Answer;
    private int _at;

    public StreamingMessagePage()
    {
        InitializeComponent();
        Reply.ModelName = "Claude Opus 5.5";
        Reply.ModelMark = AiDemoData.Claude;
        Reply.Sources = AiDemoData.Sources;
        Reply.Text = AiDemoData.Answer;
        Reply.Status = StreamingStatus.Done;
        _first.Tick += (_, _) => { _first.Stop(); Reply.Status = StreamingStatus.Streaming; _timer.Start(); };
        _timer.Tick += (_, _) => Tick();
        Unloaded += (_, _) => { _first.Stop(); _timer.Stop(); };
    }

    // The network hands over uneven pieces; the message paces them itself.
    private void Tick()
    {
        int size = Math.Min(_script.Length - _at, Random.Shared.Next(3, 28));
        Reply.Append(_script.Substring(_at, size));
        _at += size;
        if (_at < _script.Length) { return; }
        _timer.Stop();
        Reply.Status = StreamingStatus.Done;
    }

    private void Start(string script)
    {
        _script = script;
        _at = 0;
        _timer.Stop();
        Reply.Error = null;
        Reply.Status = StreamingStatus.Thinking;
        Reply.Text = "";
        _first.Start();
    }

    private void OnReplay(object sender, RoutedEventArgs e) => Start(AiDemoData.Answer);
    private void OnLong(object sender, RoutedEventArgs e) => Start(string.Join("\n\n", Enumerable.Repeat(AiDemoData.Answer, 4)));
    private void OnRetry(object? sender, EventArgs e) => Start(_script);
    private void OnStop(object? sender, EventArgs e) { _first.Stop(); _timer.Stop(); Reply.Status = StreamingStatus.Stopped; }
    private void OnFail(object sender, RoutedEventArgs e) { _first.Stop(); _timer.Stop(); Reply.Error = "The connection closed before the reply finished."; Reply.Status = StreamingStatus.Error; }
}
