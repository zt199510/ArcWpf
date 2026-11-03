using System.Windows;
using ArcWpf.Controls;
namespace ArcWpf.Tests;

[TestClass] public sealed class SignaturePadTests
{
    // A stroke drawn on the live Arc page (2026-11-03) and the path Arc's own code produced for it.
    private static readonly (double X, double Y)[] ArcPoints = [(59.8125,130.265625),(69.8125,118.265625),(78.8125,106.265625),(88.8125,96.265625),(97.8125,88.265625),(107.8125,83.265625),(116.8125,79.265625),(126.8125,79.265625),(135.8125,80.265625),(145.8125,85.265625),(154.8125,91.265625),(164.8125,99.265625),(173.8125,109.265625),(183.8125,119.265625),(192.8125,130.265625),(202.8125,141.265625),(211.8125,150.265625),(221.8125,159.265625),(230.8125,165.265625),(240.8125,170.265625),(249.8125,173.265625),(259.8125,173.265625),(268.8125,171.265625),(278.8125,168.265625),(287.8125,162.265625),(297.8125,155.265625),(306.8125,147.265625),(316.8125,138.265625),(325.8125,129.265625),(335.8125,120.265625),(344.8125,112.265625),(354.8125,106.265625),(363.8125,100.265625),(373.8125,96.265625),(382.8125,94.265625),(392.8125,94.265625),(401.8125,96.265625),(411.8125,99.265625),(420.8125,104.265625),(430.8125,109.265625),(439.8125,116.265625)];
    private const double ArcBoxWidth = 521.625, ArcBoxHeight = 226.03125;
    private const string ArcPath = "M68.28 149.41 Q68.28 149.41 70.99 144.91 73.69 140.41 78.25 134.82 82.81 129.24 88.13 123.56 93.45 117.87 98.76 112.8 104.07 107.74 109.69 103.93 115.32 100.13 120.77 97.19 126.22 94.25 131.94 93.01 137.65 91.77 143.14 91.61 148.62 91.45 154.34 93.09 160.05 94.72 165.47 97.44 170.89 100.16 176.53 104 182.17 107.84 187.55 112.8 192.93 117.76 198.53 123.19 204.13 128.61 209.48 134.57 214.84 140.52 220.41 146.68 225.99 152.84 231.32 158.42 236.65 163.99 242.21 169.32 247.76 174.65 253.06 178.86 258.36 183.08 263.89 186.5 269.41 189.92 274.67 192.33 279.92 194.75 285.38 195.75 290.84 196.76 296.09 196.52 301.33 196.29 306.81 195.21 312.3 194.14 317.56 191.71 322.82 189.28 328.35 185.94 333.88 182.6 339.19 178.54 344.5 174.48 350.06 169.78 355.62 165.08 360.96 160.1 366.3 155.13 371.89 150.03 377.47 144.93 382.84 140.1 388.21 135.27 393.82 131.23 399.43 127.19 404.81 123.48 410.19 119.77 415.83 116.85 421.47 113.94 426.91 112.03 432.34 110.12 438.02 109.32 443.71 108.52 449.15 108.86 454.58 109.19 460.25 110.35 465.91 111.52 471.31 113.68 476.72 115.85 482.33 118.46 487.94 121.08 497.1 127.12 506.26 133.17 506.32 133.23 506.39 133.28 506.44 133.36 506.49 133.43 506.52 133.51 506.55 133.59 506.56 133.68 506.57 133.77 506.55 133.85 506.54 133.94 506.5 134.02 506.46 134.1 506.4 134.17 506.35 134.23 506.28 134.28 506.2 134.33 506.12 134.36 506.04 134.39 505.95 134.4 505.86 134.41 505.78 134.39 505.69 134.38 505.61 134.34 505.53 134.3 505.53 134.3 505.53 134.3 496.25 128.58 486.98 122.85 481.43 120.33 475.88 117.8 470.59 115.67 465.3 113.54 459.8 112.42 454.3 111.3 449.05 110.97 443.79 110.63 438.31 111.41 432.84 112.18 427.58 114.03 422.32 115.88 416.8 118.74 411.28 121.59 405.97 125.26 400.65 128.93 395.1 132.93 389.55 136.93 384.22 141.72 378.9 146.51 373.32 151.6 367.75 156.69 362.39 161.68 357.04 166.67 351.44 171.41 345.84 176.14 340.45 180.25 335.07 184.36 329.44 187.76 323.81 191.16 318.38 193.65 312.95 196.15 307.27 197.27 301.59 198.4 296.14 198.63 290.69 198.87 284.99 197.82 279.29 196.76 273.85 194.28 268.41 191.79 262.77 188.31 257.14 184.82 251.75 180.54 246.36 176.26 240.75 170.88 235.14 165.51 229.78 159.9 224.42 154.3 218.83 148.13 213.24 141.96 207.91 136.04 202.57 130.11 197 124.73 191.44 119.34 186.13 114.45 180.81 109.55 175.29 105.81 169.77 102.07 164.5 99.45 159.23 96.83 153.79 95.28 148.34 93.73 143.15 93.94 137.95 94.15 132.53 95.37 127.11 96.6 121.92 99.51 116.74 102.43 111.34 106.22 105.95 110.02 100.89 115.09 95.84 120.17 90.79 125.93 85.74 131.7 81.5 137.48 77.25 143.26 73.28 146.77 69.32 150.27 69.25 150.33 69.19 150.39 69.11 150.43 69.03 150.47 68.95 150.49 68.86 150.51 68.77 150.51 68.68 150.5 68.6 150.48 68.52 150.45 68.44 150.41 68.37 150.36 68.31 150.29 68.25 150.23 68.21 150.15 68.17 150.07 68.15 149.99 68.13 149.9 68.13 149.82 68.14 149.73 68.16 149.64 68.19 149.56 68.24 149.49 68.28 149.41 68.28 149.41 Z";

    private static List<Point> PadPoints() => ArcPoints.Select(p => new Point(p.X / ArcBoxWidth * SignatureInk.PadWidth, p.Y / ArcBoxHeight * SignatureInk.PadHeight)).ToList();

    private static void WithPad(Action<SignaturePad> action) => Sta.Run(() =>
    {
        var pad = new SignaturePad { Width = 521.625 };
        var window = new Window { Content = pad, Width = 600, Height = 500, ShowActivated = false, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(pad); }
        finally { window.Close(); }
    });

    [TestMethod] public void OutlineHasNoPointsForNoInput() => Assert.AreEqual(0, SignatureInk.Outline([], new SignatureOutlineOptions(5.6)).Count);

    [TestMethod] public void InkColoursExportAsSourceHex()
    {
        Assert.AreEqual("#1d3fae", SignatureInk.ExportColor(SignatureInkColor.Blue));
        Assert.AreEqual("#5b35c8", SignatureInk.ExportColor(SignatureInkColor.Violet));
    }

    [TestMethod] public void WidthsMatchSource()
    {
        Assert.AreEqual(3.4, SignatureInk.SizeOf(SignatureInkWidth.Fine));
        Assert.AreEqual(5.6, SignatureInk.SizeOf(SignatureInkWidth.Medium));
        Assert.AreEqual(8.4, SignatureInk.SizeOf(SignatureInkWidth.Bold));
    }

    [TestMethod] public void DrawnStrokePathEqualsArcOutput() => WithPad(pad =>
    {
        pad.AddStrokeForTest(PadPoints());
        Sta.Pump();
        Assert.AreEqual(ArcPath, pad.StrokePathForTest(0));
    });

    [TestMethod] public void HintLeavesAfterTheFirstStroke() => WithPad(pad =>
    {
        Assert.IsTrue(pad.IsHintShown);
        pad.AddStrokeForTest(PadPoints());
        Sta.Pump();
        Assert.AreEqual(1, pad.InkPaths.Count());
    });

    [TestMethod] public void UndoAndRedoWalkStrokes() => WithPad(pad =>
    {
        pad.AddStrokeForTest(PadPoints());
        pad.AddStrokeForTest(PadPoints());
        Sta.Pump();
        Assert.AreEqual(2, pad.InkPaths.Count());
        pad.Undo();
        Sta.Pump();
        Assert.AreEqual(1, pad.InkPaths.Count());
        pad.Redo();
        Sta.Pump();
        Assert.AreEqual(2, pad.InkPaths.Count());
    });

    [TestMethod] public void ClearWipesThenEmpties() => WithPad(pad =>
    {
        pad.AddStrokeForTest(PadPoints());
        Sta.Pump();
        pad.Clear();
        pad.FinishWipeForTest();
        Sta.Pump();
        Assert.IsFalse(pad.IsWiping);
        Assert.AreEqual(0, pad.InkPaths.Count());
    });

    [TestMethod] public void SvgExportCarriesThePaths() => WithPad(pad =>
    {
        pad.AddStrokeForTest(PadPoints());
        Sta.Pump();
        string svg = SignatureInk.ToSvg(pad.StrokesForTest);
        StringAssert.Contains(svg, "<svg");
        StringAssert.Contains(svg, "<path");
    });
}
