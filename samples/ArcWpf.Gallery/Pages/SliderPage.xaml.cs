using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;
public partial class SliderPage : UserControl
{
    private int _commits;
    private double _unmuted = 60;
    public SliderPage()
    {
        InitializeComponent(); DataContext = new SliderModel();
        Reference.Marks = new[] { 0, 25, 50, 75, 100 }.Select(n => new SliderMark(n, n + "%")).ToArray();
        Bound.Marks = new[] { 50, 100, 150, 200 }.Select(n => new SliderMark(n, n + "%")).ToArray();
    }
    private void ToggleMute(object sender, RoutedEventArgs e)
    {
        if (Reference.Value == 0) Reference.SetCurrentValue(Controls.Slider.ValueProperty, _unmuted);
        else { _unmuted = Reference.Value; Reference.SetCurrentValue(Controls.Slider.ValueProperty, 0d); }
        VolumeWaves.Visibility = Reference.Value == 0 ? Visibility.Hidden : Visibility.Visible;
    }
    private void SetAmount(object sender, RoutedEventArgs e) => ((SliderModel)DataContext).Amount = 150;
    private void Committed(object sender, RoutedEventArgs e) => CommitSummary.Text = "完成操作：" + ++_commits;
}
public sealed class SliderModel : INotifyPropertyChanged
{
    private double _amount = 100;
    public double Amount { get => _amount; set { _amount = value; PropertyChanged?.Invoke(this, new(nameof(Amount))); PropertyChanged?.Invoke(this, new(nameof(Summary))); } }
    public string Summary => $"Value: {Amount:0.##}%";
    public event PropertyChangedEventHandler? PropertyChanged;
}
