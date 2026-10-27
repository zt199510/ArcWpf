using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
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
namespace ArcWpf.Tests;
[TestClass]
public sealed class OTPInputTests
{
    [TestMethod] public void ValueBindingNormalizationAndCompletionAreSeparate() => Sta.Run(()=>InWindow(c=>
    {
        var model=new Model(); c.SetBinding(OTPInput.ValueProperty,new Binding(nameof(Model.Code)){Source=model});
        int changes=0,complete=0; c.ValueChanged+=(_,_)=>changes++; c.Completed+=(_,_)=>complete++;
        model.Code="a12-34567"; Assert.AreEqual("123456",c.Value); Assert.IsTrue(c.IsComplete); Assert.AreEqual(0,complete);
        c.Clear(); Assert.AreEqual("",model.Code); c.ApplyInput(0,"98 76-54"); Assert.AreEqual("987654",model.Code); Assert.AreEqual(1,complete);
        c.ApplyInput(0,"987654"); Assert.AreEqual(1,complete); c.ApplyInput(5,"3"); Assert.AreEqual(2,complete);
        Assert.IsNotNull(c.GetBindingExpression(OTPInput.ValueProperty)); Assert.IsGreaterThanOrEqualTo(3,changes);
        c.Length=4; Assert.AreEqual(4,c.Value.Length); Assert.HasCount(4,c.Digits); Assert.AreEqual(2,complete);
        Assert.Throws<ArgumentException>(()=>c.Length=0); Assert.Throws<ArgumentException>(()=>c.Length=13);
    }));
    [TestMethod] public void NativeTextInputPasteAndNavigationUseSameEditingContract() => Sta.Run(()=>InWindow(c=>
    {
        c.FocusDigit(0); Type(c.Digits[0],"1"); Assert.AreEqual("1",c.Value); Assert.IsTrue(c.Digits[1].IsKeyboardFocused);
        Type(c.Digits[1],"x"); Assert.AreEqual("1",c.Value); Assert.AreEqual("",c.Digits[1].Text);
        var paste=new DataObjectPastingEventArgs(new DataObject(DataFormats.UnicodeText,"2-3 4567"),false,DataFormats.UnicodeText){RoutedEvent=DataObject.PastingEvent};
        c.Digits[1].RaiseEvent(paste); Assert.IsTrue(paste.CommandCancelled); Assert.AreEqual("123456",c.Value); Assert.IsTrue(c.Digits[5].IsKeyboardFocused); var emptyPaste=new DataObjectPastingEventArgs(new DataObject(DataFormats.UnicodeText,""),false,DataFormats.UnicodeText){RoutedEvent=DataObject.PastingEvent}; c.Digits[5].RaiseEvent(emptyPaste); Assert.AreEqual("123456",c.Value);
        Key(c.Digits[5],System.Windows.Input.Key.Left); Assert.IsTrue(c.Digits[4].IsKeyboardFocused);
        Key(c.Digits[4],System.Windows.Input.Key.Delete); Assert.AreEqual("12346",c.Value);
        c.FocusDigit(5); Key(c.Digits[5],System.Windows.Input.Key.Back); Assert.AreEqual("1234",c.Value); Assert.IsTrue(c.Digits[4].IsKeyboardFocused);
        Key(c.Digits[4],System.Windows.Input.Key.Home); Assert.IsTrue(c.Digits[0].IsKeyboardFocused);
        Type(c.Digits[0],"9"); Assert.AreEqual("9234",c.Value);
        c.Clear(); Type(c.Digits[5],"8"); Assert.AreEqual("8",c.Value); Assert.IsTrue(c.Digits[1].IsKeyboardFocused);
    }));
    [TestMethod] public void DisabledReadOnlyAndTemplatesPreserveValue() => Sta.Run(()=>InWindow(c=>
    {
        c.Value="123"; c.IsReadOnly=true; Type(c.Digits[0],"9"); Assert.AreEqual("123",c.Value); Assert.IsTrue(c.Digits.All(d=>d.IsReadOnly));
        c.IsReadOnly=false; c.IsEnabled=false; c.ApplyInput(0,"8"); Assert.AreEqual("123",c.Value);
        c.IsEnabled=true; var template=c.Template; var old=c.Digits[0]; c.Template=new ControlTemplate(typeof(OTPInput)); c.ApplyTemplate();
        old.Text="7"; Assert.AreEqual("123",c.Value); c.Template=template; c.ApplyTemplate(); c.UpdateLayout(); Assert.AreEqual("1",c.Digits[0].Text);
        c.Visibility=Visibility.Hidden; Assert.IsFalse(c.IsMotionRunning); c.Visibility=Visibility.Visible;
    }));
    [TestMethod] public void AutomationUsesNamedNativeEditors() => Sta.Run(()=>InWindow(c=>
    {
        var group=UIElementAutomationPeer.CreatePeerForElement(c)!; Assert.AreEqual(AutomationControlType.Group,group.GetAutomationControlType()); Assert.AreEqual("Verification code",group.GetName());
        var peer=UIElementAutomationPeer.CreatePeerForElement(c.Digits[0])!; Assert.AreEqual(AutomationControlType.Edit,peer.GetAutomationControlType()); Assert.Contains("digit 1 of 6",peer.GetName());
        var value=(IValueProvider)peer.GetPattern(PatternInterface.Value)!; value.SetValue("5"); Assert.AreEqual("5",c.Value); Assert.AreEqual("5",value.Value);
        c.IsReadOnly=true; Assert.IsTrue(value.IsReadOnly); c.Description="Email"; c.ErrorMessage="Retry"; Assert.AreEqual("Email Retry",AutomationProperties.GetHelpText(c.Digits[0]));
        Assert.IsFalse(InputMethod.GetIsInputMethodEnabled(c.Digits[0]));
    }));
    [TestMethod] public void SlotCoordinatesShrinkAndRingTracksResize() => Sta.Run(()=>InWindow(c=>
    {
        ArcMotion.SetLevel(c,ArcMotionLevel.Off);
        foreach(double width in new[]{400d,180d,70d})
        {
            c.Width=width; c.UpdateLayout(); c.FocusDigit(5); c.UpdateLayout();
            var first=c.Digits[0]; var last=c.Digits[5]; var origin=first.TranslatePoint(new Point(),c); var end=last.TranslatePoint(new Point(last.ActualWidth,0),c);
            Assert.AreEqual(48d,first.ActualHeight,.01); Assert.IsLessThanOrEqualTo(width+.1,end.X); Assert.IsGreaterThan(0d,first.ActualWidth);
            if(width==400) { Assert.AreEqual(42d,first.ActualWidth,.01); Assert.AreEqual(292d,end.X-origin.X,.01); }
            var ring=Part<Border>(c,"PART_Ring"); Assert.AreEqual(last.ActualWidth,ring.ActualWidth,.1);
            Assert.AreEqual(last.TranslatePoint(new Point(),c).X,ring.TranslatePoint(new Point(),c).X,.1);
            var hit=c.InputHitTest(first.TranslatePoint(new Point(first.ActualWidth/2,24),c)); Assert.IsTrue(Popover.IsWithin(hit as DependencyObject,first));
        }
    }));
    [TestMethod] public void FocusGlyphShakeAndMessagesReachStableFinalFrames() => Sta.Run(()=>InWindow(c=>
    {
        Pump(120); c.FocusDigit(0); Pump(350); var ring=Part<Border>(c,"PART_Ring"); var move=(TranslateTransform)((TransformGroup)ring.RenderTransform).Children[1];
        c.FocusDigit(5); for(int i=0;i<8;i++){Pump(20);if(move.X is >0 and <250)break;} Assert.IsTrue(move.X>0 && move.X<250);
        c.FocusDigit(1); c.ApplyInput(0,"123456"); c.Description="New helper message that wraps across several lines for testing"; c.ErrorMessage="Wrong code";
        Pump(40); Assert.IsTrue(c.IsMotionRunning); c.ErrorMessage=""; c.ApplyInput(0,"6"); Pump(900);
        Assert.IsFalse(c.IsMotionRunning); Assert.AreEqual(1d,ring.Opacity,.001); Assert.AreEqual("6",c.Digits[0].Text);
        var painted=(Border)c.Digits[0].Template.FindName("PART_Surface",c.Digits[0]);
        Assert.AreEqual(((SolidColorBrush)c.Digits[0].BorderBrush).Color,((SolidColorBrush)painted.BorderBrush).Color,"Filled border must survive animation clock removal.");
        Assert.AreEqual(((SolidColorBrush)c.Digits[0].Background).Color,((SolidColorBrush)painted.Background).Color,"Filled background must survive animation clock removal.");
        foreach(var digit in c.Digits) { var host=(Grid)digit.Template.FindName("PART_Glyphs",digit); Assert.AreEqual(1,host.Children.Count); Assert.AreEqual(1d,host.Children[0].Opacity,.001); }
        ArcMotion.SetLevel(c,ArcMotionLevel.Reduced); c.FocusDigit(0); c.ErrorMessage="Retry"; c.ApplyInput(0,"654321"); Pump(200); Assert.IsFalse(c.IsMotionRunning);
        ArcMotion.SetLevel(c,ArcMotionLevel.Off); c.ApplyInput(0,"123456"); c.FocusDigit(2); Assert.IsFalse(c.IsMotionRunning);
    }));
    [TestMethod] public void ThemeColorsAndStatePriorityRefresh() => Sta.Run(()=>InWindow(c=>
    {
        ArcMotion.SetLevel(c,ArcMotionLevel.Off); c.Value="1";
        Assert.AreEqual(((SolidColorBrush)c.FindResource("ArcWpf.Brush.Accent")).Color,((SolidColorBrush)c.Digits[0].BorderBrush).Color);
        c.ErrorMessage="Wrong"; Assert.AreEqual(((SolidColorBrush)c.FindResource("ArcWpf.Brush.Danger")).Color,((SolidColorBrush)c.Digits[0].BorderBrush).Color);
        Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single().Mode=ArcThemeMode.Dark; c.UpdateLayout();
        var surface=(Border)c.Digits[0].Template.FindName("PART_Surface",c.Digits[0]); Assert.AreEqual(((SolidColorBrush)c.FindResource("ArcWpf.Brush.OTPErrorSurface")).Color,((SolidColorBrush)surface.Background).Color);
        Assert.AreEqual(((SolidColorBrush)c.FindResource("ArcWpf.Brush.Danger")).Color,((SolidColorBrush)Part<Border>(c,"PART_Ring").BorderBrush).Color);
        c.ErrorMessage=""; Assert.AreEqual(((SolidColorBrush)c.FindResource("ArcWpf.Brush.Accent")).Color,((SolidColorBrush)surface.BorderBrush).Color);
    }));
    [TestMethod] public void GalleryRendersLightDarkAndFilledState() => Sta.Run(()=>
    {
        var page=new OTPInputPage(); page.SetResourceReference(Control.BackgroundProperty,"ArcWpf.Brush.Background"); var window=new Window{Content=page,Width=720,Height=1080,ShowActivated=false};
        try { window.Show(); Pump(500); Save(window,"gallery-light.png"); var c=(OTPInput)page.FindName("Editable"); c.ApplyInput(0,"123456"); Pump(650); foreach(var digit in c.Digits) { Assert.IsTrue(digit.HasDigit); var shell=(Border)digit.Template.FindName("PART_Surface",digit); Assert.AreEqual(((SolidColorBrush)c.FindResource("ArcWpf.Brush.Accent")).Color,((SolidColorBrush)shell.BorderBrush).Color); } var filled=Save(window,"gallery-filled.png"); var pixel=new byte[4]; var point=c.Digits[0].TranslatePoint(new Point(5,24),window); filled.CopyPixels(new Int32Rect((int)point.X,(int)point.Y,1,1),pixel,4,0); Assert.IsLessThan((byte)245,pixel[2],"Filled surface must actually render the updated brush."); Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single().Mode=ArcThemeMode.Dark; Pump(400); Save(window,"gallery-dark.png"); }
        finally { window.Close(); Sta.Pump(); }
    });
    private static void Type(OTPDigit digit,string text)=>digit.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice,new TextComposition(InputManager.Current,digit,text)){RoutedEvent=TextCompositionManager.PreviewTextInputEvent});
    private static void Key(UIElement target,Key key)=>target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(target)!,0,key){RoutedEvent=Keyboard.PreviewKeyDownEvent});
    private static T Part<T>(OTPInput c,string name) where T:class => (T)c.Template.FindName(name,c);
    private static void InWindow(Action<OTPInput> action)
    { var c=new OTPInput{Label="Verification code",Width=400,Margin=new Thickness(30),VerticalAlignment=VerticalAlignment.Top}; var w=new Window{Content=c,Width=600,Height=500,ShowActivated=false}; try{w.Show();Sta.Pump();action(c);} finally{w.Close();Sta.Pump();Assert.IsFalse(c.IsMotionRunning);} }
    private static void Pump(int ms){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    private static RenderTargetBitmap Save(FrameworkElement element,string name)
    {
        element.UpdateLayout();var bounds=new Rect(0,0,element.ActualWidth,element.ActualHeight);var visual=new DrawingVisual();
        using(var draw=visual.RenderOpen()){draw.DrawRectangle((Brush)element.FindResource("ArcWpf.Brush.Background"),null,bounds);draw.DrawRectangle(new VisualBrush(element){ViewboxUnits=BrushMappingMode.Absolute,Viewbox=bounds,Stretch=Stretch.Fill},null,bounds);}
        var bitmap=new RenderTargetBitmap((int)bounds.Width,(int)bounds.Height,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var root=new System.IO.DirectoryInfo(AppContext.BaseDirectory);while(root is not null && !System.IO.File.Exists(System.IO.Path.Combine(root.FullName,"ArcWpf.slnx")))root=root.Parent;
        var folder=System.IO.Path.Combine(root!.FullName,"artifacts","otp-input");System.IO.Directory.CreateDirectory(folder);using var file=System.IO.File.Create(System.IO.Path.Combine(folder,name));encoder.Save(file); return bitmap;
    }
    private sealed class Model:INotifyPropertyChanged
    { private string _code="";public string Code{get=>_code;set{_code=value;PropertyChanged?.Invoke(this,new(nameof(Code)));}}public event PropertyChangedEventHandler? PropertyChanged; }
}




