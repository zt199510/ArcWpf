using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ArcWpf.Controls;

public partial class MetricCard
{
    private Border? _changeChip;
    private TextBlock? _changeText;
    private ScaleTransform? _changeScale;
    private TranslateTransform? _changeMove;
    private BlurEffect? _changeBlur;
    private readonly AutoWidthMotion _changeWidthMotion = new();
    private readonly OwnedMotion _changeMotion = new();
    private string _lastChange = string.Empty;
    private int _changeVersion;

    private void UpdateChangeMotion(bool animate)
    {
        if (_changeChip is null || _changeText is null) return;
        string change = DisplayChange;
        if (string.IsNullOrEmpty(change))
        {
            if (animate && IsLoaded && ArcMotion.GetLevel(this) == ArcMotionLevel.Full && _lastChange.Length > 0 && _changeScale is not null)
            {
                int version = ++_changeVersion;
                _changeMotion.Stop();
                _changeWidthMotion.Stop();
                _changeChip.Visibility = Visibility.Visible;
                _changeMotion.Double(_changeChip, OpacityProperty, 0, .16, true, completed: () =>
                {
                    if (version != _changeVersion || DisplayChange.Length != 0) return;
                    _changeChip.Visibility = Visibility.Collapsed;
                    _changeText.Text = string.Empty;
                    _changeChip.Opacity = 1;
                    _changeScale.ScaleX = _changeScale.ScaleY = 1;
                });
                _changeMotion.Double(_changeScale, ScaleTransform.ScaleXProperty, .96, .16, true);
                _changeMotion.Double(_changeScale, ScaleTransform.ScaleYProperty, .96, .16, true);
            }
            else { StopChangeMotion(); _changeChip.Visibility = Visibility.Collapsed; _changeText.Text = string.Empty; }
            _lastChange = change;
            return;
        }
        bool changed = change != _lastChange;
        ++_changeVersion;
        _changeChip.Visibility = Visibility.Visible;
        _changeText.Text = change;
        if (changed)
        {
            _changeMotion.Stop();
            _changeChip.Opacity = 1;
            _changeText.Opacity = 1;
            if (_changeScale is not null) _changeScale.ScaleX = _changeScale.ScaleY = 1;
            if (_changeMove is not null) _changeMove.Y = 0;
            if (_changeBlur is not null) _changeBlur.Radius = 0;
        }
        var measure = new TextBlock
        {
            Text = change, FontFamily = _changeText.FontFamily, FontSize = _changeText.FontSize,
            FontWeight = _changeText.FontWeight
        };
        measure.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double target = measure.DesiredSize.Width + 18;
        bool moving = animate && IsLoaded && IsVisible && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        _changeWidthMotion.SetTarget(target, moving);
        if (changed && moving)
        {
            if (_lastChange.Length == 0 && _changeScale is not null)
            {
                _changeChip.Opacity = 0;
                _changeScale.ScaleX = _changeScale.ScaleY = .96;
                _changeMotion.Double(_changeChip, OpacityProperty, 1, .16, true, enter: true);
                _changeMotion.Double(_changeScale, ScaleTransform.ScaleXProperty, 1, .26, true, spring: true);
                _changeMotion.Double(_changeScale, ScaleTransform.ScaleYProperty, 1, .26, true, spring: true);
            }
            else if (_changeMove is not null && _changeBlur is not null)
            {
                int direction = Amount(change) < Amount(_lastChange) ? -1 : 1;
                _changeText.Opacity = 0;
                _changeMove.Y = direction * 4;
                _changeBlur.Radius = 2;
                _changeMotion.Double(_changeText, OpacityProperty, 1, .24, true, enter: true);
                _changeMotion.Double(_changeMove, TranslateTransform.YProperty, 0, .24, true, enter: true);
                _changeMotion.Double(_changeBlur, BlurEffect.RadiusProperty, 0, .24, true, enter: true);
            }
        }
        _lastChange = change;
    }
    private static double Amount(string text)
    {
        var match = Regex.Match(text, @"[+\-−]?\d+(?:[.,]\d+)?");
        if (!match.Success) return double.NaN;
        string value = match.Value.Replace('−', '-');
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out double parsed)) return parsed;
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : double.NaN;
    }
    private void StopChangeMotion()
    {
        ++_changeVersion;
        _changeWidthMotion.Stop();
        _changeMotion.Stop();
        if (_changeChip is not null) _changeChip.Opacity = 1;
        if (_changeText is not null) _changeText.Opacity = 1;
        if (_changeScale is not null) _changeScale.ScaleX = _changeScale.ScaleY = 1;
        if (_changeMove is not null) _changeMove.Y = 0;
        if (_changeBlur is not null) _changeBlur.Radius = 0;
        if (_changeChip is not null) _changeChip.Visibility = string.IsNullOrEmpty(DisplayChange) ? Visibility.Collapsed : Visibility.Visible;
        if (_changeText is not null) _changeText.Text = DisplayChange;
    }
}
