using System.ComponentModel;using System.Windows.Controls.Primitives;using NativeButton=System.Windows.Controls.Button;using System.Windows;using System.Windows.Automation;using System.Windows.Automation.Peers;using System.Windows.Controls;using System.Windows.Media;using System.Windows.Threading;using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass]public sealed class ToastTests
{
 [TestMethod]public void HostQueuesNotificationsAndRemovesAfterClose()=>Sta.Run(()=>{var host=new ToastHost();Layout(host);var first=host.Show("Changes saved","Synced to all devices.",0);var second=host.Show("Another update");Layout(host);Assert.AreEqual(2,host.Items.Count);Assert.IsTrue(first.IsPresented);first.Close();Pump(220);Assert.AreEqual(1,host.Items.Count);host.DismissAll();Pump(220);Assert.AreEqual(0,host.Items.Count);});
 [TestMethod]public void GeometryUses416Width26RadiusAndSuccessMark()=>Sta.Run(()=>{var t=new Toast{Title="Changes saved",Description="Synced to all devices."};Layout(t,500,160);var s=Part<Border>(t,"PART_Surface");Assert.AreEqual(416d,s.ActualWidth,.01);Assert.AreEqual(new CornerRadius(26),s.CornerRadius);Assert.AreEqual(new Thickness(16,14,14,14),s.Padding);Assert.AreEqual(30d,Part<Border>(t,"SuccessMark").ActualWidth,.01);t.Close();});
 [TestMethod]public void TimeoutClosesAndDurationZeroDisablesTimer()=>Sta.Run(()=>{var t=new Toast{Title="Saved",Duration=80};Layout(t);Pump(230);Assert.IsFalse(t.IsOpen);var keep=new Toast{Title="Keep",Duration=0};Layout(keep);Pump(180);Assert.IsTrue(keep.IsPresented);keep.Close();});
 [TestMethod]public void ThemeUpdatesAndGalleryConstructs()=>Sta.Run(()=>{var t=new Toast{Title="Saved"};var theme=new ArcThemeResources();t.Resources.MergedDictionaries.Add(theme);Layout(t);var s=Part<Border>(t,"PART_Surface");var c=((SolidColorBrush)s.Background).Color;theme.Mode=ArcThemeMode.Dark;Sta.Pump();Assert.AreNotEqual(c,((SolidColorBrush)s.Background).Color);Layout(new ArcWpf.Gallery.Pages.ToastPage(),800,700);});
 static void Layout(FrameworkElement e,double w=500,double h=200){if(!e.IsInitialized){e.BeginInit();e.EndInit();}e.Measure(new Size(w,h));e.Arrange(new Rect(0,0,w,h));e.UpdateLayout();Sta.Pump();}static T Part<T>(Control c,string n)where T:FrameworkElement=>(T)c.Template.FindName(n,c);static void Pump(int ms){var f=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};timer.Tick+=(_,_)=>{timer.Stop();f.Continue=false;};timer.Start();Dispatcher.PushFrame(f);}private sealed class Model:INotifyPropertyChanged{public bool Open{get;set;}public event PropertyChangedEventHandler? PropertyChanged;}
}




