using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace ArcWpf.Gallery.Navigation;
public partial class GalleryNavigation:UserControl
{
    public ObservableCollection<NavigationNode> Groups {get;}=[];
    private readonly Dictionary<string,bool> _expandedBeforeSearch=[];
    private string _selectedId="";
    private bool _filtering;
    private DispatcherOperation? _pending;
    public event EventHandler<string>? PageSelected;
    public string SelectedPageId=>_selectedId;
    public string Query {get=>SearchBox.Text;set=>SearchBox.Text=value;}
    public GalleryNavigation(){InitializeComponent();Tree.ItemsSource=Groups;Tree.LayoutUpdated+=(_,_)=>UpdateSticky();Unloaded+=(_,_)=>{_pending?.Abort();_pending=null;};}
    public void SetGroups(IEnumerable<NavigationNode> groups){Groups.Clear();foreach(var g in groups){g.Count=g.Children.Count;Groups.Add(g);}TotalCount.Text=Groups.Sum(g=>g.Count).ToString();}
    public void FocusSearch(){SearchBox.Focus();SearchBox.SelectAll();}
    public bool SelectPage(string id,bool reveal=true)
    {
        var group=Groups.FirstOrDefault(g=>g.Children.Any(n=>n.Id==id));var node=group?.Children.FirstOrDefault(n=>n.Id==id);if(node is null)return false;
        _selectedId=id;foreach(var g in Groups)foreach(var n in g.Children)n.IsSelected=ReferenceEquals(n,node);
        if(reveal&&node.Visibility==Visibility.Visible){group!.IsExpanded=true;QueueReveal(node);}
        return true;
    }
    private void QueueReveal(NavigationNode node)
    {
        _pending?.Abort();_pending=Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{_pending=null;Tree.UpdateLayout();foreach(var group in Groups){if(Tree.ItemContainerGenerator.ContainerFromItem(group) is NavigationItem root && root.ItemContainerGenerator.ContainerFromItem(node) is NavigationItem item){item.BringIntoView();return;}}}));
    }
    private void SelectedChanged(object sender,RoutedPropertyChangedEventArgs<object> e)
    {if(e.NewValue is NavigationNode {IsGroup:false} node){_selectedId=node.Id;if(!_filtering)PageSelected?.Invoke(this,node.Id);}}
    private void SearchChanged(object sender,TextChangedEventArgs e)
    {
        if(Tree is null)return;string query=SearchBox.Text.Trim();bool searching=query.Length>0;
        if(searching&&_expandedBeforeSearch.Count==0)foreach(var g in Groups)_expandedBeforeSearch[g.Id]=g.IsExpanded;
        _filtering=true;
        try{foreach(var group in Groups){bool groupMatch=Matches(group,query);foreach(var node in group.Children)node.Visibility=!searching||groupMatch||Matches(node,query)?Visibility.Visible:Visibility.Collapsed;group.Count=group.Children.Count(n=>n.Visibility==Visibility.Visible);group.Visibility=group.Count>0?Visibility.Visible:Visibility.Collapsed;if(searching)group.IsExpanded=group.Count>0;else if(_expandedBeforeSearch.TryGetValue(group.Id,out var expanded))group.IsExpanded=expanded;}}
        finally{_filtering=false;}
        if(!searching)_expandedBeforeSearch.Clear();int total=Groups.Sum(g=>g.Count);TotalCount.Text=total.ToString();Empty.Visibility=total==0?Visibility.Visible:Visibility.Collapsed;Watermark.Visibility=SearchBox.Text.Length>0?Visibility.Collapsed:Visibility.Visible;Shortcut.Visibility=SearchBox.Text.Length>0?Visibility.Collapsed:Visibility.Visible;ClearButton.Visibility=SearchBox.Text.Length>0?Visibility.Visible:Visibility.Collapsed;
        Scroller.ScrollToTop();if(!searching&&_selectedId.Length>0)SelectPage(_selectedId,false);
    }
    private static bool Matches(NavigationNode node,string text)=>node.Label.Contains(text,StringComparison.OrdinalIgnoreCase)||node.Id.Contains(text,StringComparison.OrdinalIgnoreCase);
    private void ClearSearch(object sender,RoutedEventArgs e){Query="";FocusSearch();}
    private void SearchKey(object sender,KeyEventArgs e)
    {
        if(e.Key==Key.Escape){Query="";e.Handled=true;return;}
        if(e.Key is not(Key.Enter or Key.Down))return;
        var node=Groups.SelectMany(g=>g.Children).FirstOrDefault(n=>n.Visibility==Visibility.Visible);if(node is null)return;
        _filtering=true;
        try{SelectPage(node.Id);Tree.UpdateLayout();foreach(var g in Groups)if(Tree.ItemContainerGenerator.ContainerFromItem(g) is NavigationItem group && group.ItemContainerGenerator.ContainerFromItem(node) is NavigationItem item)item.Focus();}
        finally{_filtering=false;}
        PageSelected?.Invoke(this,node.Id);e.Handled=true;
    }
    private void Scrolled(object sender,ScrollChangedEventArgs e)
    {double h=Scroller.ViewportHeight;if(h<=0)return;bool more=Scroller.ScrollableHeight-Scroller.VerticalOffset>1;Scroller.OpacityMask=more?new LinearGradientBrush(new GradientStopCollection{new(Colors.Black,0),new(Colors.Black,Math.Max(0,1-40/h)),new(Colors.Transparent,1)},new Point(0,0),new Point(0,1)):null;UpdateSticky();}
    private void UpdateSticky()
    {
        if(!IsLoaded||Scroller is null)return;NavigationNode? current=null;double next=double.PositiveInfinity;
        foreach(var node in Groups.Where(g=>g.Visibility==Visibility.Visible))
        {if(Tree.ItemContainerGenerator.ContainerFromItem(node) is not NavigationItem item)continue;double y=item.TranslatePoint(new Point(),Scroller).Y;if(y<0&&item.IsExpanded&&y+item.ActualHeight>0)current=node;else if(y>=0)next=Math.Min(next,y);}
        StickyGroup.Visibility=current is null?Visibility.Collapsed:Visibility.Visible;if(current is not null){StickyGroup.DataContext=current;StickyGroup.RenderTransform=new TranslateTransform(0,Math.Min(0,next-28));}
    }
    private void StickyClick(object sender,RoutedEventArgs e){if(StickyGroup.DataContext is NavigationNode node){node.IsExpanded=false;Tree.UpdateLayout();UpdateSticky();}}
}
