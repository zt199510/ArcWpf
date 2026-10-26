using System.Windows;

namespace ArcWpf;

public static class ArcMotion
{
    public static readonly DependencyProperty LevelProperty = DependencyProperty.RegisterAttached(
        "Level", typeof(ArcMotionLevel), typeof(ArcMotion),
        new FrameworkPropertyMetadata(ArcMotionLevel.Full, FrameworkPropertyMetadataOptions.Inherits),
        value => value is ArcMotionLevel level && Enum.IsDefined(level));

    public static ArcMotionLevel GetLevel(DependencyObject element) => (ArcMotionLevel)element.GetValue(LevelProperty);
    public static void SetLevel(DependencyObject element, ArcMotionLevel value) => element.SetValue(LevelProperty, value);
}
