using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ArcWpf.Controls;

// Template-private rolling readout. Numeric columns keep identity by place value, symbols by side.
internal sealed class SliderNumber : StackPanel
{
    private sealed class Column
    {
        internal required string Key;
        internal required Border Slot;
        internal required Grid Face;
        internal required ScaleTransform Scale;
        internal required TranslateTransform Move;
        internal required BlurEffect Blur;
        internal readonly SliderSpring Wheel = new();
        internal readonly List<(TextBlock Text, TranslateTransform Move, BlurEffect Blur)> Glyphs = [];
        internal int Digit = -1;
        internal bool Leaving;
        internal double NaturalWidth;
    }
    private readonly Dictionary<string, Column> _columns = [];
    private readonly OwnedMotion _motion = new();
    private double _lastValue;
    private string? _text;
    internal bool IsRunning => _motion.IsRunning || _columns.Values.Any(c => c.Wheel.IsRunning);
    internal SliderNumber() { Orientation = Orientation.Horizontal; FlowDirection = FlowDirection.LeftToRight; IsHitTestVisible = false; }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_text is null || _columns is null) return;
        if (e.Property == TextBlock.FontSizeProperty || e.Property == TextBlock.FontFamilyProperty || e.Property == TextBlock.FontWeightProperty || e.Property == TextBlock.FontStyleProperty || e.Property == TextBlock.FontStretchProperty)
        {
            string text = _text; Stop(); Children.Clear(); _columns.Clear(); _text = null;
            SetNumber(text, _lastValue, false, ArcMotionLevel.Off);
        }
    }
    internal void SetNumber(string text, double value, bool animate, ArcMotionLevel level)
    {
        if (text == _text) { if (!animate || level != ArcMotionLevel.Full) Stop(); return; }
        bool initial = _text is null, full = level == ArcMotionLevel.Full;
        animate &= !initial && level != ArcMotionLevel.Off && IsLoaded && IsVisible;
        int direction = value >= _lastValue ? 1 : -1;
        _lastValue = value; _text = text;
        var parts = Parts(text); var live = parts.Select(p => p.Key).ToHashSet();
        foreach (var old in _columns.Values.Where(c => !live.Contains(c.Key)).ToArray())
        {
            old.Leaving = true;
            if (!animate || !full) { Remove(old); continue; }
            _motion.Double(old.Scale, ScaleTransform.ScaleXProperty, .6, .4, true, spring: true);
            _motion.Double(old.Scale, ScaleTransform.ScaleYProperty, .6, .4, true, spring: true);
            _motion.Double(old.Move, TranslateTransform.YProperty, -direction * FontSize() * .3, .16, true);
            _motion.Double(old.Blur, BlurEffect.RadiusProperty, 2, .12, true);
            _motion.Double(old.Face, OpacityProperty, 0, .12, true);
            _motion.Double(old.Slot, WidthProperty, 0, .4, true, spring: true, completed: () => { if (old.Leaving) Remove(old); });
        }
        int position = 0;
        foreach (var part in parts)
        {
            bool added = !_columns.TryGetValue(part.Key, out var column);
            if (added)
            {
                column = Create(part.Key, part.Text, part.Digit);
                _columns.Add(part.Key, column);
                if (animate)
                {
                    column.Slot.Width = full ? 0 : column.NaturalWidth; column.Face.Opacity = 0;
                    column.Scale.ScaleX = column.Scale.ScaleY = full ? .6 : 1;
                    column.Move.Y = full ? direction * FontSize() * .3 : 0; column.Blur.Radius = full ? 4 : 0;
                }
            }
            var item = column!; item.Leaving = false;
            if (Children.Contains(item.Slot)) Children.Remove(item.Slot);
            Children.Insert(Math.Min(position++, Children.Count), item.Slot);
            if (part.Digit >= 0 && item.Digit != part.Digit)
            {
                double target = item.Wheel.Target + (direction > 0 ? (part.Digit - item.Digit + 10) % 10 : -((item.Digit - part.Digit + 10) % 10));
                item.Digit = part.Digit;
                if (animate && full) item.Wheel.To(target); else item.Wheel.Snap(part.Digit);
            }
            _motion.Double(item.Slot, WidthProperty, item.NaturalWidth, .42, animate && full, spring: true);
            _motion.Double(item.Scale, ScaleTransform.ScaleXProperty, 1, .42, animate && full, spring: true);
            _motion.Double(item.Scale, ScaleTransform.ScaleYProperty, 1, .42, animate && full, spring: true);
            _motion.Double(item.Move, TranslateTransform.YProperty, 0, .26, animate && full, spring: true);
            _motion.Double(item.Blur, BlurEffect.RadiusProperty, 0, .24, animate && full, enter: true);
            _motion.Double(item.Face, OpacityProperty, 1, full ? .42 : .12, animate, spring: full);
            DrawWheel(item);
        }
    }
    private double FontSize() => TextBlock.GetFontSize(this);
    private Column Create(string key, string symbol, int digit)
    {
        var typeface = new Typeface(TextBlock.GetFontFamily(this), TextBlock.GetFontStyle(this), TextBlock.GetFontWeight(this), TextBlock.GetFontStretch(this));
        var format = new FormattedText(digit >= 0 ? "0" : symbol, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, FontSize(), Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        double width = Math.Ceiling(format.WidthIncludingTrailingWhitespace * VisualTreeHelper.GetDpi(this).DpiScaleX) / VisualTreeHelper.GetDpi(this).DpiScaleX;
        var face = new Grid { Width = width, Height = Math.Ceiling(format.Height) + FontSize() * .4, ClipToBounds = true, RenderTransformOrigin = new Point(.5, .5) };
        if (digit >= 0)
        {
            double feather = Math.Min(.49, FontSize() * .3 / face.Height);
            var mask = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            mask.GradientStops.Add(new(Colors.Transparent, 0)); mask.GradientStops.Add(new(Colors.Black, feather));
            mask.GradientStops.Add(new(Colors.Black, 1 - feather)); mask.GradientStops.Add(new(Colors.Transparent, 1));
            mask.Freeze(); face.OpacityMask = mask;
        }
        var scale = new ScaleTransform(); var move = new TranslateTransform(); var blur = new BlurEffect { Radius = 0 };
        var group = new TransformGroup(); group.Children.Add(scale); group.Children.Add(move); face.RenderTransform = group; face.Effect = blur;
        var slot = new Border { Width = width, Child = face, ClipToBounds = true, VerticalAlignment = VerticalAlignment.Center };
        var column = new Column { Key = key, Slot = slot, Face = face, Scale = scale, Move = move, Blur = blur, NaturalWidth = width, Digit = digit };
        column.Wheel.Snap(digit);
        if (digit < 0) face.Children.Add(new TextBlock { Text = symbol, VerticalAlignment = VerticalAlignment.Center });
        else for (int i = 0; i < 10; i++)
        {
            var offset = new TranslateTransform(); var soft = new BlurEffect { Radius = 0 };
            var glyph = new TextBlock { Text = i.ToString(CultureInfo.InvariantCulture), VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center, RenderTransform = offset, Effect = soft };
            face.Children.Add(glyph); column.Glyphs.Add((glyph, offset, soft));
        }
        return column;
    }
    private void DrawWheel(Column column)
    {
        for (int digit = 0; digit < column.Glyphs.Count; digit++)
        {
            var glyph = column.Glyphs[digit]; double offset = (((digit - column.Wheel.Position) % 10 + 15) % 10) - 5;
            double distance = Math.Abs(offset);
            glyph.Move.Y = offset * FontSize() * 1.05;
            glyph.Text.Opacity = Math.Max(0, 1 - Math.Pow(distance, 1.5) * 1.1);
            glyph.Text.Visibility = distance >= 1 ? Visibility.Hidden : Visibility.Visible;
            glyph.Blur.Radius = distance < .02 || distance >= 1 ? 0 : distance * 3;
        }
    }
    private void Remove(Column column) { Children.Remove(column.Slot); _columns.Remove(column.Key); }
    internal void Step(double seconds) { foreach (var column in _columns.Values) { column.Wheel.Step(seconds); DrawWheel(column); } }
    internal void Stop()
    {
        _motion.Stop();
        foreach (var column in _columns.Values.ToArray())
        {
            if (column.Leaving) { Remove(column); continue; }
            column.Wheel.Snap(column.Digit); DrawWheel(column);
        }
    }
    internal void Detach() { Stop(); if (Parent is Panel panel) panel.Children.Remove(this); }
    private static List<(string Key, string Text, int Digit)> Parts(string text)
    {
        static bool Digit(char c) => c is >= '0' and <= '9';
        int first = text.ToList().FindIndex(Digit), last = text.ToList().FindLastIndex(Digit), place = text.Count(Digit);
        var parts = new List<(string, string, int)>();
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            string key = first < 0 || i < first ? $"p{i}:{c}" : i > last ? $"s{text.Length - i}:{c}" : Digit(c) ? $"d{--place}" : $"g{place}:{c}";
            parts.Add((key, c.ToString(), Digit(c) ? c - '0' : -1));
        }
        return parts;
    }
}
