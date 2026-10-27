using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ArcWpf.Controls;
using ArcWpf.Gallery.Pages;
namespace ArcWpf.Tests;
[TestClass]
public sealed class SegmentedControlTests
{
    [TestMethod] public void NativeSelectionBindingAndDynamicItems() => Sta.Run(()=>InWindow(c=>
    {
        var options=new ObservableCollection<Option>{new("day","Day"),new("week","Week"),new("month","Month")};var model=new Model();
        c.Items.Clear();c.ItemsSource=options;c.DisplayMemberPath="Label";c.SelectedValuePath="Id";c.SetBinding(SegmentedControl.SelectedValueProperty,new Binding(nameof(Model.Value)){Source=model,Mode=BindingMode.TwoWay});Pump(100);
        Assert.AreEqual(1,c.SelectedIndex);Click(Item(c,2));Assert.AreEqual("month",model.Value);Assert.IsNotNull(c.GetBindingExpression(SegmentedControl.SelectedValueProperty));
        model.Value="day";Pump(50);Assert.AreEqual(0,c.SelectedIndex);
        options.Add(new("year","Year"));Pump(50);Assert.IsNotNull(c.ItemContainerGenerator.ContainerFromIndex(3));
        options.RemoveAt(0);Pump(50);Assert.IsFalse(c.Items.Contains(options.FirstOrDefault(o=>o.Id=="day")));
        c.SelectionMode=SelectionMode.Multiple;Assert.AreEqual(SelectionMode.Single,c.SelectionMode);
    }));
    [TestMethod] public void MouseSelectionIntentAndNativeAutomation() => Sta.Run(()=>InWindow(c=>
    {
        int changes=0,intents=0;c.SelectionChanged+=(_,_)=>changes++;c.OptionIntent+=(_,e)=>{Assert.IsNotNull(e.Item);intents++;};
        var item=Item(c,2);Click(item);Assert.AreEqual(2,c.SelectedIndex);Click(item);Assert.AreEqual(1,changes);Assert.IsGreaterThan(0,intents);
        var hit=c.InputHitTest(item.TranslatePoint(new Point(item.ActualWidth/2,18),c));Assert.IsTrue(Popover.IsWithin(hit as DependencyObject,item));
        var peer=UIElementAutomationPeer.CreatePeerForElement(c)!;Assert.AreEqual("Range",peer.GetName());Assert.AreEqual(AutomationControlType.List,peer.GetAutomationControlType());
        var selection=(ISelectionProvider)peer.GetPattern(PatternInterface.Selection)!;Assert.IsFalse(selection.CanSelectMultiple);Assert.HasCount(1,selection.GetSelection());
        var child=peer.GetChildren().First(p=>p.GetName()=="Day");var provider=(ISelectionItemProvider)child.GetPattern(PatternInterface.SelectionItem)!;provider.Select();Assert.AreEqual(0,c.SelectedIndex);
        Item(c,1).IsEnabled=false;Click(Item(c,1));Assert.AreEqual(0,c.SelectedIndex);
    }));
    [TestMethod] public void NavigationSkipsDisabledWrapsAndKeepsOneTabStop() => Sta.Run(()=>InWindow(c=>
    {
        Item(c,1).IsEnabled=false;c.SelectedIndex=0;Pump(50);Item(c,0).Focus();PressKey(c,Key.Right);Pump(50);Assert.AreEqual(2,c.SelectedIndex);Assert.IsTrue(Item(c,2).IsKeyboardFocused);Assert.IsNull(Item(c,2).FocusVisualStyle,"Focused selection must not add the extra dark outline.");
        PressKey(c,Key.Down);Pump(50);Assert.AreEqual(0,c.SelectedIndex);PressKey(c,Key.End);Pump(50);Assert.AreEqual(2,c.SelectedIndex);PressKey(c,Key.Home);Pump(50);Assert.AreEqual(0,c.SelectedIndex);
        Assert.HasCount(1,c.Containers().Where(x=>x.IsTabStop));c.FlowDirection=FlowDirection.RightToLeft;Pump(50);PressKey(c,Key.Left);Pump(50);Assert.AreEqual(2,c.SelectedIndex);
        c.IsEnabled=false;PressKey(c,Key.Home);Assert.AreEqual(2,c.SelectedIndex);
    }));
    [TestMethod] public void GeometryIncludesFrameSpacingAndAccessory() => Sta.Run(()=>InWindow(c=>
    {
        ArcMotion.SetLevel(c,ArcMotionLevel.Off);c.SelectedIndex=1;Pump(50);var one=Item(c,0);var two=Item(c,1);var ring=Part<Border>(c,"PART_Selection");
        Assert.AreEqual(44d,c.ActualHeight,.1);Assert.AreEqual(36d,two.ActualHeight,.1);
        Assert.AreEqual(4d,one.TranslatePoint(new Point(),c).X,.1);Assert.AreEqual(2d,two.TranslatePoint(new Point(),c).X-one.TranslatePoint(new Point(one.ActualWidth,0),c).X,.1);
        Assert.AreEqual(two.ActualWidth,ring.ActualWidth,.1);Assert.AreEqual(two.TranslatePoint(new Point(),c).X,ring.TranslatePoint(new Point(),c).X,.1);
        double previous=two.ActualWidth;two.Icon=new Border{Width=16,Height=16,Background=Brushes.Gray};two.Accessory=new TextBlock{Text="8"};c.UpdateLayout();Pump(50);
        Assert.IsGreaterThan(previous+22,two.ActualWidth);Assert.AreEqual(two.ActualWidth,ring.ActualWidth,.1);
        c.FlowDirection=FlowDirection.RightToLeft;Pump(100);var root=Window.GetWindow(c);Assert.AreEqual(two.TransformToAncestor(root).TransformBounds(new Rect(two.RenderSize)).X,ring.TransformToAncestor(root).TransformBounds(new Rect(ring.RenderSize)).X,.1);
    }));
    [TestMethod] public void NarrowTrackScrollsAndRevealsSelectedWithEdgeFades() => Sta.Run(()=>InWindow(c=>
    {
        ArcMotion.SetLevel(c,ArcMotionLevel.Off);c.Items.Add(new SegmentedItem{Content="Custom long range"});c.Width=150;c.UpdateLayout();Pump(100);var scroll=Part<ScrollViewer>(c,"PART_Scroll");
        Assert.IsGreaterThan(0d,scroll.ScrollableWidth);Assert.IsNotNull(scroll.OpacityMask);c.SelectedIndex=3;Pump(100);Assert.IsGreaterThan(0d,scroll.HorizontalOffset);
        var item=Item(c,3);var p=item.TranslatePoint(new Point(),scroll);Assert.IsTrue(p.X<scroll.ViewportWidth && p.X+item.ActualWidth>0);
        c.SelectedIndex=0;Pump(100);Assert.AreEqual(0d,scroll.HorizontalOffset,.1);
        c.Width=650;c.UpdateLayout();Pump(100);Assert.AreEqual(0d,scroll.ScrollableWidth,.1);Assert.IsNull(scroll.OpacityMask);
    }));
    [TestMethod] public void SelectionSpringReversesAndKeepsFinalGeometry() => Sta.Run(()=>InWindow(c=>
    {
        Pump(120);var ring=Part<Border>(c,"PART_Selection");var transform=(TranslateTransform)ring.RenderTransform;double start=transform.X;c.SelectedIndex=2;
        for(int i=0;i<10;i++){Pump(20);if(transform.X>start+.1)break;}Assert.IsGreaterThan(start,transform.X);double target=Item(c,2).TranslatePoint(new Point(),Part<FrameworkElement>(c,"PART_Track")).X;Assert.IsLessThan(target,transform.X);
        c.SelectedIndex=0;Pump(1900);Assert.IsFalse(c.IsMotionRunning);Assert.AreEqual(0d,transform.X,.05);Assert.AreEqual(Item(c,0).ActualWidth,ring.Width,.05);
        var label=(FrameworkElement)Item(c,0).Template.FindName("PART_Label",Item(c,0));Assert.AreEqual(((SolidColorBrush)c.FindResource("ArcWpf.Brush.Foreground")).Color,((SolidColorBrush)TextElement.GetForeground(label)).Color);
        c.InvalidateVisual();Pump(100);Assert.AreEqual(0d,transform.X,.05);
        ArcMotion.SetLevel(c,ArcMotionLevel.Reduced);c.SelectedIndex=2;Pump(50);Assert.IsFalse(c.IsMotionRunning);Assert.AreEqual(target,transform.X,.1);
        ArcMotion.SetLevel(c,ArcMotionLevel.Off);c.SelectedIndex=1;Pump(50);Assert.IsFalse(c.IsMotionRunning);
    }));
    [TestMethod] public void TemplatesVisibilityThemeAndInstancesRemainIndependent() => Sta.Run(()=>InWindow(c=>
    {
        c.SelectedIndex=2;Pump(40);c.Visibility=Visibility.Hidden;Assert.IsFalse(c.IsMotionRunning);c.Visibility=Visibility.Visible;Pump(80);
        var template=c.Template;c.Template=new ControlTemplate(typeof(SegmentedControl));c.ApplyTemplate();Assert.IsFalse(c.IsMotionRunning);c.SelectedIndex=0;c.Template=template;c.ApplyTemplate();Pump(100);Assert.AreEqual(Visibility.Visible,Part<Border>(c,"PART_Selection").Visibility);
        Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single().Mode=ArcThemeMode.Dark;Pump(200);Assert.AreEqual(((SolidColorBrush)c.FindResource("ArcWpf.Brush.Surface")).Color,((SolidColorBrush)Part<Border>(c,"PART_Selection").Background).Color);
        c.SelectedIndex=-1;Pump(50);Assert.AreEqual(Visibility.Hidden,Part<Border>(c,"PART_Selection").Visibility);Assert.HasCount(1,c.Containers().Where(x=>x.IsTabStop));
        var other=new SegmentedControl();other.Items.Add(new SegmentedItem{Content="Other"});other.SelectedIndex=0;Assert.AreEqual(-1,c.SelectedIndex);
    }));
    [TestMethod] public void GalleryRendersLightDarkAndOverflow() => Sta.Run(()=>
    {
        var page=new SegmentedControlPage();page.SetResourceReference(Control.BackgroundProperty,"ArcWpf.Brush.Background");var window=new Window{Content=page,Width=720,Height=1060,ShowActivated=false};
        try{window.Show();Pump(500);Save(window,"gallery-light.png");var narrow=(SegmentedControl)page.FindName("Narrow");narrow.SelectedIndex=3;Pump(1900);Save(window,"gallery-overflow.png");Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single().Mode=ArcThemeMode.Dark;Pump(400);Save(window,"gallery-dark.png");}
        finally{window.Close();Sta.Pump();}
    });
    private static SegmentedItem Item(SegmentedControl c,int i)=>(SegmentedItem)c.ItemContainerGenerator.ContainerFromIndex(i);
    private static T Part<T>(SegmentedControl c,string name) where T:class=>(T)c.Template.FindName(name,c);
    private static void Click(SegmentedItem item)=>item.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=Mouse.MouseDownEvent});
    private static void PressKey(UIElement target,Key key)=>target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(target)!,0,key){RoutedEvent=Keyboard.PreviewKeyDownEvent});
    private static void InWindow(Action<SegmentedControl> action)
    {var c=new SegmentedControl{Label="Range",Margin=new Thickness(30),SelectedIndex=0};c.Items.Add(new SegmentedItem{Content="Day"});c.Items.Add(new SegmentedItem{Content="Week"});c.Items.Add(new SegmentedItem{Content="Month"});var w=new Window{Content=c,Width=600,Height=400,ShowActivated=false};try{w.Show();Pump(100);action(c);}finally{w.Close();Sta.Pump();Assert.IsFalse(c.IsMotionRunning);}}
    private static void Pump(int ms){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    private static void Save(FrameworkElement element,string name)
    {
        element.UpdateLayout();var bounds=new Rect(0,0,element.ActualWidth,element.ActualHeight);var visual=new DrawingVisual();using(var draw=visual.RenderOpen()){draw.DrawRectangle((Brush)element.FindResource("ArcWpf.Brush.Background"),null,bounds);draw.DrawRectangle(new VisualBrush(element){ViewboxUnits=BrushMappingMode.Absolute,Viewbox=bounds,Stretch=Stretch.Fill},null,bounds);}
        var bitmap=new RenderTargetBitmap((int)bounds.Width,(int)bounds.Height,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));var root=new System.IO.DirectoryInfo(AppContext.BaseDirectory);while(root is not null&&!System.IO.File.Exists(System.IO.Path.Combine(root.FullName,"ArcWpf.slnx")))root=root.Parent;
        var dir=System.IO.Path.Combine(root!.FullName,"artifacts","segmented-control");System.IO.Directory.CreateDirectory(dir);using var file=System.IO.File.Create(System.IO.Path.Combine(dir,name));encoder.Save(file);
    }
    private sealed record Option(string Id,string Label);
    private sealed class Model:INotifyPropertyChanged{private string _value="week";public string Value{get=>_value;set{_value=value;PropertyChanged?.Invoke(this,new(nameof(Value)));}}public event PropertyChangedEventHandler? PropertyChanged;}
}

