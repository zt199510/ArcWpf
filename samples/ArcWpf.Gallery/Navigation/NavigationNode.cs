using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
namespace ArcWpf.Gallery.Navigation;
public sealed class NavigationNode : INotifyPropertyChanged
{
    public string Id { get; init; }="";
    public string Label { get; init; }="";
    public ObservableCollection<NavigationNode> Children {get;}=[];
    public bool IsGroup=>Children.Count>0;
    private bool _expanded=true,_selected;
    private Visibility _visibility=Visibility.Visible;
    private int _count;
    public bool IsExpanded{get=>_expanded;set{_expanded=value;Notify(nameof(IsExpanded));}}
    public bool IsSelected{get=>_selected;set{_selected=value;Notify(nameof(IsSelected));}}
    public Visibility Visibility{get=>_visibility;set{_visibility=value;Notify(nameof(Visibility));}}
    public int Count{get=>_count;set{_count=value;Notify(nameof(Count));}}
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify(string name)=>PropertyChanged?.Invoke(this,new(name));
}
