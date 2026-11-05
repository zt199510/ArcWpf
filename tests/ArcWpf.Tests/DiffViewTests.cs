using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class DiffViewTests
{
    private static void With(Action<DiffView> action, double width = 900, double height = 500) => Sta.Run(() =>
    {
        var view = new DiffView();
        var window = new Window { Content = view, Width = width, Height = height, ShowActivated = false, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(view); }
        finally { window.Close(); }
    });

    private static string Numbered(int count, Func<int, string>? line = null) => string.Concat(Enumerable.Range(1, count).Select(i => (line?.Invoke(i) ?? $"line {i}") + "\n"));

    private static string Shape(DiffFile file) => string.Concat(file.Lines.Select(line => line.Kind switch { DiffLineKind.Added => '+', DiffLineKind.Removed => '-', _ => '=' }));

    // Applying the changes to the old text must give the new text, whatever path the alignment took.
    private static void AssertRoundTrips(string before, string after)
    {
        var file = DiffFile.FromText("a.txt", before, after);
        string[] Lines(string text) => text.Length == 0 ? [] : text.TrimEnd('\n').Split('\n');
        CollectionAssert.AreEqual(Lines(before), file.Lines.Where(l => l.Kind != DiffLineKind.Added).Select(l => l.Text).ToArray(), "old side");
        CollectionAssert.AreEqual(Lines(after), file.Lines.Where(l => l.Kind != DiffLineKind.Removed).Select(l => l.Text).ToArray(), "new side");
        int oldNo = 1, newNo = 1;
        foreach (var line in file.Lines)
        {
            if (line.Kind != DiffLineKind.Added) { Assert.AreEqual(oldNo++, line.OldNumber); } else { Assert.IsNull(line.OldNumber); }
            if (line.Kind != DiffLineKind.Removed) { Assert.AreEqual(newNo++, line.NewNumber); } else { Assert.IsNull(line.NewNumber); }
        }
    }

    [TestMethod]
    public void AlignmentKeepsSharedLinesAndPutsRemovalsFirst()
    {
        var file = DiffFile.FromText("a.txt", "a\nb\nc\nd\n", "a\nx\nc\ny\nd\n");
        Assert.AreEqual("=-+=+=", Shape(file));
        Assert.AreEqual(2, file.AddedLines);
        Assert.AreEqual(1, file.RemovedLines);
        Assert.AreEqual(DiffFileStatus.Modified, file.Status);
    }

    [TestMethod]
    public void AlignmentRoundTripsOnRandomEdits()
    {
        var random = new Random(4242);
        for (int round = 0; round < 300; round++)
        {
            string Pick() => string.Concat(Enumerable.Range(0, random.Next(0, 30)).Select(_ => "abcde"[random.Next(5)] + "\n"));
            AssertRoundTrips(Pick(), Pick());
        }
    }

    [TestMethod]
    public void AlignmentFindsTheShortestEdit()
    {
        // The classic Myers example has a shortest edit of 5.
        var file = DiffFile.FromText("a.txt", "a\nb\nc\na\nb\nb\na\n", "c\nb\na\nb\na\nc\n");
        Assert.AreEqual(5, file.AddedLines + file.RemovedLines);
    }

    [TestMethod]
    public void FilesTooDifferentFallBackToAReplacementAndSaySo()
    {
        string before = Numbered(4000, i => "old " + i), after = Numbered(4000, i => "new " + i);
        var file = DiffFile.FromText("a.txt", before, after);
        Assert.AreEqual(4000, file.AddedLines);
        Assert.AreEqual(4000, file.RemovedLines);
        Assert.IsTrue(file.Notes.Any(note => note.Contains("too different")));
        AssertRoundTrips(before, after);
    }

    [TestMethod]
    public void NewAndDeletedFilesAreMarked()
    {
        var added = DiffFile.FromText("a.txt", null, "x\ny\n");
        Assert.AreEqual(DiffFileStatus.Added, added.Status);
        Assert.AreEqual(2, added.AddedLines);
        Assert.IsTrue(added.Notes.Contains("New file."));
        var deleted = DiffFile.FromText("a.txt", "x\n", null);
        Assert.AreEqual(DiffFileStatus.Deleted, deleted.Status);
        Assert.AreEqual(1, deleted.RemovedLines);
        Assert.AreEqual(DiffFileStatus.Renamed, DiffFile.FromText("b.txt", "x\n", "x\n", oldPath: "a.txt").Status);
    }

    [TestMethod]
    public void ChangesThatLeaveTheTextAloneAreStillReported()
    {
        var endings = DiffFile.FromText("a.txt", "one\ntwo\n", "one\r\ntwo\r\n");
        Assert.AreEqual(0, endings.AddedLines + endings.RemovedLines);
        Assert.IsTrue(endings.Notes.Any(note => note.Contains("Line endings changed on 2 lines")));
        Assert.IsTrue(DiffFile.FromText("a.txt", "one\n", "one").Notes.Any(note => note.Contains("newline at the end of the file was removed")));
        Assert.IsTrue(DiffFile.FromText("a.txt", "one", "one\n").Notes.Any(note => note.Contains("newline was added")));
        Assert.IsTrue(DiffFile.FromText("a.txt", "one\n", "﻿one\n").Notes.Any(note => note.Contains("byte order mark")));
        var binary = DiffFile.FromText("a.png", "a\0b", "a\0c");
        Assert.IsTrue(binary.IsBinary);
        Assert.IsTrue(binary.Notes.Any(note => note.Contains("Binary")));
    }

    [TestMethod]
    public void InvisibleCharactersBecomeVisibleButCopyKeepsTheOriginal()
    {
        string line = "if (a) {‮ } ⁦x⁩";
        var file = DiffFile.FromText("a.ts", "b\n", line + "\n");
        Assert.IsTrue(file.Notes.Any(note => note.Contains("invisible or bidirectional")));
        string shown = DiffText.Show(line, out var marks);
        StringAssert.Contains(shown, "[U+202E]");
        StringAssert.Contains(shown, "[U+2066]");
        Assert.IsFalse(shown.Contains('‮'));
        Assert.AreEqual(3, marks!.Count);
        Assert.AreEqual(line, file.Lines.First(l => l.Kind == DiffLineKind.Added).Text);

        With(view =>
        {
            view.Files = [file];
            Sta.Pump();
            view.RowsForTest.SelectedIndex = view.CurrentRowsForTest.ToList().FindIndex(row => row.New is not null);
            Assert.AreEqual(line, view.CopyTextForTest());
        });
    }

    [TestMethod]
    public void DisplayExpandsTabsAndCutsVeryLongLines()
    {
        Assert.AreEqual("ab  c", DiffText.Show("ab\tc", out _));
        Assert.AreEqual("    c", DiffText.Show("\tc", out _));
        string shown = DiffText.Show(new string('x', DiffText.MaxShown + 500), out _);
        Assert.IsTrue(shown.EndsWith("500 more characters", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ChangedRangeIsTheMiddleAndSkipsUnrelatedLines()
    {
        var ranges = DiffText.ChangedRanges("return value;", "return value ?? other;");
        Assert.AreEqual((12, 0), ranges!.Value.Old);
        Assert.AreEqual((12, 9), ranges.Value.New);
        Assert.IsNull(DiffText.ChangedRanges("completely different", "nothing shared here!"));
        // Never cut a surrogate pair in half.
        var pair = DiffText.ChangedRanges("value 😀 tail", "value 😁 tail")!.Value;
        Assert.AreEqual("😀", "value 😀 tail".Substring(pair.Old.Start, pair.Old.Length));
    }

    [TestMethod]
    public void PatchReadsNamesHunksAndNumbers()
    {
        const string patch = "diff --git a/src/a.ts b/src/a.ts\nindex 1..2 100644\n--- a/src/a.ts\n+++ b/src/a.ts\n@@ -10,3 +10,3 @@ function f() {\n one\n-two\n+TWO\n three\n@@ -40,2 +40,3 @@\n four\n+five\n six\ndiff --git a/new.txt b/new.txt\nnew file mode 100644\n--- /dev/null\n+++ b/new.txt\n@@ -0,0 +1 @@\n+hello\ndiff --git a/gone.txt b/gone.txt\ndeleted file mode 100644\n--- a/gone.txt\n+++ /dev/null\n@@ -1 +0,0 @@\n-bye\n";
        var files = DiffFile.ParsePatch(patch);
        Assert.AreEqual(3, files.Count);
        Assert.AreEqual("src/a.ts", files[0].Path);
        Assert.AreEqual("ts", files[0].Language);
        Assert.AreEqual(2, files[0].AddedLines);
        Assert.AreEqual(1, files[0].RemovedLines);
        Assert.AreEqual(11, files[0].Lines[1].OldNumber);
        Assert.AreEqual(11, files[0].Lines[2].NewNumber);
        Assert.AreEqual(DiffFileStatus.Added, files[1].Status);
        Assert.AreEqual("new.txt", files[1].Path);
        Assert.AreEqual(DiffFileStatus.Deleted, files[2].Status);
        Assert.AreEqual("gone.txt", files[2].Path);
    }

    [TestMethod]
    public void PatchCarriesRenamesModesQuotedPathsAndContentThatLooksLikeHeaders()
    {
        var rename = DiffFile.ParsePatch("diff --git a/x y.txt b/z.txt\nsimilarity index 100%\nrename from x y.txt\nrename to z.txt\nold mode 100644\nnew mode 100755\n")[0];
        Assert.AreEqual(DiffFileStatus.Renamed, rename.Status);
        Assert.AreEqual("x y.txt", rename.OldPath);
        Assert.AreEqual("z.txt", rename.Path);
        Assert.IsTrue(rename.Notes.Contains("File mode changed from 100644 to 100755."));

        var quoted = DiffFile.ParsePatch("diff --git \"a/\\346\\226\\207.txt\" \"b/\\346\\226\\207.txt\"\n--- \"a/\\346\\226\\207.txt\"\n+++ \"b/\\346\\226\\207.txt\"\n@@ -1 +1 @@\n-a\n+b\n")[0];
        Assert.AreEqual("文.txt", quoted.Path);

        var tricky = DiffFile.ParsePatch("--- a/a.md\n+++ b/a.md\n@@ -1,2 +1,2 @@\n--- not a header\n+++ also not\n same\n");
        Assert.AreEqual(1, tricky.Count);
        Assert.AreEqual("-- not a header", tricky[0].Lines[0].Text);
        Assert.AreEqual("++ also not", tricky[0].Lines[1].Text);
    }

    [TestMethod]
    public void UnreadablePatchesAreNeverShownAsNoChanges()
    {
        var garbage = DiffFile.ParsePatch("this is not a diff");
        Assert.AreEqual(1, garbage.Count);
        Assert.IsTrue(garbage[0].Notes.Any(note => note.Contains("not a unified diff")));
        Assert.AreEqual(0, DiffFile.ParsePatch("").Count);

        var cut = DiffFile.ParsePatch("--- a/a.txt\n+++ b/a.txt\n@@ -1,5 +1,5 @@\n one\n-two\n")[0];
        Assert.IsTrue(cut.Notes.Any(note => note.Contains("ended early")));
        var bad = DiffFile.ParsePatch("--- a/a.txt\n+++ b/a.txt\n@@ nonsense @@\n")[0];
        Assert.IsTrue(bad.Notes.Any(note => note.Contains("could not be read")));
        Assert.IsTrue(DiffFile.ParsePatch("diff --git a/a.bin b/a.bin\nBinary files a/a.bin and b/a.bin differ\n")[0].IsBinary);
    }

    [TestMethod]
    public void UnchangedStretchesFoldAndUnfold() => With(view =>
    {
        string after = Numbered(40, i => i == 20 ? "changed" : $"line {i}");
        view.Files = [DiffFile.FromText("a.txt", Numbered(40), after)];
        Sta.Pump();
        var rows = view.CurrentRowsForTest;
        // 16 hidden, 3 context, the change (2 rows), 3 context, 17 hidden.
        Assert.AreEqual(10, rows.Count);
        Assert.AreEqual(16, rows[0].GapCount);
        Assert.IsTrue(rows[0].CanExpand);
        Assert.AreEqual(17, rows[^1].GapCount);
        view.RowsForTest.InvokeGap(rows[0]);
        Sta.Pump();
        Assert.AreEqual(25, view.CurrentRowsForTest.Count);
        Assert.IsFalse(view.CurrentRowsForTest[0].IsGap);
        view.ExpandAll();
        Sta.Pump();
        Assert.AreEqual(41, view.CurrentRowsForTest.Count);
        Assert.IsFalse(view.CurrentRowsForTest.Any(row => row.IsGap));
    });

    [TestMethod]
    public void PatchGapsCannotBeExpanded() => With(view =>
    {
        view.Files = DiffFile.ParsePatch("--- a/a.txt\n+++ b/a.txt\n@@ -10,1 +10,1 @@\n-a\n+b\n@@ -50,1 +50,1 @@\n-c\n+d\n");
        Sta.Pump();
        var gaps = view.CurrentRowsForTest.Where(row => row.IsGap).ToList();
        Assert.AreEqual(2, gaps.Count);
        Assert.AreEqual(9, gaps[0].GapCount);
        Assert.AreEqual(39, gaps[1].GapCount);
        Assert.IsTrue(gaps.All(gap => !gap.CanExpand));
        StringAssert.Contains(gaps[0].GapText, "not in this patch");
    });

    [TestMethod]
    public void SplitPairsRemovalsWithAdditionsAndFallsBackWhenNarrowOrOneSided() => With(view =>
    {
        var both = DiffFile.FromText("a.txt", "a\nb\nc\n", "a\nX\nY\nc\n");
        view.Mode = DiffViewMode.Split;
        view.Files = [both];
        Sta.Pump();
        Assert.AreEqual(DiffViewMode.Split, view.EffectiveMode);
        Assert.IsTrue(view.RowsForTest.IsSplit);
        var change = view.CurrentRowsForTest.Where(row => row.IsChange).ToList();
        Assert.AreEqual(2, change.Count);
        Assert.AreEqual("b", change[0].Old!.Line.Text);
        Assert.AreEqual("X", change[0].New!.Line.Text);
        Assert.IsNull(change[1].Old);

        ((Window)view.Parent).Width = 500;
        Sta.Pump();
        Assert.AreEqual(DiffViewMode.Split, view.Mode, "the choice is kept");
        Assert.AreEqual(DiffViewMode.Unified, view.EffectiveMode);

        ((Window)view.Parent).Width = 900;
        Sta.Pump();
        Assert.AreEqual(DiffViewMode.Split, view.EffectiveMode);

        view.Files = [DiffFile.FromText("b.txt", "a\n", "a\nb\n")];
        Sta.Pump();
        Assert.AreEqual(DiffViewMode.Unified, view.EffectiveMode);
    });

    [TestMethod]
    public void LargeDiffOnlyRealizesTheViewportEvenInAStackPanel() => Sta.Run(() =>
    {
        string[] lines = Enumerable.Range(1, 20000).Select(i => "row " + i).ToArray();
        string[] edited = lines.ToArray();
        for (int i = 0; i < edited.Length; i += 2) { edited[i] += " edited"; }
        var view = new DiffView { Files = [DiffFile.FromText("a.txt", string.Join("\n", lines), string.Join("\n", edited))] };
        var window = new Window { Content = new ScrollViewer { Content = new StackPanel { Children = { view } } }, Width = 900, Height = 700, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            window.Show();
            Sta.Pump();
            Assert.AreEqual(20000, view.CurrentRowsForTest.Count);
            Assert.AreEqual(480, view.RowsForTest.ActualHeight, 1, "an unbounded parent still gets a finite height");
            Assert.IsTrue(view.RowsForTest.LiveCountForTest < 60, $"realized {view.RowsForTest.LiveCountForTest}");
            view.RowsForTest.Scroller!.ScrollToVerticalOffset(15000);
            Sta.Pump();
            Assert.IsTrue(view.RowsForTest.LiveCountForTest < 60, $"realized {view.RowsForTest.LiveCountForTest} after scrolling");
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void RowsPastTheLimitAreReported() => With(view =>
    {
        view.MaxRows = 100;
        view.Files = [DiffFile.FromText("a.txt", "", Numbered(500))];
        Sta.Pump();
        Assert.AreEqual(100, view.CurrentRowsForTest.Count);
        string notes = string.Concat(LogicalText(view));
        StringAssert.Contains(notes, "Only the first 100 rows");
        StringAssert.Contains(notes, "400 changed lines");
    });

    private static IEnumerable<string> LogicalText(DependencyObject root)
    {
        if (root is TextBlock block) { yield return block.Text; }
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            foreach (string found in LogicalText(System.Windows.Media.VisualTreeHelper.GetChild(root, i))) { yield return found; }
        }
    }

    [TestMethod]
    public void NextAndPreviousStepThroughChanges() => With(view =>
    {
        string after = Numbered(200, i => i is 10 or 100 or 190 ? "changed" : $"line {i}");
        view.Files = [DiffFile.FromText("a.txt", Numbered(200), after)];
        Sta.Pump();
        Assert.AreEqual(3, view.ChangeCount);
        view.NextChange();
        Assert.AreEqual("line 10", ((DiffRow)view.RowsForTest.SelectedItem).Old!.Line.Text);
        view.NextChange();
        Assert.AreEqual("line 100", ((DiffRow)view.RowsForTest.SelectedItem).Old!.Line.Text);
        view.NextChange();
        view.NextChange();
        Assert.AreEqual("line 190", ((DiffRow)view.RowsForTest.SelectedItem).Old!.Line.Text, "stops at the last");
        view.PreviousChange();
        Assert.AreEqual("line 100", ((DiffRow)view.RowsForTest.SelectedItem).Old!.Line.Text);
    });

    [TestMethod]
    public void FilesFollowTheCollectionAndKeepSelection() => With(view =>
    {
        var first = DiffFile.FromText("a.txt", "a\n", "b\n");
        var second = DiffFile.FromText("b.txt", "c\n", "d\n");
        var files = new ObservableCollection<DiffFile> { first };
        view.Files = files;
        Sta.Pump();
        Assert.AreSame(first, view.SelectedFile);
        files.Add(second);
        Sta.Pump();
        Assert.AreSame(first, view.SelectedFile);
        view.SelectedFile = second;
        Sta.Pump();
        Assert.AreEqual("c", view.CurrentRowsForTest[0].Old!.Line.Text);
        files.Remove(second);
        Sta.Pump();
        Assert.AreSame(first, view.SelectedFile);
    });

    [TestMethod]
    public void NoChangesShowsTheEmptyTextAndKeepsItsNotes() => With(view =>
    {
        view.Files = [DiffFile.FromText("a.txt", "same\n", "same\r\n")];
        Sta.Pump();
        Assert.AreEqual(0, view.CurrentRowsForTest.Count);
        string text = string.Concat(LogicalText(view));
        StringAssert.Contains(text, "Line endings changed");
        StringAssert.Contains(text, "The text itself is unchanged.");
        Assert.IsFalse(text.Contains("No changes"), "a note says something changed");
        view.Files = [DiffFile.FromText("b.txt", "same\n", "same\n")];
        Sta.Pump();
        StringAssert.Contains(string.Concat(LogicalText(view)), "No changes");
    });

    [TestMethod]
    public void HorizontalScrollMovesTextUpToTheLongestLine() => With(view =>
    {
        view.Files = [DiffFile.FromText("a.txt", "x\n", new string('y', 400) + "\n")];
        Sta.Pump();
        var bar = FindBar(view);
        Assert.AreEqual(Visibility.Visible, bar.Visibility);
        Assert.IsTrue(bar.Maximum > 1000, $"range {bar.Maximum}");
        bar.Value = 300;
        Assert.AreEqual(300, view.RowsForTest.ScrollX);
        bar.Value = bar.Maximum;
        Assert.AreEqual(bar.Maximum, view.RowsForTest.ScrollX);
    });

    private static System.Windows.Controls.Primitives.ScrollBar FindBar(DiffView view) => (System.Windows.Controls.Primitives.ScrollBar)view.Template.FindName("PART_HorizontalBar", view);

    [TestMethod]
    public void SplitCopiesTheSideLastClicked() => With(view =>
    {
        view.Mode = DiffViewMode.Split;
        view.Files = [DiffFile.FromText("a.txt", "old\n", "new\n")];
        Sta.Pump();
        view.RowsForTest.SelectedIndex = 0;
        Assert.AreEqual("new", view.CopyTextForTest());
        view.RowsForTest.NoteClick(10);
        Assert.AreEqual("old", view.CopyTextForTest());
        view.RowsForTest.NoteClick(view.RowsForTest.ActualWidth - 10);
        Assert.AreEqual("new", view.CopyTextForTest());
    });

    [TestMethod]
    public void AThemeSwitchReachesRowsAlreadyDrawn() => Sta.Run(() =>
    {
        var theme = new ArcWpf.ArcThemeResources();
        var view = new DiffView { Files = [DiffFile.FromText("a.ts", "const a = 1;\n", "const a = 2;\n")] };
        var window = new Window { Content = view, Width = 900, Height = 300, ShowActivated = false, ShowInTaskbar = false };
        window.Resources.MergedDictionaries.Add(theme);
        try
        {
            window.Show();
            Sta.Pump();
            var before = ((SolidColorBrush)view.RowsForTest.Palette.Added).Color;
            theme.Mode = ArcWpf.ArcThemeMode.Dark;
            Sta.Pump();
            var after = ((SolidColorBrush)view.RowsForTest.Palette.Added).Color;
            Assert.AreNotEqual(before, after);
            Assert.AreEqual(((SolidColorBrush)theme["ArcWpf.Brush.DiffAdded"]).Color, after);
        }
        finally { window.Close(); }
    });
}
