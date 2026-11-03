using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Tests;

[TestClass]
public sealed class CarouselTests
{
    [TestMethod]
    public void IndexIsClampedAndNavigationMovesWithinItems() => Sta.Run(() =>
    {
        var carousel = new Carousel { SelectedIndex = 10 };
        carousel.Items.Add("one"); carousel.Items.Add("two"); carousel.Items.Add("three");
        carousel.SelectedIndex = 10;
        carousel.Next();
        Assert.AreEqual(2, carousel.SelectedIndex);
        carousel.Next(); Assert.AreEqual(2, carousel.SelectedIndex);
        carousel.Previous(); Assert.AreEqual(1, carousel.SelectedIndex);
    });

    [TestMethod]
    public void KeyboardMovesSlidesAndRaisesSelectionEvent() => Sta.Run(() =>
    {
        var carousel = new Carousel(); carousel.Items.Add("one"); carousel.Items.Add("two");
        int changes = 0; carousel.SelectedIndexChanged += (_, _) => changes++;
        var window = new Window { Content = carousel, Width = 300, Height = 180, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); carousel.Focus();
            var args = new KeyEventArgs(Keyboard.PrimaryDevice, System.Windows.PresentationSource.FromVisual(carousel)!, 0, Key.Right) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            carousel.RaiseEvent(args);
            Assert.AreEqual(1, carousel.SelectedIndex);
            Assert.AreEqual(1, changes);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void WindowTemplateRendersTrackAndControls() => Sta.Run(() =>
    {
        var carousel = new Carousel { Width = 520, Height = 260, SlideWidth = 220 };
        carousel.Items.Add(new Border { Width = 220, Height = 150 }); carousel.Items.Add(new Border { Width = 220, Height = 150 });
        var window = new Window { Content = carousel, Width = 620, Height = 340, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); carousel.UpdateLayout();
            Assert.IsNotNull(carousel.Template.FindName("PART_Viewport", carousel));
            Assert.IsNotNull(carousel.Template.FindName("PART_Track", carousel));
            Assert.IsGreaterThan(0d, carousel.ActualWidth);
        }
        finally { window.Close(); }
    });
    [TestMethod]
    public void WindowRendersActiveIndicatorPill() => Sta.Run(() =>
    {
        var carousel = new Carousel { Width = 420, Height = 220 };
        carousel.Items.Add("one"); carousel.Items.Add("two"); carousel.Items.Add("three");
        carousel.SelectedIndex = 1;
        var window = new Window { Content = carousel, Width = 520, Height = 300, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); carousel.UpdateLayout();
            var indicators = (CarouselIndicators)carousel.Template.FindName("PART_Indicators", carousel)!;
            Assert.AreEqual(3, indicators.Items.Count);
            Assert.IsTrue(((CarouselIndicator)indicators.ItemContainerGenerator.ContainerFromIndex(1)!).IsActive);
            Assert.IsFalse(((CarouselIndicator)indicators.ItemContainerGenerator.ContainerFromIndex(0)!).IsActive);
        }
        finally { window.Close(); }
    });
}
