using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ArcWpf.Controls;

internal sealed class PasswordMessage : Grid
{
    private sealed record Word(Border Box, TranslateTransform Move, BlurEffect Blur);
    private readonly WrapPanel _live = new() { Margin = new Thickness(0, 8, 0, 0), VerticalAlignment = VerticalAlignment.Top };
    private readonly Canvas _exits = new() { IsHitTestVisible = false };
    private readonly Dictionary<string, Word> _words = [];
    private readonly OwnedMotion _motion = new();
    private string _text = "";
    private bool _initial = true;
    private readonly double _wordExitSeconds;
    internal bool IsMotionRunning => _motion.IsRunning;
    internal PasswordMessage(double wordExitSeconds = .18) { _wordExitSeconds = wordExitSeconds; ClipToBounds = true; Height = 0; VerticalAlignment = VerticalAlignment.Top; Children.Add(_live); Children.Add(_exits); Unloaded += (_, _) => Stop(); }
    internal void SetText(string text, bool animate)
    {
        var level = ArcMotion.GetLevel(this); bool full = level == ArcMotionLevel.Full;
        animate &= !_initial && IsLoaded && IsVisible && level != ArcMotionLevel.Off;
        if (text == _text && !_initial) { if (!animate) Stop(); UpdateHeight(animate && full); return; }
        _initial = false; _text = text;
        var parts = text.Length == 0 ? Array.Empty<string>() : text.Split(' ');
        var keep = parts.Select((word, i) => $"{i}:{word}").ToHashSet();
        foreach (var pair in _words.Where(p => !keep.Contains(p.Key)).ToArray())
        {
            var word = pair.Value;
            if (_live.Children.Contains(word.Box))
            {
                var point = word.Box.TranslatePoint(new Point(), this); _live.Children.Remove(word.Box);
                _exits.Children.Add(word.Box); Canvas.SetLeft(word.Box, point.X); Canvas.SetTop(word.Box, point.Y);
            }
            if (!animate || !full) { _exits.Children.Remove(word.Box); _words.Remove(pair.Key); continue; }
            _motion.Double(word.Move, TranslateTransform.YProperty, -TextBlock.GetFontSize(this) * .35, _wordExitSeconds, true);
            _motion.Double(word.Blur, BlurEffect.RadiusProperty, 2, _wordExitSeconds, true);
            _motion.Double(word.Box, OpacityProperty, 0, _wordExitSeconds, true, completed: () => { if (!_live.Children.Contains(word.Box)) { _exits.Children.Remove(word.Box); _words.Remove(pair.Key); } });
        }
        _live.Children.Clear();
        for (int i = 0; i < parts.Length; i++)
        {
            string key = $"{i}:{parts[i]}";
            if (!_words.TryGetValue(key, out var word))
            {
                var move = new TranslateTransform { Y = animate && full ? TextBlock.GetFontSize(this) * .35 : 0 };
                var blur = new BlurEffect { Radius = animate && full ? 4 : 0 };
                var block = new TextBlock { Text = parts[i] + (i < parts.Length - 1 ? " " : ""), TextWrapping = TextWrapping.Wrap };
                var box = new Border { Child = block, RenderTransform = move, Effect = blur, Opacity = animate ? 0 : 1 };
                word = new(box, move, blur); _words.Add(key, word);
            }
            if (word.Box.Child is TextBlock content) content.Text = parts[i] + (i < parts.Length - 1 ? " " : "");
            _exits.Children.Remove(word.Box); _live.Children.Add(word.Box);
            _motion.Double(word.Move, TranslateTransform.YProperty, 0, .24, animate && full, enter: true);
            _motion.Double(word.Blur, BlurEffect.RadiusProperty, 0, .24, animate && full, enter: true);
            _motion.Double(word.Box, OpacityProperty, 1, full ? .24 : .12, animate, enter: true);
        }
        _motion.Double(this, OpacityProperty, text.Length > 0 ? 1 : 0, full && text.Length > 0 ? .16 : .12, animate);
        UpdateHeight(animate && full);
    }
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) { base.OnRenderSizeChanged(sizeInfo); if (sizeInfo.WidthChanged) UpdateHeight(IsLoaded && ArcMotion.GetLevel(this) == ArcMotionLevel.Full); }
    private void UpdateHeight(bool animate)
    {
        double width = Math.Max(0, ActualWidth);
        foreach (var word in _words.Values) word.Box.MaxWidth = width;
        _live.Measure(new Size(width, double.PositiveInfinity));
        double target = _text.Length == 0 ? 0 : _live.DesiredSize.Height;
        _motion.Double(this, HeightProperty, target, .4, animate && IsVisible, spring: true);
    }
    internal void Stop()
    {
        _motion.Stop();
        foreach (var pair in _words.Where(p => !_live.Children.Contains(p.Value.Box)).ToArray()) _words.Remove(pair.Key);
        _exits.Children.Clear();
    }
    internal void Detach() { Stop(); if (Parent is Panel panel) panel.Children.Remove(this); }
}
