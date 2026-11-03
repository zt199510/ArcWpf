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
    public void TrackPlacesSlidesAroundTheCenterAndDimsNeighbours() => Sta.Run(() =>
    {
        var carousel = new Carousel { Width = 522, SlideWidth = 256 };
        for (int i = 0; i < 3; i++) { carousel.Items.Add(new Border { Height = 300 }); }
        carousel.SelectedIndex = 1;
        var window = new Window { Content = carousel, Width = 620, Height = 500, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); carousel.UpdateLayout();
            Assert.AreEqual(1d, carousel.Position, .001);
            var track = (CarouselTrack)carousel.Template.FindName("PART_Track", carousel)!;
            var active = (FrameworkElement)track.Children[1];
            Assert.AreEqual(1d, active.Opacity, .001);
            Assert.AreEqual(.5, track.Children[0].Opacity, .001);
            var center = active.TranslatePoint(new Point(active.ActualWidth / 2, 0), carousel).X;
            Assert.AreEqual(261d, center, .5, "the active slide sits in the middle of the viewport");
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void RotationDrainsThePillAndAdvancesAndPausingRefills() => Sta.Run(() =>
    {
        var carousel = new Carousel { Width = 400, Interval = TimeSpan.FromSeconds(2), IsPlaying = true };
        for (int i = 0; i < 2; i++) { carousel.Items.Add(new Border { Height = 100 }); }
        var window = new Window { Content = carousel, Width = 500, Height = 300, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); carousel.UpdateLayout();
            carousel.AdvanceClock(1);
            Assert.AreEqual(.5, carousel.Drain(0), .05);
            carousel.IsPlaying = false;
            for (int i = 0; i < 10; i++) { carousel.AdvanceClock(.05); }
            Assert.AreEqual(0d, carousel.Drain(0), .001);
            Assert.AreEqual(0, carousel.SelectedIndex);
            carousel.IsPlaying = true;
            carousel.AdvanceClock(2.1);
            Assert.AreEqual(1, carousel.SelectedIndex);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void ReducedMotionHasNoRotationButKeepsTheControls() => Sta.Run(() =>
    {
        var carousel = new Carousel { Width = 400, Interval = TimeSpan.FromSeconds(1), IsPlaying = true };
        ArcMotion.SetLevel(carousel, ArcMotionLevel.Off);
        for (int i = 0; i < 2; i++) { carousel.Items.Add(new Border { Height = 100 }); }
        var window = new Window { Content = carousel, Width = 500, Height = 300, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); carousel.UpdateLayout();
            carousel.AdvanceClock(5);
            Assert.AreEqual(0, carousel.SelectedIndex);
            carousel.Next();
            Assert.AreEqual(1d, carousel.Position, .001, "reduced motion jumps to the slide at once");
        }
        finally { window.Close(); }
    });
}
