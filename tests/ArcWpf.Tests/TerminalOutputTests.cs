using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class TerminalOutputTests
{
    private const string E = "\u001b[";

    private static void With(Action<TerminalOutput> action, double height = 300) => Sta.Run(() =>
    {
        var view = new TerminalOutput();
        var window = new Window { Content = view, Width = 800, Height = height, ShowActivated = false, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(view); }
        finally { window.Close(); }
    });

    private static TerminalBuffer Fed(params string[] chunks)
    {
        var buffer = new TerminalBuffer();
        foreach (string chunk in chunks) { buffer.Feed(chunk); }
        return buffer;
    }

    private static string[] Text(TerminalBuffer buffer) => buffer.Lines.Take(buffer.ShownCount).Select(line => line.Text).ToArray();

    [TestMethod]
    public void LinesSplitOnNewlinesWithoutATrailingBlank()
    {
        CollectionAssert.AreEqual(new[] { "one", "two" }, Text(Fed("one\ntwo\n")));
        CollectionAssert.AreEqual(new[] { "one", "two" }, Text(Fed("one\r\ntwo")));
        CollectionAssert.AreEqual(new[] { "a", "", "b" }, Text(Fed("a\n\nb\n")));
        Assert.AreEqual(0, Fed().ShownCount);
    }

    [TestMethod]
    public void CarriageReturnRewritesAProgressLine()
    {
        CollectionAssert.AreEqual(new[] { "100%" }, Text(Fed("10%\r50%\r100%")));
        CollectionAssert.AreEqual(new[] { "short tring" }, Text(Fed("long string\rshort ")));
        CollectionAssert.AreEqual(new[] { "done" }, Text(Fed($"loading a lot\r{E}Kdone")));
        CollectionAssert.AreEqual(new[] { "abX" }, Text(Fed("abc\bX")));
    }

    [TestMethod]
    public void OverwritingKeepsWhatLiesBeyondTheNewText() => CollectionAssert.AreEqual(new[] { "XYcdef" }, Text(Fed("abcdef\rXY")));

    [TestMethod]
    public void ErasingAndMovingWorkAcrossLines()
    {
        CollectionAssert.AreEqual(new[] { "ab" }, Text(Fed($"abcd{E}2D{E}K")));
        CollectionAssert.AreEqual(new[] { "  cd" }, Text(Fed($"abcd{E}3D{E}1K")));
        Assert.AreEqual("", Fed($"abc\n{E}A{E}2K").Lines[0].Text);
        CollectionAssert.AreEqual(new[] { "a  b" }, Text(Fed($"a{E}4Gb")));
        // npm-style: go up and rewrite an earlier line.
        CollectionAssert.AreEqual(new[] { "first!", "second" }, Text(Fed("first\nsecond\n", $"{E}2A{E}6G!")));
    }

    [TestMethod]
    public void EscapesSplitAcrossChunksStillWork()
    {
        var buffer = Fed("a\u001b", "[3", "1mb\u001b[", "0mc");
        CollectionAssert.AreEqual(new[] { "abc" }, Text(buffer));
        Assert.AreEqual(1, buffer.StyleOf(buffer.Lines[0].StyleAt(1)).Foreground);
        Assert.AreEqual(-1, buffer.StyleOf(buffer.Lines[0].StyleAt(2)).Foreground);
    }

    [TestMethod]
    public void ColorsCoverNamedIndexedAndTrueColor()
    {
        TerminalStyle Style(string code) { var b = Fed($"{E}{code}mx"); return b.StyleOf(b.Lines[0].StyleAt(0)); }
        Assert.AreEqual(3, Style("33").Foreground);
        Assert.AreEqual(9, Style("91").Foreground);
        Assert.AreEqual(4, Style("44").Background);
        Assert.AreEqual(12, Style("104").Background);
        Assert.AreEqual(5, Style("38;5;5").Foreground);
        Assert.AreEqual(TerminalStyle.RgbBase + 0xFF0000, Style("38;5;196").Foreground);
        Assert.AreEqual(TerminalStyle.RgbBase + 0x080808, Style("48;5;232").Background);
        Assert.AreEqual(TerminalStyle.RgbBase + 0x0A141E, Style("38;2;10;20;30").Foreground);
        Assert.AreEqual(TerminalStyle.RgbBase + 0x0A141E, Style("38:2:10:20:30").Foreground);
        Assert.IsTrue(Style("1;4").Flags.HasFlag(TerminalFlags.Bold | TerminalFlags.Underline));
        Assert.AreEqual(TerminalFlags.None, Style("1;22").Flags);
        Assert.AreEqual(-1, Style("31;0").Foreground);
        Assert.AreEqual(-1, Style("31;39").Foreground);
        Assert.AreEqual(-1, Fed($"{E}31m{E}mx").StyleOf(0).Foreground);
    }

    [TestMethod]
    public void TitlesAndLinksAreRemovedButTheirTextStays()
    {
        CollectionAssert.AreEqual(new[] { "ab" }, Text(Fed("a\u001b]0;window title\u0007b")));
        CollectionAssert.AreEqual(new[] { "click here" }, Text(Fed("\u001b]8;;https://x.test\u001b\\click here\u001b]8;;\u001b\\")));
        CollectionAssert.AreEqual(new[] { "ab" }, Text(Fed("a\u001b(Bb")));
        CollectionAssert.AreEqual(new[] { "ab" }, Text(Fed("a\u009b31mb")));
    }

    [TestMethod]
    public void AnUnterminatedEscapeCannotSwallowLaterOutput()
    {
        var buffer = Fed("\u001b]0;never closed" + new string('x', 70000) + "after");
        Assert.IsTrue(buffer.Lines[0].Text.EndsWith("after", StringComparison.Ordinal));
        var csi = Fed(E + new string('1', 500) + "after");
        Assert.IsTrue(csi.Lines[0].Text.EndsWith("after", StringComparison.Ordinal));
    }

    [TestMethod]
    public void InvisibleCharactersBecomeVisibleAndControlsAreDropped()
    {
        var buffer = Fed("a‮b\u0001\u0007c");
        CollectionAssert.AreEqual(new[] { "a[U+202E]bc" }, Text(buffer));
        Assert.IsTrue(buffer.StyleOf(buffer.Lines[0].StyleAt(1)).Flags.HasFlag(TerminalFlags.Mark));
    }

    [TestMethod]
    public void FullScreenProgramsCannotEraseEarlierOutputAndAreReported()
    {
        var buffer = Fed($"keep me\n{E}2J{E}3J{E}H");
        Assert.AreEqual("keep me", buffer.Lines[0].Text);
        Assert.IsTrue(buffer.UsedScreenControls);
        Assert.IsTrue(Fed($"{E}?1049h").UsedScreenControls);
        Assert.IsFalse(Fed($"{E}?25l plain").UsedScreenControls);
    }

    [TestMethod]
    public void VeryLongLinesAndTabsAreBounded()
    {
        var buffer = Fed(new string('x', TerminalBuffer.MaxLineLength + 500));
        Assert.AreEqual(TerminalBuffer.MaxLineLength, buffer.Lines[0].Length);
        StringAssert.EndsWith(buffer.Lines[0].Text, "500 more characters");
        CollectionAssert.AreEqual(new[] { "ab      c" }, Text(Fed("ab\tc")));
    }

    [TestMethod]
    public void ManyStylesDoNotGrowWithoutEnd()
    {
        var buffer = new TerminalBuffer();
        for (int i = 0; i < 20000; i++) { buffer.Feed($"{E}38;2;{i % 256};{i / 256};0mx"); }
        Assert.AreEqual(16000, buffer.Lines[0].Length);
        Assert.AreEqual(0, buffer.Lines[0].StyleAt(15999), "past the limit, text keeps its characters and loses its color");
    }

    [TestMethod]
    public void WritesFromAnotherThreadArriveInOrder() => With(view =>
    {
        var threadId = Environment.CurrentManagedThreadId;
        var worker = Task.Run(() =>
        {
            Assert.AreNotEqual(threadId, Environment.CurrentManagedThreadId);
            for (int i = 0; i < 500; i++) { view.WriteLine("line " + i); }
        });
        Until(() => worker.IsCompleted);
        view.Flush();
        Assert.AreEqual(500, view.ShownLinesForTest);
        StringAssert.StartsWith(view.GetText(), "line 0");
        StringAssert.EndsWith(view.GetText(), "line 499");
    });

    [TestMethod]
    public void AWriteShowsWithoutAnExplicitFlush() => With(view =>
    {
        view.WriteLine("hello");
        Until(() => view.ShownLinesForTest == 1);
        Assert.AreEqual(1, view.ShownLinesForTest);
    });

    [TestMethod]
    public void OnlyTheViewportIsRealizedAndOldLinesGo() => With(view =>
    {
        view.MaxLines = 1000;
        for (int i = 0; i < 5000; i++) { view.WriteLine("line " + i); }
        view.Flush();
        Sta.Pump();
        Assert.IsTrue(view.ShownLinesForTest <= 1100, $"kept {view.ShownLinesForTest}");
        StringAssert.EndsWith(view.GetText(), "line 4999");
        Assert.IsTrue(view.RowsForTest.LiveCountForTest < 40, $"realized {view.RowsForTest.LiveCountForTest}");
        StringAssert.Contains(string.Concat(AllText(view)), "Earlier output is gone");
    });

    [TestMethod]
    public void AnUnboundedParentStillGetsAFiniteHeight() => Sta.Run(() =>
    {
        var view = new TerminalOutput();
        var window = new Window { Content = new ScrollViewer { Content = new StackPanel { Children = { view } } }, Width = 800, Height = 700, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            window.Show();
            for (int i = 0; i < 5000; i++) { view.WriteLine("line " + i); }
            view.Flush();
            Sta.Pump();
            Assert.AreEqual(320, view.RowsForTest.ActualHeight, 1);
            Assert.IsTrue(view.RowsForTest.LiveCountForTest < 40, $"realized {view.RowsForTest.LiveCountForTest}");
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void FollowsUntilScrolledAwayAndJumpReturns() => With(view =>
    {
        for (int i = 0; i < 200; i++) { view.WriteLine("line " + i); }
        view.Flush();
        Sta.Pump();
        var scroller = view.RowsForTest.Scroller!;
        Assert.IsTrue(view.IsFollowing);
        Assert.AreEqual(scroller.ScrollableHeight, scroller.VerticalOffset, .5);
        Assert.IsFalse(view.JumpShownForTest);

        scroller.ScrollToVerticalOffset(20);
        Sta.Pump();
        Assert.IsFalse(view.IsFollowing);
        Assert.IsTrue(view.JumpShownForTest);
        view.WriteLine("more");
        view.Flush();
        Sta.Pump();
        Assert.AreEqual(20, scroller.VerticalOffset, .5, "stays where it was left");
        Assert.IsFalse(view.IsFollowing);

        view.JumpForTest.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Sta.Pump();
        Assert.IsTrue(view.IsFollowing);
        Assert.AreEqual(scroller.ScrollableHeight, scroller.VerticalOffset, .5);
        Assert.IsFalse(view.JumpShownForTest);

        scroller.ScrollToVerticalOffset(20);
        Sta.Pump();
        scroller.ScrollToEnd();
        Sta.Pump();
        Assert.IsTrue(view.IsFollowing, "reaching the end by hand follows again");
    });

    [TestMethod]
    public void AProgressLineLeavesFollowingFreeToBeReleased() => With(view =>
    {
        for (int i = 0; i < 200; i++) { view.WriteLine("line " + i); }
        view.Write("10%");
        view.Flush();
        Sta.Pump();
        view.Write("\r20%");
        view.Flush();
        Sta.Pump();
        var scroller = view.RowsForTest.Scroller!;
        scroller.ScrollToVerticalOffset(20);
        Sta.Pump();
        Assert.IsFalse(view.IsFollowing);
    });

    [TestMethod]
    public void TrimmingKeepsTheLinesBeingRead() => With(view =>
    {
        view.MaxLines = 1000;
        for (int i = 0; i < 1000; i++) { view.WriteLine("line " + i); }
        view.Flush();
        Sta.Pump();
        var scroller = view.RowsForTest.Scroller!;
        scroller.ScrollToVerticalOffset(500);
        Sta.Pump();
        Assert.IsFalse(view.IsFollowing);
        var list = (ListBox)view.RowsForTest;
        string reading = ((TerminalLine)list.Items[(int)scroller.VerticalOffset]).Text;
        for (int i = 0; i < 200; i++) { view.WriteLine("more " + i); }
        view.Flush();
        Sta.Pump();
        Assert.AreEqual(reading, ((TerminalLine)list.Items[(int)scroller.VerticalOffset]).Text);
        Assert.IsFalse(view.IsFollowing);
    });

    [TestMethod]
    public void BadgeTextStaysLegibleOnItsBackground()
    {
        var gray = new SolidColorBrush(Color.FromRgb(158, 158, 158));
        var yellow = new SolidColorBrush(Color.FromRgb(230, 190, 70));
        var red = new SolidColorBrush(Color.FromRgb(160, 40, 40));
        Assert.AreSame(Brushes.Black, TerminalPalette.Readable(gray, yellow));
        Assert.AreSame(Brushes.White, TerminalPalette.Readable(new SolidColorBrush(Color.FromRgb(120, 60, 60)), red));
        Assert.AreSame(gray, TerminalPalette.Readable(gray, new SolidColorBrush(Color.FromRgb(20, 20, 20))), "already fine");
    }

    [TestMethod]
    public void StatusShowsTextAndStopOnlyWhileRunning() => With(view =>
    {
        Assert.IsFalse(view.StopShownForTest);
        view.Status = TerminalStatus.Running;
        Assert.IsTrue(view.StopShownForTest);
        StringAssert.StartsWith(view.StatusTextForTest, "Running");
        int stops = 0;
        view.StopRequested += (_, _) => stops++;
        view.StopForTest.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert.AreEqual(1, stops);
        Assert.AreEqual(TerminalStatus.Running, view.Status, "the host decides when it has stopped");

        view.ExitCode = 3;
        view.Status = TerminalStatus.Failed;
        StringAssert.StartsWith(view.StatusTextForTest, "Exit 3");
        Assert.IsFalse(view.StopShownForTest);
        view.CanStop = false;
        view.Status = TerminalStatus.Running;
        Assert.IsFalse(view.StopShownForTest);
        view.Status = TerminalStatus.Succeeded;
        StringAssert.StartsWith(view.StatusTextForTest, "Done");
    });

    [TestMethod]
    public void FinishingShowsEverythingWrittenBeforeIt() => With(view =>
    {
        view.Status = TerminalStatus.Running;
        view.WriteLine("last words");
        view.Status = TerminalStatus.Succeeded;
        Assert.AreEqual(1, view.ShownLinesForTest);
    });

    [TestMethod]
    public void EmptyTextFollowsTheStatus() => With(view =>
    {
        view.Status = TerminalStatus.Running;
        StringAssert.Contains(string.Concat(AllText(view)), "Waiting for output");
        view.Status = TerminalStatus.Succeeded;
        StringAssert.Contains(string.Concat(AllText(view)), "No output");
    });

    [TestMethod]
    public void ClearStartsOver() => With(view =>
    {
        view.WriteLine($"{E}31mred");
        view.Flush();
        view.Clear();
        Assert.AreEqual(0, view.ShownLinesForTest);
        view.WriteLine("plain");
        view.Flush();
        Assert.AreEqual("plain", view.GetText());
        Assert.AreEqual(-1, view.BufferForTest.StyleOf(view.BufferForTest.Lines[0].StyleAt(0)).Foreground, "the pen was reset");
    });

    [TestMethod]
    public void SelectionCopiesInOrder() => With(view =>
    {
        for (int i = 0; i < 5; i++) { view.WriteLine("line " + i); }
        view.Flush();
        Sta.Pump();
        var rows = (ListBox)view.RowsForTest;
        rows.SelectedItems.Add(rows.Items[3]);
        rows.SelectedItems.Add(rows.Items[1]);
        Assert.AreEqual("line 1" + Environment.NewLine + "line 3", view.CopyTextForTest());
    });

    [TestMethod]
    public void AThemeSwitchChangesColorsOfLinesAlreadyDrawn() => Sta.Run(() =>
    {
        var theme = new ArcWpf.ArcThemeResources();
        var view = new TerminalOutput();
        var window = new Window { Content = view, Width = 800, Height = 300, ShowActivated = false, ShowInTaskbar = false };
        window.Resources.MergedDictionaries.Add(theme);
        try
        {
            window.Show();
            view.WriteLine($"{E}31mred");
            view.Flush();
            Sta.Pump();
            var list = (TerminalRowList)view.RowsForTest;
            var before = ((SolidColorBrush)list.Palette.Of(1, false, list.Palette.Ink)).Color;
            theme.Mode = ArcWpf.ArcThemeMode.Dark;
            Sta.Pump();
            var after = ((SolidColorBrush)list.Palette.Of(1, false, list.Palette.Ink)).Color;
            Assert.AreNotEqual(before, after);
            Assert.AreEqual(((SolidColorBrush)theme["ArcWpf.Brush.TerminalAnsi1"]).Color, after);
        }
        finally { window.Close(); }
    });

    private static void Until(Func<bool> done)
    {
        var limit = DateTime.UtcNow.AddSeconds(10);
        while (!done() && DateTime.UtcNow < limit) { Sta.Pump(); Thread.Sleep(5); }
    }

    private static IEnumerable<string> AllText(DependencyObject root)
    {
        if (root is TextBlock block) { yield return block.Text; }
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            foreach (string found in AllText(VisualTreeHelper.GetChild(root, i))) { yield return found; }
        }
    }
}
