using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class ReactionsTests
{
 [TestMethod] public void ToggleAddsAndRemovesReaction(){Sta.Run(()=>{var r=new Reactions();r.Options.Add(new ReactionOption{Id="like",Label="Like"});r.ToggleForTest("like");Assert.AreEqual(1,r.Values.Single().Count);Assert.IsTrue(r.Values.Single().Reacted);r.ToggleForTest("like");Assert.IsEmpty(r.Values);});}
 [TestMethod] public void ExistingCountChangesWithReaction(){Sta.Run(()=>{var r=new Reactions();r.Options.Add(new ReactionOption{Id="like",Label="Like"});r.Values.Add(new Reaction{Id="like",Count=3,Reacted=true});r.ToggleForTest("like");Assert.AreEqual(2,r.Values.Single().Count);});}
 [TestMethod] public void WindowContainsSharedPickerHighlight(){Sta.Run(()=>{var r=new Reactions{Width=260};r.Options.Add(new ReactionOption{Id="like",Label="Like"});var w=new System.Windows.Window{Content=r,Width=360,Height=180,ShowInTaskbar=false};try{w.Show();Sta.Pump();Assert.IsNotNull(r.Template.FindName("PART_Highlight",r));}finally{w.Close();}});}
 [TestMethod] public void AddButtonGlyphTracksPickerState(){Sta.Run(()=>{var r=new Reactions{Width=260};r.Options.Add(new ReactionOption{Id="like",Label="Like"});var w=new System.Windows.Window{Content=r,Width=360,Height=180,ShowInTaskbar=false};try{w.Show();Sta.Pump();var add=(System.Windows.Controls.Button)r.Template.FindName("PART_Add",r)!;add.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Assert.AreEqual("×",add.Content);add.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Assert.AreEqual("＋",add.Content);}finally{w.Close();}});}
}
