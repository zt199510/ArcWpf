using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class ModelPickerTests
{
    private static readonly ModelEffort[] All = [ModelEffort.Low, ModelEffort.Medium, ModelEffort.High, ModelEffort.ExtraHigh, ModelEffort.Max];
    private static readonly PickerModel[] Models =
    [
        new("opus", "Claude Opus", "anthropic") { Short = "Opus", Blurb = "Flagship", Description = "The flagship.", Context = "1M tokens", InputPrice = 4, OutputPrice = 20, Efforts = All, DefaultEffort = ModelEffort.Medium },
        new("sonnet", "Claude Sonnet", "anthropic") { Blurb = "Everyday", Description = "Everyday work.", InputPrice = 3, OutputPrice = 15, Efforts = [ModelEffort.Low, ModelEffort.High], DefaultEffort = ModelEffort.Low },
        new("gpt", "GPT Sol", "openai") { Blurb = "General" },
        new("haiku", "Claude Haiku", "anthropic") { Blurb = "Quick" },
        new("mini", "GPT Mini", "openai") { Blurb = "Small" },
    ];

    private static void Run(Action<ModelPicker> action, double width = 700, Action<ModelPicker>? setup = null)
    {
        Sta.Run(() =>
        {
            var picker = new ModelPicker { Models = Models, Featured = ["opus", "sonnet", "gpt"], Value = "opus" };
            ArcMotion.SetLevel(picker, ArcMotionLevel.Off);
            setup?.Invoke(picker);
            var window = new Window { Content = picker, Width = width, Height = 600, ShowActivated = false, ShowInTaskbar = false };
            try { window.Show(); Sta.Pump(); action(picker); }
            finally { picker.IsOpen = false; window.Close(); }
        });
    }

    [TestMethod]
    public void AnUnacceptedEffortSnapsToTheNearest()
    {
        Assert.AreEqual(ModelEffort.High, ModelEffortText.Snap(ModelEffort.Max, [ModelEffort.Low, ModelEffort.High]));
        Assert.AreEqual(ModelEffort.Low, ModelEffortText.Snap(ModelEffort.Medium, [ModelEffort.Low, ModelEffort.High]));
        Assert.AreEqual(ModelEffort.Medium, ModelEffortText.Snap(ModelEffort.Medium, All));
    }

    [TestMethod]
    public void ThePillAndReviewShowTheChoice() => Run(picker =>
    {
        Assert.AreEqual("Claude Opus", picker.PillNameForTest);
        StringAssert.Contains(picker.PillAutomationForTest, "medium effort");
        Assert.AreEqual("The flagship.", picker.DescriptionForTest);
        Assert.AreEqual("1M tokens", picker.CellForTest(0));
        Assert.AreEqual("$4.00", picker.CellForTest(1));
        Assert.AreEqual("$20.00", picker.CellForTest(2));
        Assert.AreEqual("Medium", picker.CellForTest(3));
    });

    [TestMethod]
    public void TheMenuListsFeaturedModelsThenEffortAndMore() => Run(picker =>
    {
        picker.IsOpen = true;
        Sta.Pump();
        Assert.IsTrue(picker.PopupOpenForTest);
        CollectionAssert.AreEqual(new[] { "opus", "sonnet", "gpt", "Effort", "More models" }, picker.RowLabelsForTest);
    });

    [TestMethod]
    public void AModelWithoutEffortsDropsTheRowAndTheValue() => Run(picker =>
    {
        picker.Value = "gpt";
        picker.IsOpen = true;
        Sta.Pump();
        CollectionAssert.DoesNotContain(picker.RowLabelsForTest, "Effort");
        Assert.AreEqual("–", picker.CellForTest(3));
    });

    [TestMethod]
    public void PickingAModelClosesTheMenuAndSnapsTheEffort() => Run(picker =>
    {
        PickerModel? chosen = null;
        int efforts = 0;
        picker.ModelChosen += (_, e) => chosen = e.Model;
        picker.EffortChanged += (_, _) => efforts++;
        picker.Effort = ModelEffort.Max;
        Sta.Pump();
        picker.IsOpen = true;
        Sta.Pump();
        picker.RowForTest("sonnet").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Sta.Pump();
        Assert.AreEqual("sonnet", chosen!.Id);
        Assert.IsFalse(picker.IsOpen);
        Assert.AreEqual(ModelEffort.High, picker.Effort);
        Assert.AreEqual(1, efforts);
        Assert.AreEqual("High", picker.CellForTest(3));
        StringAssert.Contains(picker.ConfirmForTest, "Claude Sonnet");
    });

    [TestMethod]
    public void TheSubmenusListEffortsAndOtherModels() => Run(picker =>
    {
        picker.IsOpen = true;
        Sta.Pump();
        picker.OpenSubForTest2("Effort");
        Sta.Pump();
        CollectionAssert.AreEqual(new[] { "Low", "Medium", "High", "Extra high", "Max" }, picker.SubLabelsForTest);
        picker.SubRowForTest("High").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Sta.Pump();
        Assert.AreEqual(ModelEffort.High, picker.Effort);
        picker.IsOpen = true;
        Sta.Pump();
        picker.OpenSubForTest2("More");
        Sta.Pump();
        CollectionAssert.AreEqual(new[] { "haiku", "mini" }, picker.SubLabelsForTest);
    });

    [TestMethod]
    public void EscapeClosesAndNarrowWindowsUsePages() => Run(picker =>
    {
        picker.IsOpen = true;
        Sta.Pump();
        Assert.IsTrue(picker.PagesForTest);
        picker.OpenSubForTest2("Effort");
        Sta.Pump();
        Assert.AreEqual("Effort", picker.OpenSubForTest);
        picker.OpenSubForTest2("None");
        Sta.Pump();
        CollectionAssert.Contains(picker.RowLabelsForTest, "Effort");
        Assert.IsTrue(picker.KeyHandledForTest(Key.Escape));
        Assert.IsFalse(picker.IsOpen);
    }, 480);

    [TestMethod]
    public void TheMenuOpensBelowOrAboveThePillWithAGap()
    {
        foreach (bool up in new[] { false, true })
        {
            Sta.Run(() =>
            {
                var picker = new ModelPicker { Models = Models, Value = "opus", OpensUpward = up, ShowSummary = false, VerticalAlignment = up ? VerticalAlignment.Bottom : VerticalAlignment.Top };
                ArcMotion.SetLevel(picker, ArcMotionLevel.Off);
                var window = new Window { Content = picker, Left = 0, Top = up ? 500 : 0, Width = 700, Height = up ? 200 : 100, ShowActivated = false, ShowInTaskbar = false };
                try
                {
                    window.Show();
                    Sta.Pump();
                    picker.IsOpen = true;
                    Sta.Pump();
                    var menu = picker.MenuForTest;
                    var pill = picker.PillForTest!;
                    double gap = up ? pill.PointToScreen(new Point()).Y - menu.PointToScreen(new Point(0, menu.ActualHeight)).Y : menu.PointToScreen(new Point()).Y - pill.PointToScreen(new Point(0, pill.ActualHeight)).Y;
                    Assert.AreEqual(8, gap, 1.5);
                }
                finally { picker.IsOpen = false; window.Close(); }
            });
        }
    }

    [TestMethod]
    public void CompactDropsTheReviewAndFoldsWithTheToolbar() => Run(picker =>
    {
        Assert.AreEqual("Opus", picker.PillNameForTest);
        Assert.IsFalse(picker.SummaryShownForTest);
        picker.FoldLevel = 1;
        Sta.Pump();
        Assert.AreEqual(1, picker.FoldLevel);
    }, 700, picker => { picker.IsCompact = true; picker.ShowSummary = false; });
}
