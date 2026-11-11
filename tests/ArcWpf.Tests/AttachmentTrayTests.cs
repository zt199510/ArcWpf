using System.Windows;
using System.Windows.Controls.Primitives;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class AttachmentTrayTests
{
    private static void With(Action<AttachmentTray> action, AttachmentTrayLayout layout = AttachmentTrayLayout.List) => Sta.Run(() =>
    {
        var tray = new AttachmentTray { Layout = layout };
        ArcMotion.SetLevel(tray, ArcMotionLevel.Off);
        var window = new Window { Content = tray, Width = 520, Height = 400, ShowActivated = false, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(tray); }
        finally { window.Close(); }
    });

    private static void Press(System.Windows.Controls.Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    private static PromptAttachment Uploading(string name, double progress = .25)
    {
        var item = new PromptAttachment(name) { Detail = "12 KB" };
        item.Progress = progress;
        item.Status = PromptAttachmentStatus.Uploading;
        return item;
    }

    private static PromptAttachment Failed(string name, string error)
    {
        var item = new PromptAttachment(name) { Detail = "12 KB" };
        item.Error = error;
        item.Status = PromptAttachmentStatus.Error;
        return item;
    }

    [TestMethod]
    public void UploadShowsCountedPercentAndBar() => With(tray =>
    {
        var item = Uploading("a.md", .25);
        tray.Items.Add(item);
        Sta.Pump();
        Assert.AreEqual("12 KB  ·  Uploading 25%", tray.MetaForTest(item));
        Assert.IsTrue(tray.BarShownForTest(item));
        Assert.IsFalse(tray.CheckShownForTest(item));
        item.Progress = .5;
        Sta.Pump();
        Assert.AreEqual("12 KB  ·  Uploading 50%", tray.MetaForTest(item));
    });

    [TestMethod]
    public void FinishedUploadFoldsTheBarAndShowsTheCheck() => With(tray =>
    {
        var item = Uploading("a.md");
        tray.Items.Add(item);
        Sta.Pump();
        item.Progress = 1;
        item.Status = PromptAttachmentStatus.Done;
        Sta.Pump();
        Assert.AreEqual("12 KB  ·  Uploaded", tray.MetaForTest(item));
        Assert.IsFalse(tray.BarShownForTest(item));
        Assert.IsTrue(tray.CheckShownForTest(item));
        Assert.AreEqual(0, tray.CheckDashOffsetForTest(item));
        Assert.IsFalse(tray.RetryShownForTest(item));
    });

    [TestMethod]
    public void FailureShowsReasonAndRetryRestartsFromZero() => With(tray =>
    {
        var item = Failed("a.md", "Network lost");
        tray.Items.Add(item);
        Sta.Pump();
        Assert.AreEqual("12 KB  ·  Network lost", tray.MetaForTest(item));
        Assert.IsTrue(tray.RetryShownForTest(item));
        PromptAttachment? asked = null;
        tray.RetryRequested += (_, e) => asked = e.Item;
        Press(tray.RetryForTest(item));
        Sta.Pump();
        Assert.AreSame(item, asked);
        Assert.AreEqual(PromptAttachmentStatus.Uploading, item.Status);
        Assert.IsNull(item.Error);
        Assert.AreEqual(0, tray.ShownProgressForTest(item));
        Assert.IsTrue(tray.BarShownForTest(item));
        Assert.IsFalse(tray.RetryShownForTest(item));
    });

    [TestMethod]
    public void RemoveTakesTheItemOutAndReportsIt() => With(tray =>
    {
        var first = new PromptAttachment("a.md");
        var second = new PromptAttachment("b.md");
        tray.Items.Add(first);
        tray.Items.Add(second);
        Sta.Pump();
        PromptAttachment? removed = null;
        tray.Removed += (_, e) => removed = e.Item;
        Press(tray.RemoveForTest(first));
        Sta.Pump();
        Assert.AreSame(first, removed);
        Assert.HasCount(1, tray.Items);
        Assert.HasCount(1, tray.ShownForTest);
        Assert.AreSame(second, tray.ShownForTest[0]);
    });

    [TestMethod]
    public void RemovedAttachmentNoLongerDrivesItsRow() => With(tray =>
    {
        var item = Uploading("a.md");
        tray.Items.Add(item);
        Sta.Pump();
        Press(tray.RemoveForTest(item));
        Sta.Pump();
        item.Status = PromptAttachmentStatus.Done;
        item.Progress = 1;
        Sta.Pump();
        Assert.IsEmpty(tray.ShownForTest);
    });

    [TestMethod]
    public void PreviewIsAThumbnailButtonOnlyWhenThereIsAPicture() => With(tray =>
    {
        var plain = new PromptAttachment("a.md");
        var image = new System.Windows.Media.DrawingImage(new System.Windows.Media.GeometryDrawing(System.Windows.Media.Brushes.Red, null, new System.Windows.Media.RectangleGeometry(new Rect(0, 0, 8, 8))));
        var picture = new PromptAttachment("b.png") { Preview = image };
        tray.Items.Add(plain);
        tray.Items.Add(picture);
        Sta.Pump();
        Assert.IsNull(tray.ThumbnailForTest(plain));
        PromptAttachment? asked = null;
        tray.PreviewRequested += (_, e) => asked = e.Item;
        Press((System.Windows.Controls.Button)tray.ThumbnailForTest(picture)!);
        Assert.AreSame(picture, asked);
    });

    [TestMethod]
    public void MissingImageFileFallsBackToTheIcon() => With(tray =>
    {
        var item = new PromptAttachment("b.png") { FilePath = @"C:\does\not\exist\b.png" };
        tray.Items.Add(item);
        Sta.Pump();
        Assert.IsNull(tray.ThumbnailForTest(item));
    });

    [TestMethod]
    public void ChipsLayoutKeepsStatesAndLaysOutSideways() => With(tray =>
    {
        var failed = Failed("a.md", "Too large");
        var running = Uploading("b.md");
        tray.Items.Add(failed);
        tray.Items.Add(running);
        Sta.Pump();
        Assert.IsTrue(tray.RetryShownForTest(failed));
        Assert.AreEqual("Cancel upload", System.Windows.Automation.AutomationProperties.GetName(tray.RemoveForTest(running)));
        Assert.AreEqual("Remove a.md", System.Windows.Automation.AutomationProperties.GetName(tray.RemoveForTest(failed)));
        Assert.AreEqual(44, tray.EntryForTest(failed).ActualHeight, .5);
        Assert.AreEqual(tray.EntryForTest(failed).TranslatePoint(new Point(), tray).Y, tray.EntryForTest(running).TranslatePoint(new Point(), tray).Y, .5);
    }, AttachmentTrayLayout.Chips);

    [TestMethod]
    public void SwitchingLayoutRebuildsFromTheSameItems() => With(tray =>
    {
        tray.Items.Add(new PromptAttachment("a.md"));
        tray.Items.Add(Failed("b.md", "No"));
        Sta.Pump();
        tray.Layout = AttachmentTrayLayout.Chips;
        Sta.Pump();
        Assert.HasCount(2, tray.ShownForTest);
        tray.Items.Clear();
        Sta.Pump();
        Assert.IsEmpty(tray.ShownForTest);
    });
}
