using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class ReasoningBlockPage : UserControl
{
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(70) };
    private int _at;

    public ReasoningBlockPage()
    {
        InitializeComponent();
        Block.ModelName = "Claude Opus 5.5";
        Block.ModelMark = AiDemoData.Claude;
        Block.Reasoning = AiDemoData.Reasoning;
        Block.Seconds = 7;
        Block.Status = ReasoningStatus.Done;
        _timer.Tick += (_, _) => Stream();
        Unloaded += (_, _) => _timer.Stop();
    }

    private void Stream()
    {
        _at = Math.Min(AiDemoData.Reasoning.Length, _at + 16);
        Block.Reasoning = AiDemoData.Reasoning[.._at];
        if (_at < AiDemoData.Reasoning.Length) { return; }
        _timer.Stop();
        Block.Status = ReasoningStatus.Done;
    }

    private void OnThink(object sender, RoutedEventArgs e)
    {
        _at = 0;
        Block.Seconds = null;
        Block.Error = null;
        Block.Reasoning = "";
        Block.Status = ReasoningStatus.Thinking;
        _timer.Start();
    }

    private void OnStop(object? sender, EventArgs e) { _timer.Stop(); Block.Status = ReasoningStatus.Stopped; }
    private void OnFail(object sender, RoutedEventArgs e) { _timer.Stop(); Block.Error = "The model stopped responding"; Block.Status = ReasoningStatus.Error; }
    private void OnRetry(object? sender, EventArgs e) => OnThink(sender!, new RoutedEventArgs());
    private void OnPlain(object sender, RoutedEventArgs e) => Block.Variant = Plain.IsChecked == true ? ReasoningVariant.Plain : ReasoningVariant.Card;
}
