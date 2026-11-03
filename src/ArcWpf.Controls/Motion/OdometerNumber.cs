using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Data;

namespace ArcWpf.Controls;

internal sealed class OdometerNumber : UserControl
{
    /// <summary>Letter spacing in ems, like CSS letter-spacing. Set it before the first value, because columns keep the width they were built with.</summary>
    internal double Tracking { get; init; }
    private sealed record Part(string Key, char Character, bool Digit);
    private readonly StackPanel _track = new() { Orientation = Orientation.Horizontal };
    private readonly Dictionary<string, DigitColumn> _digits = [];
    private readonly Dictionary<string, DigitColumn> _retiring = [];
    private readonly Dictionary<string, TextBlock> _symbols = [];
    private readonly Dictionary<string, int> _pendingTargets = [];
    private readonly OwnedMotion _layoutMotion = new();
    private string _text = string.Empty;
    private double _value;
    private bool _revealed;
    internal bool RevealOnLoad { get; set; } = true;
    internal bool IsMotionRunning => _layoutMotion.IsRunning || _digits.Values.Any(column => column.IsMotionRunning);
    internal IReadOnlyDictionary<string, DigitColumn> DigitColumns => _digits;

    internal OdometerNumber()
    {
        Content = _track;
        Focusable = false;
        IsHitTestVisible = false;
        Loaded += (_, _) =>
        {
            if (RevealOnLoad) Reveal();
        };
        Unloaded += (_, _) => Stop();
    }
    internal void Set(double value, string text, bool animate)
    {
        if (!double.IsFinite(value)) { Stop(); _track.Children.Clear(); _digits.Clear(); _symbols.Clear(); _pendingTargets.Clear(); _text = string.Empty; return; }
        if (_text == text && _value == value) return;
        int direction = value >= _value ? 1 : -1;
        _value = value;
        _text = text;
        bool moving = animate && _revealed && IsLoaded && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        var parts = Parse(text);
        if (!_revealed) _pendingTargets.Clear();
        var needed = parts.Where(part => part.Digit).Select(part => part.Key).ToHashSet();
        var neededSymbols = parts.Where(part => !part.Digit).Select(part => part.Key).ToHashSet();
        foreach (var key in _symbols.Keys.Where(key => !neededSymbols.Contains(key)).ToArray()) _symbols.Remove(key);
        foreach (var key in _digits.Keys.Where(key => !needed.Contains(key)).ToArray())
        {
            var column = _digits[key];
            _digits.Remove(key);
            if (moving && _track.Children.Contains(column.Root)) _retiring[key] = column;
            else { _track.Children.Remove(column.Root); column.Stop(); }
        }
        var ordered = new List<UIElement>(parts.Count);
        var updates = new List<(DigitColumn Column, int Digit)>();
        var entering = new List<DigitColumn>();
        foreach (var part in parts)
        {
            if (!part.Digit)
            {
                if (!_symbols.TryGetValue(part.Key, out var symbol)) _symbols[part.Key] = symbol = MakeText(part.Character.ToString());
                else symbol.Text = part.Character.ToString();
                ordered.Add(symbol);
                continue;
            }
            int digit = (int)char.GetNumericValue(part.Character);
            int shown = _revealed ? digit : 0;
            if (!_revealed) _pendingTargets[part.Key] = digit;
            if (!_digits.TryGetValue(part.Key, out var column))
            {
                if (_retiring.Remove(part.Key, out var restored)) { column = restored; entering.Add(column); }
                else { column = new DigitColumn(shown, this); if (moving) { column.Root.Width = 0; column.Root.Opacity = 0; entering.Add(column); } }
                _digits.Add(part.Key, column);
            }
            updates.Add((column, shown));
            ordered.Add(column.Root);
        }
        foreach (var (key, column) in _retiring.ToArray())
        {
            int position = _track.Children.IndexOf(column.Root);
            ordered.Insert(Math.Clamp(position, 0, ordered.Count), column.Root);
        }
        for (int index = 0; index < ordered.Count; index++)
        {
            var child = ordered[index];
            if (index < _track.Children.Count && ReferenceEquals(_track.Children[index], child)) continue;
            if (_track.Children.Contains(child)) _track.Children.Remove(child);
            _track.Children.Insert(index, child);
        }
        while (_track.Children.Count > ordered.Count) _track.Children.RemoveAt(_track.Children.Count - 1);
        foreach (var column in entering)
        {
            _layoutMotion.Double(column.Root, WidthProperty, column.NaturalWidth, .42, true, spring: true);
            _layoutMotion.Double(column.Root, OpacityProperty, 1, .16, true, enter: true);
        }
        foreach (var (key, column) in _retiring.ToArray())
        {
            _layoutMotion.Double(column.Root, WidthProperty, 0, .42, moving, spring: moving, completed: () =>
            {
                if (!_retiring.TryGetValue(key, out var active) || !ReferenceEquals(active, column)) return;
                _retiring.Remove(key);
                _track.Children.Remove(column.Root);
                column.Stop();
            });
            _layoutMotion.Double(column.Root, OpacityProperty, 0, .16, moving);
        }
        foreach (var (column, digit) in updates) column.Update(digit, direction, moving);
    }
    internal void Stop()
    {
        _layoutMotion.Stop();
        foreach (var column in _retiring.Values) { _track.Children.Remove(column.Root); column.Stop(); }
        _retiring.Clear();
        foreach (var column in _digits.Values) { column.Root.Width = column.NaturalWidth; column.Root.Opacity = 1; }
        foreach (var column in _digits.Values) column.Stop();
    }
    internal void Reveal()
    {
        if (_revealed) return;
        _revealed = true;
        bool animate = ArcMotion.GetLevel(this) == ArcMotionLevel.Full && IsLoaded;
        foreach (var (key, column) in _digits)
        {
            int target = _pendingTargets.TryGetValue(key, out var digit) ? digit : column.Digit;
            column.Update(target, _value < 0 ? -1 : 1, animate);
        }
        _pendingTargets.Clear();
    }
    private TextBlock MakeText(string text)
    {
        var symbol = new TextBlock
        {
            Text = text, FontFamily = FontFamily, FontSize = FontSize, FontWeight = FontWeight,
            VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false, Margin = new Thickness(0, 0, Tracking * FontSize, 0)
        };
        BindingOperations.SetBinding(symbol, TextBlock.ForegroundProperty, new Binding(nameof(Foreground)) { Source = this });
        return symbol;
    }
    private static List<Part> Parse(string text)
    {
        string decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        int decimalAt = text.IndexOf(decimalSeparator, StringComparison.Ordinal);
        if (decimalAt < 0) decimalAt = text.Length;
        int integerCount = text[..decimalAt].Count(char.IsDigit);
        int integerSeen = 0, fractionSeen = 0;
        var parts = new List<Part>(text.Length);
        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            if (char.IsDigit(character))
            {
                bool fraction = index > decimalAt;
                string key = fraction ? $"f{fractionSeen++}" : $"i{integerCount - ++integerSeen}";
                parts.Add(new Part(key, character, true));
            }
            else parts.Add(new Part($"s{index}", character, false));
        }
        return parts;
    }

    internal sealed class DigitColumn
    {
        private readonly Canvas _strip;
        private readonly TranslateTransform _move;
        private readonly BlurEffect _blur = new();
        private readonly OwnedMotion _motion = new();
        private readonly double _height;
        private int _logicalIndex;
        private int _version;
        internal Grid Root { get; }
        internal double NaturalWidth { get; }
        internal int Digit { get; private set; }
        internal bool IsMotionRunning => _motion.IsRunning;
        internal double Position => _move.Y;

        internal DigitColumn(int digit, OdometerNumber owner)
        {
            Digit = digit;
            var family = owner.FontFamily;
            double size = owner.FontSize;
            var weight = owner.FontWeight;
            var measure = new TextBlock { Text = "0", FontFamily = family, FontSize = size, FontWeight = weight };
            measure.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double glyphWidth = Math.Max(1, measure.DesiredSize.Width);
            double width = Math.Max(1, glyphWidth + owner.Tracking * size);
            NaturalWidth = width;
            _height = Math.Max(1, measure.DesiredSize.Height);
            Root = new Grid { Width = width, Height = _height, ClipToBounds = true, IsHitTestVisible = false };
            _strip = new Canvas { Width = width, Height = _height * 30, IsHitTestVisible = false, Effect = _blur, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left };
            _move = new TranslateTransform();
            _strip.RenderTransform = _move;
            for (int index = 0; index < 30; index++)
            {
                var glyph = new TextBlock
                {
                    Text = (index % 10).ToString(CultureInfo.InvariantCulture), Width = glyphWidth, Height = _height,
                    FontFamily = family, FontSize = size, FontWeight = weight, TextAlignment = TextAlignment.Center,
                    IsHitTestVisible = false
                };
                BindingOperations.SetBinding(glyph, TextBlock.ForegroundProperty, new Binding(nameof(Foreground)) { Source = owner });
                Canvas.SetLeft(glyph, (width - glyphWidth) / 2);
                Canvas.SetTop(glyph, index * _height);
                _strip.Children.Add(glyph);
            }
            Root.Children.Add(_strip);
            Jump(digit);
        }
        internal void Jump(int digit)
        {
            _motion.Stop();
            Digit = digit;
            _logicalIndex = 10 + digit;
            _move.Y = -_logicalIndex * _height;
            _blur.Radius = 0;
        }
        internal void Update(int digit, int direction, bool animate)
        {
            if (Digit == digit) return;
            if (!animate) { Jump(digit); return; }
            int steps = direction >= 0 ? (digit - Digit + 10) % 10 : -(Digit - digit + 10) % 10;
            Digit = digit;
            _logicalIndex += steps;
            // 重复的数字周期可等价平移回中段，避免连续改值越过字形带边界。
            while (_logicalIndex < 1 || _logicalIndex > 28)
            {
                int shift = _logicalIndex < 1 ? 10 : -10;
                double current = _move.Y;
                _motion.Stop();
                _logicalIndex += shift;
                _move.Y = current - shift * _height;
            }
            int destination = _logicalIndex;
            int version = ++_version;
            _blur.Radius = 1;
            _motion.Double(_move, TranslateTransform.YProperty, -destination * _height, .42, true, spring: true, completed: () =>
            {
                if (version != _version) return;
                _logicalIndex = 10 + digit;
                _move.Y = -_logicalIndex * _height;
            });
            _motion.Double(_blur, BlurEffect.RadiusProperty, 0, .24, true);
        }
        internal void Stop() => Jump(Digit);
    }
}
