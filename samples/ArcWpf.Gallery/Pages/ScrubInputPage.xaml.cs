using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class ScrubInputPage : UserControl
{
    private double _radius = 18;
    private string _cornerUnit = "px";

    public ScrubInputPage()
    {
        InitializeComponent();
        Corner.Units = ["px", "%"];
        Corner.Unit = "px";
        // Dot grid behind the frame: one dot every 20px, as a mask so it follows the theme.
        Dots.OpacityMask = new DrawingBrush(new GeometryDrawing(Brushes.Black, null, new EllipseGeometry(new Point(10, 10), 1, 1))) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 20, 20), ViewportUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, 20, 20), ViewboxUnits = BrushMappingMode.Absolute };
        Apply();
    }

    private void OnValue(object? sender, ScrubInputChangedEventArgs e) => Apply();

    private void OnCorner(object? sender, ScrubInputChangedEventArgs e)
    {
        _radius = _cornerUnit == "%" ? e.Value / 100 * Math.Min(W.Value, H.Value) : e.Value;
        Apply();
    }

    // The same radius, stated either way: a percent of the frame's short side is capped at 50, where it is already fully round.
    private void OnCornerUnit(object? sender, string unit)
    {
        _cornerUnit = unit;
        ShowCorner();
    }

    private void ShowCorner()
    {
        bool percent = _cornerUnit == "%";
        Corner.Maximum = percent ? 50 : 120;
        Corner.Value = Math.Round(percent ? _radius / Math.Min(W.Value, H.Value) * 100 : _radius, MidpointRounding.AwayFromZero);
    }

    private void Apply()
    {
        if (Move is null) { return; }
        (Move.X, Move.Y, Spin.Angle) = (X.Value, Y.Value, Rotation.Value);
        (Frame.Width, Frame.Height) = (W.Value, H.Value);
        Frame.Opacity = Fade.Value / 100;
        double radius = Math.Min(_radius, Math.Min(W.Value, H.Value) / 2);
        Picture.Clip = new RectangleGeometry(new Rect(0, 0, W.Value, H.Value), radius, radius);
        Size.Text = $"{W.Value:0} × {H.Value:0}";
        Reset.IsEnabled = X.Value != 0 || Y.Value != 0 || W.Value != 176 || H.Value != 132 || Rotation.Value != 0 || _radius != 18 || Fade.Value != 100;
    }

    private void OnReset(object sender, RoutedEventArgs e)
    {
        (X.Value, Y.Value, W.Value, H.Value, Rotation.Value, Fade.Value) = (0, 0, 176, 132, 0, 100);
        _radius = 18;
        ShowCorner();
        Apply();
    }
}
