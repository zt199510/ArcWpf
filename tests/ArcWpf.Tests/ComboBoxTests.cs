using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ArcWpf.Controls;
using ArcWpf.Gallery.Pages;
using ArcComboBox=ArcWpf.Controls.ComboBox;
namespace ArcWpf.Tests;
[TestClass]
public sealed class ComboBoxTests
{
    [TestMethod] public void KeywordFilterAndSelectionBindingRemainSeparate()=>Sta.Run(()=>InWindow(c=>
    {
        var model=new ComboBoxModel{Zone="utc"};c.SetBinding(ArcComboBox.SelectedValueProperty,new Binding("Zone"){Source=model,Mode=BindingMode.TwoWay});
        Assert.AreEqual("UTC",c.Text);c.IsDropDownOpen=true;c.QueryText=" BERLIN ";Pump(50);
        var list=Part<ListBox>(c,"PART_List");Assert.HasCount(1,list.Items);Assert.AreEqual("utc",model.Zone);Assert.AreEqual("UTC",c.DisplayPlaceholder);
        list.SelectedItem=list.Items[0];Assert.AreEqual("cet",model.Zone);Assert.AreEqual("Central European",c.Text);Assert.IsFalse(c.IsDropDownOpen);Assert.AreEqual("",c.QueryText);Assert.IsNotNull(c.GetBindingExpression(ArcComboBox.SelectedValueProperty));
        c.ClearSelection();Assert.AreEqual("",model.Zone);Assert.IsTrue(c.IsDropDownOpen);Assert.IsFalse(c.HasSelection);
        c.IsDropDownOpen=false;model.Zone="pst";Assert.AreEqual("Pacific",c.Text);
    }));
    [TestMethod] public void CollectionRefreshNeverFiltersSharedViewsOrCommitsQuery()=>Sta.Run(()=>InWindow(c=>
    {
        var options=new ObservableCollection<ComboBoxOption>(c.Options!);c.Options=options;c.SelectedValue="cet";c.IsDropDownOpen=true;c.QueryText="unmatched";
        Assert.HasCount(4,CollectionViewSource.GetDefaultView(options).Cast<object>());Assert.AreEqual("cet",c.SelectedValue);Assert.AreEqual(Visibility.Visible,Part<FrameworkElement>(c,"PART_Empty").Visibility);
        options.Add(new("new","Unmatched new"));Assert.HasCount(1,Part<ListBox>(c,"PART_List").Items);
        c.IsDropDownOpen=false;Assert.AreEqual("Central European",c.Text);Assert.AreEqual("",c.QueryText);
        c.SelectedValue="unknown";Assert.AreEqual("",c.Text);Assert.IsFalse(c.HasSelection);Assert.AreEqual("unknown",c.SelectedValue);
        ArcMotion.SetLevel(c,ArcMotionLevel.Off);options.Add(new("unknown","Now available"));Assert.IsTrue(c.HasSelection);Assert.AreEqual("Now available",c.Text);Assert.IsTrue(Part<System.Windows.Controls.Button>(c,"PART_Clear").IsEnabled);
        options.Remove(options.Last());Assert.IsFalse(c.HasSelection);Assert.AreEqual(Visibility.Collapsed,Part<FrameworkElement>(c,"PART_ClearVisual").Visibility);
    }));
    [TestMethod] public void NativeEditingUndoAndCompositionProtectSelection()=>Sta.Run(()=>InWindow(c=>
    {
        Window.GetWindow(c).Activate();c.Focus();Pump(40);Assert.IsTrue(c.IsDropDownOpen);
        c.SelectedText="ber";Assert.AreEqual("ber",c.QueryText);Assert.IsTrue(c.CanUndo);c.Undo();Assert.AreEqual("",c.QueryText);
        c.SelectedValue="utc";var composition=new TextComposition(InputManager.Current,c,"中");
        c.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice,composition){RoutedEvent=TextCompositionManager.PreviewTextInputStartEvent});
        Key(c,System.Windows.Input.Key.Down);Key(c,System.Windows.Input.Key.Enter);Key(c,System.Windows.Input.Key.Escape);Assert.IsTrue(c.IsDropDownOpen);Assert.AreEqual("utc",c.SelectedValue);
        c.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice,composition){RoutedEvent=TextCompositionManager.PreviewTextInputEvent});
        Key(c,System.Windows.Input.Key.Escape);Assert.IsFalse(c.IsDropDownOpen);Assert.AreEqual("UTC",c.Text);Assert.IsTrue(c.IsKeyboardFocused);
        c.IsDropDownOpen=true;c.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice,composition){RoutedEvent=TextCompositionManager.PreviewTextInputStartEvent});c.SelectedText="pending";c.Visibility=Visibility.Hidden;Assert.AreEqual("UTC",c.Text);Assert.AreEqual("",c.QueryText);
    }));
    [TestMethod] public void KeyboardNavigatesWithoutStealingEditorFocusAndSkipsDisabled()=>Sta.Run(()=>InWindow(c=>
    {
        Window.GetWindow(c).Activate();c.Focus();Pump(40);Key(c,System.Windows.Input.Key.Up);Key(c,System.Windows.Input.Key.Enter);
        Assert.AreEqual("pst",c.SelectedValue);Assert.IsFalse(c.IsDropDownOpen);Assert.IsTrue(c.IsKeyboardFocused);
        Key(c,System.Windows.Input.Key.Down);Assert.IsTrue(c.IsDropDownOpen);Key(c,System.Windows.Input.Key.Down);Key(c,System.Windows.Input.Key.Enter);Assert.AreEqual("utc",c.SelectedValue);
        c.IsReadOnly=true;c.ClearSelection();c.IsDropDownOpen=true;Assert.IsFalse(c.IsDropDownOpen);Assert.AreEqual("utc",c.SelectedValue);
        c.IsReadOnly=false;c.IsEnabled=false;c.ClearSelection();Assert.AreEqual("utc",c.SelectedValue);
    }));
    [TestMethod] public void FilteringHeightAndPopupExitReverseReachStableBase()=>Sta.Run(()=>InWindow(c=>
    {
        c.IsDropDownOpen=true;Pump(400);var host=Part<FrameworkElement>(c,"PART_HeightHost");double start=host.ActualHeight;
        c.QueryText="berlin";Pump(45);Assert.IsLessThan(start,host.ActualHeight);Assert.IsGreaterThan(36d,host.ActualHeight);
        c.QueryText="";Pump(600);Assert.AreEqual(start,host.ActualHeight,.1);Assert.IsFalse(host.HasAnimatedProperties);
        c.IsDropDownOpen=false;Pump(30);c.IsDropDownOpen=true;Pump(700);Assert.IsTrue(c.IsPresented);Assert.IsFalse(c.IsMotionRunning);
        c.IsDropDownOpen=false;Pump(700);Assert.IsFalse(c.IsPresented);Assert.IsFalse(c.IsMotionRunning);
    }));
    [TestMethod] public void ClearTextAndEmptyMotionSupportReducedOffAndUnload()=>Sta.Run(()=>InWindow(c=>
    {
        c.SelectedValue="cet";Pump(40);Assert.IsGreaterThan(0d,Part<FrameworkElement>(c,"PART_TextVisual").Opacity);
        c.ClearSelection();Pump(35);c.SelectedValue="pst";c.IsDropDownOpen=false;Pump(700);
        Assert.AreEqual(1d,Part<FrameworkElement>(c,"PART_ClearVisual").Opacity,.001);Assert.IsFalse(c.IsMotionRunning);
        c.IsDropDownOpen=true;c.QueryText="missing";Pump(40);var empty=Part<FrameworkElement>(c,"PART_Empty");Assert.IsGreaterThan(0d,empty.Opacity);Pump(320);Assert.IsFalse(empty.HasAnimatedProperties);
        ArcMotion.SetLevel(c,ArcMotionLevel.Reduced);c.ClearSelection();Pump(180);Assert.AreEqual(Visibility.Collapsed,Part<FrameworkElement>(c,"PART_ClearVisual").Visibility);
        ArcMotion.SetLevel(c,ArcMotionLevel.Off);c.SelectedValue="utc";c.IsDropDownOpen=false;Assert.IsFalse(c.IsMotionRunning);
        ArcMotion.SetLevel(c,ArcMotionLevel.Full);c.IsDropDownOpen=true;c.QueryText="berlin";Pump(30);c.Visibility=Visibility.Hidden;Assert.IsFalse(c.IsPresented);Assert.IsFalse(c.IsMotionRunning);
    }));
    [TestMethod] public void TextPlaceholderAndActionGeometryStayWithinNarrowField()=>Sta.Run(()=>InWindow(c=>
    {
        Assert.AreEqual(0d,((System.Windows.Media.Effects.BlurEffect)Part<FrameworkElement>(c,"PART_TextVisual").Effect).Radius,.001,"Initial editor and watermark must be sharp.");
        ArcMotion.SetLevel(c,ArcMotionLevel.Off);c.IsDropDownOpen=true;c.UpdateLayout();var watermark=Part<TextBlock>(c,"Watermark");
        var hint=watermark.TranslatePoint(new Point(),c);c.SelectedText="U";c.UpdateLayout();var first=c.GetRectFromCharacterIndex(0);Assert.AreEqual(hint.X,first.X,.5);
        foreach(double width in new[]{400d,180d})
        {
            c.Width=width;c.IsDropDownOpen=false;c.SelectedValue="";c.UpdateLayout();var frame=Part<FrameworkElement>(c,"PART_Trigger");var arrow=Part<FrameworkElement>(c,"PART_Chevron");var origin=frame.TranslatePoint(new Point(),Window.GetWindow(c));var arrowOrigin=arrow.TranslatePoint(new Point(),Window.GetWindow(c));
            ArcMotion.SetLevel(c,ArcMotionLevel.Full);
            foreach(string value in new[]{"cet","", "pst",""}){c.SelectedValue=value;for(int i=0;i<4;i++){Pump(80);c.UpdateLayout();Assert.AreEqual(width,frame.ActualWidth,.1);Assert.AreEqual(44d,frame.ActualHeight,.1);Assert.AreEqual(origin,frame.TranslatePoint(new Point(),Window.GetWindow(c)));Assert.AreEqual(arrowOrigin,arrow.TranslatePoint(new Point(),Window.GetWindow(c)));Assert.IsGreaterThan(30d,Part<FrameworkElement>(c,"PART_ContentHost").ActualWidth);}}
        }
    }));
    [TestMethod] public void ThemeValidationAutomationAndTemplateReplacement()=>Sta.Run(()=>InWindow(c=>
    {
        var model=new ComboBoxModel();c.SetBinding(ArcComboBox.SelectedValueProperty,new Binding("Zone"){Source=model,Mode=BindingMode.TwoWay});
        Validation.MarkInvalid(c.GetBindingExpression(ArcComboBox.SelectedValueProperty),new ValidationError(new ExceptionValidationRule(),c.GetBindingExpression(ArcComboBox.SelectedValueProperty),"Required",null));Assert.IsTrue(c.HasError);Assert.AreEqual("Required",c.EffectiveErrorMessage);
        c.IsDropDownOpen=true;Pump(80);Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single().Mode=ArcThemeMode.Dark;Pump(220);
        Assert.AreEqual(((SolidColorBrush)c.FindResource("ArcWpf.Brush.SurfaceRaised")).Color,((SolidColorBrush)Part<Border>(c,"PART_MenuSurface").Background).Color);
        var peer=UIElementAutomationPeer.CreatePeerForElement(c)!;Assert.AreEqual(AutomationControlType.ComboBox,peer.GetAutomationControlType());Assert.AreEqual("Time zone",peer.GetName());Assert.IsNotNull(peer.GetPattern(PatternInterface.Value));Assert.IsNotNull(peer.GetPattern(PatternInterface.ExpandCollapse));
        Assert.IsFalse(((ISelectionProvider)UIElementAutomationPeer.CreatePeerForElement(Part<ListBox>(c,"PART_List"))!.GetPattern(PatternInterface.Selection)).CanSelectMultiple);
        c.SelectedValue="utc";c.UpdateLayout();var clear=Part<System.Windows.Controls.Button>(c,"PART_Clear");Assert.IsTrue(clear.Focusable);Assert.IsTrue(clear.IsTabStop);Assert.IsTrue(peer.GetChildren()!.Any(p=>p.GetName()=="Clear selection"));
        ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(clear)!.GetPattern(PatternInterface.Invoke)).Invoke();Pump(50);Assert.AreEqual("",c.SelectedValue);
        var template=c.Template;c.Template=new ControlTemplate(typeof(ArcComboBox));c.ApplyTemplate();Assert.IsFalse(c.IsPresented);Assert.IsFalse(c.IsMotionRunning);c.Template=template;c.ApplyTemplate();c.UpdateLayout();Assert.IsNotNull(c.Template.FindName("PART_ContentHost",c));
    }));
    [TestMethod] public void GalleryComboBoxPageRendersAndLongListHasFiniteScroll()=>Sta.Run(()=>
    {
        var page=new ComboBoxPage();page.SetResourceReference(Control.BackgroundProperty,"ArcWpf.Brush.Background");var window=new Window{Content=page,Width=720,Height=900,ShowActivated=false};
        try
        {
            window.Show();Pump(200);var c=(ArcComboBox)page.FindName("Picker");c.IsDropDownOpen=true;Pump(450);var list=Part<ListBox>(c,"PART_List");Assert.HasCount(30,list.Items);Assert.IsLessThanOrEqualTo(300d,list.ActualHeight);
            var scroll=Find<ScrollViewer>(list);Assert.IsGreaterThan(0d,scroll.ScrollableHeight);scroll.ScrollToEnd();Pump(30);Assert.IsGreaterThan(0d,scroll.VerticalOffset);
            c.IsDropDownOpen=false;Pump(650);window.UpdateLayout();var bitmap=new RenderTargetBitmap((int)page.ActualWidth,(int)page.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(page);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));var folder=ArtifactFolder();using var file=System.IO.File.Create(System.IO.Path.Combine(folder,"gallery.png"));encoder.Save(file);
        }finally{window.Close();Sta.Pump();}
    });
    private static string ArtifactFolder(){var root=new System.IO.DirectoryInfo(AppContext.BaseDirectory);while(root is not null&&!System.IO.File.Exists(System.IO.Path.Combine(root.FullName,"ArcWpf.slnx")))root=root.Parent;var path=System.IO.Path.Combine(root?.FullName??AppContext.BaseDirectory,"artifacts","combobox");System.IO.Directory.CreateDirectory(path);return path;}
    private static T Find<T>(DependencyObject d) where T:DependencyObject {if(d is T found)return found;for(int i=0;i<VisualTreeHelper.GetChildrenCount(d);i++){try{return Find<T>(VisualTreeHelper.GetChild(d,i));}catch(InvalidOperationException){}}throw new InvalidOperationException();}
    private static T Part<T>(ArcComboBox c,string name) where T:class=>(T)c.Template.FindName(name,c);
    private static void Key(UIElement target,Key key)=>target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(target)!,0,key){RoutedEvent=Keyboard.PreviewKeyDownEvent});
    private static void InWindow(Action<ArcComboBox> action)
    {
        var c=new ArcComboBox{Label="Time zone",Width=400,VerticalAlignment=VerticalAlignment.Top,Options=new[]{new ComboBoxOption("utc","UTC"),new("disabled","Disabled",false),new("cet","Central European",Keywords:["berlin","zurich"]),new("pst","Pacific",Keywords:["san francisco"])}};
        var window=new Window{Content=c,Width=600,Height=600,ShowActivated=false};try{window.Show();Sta.Pump();action(c);}finally{window.Close();Sta.Pump();Assert.IsFalse(c.IsMotionRunning);}
    }
    private static void Pump(int ms){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
}
