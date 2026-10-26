using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ArcWpf.Controls;
using ArcWpf.Gallery.Pages;
namespace ArcWpf.Tests;

[TestClass]
public class MultiSelectTests
{
    [TestMethod] public void SelectionBindingFilteringAndExternalCollectionPreserveHiddenValues()=>Sta.Run(()=>InWindow(c=>
    {
        var model=new MultiSelectModel();c.SetBinding(MultiSelect.SelectedValuesProperty,new Binding("Values"){Source=model,Mode=BindingMode.TwoWay});
        c.SearchText="Feature";c.IsDropDownOpen=true;Pump(50);
        var list=Part<ListBox>(c,"PART_List");Assert.HasCount(1,list.Items);list.SelectedItems.Add(list.Items[0]);
        CollectionAssert.AreEquivalent(new[]{"bug","feature"},model.Values.ToArray());Assert.IsNotNull(c.GetBindingExpression(MultiSelect.SelectedValuesProperty));
        c.SearchText="not found";Assert.HasCount(0,list.Items);Assert.HasCount(2,c.SelectedValues!);
        var values=new ObservableCollection<string>{"docs"};model.Values=values;values.Add("feature");Assert.HasCount(2,c.SelectedValues!);
        c.SearchText="";Assert.HasCount(2,list.SelectedItems);c.RemoveValue("docs");CollectionAssert.AreEqual(new[]{"feature"},model.Values.ToArray());
        c.IsReadOnly=true;c.ClearSelection();Assert.HasCount(1,model.Values);c.IsReadOnly=false;c.ClearSelection();Assert.IsEmpty(model.Values);
    }));
    [TestMethod] public void OptionsReplaceDoNotMutateSharedViewOrDiscardUnknownSelection()=>Sta.Run(()=>InWindow(c=>
    {
        var options=new ObservableCollection<MultiSelectOption>(c.Options!);c.Options=options;c.SelectedValues=new[]{"bug","unknown","bug"};
        c.SearchText="Docs";Assert.HasCount(4,CollectionViewSource.GetDefaultView(options).Cast<object>());options.RemoveAt(0);
        c.SearchText="";c.IsDropDownOpen=true;Pump(30);Assert.HasCount(3,Part<ListBox>(c,"PART_List").Items);
        c.RemoveValue("unknown");CollectionAssert.AreEqual(new[]{"bug"},c.SelectedValues!.ToArray());
    }));
    [TestMethod] public void TagsExitReverseAndMotionLevelsLeaveDurableTerminalState()=>Sta.Run(()=>InWindow(c=>
    {
        c.SelectedValues=new[]{"bug","feature","docs"};Pump(550);var tags=Part<StackPanel>(c,"PART_Tags");Assert.HasCount(3,tags.Children);
        c.ClearSelection();Pump(35);Assert.IsGreaterThan(0,tags.Children.Count);c.SelectedValues=new[]{"bug","docs"};Pump(650);
        Assert.HasCount(2,tags.Children);foreach(FrameworkElement child in tags.Children){Assert.AreEqual(1d,child.Opacity,.001);Assert.IsFalse(child.HasAnimatedProperties);}
        c.ClearSelection();Pump(600);Assert.HasCount(0,tags.Children);Assert.AreEqual(1d,Part<FrameworkElement>(c,"PART_Placeholder").Opacity,.001);
        Assert.AreEqual(Visibility.Collapsed,Part<FrameworkElement>(c,"PART_ClearVisual").Visibility);
        ArcMotion.SetLevel(c,ArcMotionLevel.Reduced);c.SelectedValues=new[]{"bug"};Pump(180);Assert.HasCount(1,tags.Children);
        ArcMotion.SetLevel(c,ArcMotionLevel.Off);c.ClearSelection();Assert.HasCount(0,tags.Children);Assert.IsFalse(c.IsMotionRunning);
    }));
    [TestMethod] public void OuterBoundsAndChevronStayStableAcrossClearAndNarrowLayout()=>Sta.Run(()=>InWindow(c=>
    {
        var frame=Part<FrameworkElement>(c,"PART_Trigger");var arrow=Part<FrameworkElement>(c,"PART_Chevron");var window=Window.GetWindow(c);
        foreach(double width in new[]{400d,180d})
        {
            c.Width=width;c.UpdateLayout();var origin=frame.TranslatePoint(new Point(),window);var size=frame.RenderSize;var arrowPos=arrow.TranslatePoint(new Point(),window);
            foreach(string[] selection in new[]{new[]{"bug"},new[]{"long","docs","bug","feature"},Array.Empty<string>()})
            {
                c.SelectedValues=selection;
                for(int i=0;i<7;i++){Pump(90);window.UpdateLayout();Assert.AreEqual(size.Width,frame.ActualWidth,.1);Assert.AreEqual(44d,frame.ActualHeight,.1);Assert.AreEqual(origin,frame.TranslatePoint(new Point(),window));Assert.AreEqual(arrowPos,arrow.TranslatePoint(new Point(),window));}
            }
        }
    }));
    [TestMethod] public void PopupExitReopenThemeAndTemplateRelease()=>Sta.Run(()=>InWindow(c=>
    {
        c.IsDropDownOpen=true;Pump(60);Assert.IsTrue(c.IsPresented);c.IsDropDownOpen=false;Pump(30);c.IsDropDownOpen=true;Pump(400);Assert.IsTrue(c.IsPresented);
        var theme=Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single();theme.Mode=ArcThemeMode.Dark;Pump(240);
        var menu=Part<Border>(c,"PART_MenuSurface");Assert.AreEqual(((SolidColorBrush)c.FindResource("ArcWpf.Brush.SurfaceRaised")).Color,((SolidColorBrush)menu.Background).Color);
        c.IsDropDownOpen=false;Pump(650);Assert.IsFalse(c.IsPresented);Assert.IsFalse(c.IsMotionRunning);
        c.IsDropDownOpen=true;c.SelectedValues=new[]{"bug"};Pump(30);var template=c.Template;c.Template=new ControlTemplate(typeof(MultiSelect));c.ApplyTemplate();Assert.IsFalse(c.IsPresented);Assert.IsFalse(c.IsMotionRunning);
        c.Template=template;c.ApplyTemplate();c.UpdateLayout();Assert.IsTrue(c.HasSelection);c.IsDropDownOpen=true;Pump(30);c.Visibility=Visibility.Hidden;Assert.IsFalse(c.IsPresented);Assert.IsFalse(c.IsMotionRunning);
    }));
    [TestMethod] public void KeyboardCompositionAndNativeAutomation()=>Sta.Run(()=>InWindow(c=>
    {
        Window.GetWindow(c).Activate();c.IsDropDownOpen=true;Pump(80);var search=Part<ArcWpf.Controls.Search>(c,"PART_Search");search.Focus();
        var composition=new TextComposition(InputManager.Current,search,"中");search.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice,composition){RoutedEvent=TextCompositionManager.PreviewTextInputStartEvent});
        Key(search,System.Windows.Input.Key.Escape);Assert.IsTrue(c.IsDropDownOpen);
        search.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice,composition){RoutedEvent=TextCompositionManager.PreviewTextInputEvent});Key(search,System.Windows.Input.Key.Down);Key((UIElement)Keyboard.FocusedElement,System.Windows.Input.Key.Enter);Assert.HasCount(1,c.SelectedValues!);
        Key((UIElement)Keyboard.FocusedElement,System.Windows.Input.Key.Escape);Assert.IsFalse(c.IsDropDownOpen);Assert.IsTrue(Part<System.Windows.Controls.Button>(c,"PART_Toggle").IsKeyboardFocused);
        var peer=UIElementAutomationPeer.CreatePeerForElement(c)!;Assert.AreEqual("Labels",peer.GetName());Assert.IsNotNull(peer.GetPattern(PatternInterface.ExpandCollapse));
        var listPeer=UIElementAutomationPeer.CreatePeerForElement(Part<ListBox>(c,"PART_List"))!;Assert.IsTrue(((ISelectionProvider)listPeer.GetPattern(PatternInterface.Selection)).CanSelectMultiple);
        Assert.IsNotNull(UIElementAutomationPeer.CreatePeerForElement(search)!.GetPattern(PatternInterface.Value));
    }));
    [TestMethod] public void GalleryLoadsBoundSamplesAndFiniteScrolling()=>Sta.Run(()=>
    {
        var page=new MultiSelectPage();var window=new Window{Content=new ScrollViewer{Content=page},Width=720,Height=680,ShowActivated=false};
        try{window.Show();Pump(80);var c=(MultiSelect)page.FindName("Picker");c.IsDropDownOpen=true;Pump(80);var list=Part<ListBox>(c,"PART_List");Assert.HasCount(29,list.Items);Assert.IsLessThan(320d,list.ActualHeight);var scroll=Find<ScrollViewer>(list);Assert.IsGreaterThan(0d,scroll.ScrollableHeight);scroll.ScrollToEnd();Pump(30);Assert.IsGreaterThan(0d,scroll.VerticalOffset);}
        finally{window.Close();Sta.Pump();}
    });
    [TestMethod] public void DisabledRowsArrowOpeningAndTabLeaveField()=>Sta.Run(()=>InWindow(c=>
    {
        var window=Window.GetWindow(c);window.Activate();
        var before=new System.Windows.Controls.Button{Content="Before"};var after=new System.Windows.Controls.Button{Content="After"};
        window.Content=null;var panel=new StackPanel();panel.Children.Add(before);panel.Children.Add(c);panel.Children.Add(after);window.Content=panel;window.UpdateLayout();Pump(60);window.Activate();
        c.Options=new[]{new MultiSelectOption("disabled","Disabled",false),new("bug","Bug"),new("docs","Docs")};
        var toggle=Part<System.Windows.Controls.Button>(c,"PART_Toggle");toggle.Focus();Key(toggle,System.Windows.Input.Key.Up);Pump(100);
        var list=Part<ListBox>(c,"PART_List");Assert.AreEqual("docs",((MultiSelectOption)((ListBoxItem)Keyboard.FocusedElement).Content).Value);
        Key((UIElement)Keyboard.FocusedElement,System.Windows.Input.Key.Down);Assert.AreEqual("bug",((MultiSelectOption)((ListBoxItem)Keyboard.FocusedElement).Content).Value);
        Assert.IsFalse(((ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0)).IsEnabled);
        Key((UIElement)Keyboard.FocusedElement,System.Windows.Input.Key.Tab);Assert.IsFalse(c.IsDropDownOpen);Assert.IsTrue(after.IsKeyboardFocused);
    }));
    [TestMethod] public void CheckDrawingCountReversalAndUnloadUseRealClocks()=>Sta.Run(()=>InWindow(c=>
    {
        c.IsDropDownOpen=true;Pump(100);var list=Part<ListBox>(c,"PART_List");var item=(ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
        item.IsSelected=true;Pump(50);var check=(System.Windows.Shapes.Path)item.Template.FindName("PART_Check",item);Assert.IsGreaterThan(0d,check.Opacity);
        item.IsSelected=false;Pump(320);Assert.AreEqual(0d,check.Opacity,.001);Assert.IsFalse(check.HasAnimatedProperties);
        c.SelectedValues=new[]{"bug","docs","feature"};Pump(500);c.SelectedValues=new[]{"bug","docs","feature","long"};Pump(30);c.SelectedValues=new[]{"bug","docs","feature"};Pump(500);
        Assert.IsFalse(c.IsMotionRunning);c.ClearSelection();Pump(30);c.Visibility=Visibility.Hidden;c.Visibility=Visibility.Visible;Pump(600);Assert.HasCount(0,Part<StackPanel>(c,"PART_Tags").Children);
    }));
    [TestMethod] public void TagsKeepNaturalTextWidthAndNarrowLabelsReadable()=>Sta.Run(()=>
    {
        var page=new MultiSelectPage();page.SetResourceReference(Control.BackgroundProperty,"ArcWpf.Brush.Background");var window=new Window{Content=page,Width=720,Height=900,ShowActivated=false};
        try
        {
            window.Show();Pump(700);window.UpdateLayout();
            var lines=new List<string>();
            foreach(string name in new[]{"Reference","Picker","Narrow"})
            {
                var c=(MultiSelect)page.FindName(name);var tags=Part<StackPanel>(c,"PART_Tags");
                foreach(Border slot in tags.Children){var body=(Border)slot.Child;var grid=(Grid)body.Child;var host=(Grid)grid.Children[0];var text=(TextBlock)host.Children[1];lines.Add($"{name}: slot={slot.RenderSize} body={body.RenderSize} host={host.RenderSize} text={text.RenderSize} textDesired={text.DesiredSize} label={text.Text} hostOrigin={host.TranslatePoint(new Point(),body)}");
                    if(name!="Narrow"){var natural=new FormattedText(text.Text,System.Globalization.CultureInfo.CurrentUICulture,c.FlowDirection,new Typeface(text.FontFamily,text.FontStyle,text.FontWeight,text.FontStretch),text.FontSize,Brushes.Black,VisualTreeHelper.GetDpi(text).PixelsPerDip);Assert.IsGreaterThanOrEqualTo(Math.Ceiling(natural.WidthIncludingTrailingWhitespace)+1,host.ActualWidth,$"{name}: host {host.ActualWidth}, natural {natural.WidthIncludingTrailingWhitespace}");}
                    else if(!text.Text.StartsWith('+'))Assert.IsGreaterThanOrEqualTo(20d,host.ActualWidth,"Narrow tags must leave readable text beside removal.");
                }
            }
            var root=new System.IO.DirectoryInfo(AppContext.BaseDirectory);while(root is not null&&!System.IO.File.Exists(System.IO.Path.Combine(root.FullName,"ArcWpf.slnx")))root=root.Parent;
            string folder=System.IO.Path.Combine(root?.FullName??AppContext.BaseDirectory,"artifacts","multi-select");System.IO.Directory.CreateDirectory(folder);System.IO.File.WriteAllLines(folder+"/tag-layout.txt",lines);
            var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)page.ActualWidth,(int)page.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(page);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using var file=System.IO.File.Create(folder+"/tag-layout.png");encoder.Save(file);
        }finally{window.Close();Sta.Pump();}
    });
    private static T Find<T>(DependencyObject d) where T:DependencyObject {if(d is T found)return found;for(int i=0;i<VisualTreeHelper.GetChildrenCount(d);i++){try{return Find<T>(VisualTreeHelper.GetChild(d,i));}catch(InvalidOperationException){}}throw new InvalidOperationException();}
    private static T Part<T>(MultiSelect c,string name) where T:class=>(T)c.Template.FindName(name,c);
    private static void Key(UIElement target,Key key)=>target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(target)!,0,key){RoutedEvent=Keyboard.PreviewKeyDownEvent});
    private static void InWindow(Action<MultiSelect> action)
    {
        var c=new MultiSelect{Width=400,Label="Labels",VerticalAlignment=VerticalAlignment.Top,Options=new[]{new MultiSelectOption("bug","Bug"),new("feature","Feature"),new("docs","Docs"),new("long",new string('W',100))}};
        var window=new Window{Content=c,Width=600,Height=350,ShowActivated=false};try{window.Show();Sta.Pump();action(c);}finally{window.Close();Sta.Pump();Assert.IsFalse(c.IsMotionRunning);}
    }
    private static void Pump(int ms){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
}
