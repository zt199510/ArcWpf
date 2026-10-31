using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace ArcWpf.Controls;

public enum InlineEditBoxRole { Display, Editor, Frame, Ring, Slot }

/// <summary>
/// Lays out InlineEdit the way its CSS does: the display text alone sizes the box; the field covers it exactly; the frame behind it
/// takes its own (springing) width or height; the action slot rides on the frame's trailing edge. Nothing but the display affects layout.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class InlineEditBox : Panel
{
    public static readonly DependencyProperty RoleProperty = DependencyProperty.RegisterAttached("Role", typeof(InlineEditBoxRole), typeof(InlineEditBox), new FrameworkPropertyMetadata(InlineEditBoxRole.Display, FrameworkPropertyMetadataOptions.AffectsParentMeasure));
    public static readonly DependencyProperty FrameWidthProperty = DependencyProperty.Register(nameof(FrameWidth), typeof(double), typeof(InlineEditBox), new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsArrange));
    public static readonly DependencyProperty BoxHeightProperty = DependencyProperty.Register(nameof(BoxHeight), typeof(double), typeof(InlineEditBox), new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty IsMultilineProperty = DependencyProperty.Register(nameof(IsMultiline), typeof(bool), typeof(InlineEditBox), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty SlotTopProperty = DependencyProperty.Register(nameof(SlotTop), typeof(double), typeof(InlineEditBox), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static InlineEditBoxRole GetRole(UIElement element) => (InlineEditBoxRole)element.GetValue(RoleProperty);
    public static void SetRole(UIElement element, InlineEditBoxRole value) => element.SetValue(RoleProperty, value);
    /// <summary>Single line: the visible frame width, NaN to follow the text.</summary>
    public double FrameWidth { get => (double)GetValue(FrameWidthProperty); set => SetValue(FrameWidthProperty, value); }
    /// <summary>Multiline: the box height, NaN to follow the text.</summary>
    public double BoxHeight { get => (double)GetValue(BoxHeightProperty); set => SetValue(BoxHeightProperty, value); }
    public bool IsMultiline { get => (bool)GetValue(IsMultilineProperty); set => SetValue(IsMultilineProperty, value); }
    /// <summary>Multiline: where the slot starts, so it lines up with the first line.</summary>
    public double SlotTop { get => (double)GetValue(SlotTopProperty); set => SetValue(SlotTopProperty, value); }

    /// <summary>The size the text needs right now: width for a single line, height for multiline.</summary>
    public Size NaturalSize { get; private set; }
    internal event EventHandler? NaturalSizeChanged;

    protected override Size MeasureOverride(Size availableSize)
    {
        Size natural = default;
        foreach (UIElement child in InternalChildren)
            if (GetRole(child) == InlineEditBoxRole.Display) { child.Measure(new Size(availableSize.Width, double.PositiveInfinity)); natural = child.DesiredSize; }
        if (IsMultiline && double.IsFinite(availableSize.Width)) natural.Width = availableSize.Width;
        foreach (UIElement child in InternalChildren)
            switch (GetRole(child))
            {
                case InlineEditBoxRole.Editor: case InlineEditBoxRole.Frame: case InlineEditBoxRole.Ring: child.Measure(natural); break;
                case InlineEditBoxRole.Slot: child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); break;
            }
        if (natural != NaturalSize) { NaturalSize = natural; NaturalSizeChanged?.Invoke(this, EventArgs.Empty); }
        return new Size(natural.Width, IsMultiline && double.IsFinite(BoxHeight) ? BoxHeight : natural.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var natural = NaturalSize;
        double width = IsMultiline ? finalSize.Width : natural.Width;
        double frameWidth = IsMultiline ? finalSize.Width : double.IsFinite(FrameWidth) ? FrameWidth : natural.Width;
        double frameHeight = IsMultiline && double.IsFinite(BoxHeight) ? BoxHeight : natural.Height;
        foreach (UIElement child in InternalChildren)
            switch (GetRole(child))
            {
                case InlineEditBoxRole.Display: case InlineEditBoxRole.Editor: child.Arrange(new Rect(0, 0, width, natural.Height)); break;
                case InlineEditBoxRole.Frame: child.Arrange(new Rect(0, 0, frameWidth, frameHeight)); break;
                // box-shadow 0 0 0 3px: the ring sits outside the frame.
                case InlineEditBoxRole.Ring: child.Arrange(new Rect(-3, -3, frameWidth + 6, frameHeight + 6)); break;
                case InlineEditBoxRole.Slot:
                    double top = IsMultiline ? SlotTop : natural.Height / 2 - 14;
                    child.Arrange(new Rect(new Point(frameWidth + (IsMultiline ? 8 : 6), top), child.DesiredSize)); break;
            }
        return finalSize;
    }
}

/// <summary>The message row: opens and closes its height from a true zero while its content keeps its natural size.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class InlineEditReveal : Decorator
{
    public static readonly DependencyProperty RevealHeightProperty = DependencyProperty.Register(nameof(RevealHeight), typeof(double), typeof(InlineEditReveal), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public double RevealHeight { get => (double)GetValue(RevealHeightProperty); set => SetValue(RevealHeightProperty, value); }
    public double NaturalHeight { get; private set; }
    internal event EventHandler? NaturalHeightChanged;

    public InlineEditReveal() { ClipToBounds = true; }

    protected override Size MeasureOverride(Size constraint)
    {
        if (Child is null) return new Size(0, Math.Max(0, RevealHeight));
        Child.Measure(new Size(constraint.Width, double.PositiveInfinity));
        double natural = Child.DesiredSize.Height;
        if (Math.Abs(natural - NaturalHeight) > .1) { NaturalHeight = natural; NaturalHeightChanged?.Invoke(this, EventArgs.Empty); }
        return new Size(Child.DesiredSize.Width, Math.Max(0, RevealHeight));
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        Child?.Arrange(new Rect(0, 0, arrangeSize.Width, Child.DesiredSize.Height));
        return arrangeSize;
    }
}
