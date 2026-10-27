using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class SegmentedControlPage : UserControl
{
    private readonly RangeModel _model=new();
    public SegmentedControlPage(){InitializeComponent();DataContext=_model;}
    private void SelectMonth(object sender,RoutedEventArgs e)=>_model.Selected="month";
    private void ToggleOption(object sender,RoutedEventArgs e){if(_model.Options.Count==3)_model.Options.Add(new("year","年"));else _model.Options.RemoveAt(3);}
    private void OnIntent(object? sender,SegmentIntentEventArgs e){if(IntentText is not null && e.Item is RangeOption option)IntentText.Text=$"已到达：{option.Label}";}
    private sealed record RangeOption(string Id,string Label);
    private sealed class RangeModel:INotifyPropertyChanged
    {
        public ObservableCollection<RangeOption> Options {get;}=[new("day","日"),new("week","周"),new("month","月")];
        private string _selected="week";
        public string Selected{get=>_selected;set{_selected=value;PropertyChanged?.Invoke(this,new(nameof(Selected)));}}
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
