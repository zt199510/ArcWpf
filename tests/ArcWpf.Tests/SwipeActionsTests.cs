using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class SwipeActionsTests
{
 [TestMethod] public void FullSwipeRunsTrailingAction(){Sta.Run(()=>{bool ran=false;var row=new SwipeActionsRow{Width=300};row.Trailing.Add(new SwipeAction{Label="Archive",OnSelect=()=>ran=true});row.CommitForTest(-200);Assert.IsTrue(ran);});}
 [TestMethod] public void CloseReturnsRowHome(){Sta.Run(()=>{var row=new SwipeActionsRow();row.CommitForTest(-50);row.Close();Assert.AreEqual(0d,row.OffsetForTest);});}
}
