using System.Windows;
using System.Windows.Media;

namespace ArcWpf.Controls;

/// <summary>A stroked 24-box icon. Path data is from Lucide (ISC), see THIRD-PARTY-NOTICES.md.</summary>
public sealed class ArcDataGridIcon : FrameworkElement
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(string), typeof(ArcDataGridIcon), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(double), typeof(ArcDataGridIcon), new FrameworkPropertyMetadata(16d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(nameof(Stroke), typeof(Brush), typeof(ArcDataGridIcon), new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(nameof(Fill), typeof(Brush), typeof(ArcDataGridIcon), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(ArcDataGridIcon), new FrameworkPropertyMetadata(1.75, FrameworkPropertyMetadataOptions.AffectsRender));

    public string Icon { get => (string)GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public double Size { get => (double)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public Brush? Fill { get => (Brush?)GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public double StrokeThickness { get => (double)GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }

    public ArcDataGridIcon() => IsHitTestVisible = false;

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    protected override void OnRender(DrawingContext drawingContext) => Draw(drawingContext, Icon, new Rect(0, 0, Size, Size), Stroke, StrokeThickness, 0, Fill);

    private static readonly Dictionary<string, string> Paths = new()
    {
        ["arrow-down-wide-narrow"] = "M3 16l4 4 4-4M7 20V4M11 4h10M11 8h7M11 12h4",
        ["arrow-up-narrow-wide"] = "M3 8l4-4 4 4M7 4v16M11 12h4M11 16h7M11 20h10",
        ["arrow-up"] = "M5 12l7-7 7 7M12 19V5",
        ["check"] = "M20 6L9 17l-5-5",
        ["chevron-down"] = "M6 9l6 6 6-6",
        ["chevron-right"] = "M9 18l6-6-6-6",
        ["chevrons-down-up"] = "M7 20l5-5 5 5M7 4l5 5 5-5",
        ["chevrons-up-down"] = "M7 15l5 5 5-5M7 9l5-5 5 5",
        ["columns-3"] = "M5 3H19A2 2 0 0 1 21 5V19A2 2 0 0 1 19 21H5A2 2 0 0 1 3 19V5A2 2 0 0 1 5 3ZM9 3v18M15 3v18",
        ["copy"] = "M10 8H20A2 2 0 0 1 22 10V20A2 2 0 0 1 20 22H10A2 2 0 0 1 8 20V10A2 2 0 0 1 10 8ZM4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2",
        ["download"] = "M21 15v4a2 2 0 0 1 -2 2H5a2 2 0 0 1 -2 -2v-4M7 10L12 15 17 10M12 15L12 3",
        ["eye-off"] = "M10.733 5.076a10.744 10.744 0 0 1 11.205 6.575 1 1 0 0 1 0 .696 10.747 10.747 0 0 1 -1.444 2.49M14.084 14.158a3 3 0 0 1 -4.242 -4.242M17.479 17.499a10.75 10.75 0 0 1 -15.417 -5.151 1 1 0 0 1 0 -.696 10.75 10.75 0 0 1 4.446 -5.143M2 2l20 20",
        ["file-spreadsheet"] = "M6 22a2 2 0 0 1 -2 -2V4a2 2 0 0 1 2 -2h8a2.4 2.4 0 0 1 1.704 .706l3.588 3.588A2.4 2.4 0 0 1 20 8v12a2 2 0 0 1 -2 2zM14 2v5a1 1 0 0 0 1 1h5M8 13h2M14 13h2M8 17h2M14 17h2",
        ["file-text"] = "M6 22a2 2 0 0 1 -2 -2V4a2 2 0 0 1 2 -2h8a2.4 2.4 0 0 1 1.704 .706l3.588 3.588A2.4 2.4 0 0 1 20 8v12a2 2 0 0 1 -2 2zM14 2v5a1 1 0 0 0 1 1h5M10 9H8M16 13H8M16 17H8",
        ["funnel-x"] = "M12.531 3H3a1 1 0 0 0 -.742 1.67l7.225 7.989A2 2 0 0 1 10 14v6a1 1 0 0 0 .553 .895l2 1A1 1 0 0 0 14 21v-7a2 2 0 0 1 .517 -1.341l.427-.473M16.5 3.5l5 5M21.5 3.5l-5 5",
        ["group"] = "M3 7V5c0-1.1.9-2 2-2h2M17 3h2c1.1 0 2 .9 2 2v2M21 17v2c0 1.1-.9 2-2 2h-2M7 21H5c-1.1 0-2-.9-2-2v-2M8 7H13A1 1 0 0 1 14 8V11A1 1 0 0 1 13 12H8A1 1 0 0 1 7 11V8A1 1 0 0 1 8 7ZM11 12H16A1 1 0 0 1 17 13V16A1 1 0 0 1 16 17H11A1 1 0 0 1 10 16V13A1 1 0 0 1 11 12Z",
        ["list-filter"] = "M2 5h20M6 12h12M9 19h6",
        ["minus"] = "M5 12h14",
        ["pin-off"] = "M12 17v5M15 9.34V7a1 1 0 0 1 1 -1 2 2 0 0 0 0 -4H7.89M2 2l20 20M9 9v1.76a2 2 0 0 1 -1.11 1.79l-1.78.9A2 2 0 0 0 5 15.24V16a1 1 0 0 0 1 1h11",
        ["pin"] = "M12 17v5M9 10.76a2 2 0 0 1 -1.11 1.79l-1.78.9A2 2 0 0 0 5 15.24V16a1 1 0 0 0 1 1h12a1 1 0 0 0 1 -1v-.76a2 2 0 0 0 -1.11 -1.79l-1.78-.9A2 2 0 0 1 15 10.76V7a1 1 0 0 1 1 -1 2 2 0 0 0 0 -4H8a2 2 0 0 0 0 4 1 1 0 0 1 1 1z",
        ["redo-2"] = "M15 14l5-5-5-5M20 9H9.5A5.5 5.5 0 0 0 4 14.5A5.5 5.5 0 0 0 9.5 20H13",
        ["rotate-ccw"] = "M3 12a9 9 0 1 0 9 -9 9.75 9.75 0 0 0 -6.74 2.74L3 8M3 3v5h5",
        ["rows-2"] = "M5 3H19A2 2 0 0 1 21 5V19A2 2 0 0 1 19 21H5A2 2 0 0 1 3 19V5A2 2 0 0 1 5 3ZM3 12h18",
        ["rows-3"] = "M5 3H19A2 2 0 0 1 21 5V19A2 2 0 0 1 19 21H5A2 2 0 0 1 3 19V5A2 2 0 0 1 5 3ZM21 9H3M21 15H3",
        ["rows-4"] = "M5 3H19A2 2 0 0 1 21 5V19A2 2 0 0 1 19 21H5A2 2 0 0 1 3 19V5A2 2 0 0 1 5 3ZM21 7.5H3M21 12H3M21 16.5H3",
        ["search"] = "M3 11A8 8 0 1 1 19 11A8 8 0 1 1 3 11ZM21 21l-4.3-4.3",
        ["trash"] = "M10 11v6M14 11v6M19 6v14a2 2 0 0 1 -2 2H7a2 2 0 0 1 -2 -2V6M3 6h18M8 6V4a2 2 0 0 1 2 -2h4a2 2 0 0 1 2 2v2",
        ["undo-2"] = "M9 14L4 9l5-5M4 9h10.5a5.5 5.5 0 0 1 5.5 5.5a5.5 5.5 0 0 1 -5.5 5.5H11",
        ["x"] = "M18 6L6 18M6 6l12 12",
        ["chevron-left"] = "m15 18-6-6 6-6",
        ["play"] = "M5 5a2 2 0 0 1 3.008 -1.728l11.997 6.998a2 2 0 0 1 .003 3.458l-12 7A2 2 0 0 1 5 19z",
        ["pause"] = "M15 3H18A1 1 0 0 1 19 4V20A1 1 0 0 1 18 21H15A1 1 0 0 1 14 20V4A1 1 0 0 1 15 3Z|M6 3H9A1 1 0 0 1 10 4V20A1 1 0 0 1 9 21H6A1 1 0 0 1 5 20V4A1 1 0 0 1 6 3Z",
        ["cookie"] = "M11 17h.01|M11.496 2c.324-.016.558.292.529.615a4 4 0 0 0 4.235 4.368 .713 .713 0 0 1 .758 .757 4 4 0 0 0 4.366 4.237c.323-.03.63.204.614.527a10 10 0 0 1 -2.915 6.566A1 1 0 1 1 4.93 4.918 10 10 0 0 1 11.496 2|M12 12h.01|M16 16h.01|M16 3h.01|M21 4h.01|M21 8h.01|M7 14h.01|M9 8h.01",
        ["package"] = "M11 21.73a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16V8a2 2 0 0 0 -1 -1.73l-7-4a2 2 0 0 0 -2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73z|M12 22V12|m7.5 4.27 9 5.15",
        ["truck"] = "M14 18V6a2 2 0 0 0 -2 -2H4a2 2 0 0 0 -2 2v11a1 1 0 0 0 1 1h2|M15 18H9|M19 18h2a1 1 0 0 0 1 -1v-3.65a1 1 0 0 0 -.22 -.624l-3.48-4.35A1 1 0 0 0 17.52 8H14|M15 18.0A2 2 0 1 1 19 18.0A2 2 0 1 1 15 18.0Z|M5 18.0A2 2 0 1 1 9 18.0A2 2 0 1 1 5 18.0Z",
        ["zap"] = "M15.914 4a1.5 1.5 0 0 0 -2.474 -1.561l-9 9A1.5 1.5 0 0 0 5.5 14h4.002a.5 .5 0 0 1 .471 .666L8.086 20a1.5 1.5 0 0 0 2.475 1.56l9-9A1.5 1.5 0 0 0 18.5 10h-3.997a.5 .5 0 0 1 -.472 -.667z",
        ["store"] = "M15 21v-5a1 1 0 0 0 -1 -1h-4a1 1 0 0 0 -1 1v5|M17.774 10.31a1.12 1.12 0 0 0 -1.549 0 2.5 2.5 0 0 1 -3.451 0 1.12 1.12 0 0 0 -1.548 0 2.5 2.5 0 0 1 -3.452 0 1.12 1.12 0 0 0 -1.549 0 2.5 2.5 0 0 1 -3.77 -3.248l2.889-4.184A2 2 0 0 1 7 2h10a2 2 0 0 1 1.653 .873l2.895 4.192a2.5 2.5 0 0 1 -3.774 3.244|M4 10.95V19a2 2 0 0 0 2 2h12a2 2 0 0 0 2 -2v-8.05",
        ["party-popper"] = "M5.8 11.3 2 22l10.7-3.79|M4 3h.01|M22 8h.01|M15 2h.01|M22 20h.01|m22 2-2.24.75a2.9 2.9 0 0 0 -1.96 3.12c.1.86-.57 1.63-1.45 1.63h-.38c-.86 0-1.6.6-1.76 1.44L14 10|m22 13-.82-.33c-.86-.34-1.82.2-1.98 1.11c-.11.7-.72 1.22-1.43 1.22H17|m11 2 .33.82c.34.86-.2 1.82-1.11 1.98C9.52 4.9 9 5.52 9 6.23V7|M11 13c1.93 1.93 2.83 4.17 2 5-.83.83-3.07-.07-5-2-1.93-1.93-2.83-4.17-2-5 .83-.83 3.07.07 5 2Z",
        ["thumbs-up"] = "M15 5.88 14 10h5.83a2 2 0 0 1 1.92 2.56l-2.33 8A2 2 0 0 1 17.5 22H4a2 2 0 0 1 -2 -2v-8a2 2 0 0 1 2 -2h2.76a2 2 0 0 0 1.79 -1.11L12 2a3.13 3.13 0 0 1 3 3.88Z|M7 10v12",
        ["lightbulb"] = "M15 14c.2-1 .7-1.7 1.5-2.5 1-.9 1.5-2.2 1.5-3.5A6 6 0 0 0 6 8c0 1 .2 2.2 1.5 3.5.7.7 1.3 1.5 1.5 2.5|M9 18h6|M10 22h4",
        ["message-circle-question"] = "M2.992 16.342a2 2 0 0 1 .094 1.167l-1.065 3.29a1 1 0 0 0 1.236 1.168l3.413-.998a2 2 0 0 1 1.099 .092 10 10 0 1 0 -4.777 -4.719|M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3|M12 17h.01",
        ["smile-plus"] = "M13.267 2.08a10 10 0 1 0 8.653 8.653|M15 10V9|M16 5h6|M16.472 15a6 6 0 0 1 -8.943 0|M19 2v6|M9 10V9",
        ["heart"] = "M2 9.5a5.5 5.5 0 0 1 9.591 -3.676 .56 .56 0 0 0 .818 0A5.49 5.49 0 0 1 22 9.5c0 2.29-1.5 4-3 5.5l-5.492 5.313a2 2 0 0 1 -3 .019L5 15c-1.5-1.5-3-3.2-3-5.5",
        ["rocket"] = "M12 15v5s3.03-.55 4-2c1.08-1.62 0-5 0-5|M4.5 16.5c-1.5 1.26-2 5-2 5s3.74-.5 5-2c.71-.84.7-2.13-.09-2.91a2.18 2.18 0 0 0 -2.91 -.09|M9 12a22 22 0 0 1 2 -3.95A12.88 12.88 0 0 1 22 2c0 2.72-.78 7.5-6 11a22.4 22.4 0 0 1 -4 2z|M9 12H4s.55-3.03 2-4c1.62-1.08 5 .05 5 .05",
        ["archive"] = "M3 3H21A1 1 0 0 1 22 4V7A1 1 0 0 1 21 8H3A1 1 0 0 1 2 7V4A1 1 0 0 1 3 3Z|M4 8v11a2 2 0 0 0 2 2h12a2 2 0 0 0 2 -2V8|M10 12h4",
        ["clock-3"] = "M2 12.0A10 10 0 1 1 22 12.0A10 10 0 1 1 2 12.0Z|M12 6v6h4",
        ["ellipsis"] = "M11 12.0A1 1 0 1 1 13 12.0A1 1 0 1 1 11 12.0Z|M18 12.0A1 1 0 1 1 20 12.0A1 1 0 1 1 18 12.0Z|M4 12.0A1 1 0 1 1 6 12.0A1 1 0 1 1 4 12.0Z",
        ["arrow-left"] = "m12 19-7-7 7-7|M19 12H5",
        ["arrow-right"] = "M5 12h14|m12 5 7 7-7 7",
        ["bold"] = "M6 12h9a4 4 0 010 8H7a1 1 0 01-1-1V5a1 1 0 011-1h7a4 4 0 010 8",
        ["chevron-up"] = "m18 15-6-6-6 6",
        ["code"] = "m16 18 6-6-6-6|m8 6-6 6 6 6",
        ["command"] = "M15 6v12a3 3 0 1 0 3-3H6a3 3 0 1 0 3 3V6a3 3 0 1 0-3 3h12a3 3 0 1 0-3-3",
        ["eraser"] = "M21 21H8a2 2 0 0 1-1.42-.587l-3.994-3.999a2 2 0 0 1 0-2.828l10-10a2 2 0 0 1 2.829 0l5.999 6a2 2 0 0 1 0 2.828L12.834 21|m5.082 11.09 8.828 8.828",
        ["heading-1"] = "M4 12h8|M4 18V6|M12 18V6|m17 12 3-2v8",
        ["heading-2"] = "M4 12h8|M4 18V6|M12 18V6|M21 18h-4c0-4 4-3 4-6 0-1.5-2-2.5-4-1",
        ["heading-3"] = "M4 12h8|M4 18V6|M12 18V6|M17.5 10.5c1.7-1 3.5 0 3.5 1.5a2 2 0 0 1-2 2|M17 17.5c2 1.5 4 .3 4-1.5a2 2 0 0 0-2-2",
        ["italic"] = "M19 4L10 4|M14 20L5 20|M15 4L9 20",
        ["link"] = "M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71|M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71",
        ["list-ordered"] = "M11 5h10|M11 12h10|M11 19h10|M4 4h1v5|M4 9h2|M6.5 20H3.4c0-1 2.6-1.925 2.6-3.5a1.5 1.5 0 0 0-2.6-1.02",
        ["list"] = "M3 5h.01|M3 12h.01|M3 19h.01|M8 5h13|M8 12h13|M8 19h13",
        ["palette"] = "M12 22a1 1 0 0 1 0-20 10 9 0 0 1 10 9 5 5 0 0 1-5 5h-2.25a1.75 1.75 0 0 0-1.4 2.8l.3.4a1.75 1.75 0 0 1-1.4 2.8z|M13 6.5A0.5 0.5 0 1 1 14 6.5A0.5 0.5 0 1 1 13 6.5Z|M17 10.5A0.5 0.5 0 1 1 18 10.5A0.5 0.5 0 1 1 17 10.5Z|M6 12.5A0.5 0.5 0 1 1 7 12.5A0.5 0.5 0 1 1 6 12.5Z|M8 7.5A0.5 0.5 0 1 1 9 7.5A0.5 0.5 0 1 1 8 7.5Z",
        ["pencil"] = "M21.174 6.812a1 1 0 0 0-3.986-3.987L3.842 16.174a2 2 0 0 0-.5.83l-1.321 4.352a.5.5 0 0 0 .623.622l4.353-1.32a2 2 0 0 0 .83-.497z|m15 5 4 4",
        ["pilcrow"] = "M13 4v16|M17 4v16|M19 4H9.5a4.5 4.5 0 0 0 0 9H13",
        ["plus"] = "M5 12h14|M12 5v14",
        ["quote"] = "M16 3a2 2 0 0 0-2 2v6a2 2 0 0 0 2 2 1 1 0 0 1 1 1v1a2 2 0 0 1-2 2 1 1 0 0 0-1 1v2a1 1 0 0 0 1 1 6 6 0 0 0 6-6V5a2 2 0 0 0-2-2z|M5 3a2 2 0 0 0-2 2v6a2 2 0 0 0 2 2 1 1 0 0 1 1 1v1a2 2 0 0 1-2 2 1 1 0 0 0-1 1v2a1 1 0 0 0 1 1 6 6 0 0 0 6-6V5a2 2 0 0 0-2-2z",
        ["share-2"] = "M15 5A3 3 0 1 1 21 5A3 3 0 1 1 15 5Z|M3 12A3 3 0 1 1 9 12A3 3 0 1 1 3 12Z|M15 19A3 3 0 1 1 21 19A3 3 0 1 1 15 19Z|M8.59 13.51L15.42 17.49|M15.41 6.51L8.59 10.49",
        ["square-code"] = "m10 9-3 3 3 3|m14 15 3-3-3-3|M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2v-14a2 2 0 0 1 2 -2Z",
        ["square"] = "M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2v-14a2 2 0 0 1 2 -2Z",
        ["strikethrough"] = "M16 4H9a3 3 0 0 0-2.83 4|M14 12a4 4 0 0 1 0 8H6|M4 12L20 12",
        ["sun-moon"] = "M12 2v2|M14.837 16.385a6 6 0 1 1-7.223-7.222c.624-.147.97.66.715 1.248a4 4 0 0 0 5.26 5.259c.589-.255 1.396.09 1.248.715|M16 12a4 4 0 0 0-4-4|m19 5-1.256 1.256|M20 12h2",
        ["unlink"] = "m18.84 12.25 1.72-1.71h-.02a5.004 5.004 0 0 0-.12-7.07 5.006 5.006 0 0 0-6.95 0l-1.72 1.71|m5.17 11.75-1.71 1.71a5.004 5.004 0 0 0 .12 7.07 5.006 5.006 0 0 0 6.95 0l1.71-1.71|M8 2L8 5|M2 8L5 8|M16 19L16 22|M19 16L22 16",
        // 2026-11-03 ConfirmPopover, ChatThread, CommentThread (lucide-static 1.67.0).
        ["arrow-down"] = "M12 5v14|m19 12-7 7-7-7",
        ["paperclip"] = "m16 6-8.414 8.586a2 2 0 0 0 2.829 2.829l8.414-8.586a4 4 0 1 0-5.657-5.657l-8.379 8.551a6 6 0 1 0 8.485 8.485l8.379-8.551",
        ["wifi-off"] = "M12 20h.01|M8.5 16.429a5 5 0 0 1 7 0|M5 12.859a10 10 0 0 1 5.17-2.69|M19 12.859a10 10 0 0 0-2.007-1.523|M2 8.82a15 15 0 0 1 4.177-2.643|M22 8.82a15 15 0 0 0-11.288-3.764|m2 2 20 20",
        ["user-minus"] = "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2|M5 7A4 4 0 1 1 13 7A4 4 0 1 1 5 7Z|M22 11L16 11",
        ["trash-2"] = "M10 11v6|M14 11v6|M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6|M3 6h18|M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2",
        ["rotate-cw"] = "M21 12a9 9 0 1 1-9-9c2.52 0 4.93 1 6.74 2.74L21 8|M21 3v5h-5",
        ["corner-down-right"] = "m15 10 5 5-5 5|M4 4v7a4 4 0 0 0 4 4h12",
        ["circle-alert"] = "M2 12A10 10 0 1 1 22 12A10 10 0 1 1 2 12Z|M12 8L12 12|M12 16L12.01 16",
        ["user-plus"] = "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2|M5 7A4 4 0 1 1 13 7A4 4 0 1 1 5 7Z|M19 8L19 14|M22 11L16 11",
    };

    private static readonly Dictionary<string, Geometry> Cache = [];

    internal static Geometry Geometry(string name)
    {
        if (Cache.TryGetValue(name, out var geometry)) { return geometry; }
        // "|" separates SVG paths whose relative commands must not run on from each other.
        string[] parts = Paths[name].Split('|');
        if (parts.Length == 1) { geometry = System.Windows.Media.Geometry.Parse(parts[0]); }
        else
        {
            var group = new GeometryGroup { FillRule = FillRule.Nonzero };
            foreach (string part in parts) { group.Children.Add(System.Windows.Media.Geometry.Parse(part)); }
            geometry = group;
        }
        geometry.Freeze();
        return Cache[name] = geometry;
    }

    /// <summary>Draws an icon into a square box, with the stroke scaled along with the 24-unit artwork, as an SVG does.</summary>
    internal static void Draw(DrawingContext context, string name, Rect box, Brush stroke, double thickness = 1.75, double rotateDegrees = 0, Brush? fill = null)
    {
        if (string.IsNullOrEmpty(name) || !Paths.ContainsKey(name)) { return; }
        var pen = new Pen(stroke, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        double scale = box.Width / 24;
        var group = new TransformGroup();
        if (rotateDegrees != 0) { group.Children.Add(new RotateTransform(rotateDegrees, 12, 12)); }
        group.Children.Add(new ScaleTransform(scale, scale));
        group.Children.Add(new TranslateTransform(box.X, box.Y));
        context.PushTransform(group);
        context.DrawGeometry(fill, pen, Geometry(name));
        context.Pop();
    }
}
