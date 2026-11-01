using ArcWpf.Controls;
using System.Windows;
namespace ArcWpf.Tests;
[TestClass] public sealed class CardTests
{
    [TestMethod] public void CardContentPropertiesAreBindable() => Sta.Run(() => { var card = new Card { Title = "Title", Description = "Description", Content = "Body" }; Assert.AreEqual("Title", card.Title); Assert.AreEqual("Body", card.Content); });
    [TestMethod] public void MetricValuesAreBindable() => Sta.Run(() => { var card = new MetricCard { Value = "42", Trend = "+5%" }; Assert.AreEqual("42", card.Value); Assert.AreEqual("+5%", card.Trend); });
    [TestMethod] public void MetricTrendDefaultsPositive() => Sta.Run(() => Assert.IsTrue(new MetricCard().TrendIsPositive));
    [TestMethod] public void NegativeTrendUpdatesVisibleBrush() => Sta.Run(() =>
    {
        var card = new MetricCard { Title = "Revenue", Value = "42", Trend = "-5%", TrendIsPositive = false };
        var window = new System.Windows.Window { Content = card, Width = 300, Height = 180, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            var trend = (System.Windows.Controls.TextBlock)card.Template.FindName("TrendText", card)!;
            Assert.AreEqual(card.TryFindResource("ArcWpf.Brush.Danger"), trend.Foreground);
            Assert.AreEqual("-5%", trend.Text);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void MetricSourceLayoutFormatsNumberAndWrapsChangeOnNarrowWidth() => Sta.Run(() =>
    {
        var card = new MetricCard { Width = 300, Label = "Uptime", NumericValue = 99.9, Suffix = "%", Context = "Last 30 days", Change = "-0.2%" };
        var window = new System.Windows.Window { Content = card, Width = 380, Height = 280, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); card.UpdateLayout();
            Assert.AreEqual("Uptime", card.DisplayLabel);
            Assert.AreEqual("99.9%", card.DisplayValue);
            Assert.IsTrue(card.IsNegativeChange);
            Assert.IsTrue(card.IsCompact);
            var surface = (System.Windows.Controls.Border)card.Template.FindName("PART_Surface", card)!;
            var top = (System.Windows.Controls.Grid)card.Template.FindName("PART_Top", card)!;
            var chip = (System.Windows.Controls.Border)card.Template.FindName("ChangeChip", card)!;
            Assert.AreEqual(new Thickness(16), surface.Padding);
            Assert.AreEqual(1, System.Windows.Controls.Grid.GetRow(chip));
            Assert.AreEqual(card.TryFindResource("ArcWpf.Brush.BadgeDangerBackground"), chip.Background);
            Assert.IsGreaterThan(20d, top.ActualHeight);
            card.Change = "+0.2%";
            Assert.IsFalse(card.IsNegativeChange);
            Assert.IsTrue(card.IsPositiveChange);
            Assert.AreEqual(card.TryFindResource("ArcWpf.Brush.BadgeSuccessBackground"), chip.Background);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void MetricValueMotionSettlesAndOffStopsIt() => Sta.Run(() =>
    {
        var card = new MetricCard { Width = 420, Label = "Revenue", NumericValue = 24, Suffix = "k" };
        ArcMotion.SetLevel(card, ArcMotionLevel.Full);
        var window = new System.Windows.Window { Content = card, Width = 500, Height = 260, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            var value = (System.Windows.Controls.TextBlock)card.Template.FindName("PART_Value", card)!;
            var move = (System.Windows.Media.TranslateTransform)value.RenderTransform;
            card.NumericValue = 30;
            Assert.IsTrue(value.HasAnimatedProperties);
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            Assert.AreEqual("30k", value.Text);
            Assert.AreEqual(0d, move.Y, .01);
            Assert.AreEqual(1d, value.Opacity, .01);
            Assert.IsFalse(value.HasAnimatedProperties);
            card.NumericValue = 29;
            var sampleFrame = new System.Windows.Threading.DispatcherFrame();
            var sampleTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            sampleTimer.Tick += (_, _) => { sampleTimer.Stop(); sampleFrame.Continue = false; };
            sampleTimer.Start(); System.Windows.Threading.Dispatcher.PushFrame(sampleFrame);
            Assert.IsTrue(move.Y < 0, $"Downward value should enter from above, Y={move.Y}");
            ArcMotion.SetLevel(card, ArcMotionLevel.Off);
            Assert.AreEqual(0d, move.Y, .001);
            Assert.IsFalse(value.HasAnimatedProperties);
        }
        finally { window.Close(); }
    });
}
