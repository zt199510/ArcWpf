using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ArcWpf;
using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass]
public sealed class UndoToastTests
{
    private static (UndoToast, ObservableCollection<string>) List(int duration = 6000)
    {
        var items = new ObservableCollection<string> { "a", "b", "c" };
        var toast = new UndoToast { ItemsSource = items, Duration = duration, Height = 400, Width = 500, NounSingular = "conversation", NounPlural = "conversations" };
        ArcMotion.SetLevel(toast, ArcMotionLevel.Off);
        return (toast, items);
    }
    private static void Shown(FrameworkElement content, Action body)
    {
        var window = new Window { Width = 600, Height = 500, Content = content, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
        try { window.Show(); window.UpdateLayout(); Sta.Pump(); body(); }
        finally { window.Close(); }
    }
    private static void Wait(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    [TestMethod]
    public void RemoveCreatesUndoBatch() => Sta.Run(() =>
    {
        var c = new UndoToast();
        c.Remove("one", "archive");
        c.Remove("two", "delete");
        Assert.AreEqual(2, c.Pending.Count);
        Assert.IsTrue(c.UndoAvailable);
    });

    [TestMethod]
    public void UndoRestoresAndClearsBatch() => Sta.Run(() =>
    {
        var c = new UndoToast();
        var restored = 0;
        c.ItemsRestored += (_, e) => restored = e.Count;
        c.Remove("one");
        c.Undo();
        Assert.AreEqual(1, restored);
        Assert.AreEqual(0, c.Pending.Count);
        Assert.IsFalse(c.UndoAvailable);
    });

    [TestMethod]
    public void ToastCopyFollowsTheBatch() => Sta.Run(() =>
    {
        var (c, _) = List();
        Shown(c, () =>
        {
            c.Remove("a", "archive");
            Assert.AreEqual("Conversation archived", c.ToastText);
            c.Remove("b", "archive");
            Assert.AreEqual("2 conversations archived", c.ToastText);
            c.Remove("c", "delete");
            Assert.AreEqual("2 archived, 1 deleted", c.ToastText);
            c.Undo();
            Assert.AreEqual("3 conversations restored", c.ToastText);
        });
    });

    [TestMethod]
    public void ToastMatchesPreviewGeometry() => Sta.Run(() =>
    {
        var (c, _) = List();
        Shown(c, () =>
        {
            c.Remove("a");
            c.UpdateLayout();
            var toast = (FrameworkElement)c.Template.FindName("PART_Toast", c);
            Assert.AreEqual(Visibility.Visible, toast.Visibility);
            Assert.AreEqual(372, toast.ActualWidth);
            Assert.AreEqual(54, toast.ActualHeight);
            var origin = toast.TransformToAncestor(c).Transform(new Point());
            Assert.AreEqual((500 - 372) / 2d, origin.X, .6);
            Assert.AreEqual(400 - 12 - 54, origin.Y, .6);
        });
    });

    [TestMethod]
    public void ToastKeepsItsInsetInANarrowList() => Sta.Run(() =>
    {
        var (c, _) = List();
        c.Width = 320;
        Shown(c, () =>
        {
            c.Remove("a");
            c.UpdateLayout();
            Assert.AreEqual(296, ((FrameworkElement)c.Template.FindName("PART_Toast", c)).ActualWidth);
        });
    });

    [TestMethod]
    public void RemovedRowFoldsAndUndoReopensIt() => Sta.Run(() =>
    {
        var (c, items) = List();
        Shown(c, () =>
        {
            var first = (UndoToastContainer)c.ItemContainerGenerator.ContainerFromIndex(0);
            c.Remove("a");
            Assert.AreEqual(Visibility.Collapsed, first.Visibility);
            Assert.IsFalse(first.IsEnabled);
            Assert.AreEqual(3, items.Count, "the row stays in the items until commit");
            c.Undo();
            Assert.AreEqual(Visibility.Visible, first.Visibility);
            Assert.IsTrue(first.IsEnabled);
        });
    });

    [TestMethod]
    public void CountdownCommitsAndRemovesFromSource() => Sta.Run(() =>
    {
        var (c, items) = List(250);
        IReadOnlyList<UndoToastEntry>? committed = null;
        c.ItemsCommitted += (_, e) => committed = e;
        Shown(c, () =>
        {
            c.Remove("b");
            Wait(700);
            Assert.IsNotNull(committed);
            Assert.AreEqual(1, committed![0].OriginalIndex);
            Assert.IsFalse(items.Contains("b"));
            Assert.IsFalse(c.UndoAvailable);
        });
    });

    [TestMethod]
    public void FailedCommitReopensRows() => Sta.Run(() =>
    {
        var (c, items) = List();
        c.OnCommit = _ => Task.FromException(new InvalidOperationException());
        Shown(c, () =>
        {
            var first = (UndoToastContainer)c.ItemContainerGenerator.ContainerFromIndex(0);
            c.Remove("a", "archive");
            c.Commit();
            Assert.AreEqual(Visibility.Visible, first.Visibility);
            Assert.AreEqual(3, items.Count);
            Assert.AreEqual("Couldn't archive conversation", c.ToastText);
        });
    });

    [TestMethod]
    public void FailedUndoKeepsTheBatch() => Sta.Run(() =>
    {
        var (c, _) = List();
        c.OnUndo = _ => Task.FromException(new InvalidOperationException());
        Shown(c, () =>
        {
            c.Remove("a");
            c.Undo();
            Assert.AreEqual(1, c.Pending.Count);
            Assert.IsTrue(c.UndoAvailable);
            Assert.AreEqual("Couldn't undo, try again", c.ToastText);
        });
    });
}
