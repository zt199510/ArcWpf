using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass]
public sealed class DialogTests
{
    [TestMethod]
    public void InitialOpenAndBindingCloseKeepNativeContentAndContext()=>Sta.Run(()=>
    {
        var model=new Model{Open=true};var input=new TextBox{Text="original"};var dialog=new Dialog{Title="Title",Content=input};var binding=dialog.SetBinding(Dialog.IsOpenProperty,new Binding(nameof(Model.Open)){Source=model});
        var host=new DialogHost{Content=new Border(),Dialog=dialog,DataContext=model};Layout(host);
        Assert.IsTrue(dialog.IsOpen);Assert.IsTrue(dialog.IsPresented);Assert.AreSame(model,dialog.DataContext);
        input.Text="edited";dialog.Close();Sta.Pump();Assert.IsFalse(model.Open);Assert.IsFalse(dialog.IsPresented);
        model.Open=true;Layout(host);Assert.IsTrue(dialog.IsPresented);Assert.AreSame(input,dialog.Content);Assert.AreEqual("edited",input.Text);Assert.AreSame(binding,BindingOperations.GetBindingExpression(dialog,Dialog.IsOpenProperty));dialog.Close();
    });
    [TestMethod]
    public void CancelledClosePreservesModalAndCanLaterClose()=>Sta.Run(()=>
    {
        var (host,dialog)=Open();bool cancel=true;DialogCloseReason? reason=null;dialog.Closing+=(_,e)=>{reason=e.Reason;e.Cancel=cancel;};dialog.Close();Assert.IsTrue(dialog.IsOpen);Assert.IsTrue(dialog.IsPresented);Assert.AreEqual(DialogCloseReason.Programmatic,reason);
        cancel=false;dialog.Close();Assert.IsFalse(dialog.IsPresented);Assert.IsTrue(Part<ContentPresenter>(host,"PART_Background").IsEnabled);
    });
    [TestMethod]
    public void GeometryUses440Width24PaddingAndFitsSmallHost()=>Sta.Run(()=>
    {
        var (host,dialog)=Open();var surface=Part<Border>(dialog,"PART_Surface");var body=Part<ContentPresenter>(dialog,"Body");var header=Part<Border>(dialog,"HeaderBorder");
        Assert.AreEqual(440d,surface.ActualWidth,.01);Assert.AreEqual(180d,surface.TranslatePoint(new Point(),host).X,.01);Assert.AreEqual(new CornerRadius(34),surface.CornerRadius);
        Assert.AreEqual(25d,body.TranslatePoint(new Point(),surface).X,.01);Assert.AreEqual(24d,header.Padding.Left,.01);Assert.AreEqual(32d,Part<FrameworkElement>(dialog,"PART_CloseButton").ActualWidth,.01);
        Layout(host,300,240);Assert.AreEqual(268d,surface.ActualWidth,.01);Assert.IsLessThanOrEqualTo(208.01,surface.ActualHeight);dialog.Close();
    });
    [TestMethod]
    public void LongContentScrollsWithinHostAndDescriptionCanCollapse()=>Sta.Run(()=>
    {
        var (host,dialog)=Open();dialog.Content=new Border{Height=1800};dialog.Description="";Layout(host,600,400);var surface=Part<Border>(dialog,"PART_Surface");var scroll=(ScrollViewer)surface.Child;
        Assert.IsLessThanOrEqualTo(368.01,surface.ActualHeight);Assert.IsGreaterThan(0d,scroll.ScrollableHeight);Assert.AreEqual(Visibility.Collapsed,Part<FrameworkElement>(dialog,"DescriptionText").Visibility);dialog.Close();
    });
    [TestMethod]
    public void HostOwnsBlurAndInputBlockWithoutMutatingConsumerBindings()=>Sta.Run(()=>
    {
        var source=new Border{Opacity=.8};var background=new Border();var binding=background.SetBinding(UIElement.OpacityProperty,new Binding(nameof(Border.Opacity)){Source=source});var effect=new DropShadowEffect();background.Effect=effect;
        var host=new DialogHost{Content=background};Layout(host);var dialog=new Dialog{Title="Modal"};dialog.Show(background);Layout(host);
        var presenter=Part<ContentPresenter>(host,"PART_Background");Assert.IsFalse(presenter.IsEnabled);Assert.IsInstanceOfType<BlurEffect>(presenter.Effect);Assert.AreSame(effect,background.Effect);Assert.AreSame(binding,BindingOperations.GetBindingExpression(background,UIElement.OpacityProperty));
        dialog.Close();Assert.IsNull(presenter.Effect);Assert.IsTrue(presenter.IsEnabled);Assert.AreSame(effect,background.Effect);
    });
    [TestMethod]
    public void EntranceAndExitRealClocksReleaseModalOnlyAtCompletion()=>Sta.Run(()=>
    {
        var (host,dialog)=Open();var surface=Part<FrameworkElement>(dialog,"PART_Surface");surface.Opacity=0;dialog.Transition(true,true);Pump(500);Assert.AreEqual(1d,surface.Opacity,.001);Assert.IsFalse(dialog.IsMotionRunning);
        SetLogicalOpen(dialog,false);dialog.Transition(false,true);Assert.IsTrue(dialog.IsPresented);Assert.IsFalse(Part<ContentPresenter>(host,"PART_Background").IsEnabled);dialog.Close();dialog.Close();Pump(260);
        Assert.IsFalse(dialog.IsPresented);Assert.IsFalse(dialog.IsMotionRunning);Assert.AreEqual(0d,surface.Opacity,.001);Assert.IsFalse(surface.HasAnimatedProperties);
        dialog.IsOpen=true;Layout(host);Assert.AreEqual(1d,surface.Opacity,.001);dialog.Close();
    });
    [TestMethod]
    public void ReopenDuringExitInvalidatesOldCompletion()=>Sta.Run(()=>
    {
        var (host,dialog)=Open();int closed=0;dialog.Closed+=(_,_)=>closed++;SetLogicalOpen(dialog,false);dialog.Transition(false,true);Pump(40);dialog.IsOpen=true;Layout(host);dialog.Transition(true,true);Pump(500);
        Assert.IsTrue(dialog.IsPresented);Assert.IsTrue(dialog.IsOpen);Assert.AreEqual(0,closed);Assert.AreEqual(1d,Part<FrameworkElement>(dialog,"PART_Surface").Opacity,.001);dialog.Close();Assert.AreEqual(1,closed);
    });
    [TestMethod]
    public void ReducedAndOffRemoveScaleMotionAndFinishPendingExit()=>Sta.Run(()=>
    {
        var (host,dialog)=Open();ArcMotion.SetLevel(dialog,ArcMotionLevel.Reduced);var surface=Part<FrameworkElement>(dialog,"PART_Surface");surface.Opacity=0;dialog.Transition(true,true);
        var transforms=(TransformGroup)surface.RenderTransform;Assert.AreEqual(1d,((ScaleTransform)transforms.Children[0]).ScaleX,.001);Assert.AreEqual(0d,((TranslateTransform)transforms.Children[1]).Y,.001);Pump(240);Assert.IsFalse(dialog.IsMotionRunning);
        SetLogicalOpen(dialog,false);dialog.Transition(false,true);ArcMotion.SetLevel(dialog,ArcMotionLevel.Off);Assert.IsFalse(dialog.IsPresented);Assert.IsFalse(dialog.IsMotionRunning);
    });
    [TestMethod]
    public void ReplacementAndUnloadForceCloseDespiteCancellation()=>Sta.Run(()=>
    {
        var (host,old)=Open();old.Closing+=(_,e)=>e.Cancel=true;var next=new Dialog{Title="Next",IsOpen=true};host.Dialog=next;Layout(host);Assert.IsFalse(old.IsOpen);Assert.IsFalse(old.IsPresented);Assert.IsTrue(next.IsPresented);
        next.Closing+=(_,e)=>e.Cancel=true;next.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));Assert.IsFalse(next.IsOpen);Assert.IsFalse(next.IsPresented);Assert.IsTrue(Part<ContentPresenter>(host,"PART_Background").IsEnabled);
    });
    [TestMethod]
    public void TemplateReplacementClearsClocksAndBackdrop()=>Sta.Run(()=>
    {
        var (host,dialog)=Open();var old=Part<FrameworkElement>(dialog,"PART_Surface");old.Opacity=0;dialog.Transition(true,true);dialog.Template=new ControlTemplate(typeof(Dialog));dialog.ApplyTemplate();Assert.IsFalse(dialog.IsPresented);Assert.IsFalse(dialog.IsOpen);Assert.IsFalse(old.HasAnimatedProperties);Assert.IsFalse(dialog.IsMotionRunning);Assert.IsTrue(Part<ContentPresenter>(host,"PART_Background").IsEnabled);
    });
    [TestMethod]
    public void TextSwapClocksClearOldCopyAndKeepNewCopyAfterRefresh()=>Sta.Run(()=>
    {
        var text=new DialogSwapText{Text="Next",FontSize=18};Layout(text,300,60);text.Refresh("Previous",true);Assert.IsTrue(text.IsMotionRunning);Pump(360);
        Assert.AreEqual("",Part<TextBlock>(text,"PART_Old").Text);Assert.AreEqual(1d,Part<TextBlock>(text,"PART_Current").Opacity,.001);Assert.IsFalse(text.IsMotionRunning);
        text.Refresh("Next",false);Assert.AreEqual("Next",Part<TextBlock>(text,"PART_Current").Text);text.Text="Third";text.Refresh("Next",true);ArcMotion.SetLevel(text,ArcMotionLevel.Off);Assert.IsFalse(text.IsMotionRunning);Assert.AreEqual("",Part<TextBlock>(text,"PART_Old").Text);
    });
    [TestMethod]
    public void AutomationNameDescriptionAndDialogRoleAreExposed()=>Sta.Run(()=>
    {
        var dialog=new Dialog{Title="Publish workspace",Description="Review before continuing"};var peer=UIElementAutomationPeer.CreatePeerForElement(dialog)!;Assert.IsTrue(peer.IsDialog());Assert.AreEqual("Publish workspace",peer.GetName());Assert.AreEqual("Review before continuing",peer.GetHelpText());AutomationProperties.SetName(dialog,"Explicit");Assert.AreEqual("Explicit",peer.GetName());Assert.IsNull(UIElementAutomationPeer.CreatePeerForElement(new DialogDecorativeText()));
    });
    [TestMethod]
    public void DynamicThemeAndBackgroundOverridesRemainLive()=>Sta.Run(()=>
    {
        var (host,dialog)=Open();var theme=new ArcThemeResources();host.Resources.MergedDictionaries.Add(theme);Layout(host);var surface=Part<Border>(dialog,"PART_Surface");var before=((SolidColorBrush)surface.Background).Color;theme.Mode=ArcThemeMode.Dark;Sta.Pump();Assert.AreNotEqual(before,((SolidColorBrush)surface.Background).Color);
        dialog.Background=Brushes.Red;Assert.AreEqual(Brushes.Red,surface.Background);dialog.Close();
    });
    [TestMethod]
    public void ShowRequiresHostAndGalleryResourcesBuildWithoutWindows()=>Sta.Run(()=>
    {
        Assert.ThrowsExactly<InvalidOperationException>(()=>new Dialog().Show(new Border()));var page=new ArcWpf.Gallery.Pages.DialogPage();var host=new DialogHost{Content=page};Layout(host);var dialog=(Dialog)page.Resources["PublishDialog"];dialog.Show(page);Layout(host);Assert.IsTrue(dialog.IsPresented);Assert.AreEqual("Publish workspace",dialog.Title);dialog.Close();
    });
    [TestMethod]
    public void CancelledBoundCloseRestoresValueAndKeepsBinding()=>Sta.Run(()=>
    {
        var model=new Model{Open=true};var dialog=new Dialog{Title="Guard"};var binding=dialog.SetBinding(Dialog.IsOpenProperty,new Binding(nameof(Model.Open)){Source=model});var host=new DialogHost{Dialog=dialog,Content=new Border()};Layout(host);
        dialog.Closing+=(_,e)=>e.Cancel=true;dialog.SetCurrentValue(Dialog.IsOpenProperty,false);Sta.Pump();Assert.IsTrue(dialog.IsOpen);Assert.IsTrue(model.Open);Assert.AreSame(binding,BindingOperations.GetBindingExpression(dialog,Dialog.IsOpenProperty));dialog.RequestClose(DialogCloseReason.HostUnavailable,true);
    });
    [TestMethod]
    public void CloseButtonAndRoutedCommandDispatchThroughDialog()=>Sta.Run(()=>
    {
        var (host,dialog)=Open();DialogCloseReason? reason=null;dialog.Closing+=(_,e)=>reason=e.Reason;
        Part<ArcWpf.Controls.Button>(dialog,"PART_CloseButton").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Assert.AreEqual(DialogCloseReason.CloseButton,reason);Assert.IsFalse(dialog.IsOpen);
        dialog.IsOpen=true;Layout(host);Assert.IsTrue(Dialog.CloseCommand.CanExecute(null,dialog));Dialog.CloseCommand.Execute(null,dialog);Assert.AreEqual(DialogCloseReason.Programmatic,reason);Assert.IsFalse(dialog.IsPresented);
    });
    // Exercise the actual clock path without displaying a native Window; don't bypass the renderer's clock completion.
    private static void SetLogicalOpen(Dialog dialog,bool value){var field=typeof(Dialog).GetField("_setting",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;field.SetValue(dialog,true);try{dialog.SetCurrentValue(Dialog.IsOpenProperty,value);}finally{field.SetValue(dialog,false);}}
    private static (DialogHost,Dialog) Open(){var dialog=new Dialog{Title="Publish workspace",Description="Review changes",Content=new TextBlock{Text="Body"}};var host=new DialogHost{Content=new Border(),Dialog=dialog};Layout(host);dialog.IsOpen=true;Layout(host);return(host,dialog);}
    private static void Layout(FrameworkElement element,double width=800,double height=600){if(!element.IsInitialized){element.BeginInit();element.EndInit();}element.Measure(new Size(width,height));element.Arrange(new Rect(0,0,width,height));element.UpdateLayout();Sta.Pump();}
    private static T Part<T>(Control control,string name)where T:FrameworkElement=>(T)control.Template.FindName(name,control);
    private static void Pump(int milliseconds){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    private sealed class Model:INotifyPropertyChanged{private bool _open;public bool Open{get=>_open;set{_open=value;PropertyChanged?.Invoke(this,new(nameof(Open)));}}public event PropertyChangedEventHandler? PropertyChanged;}
}


