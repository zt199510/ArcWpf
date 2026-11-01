using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public partial class Dialog
{
    private FrameworkElement? _morphOrigin;
    private ContentPresenter? _morphBody;
    private bool _morphPrepared;
    private bool _morphQueued;
    private int _morphGeneration;
    private readonly record struct MorphGeometry(double ScaleX, double ScaleY, double MoveX, double MoveY);

    internal void MorphFrom(FrameworkElement origin)
    {
        if (!IsOpen || !IsPresented || ArcMotion.GetLevel(this) != ArcMotionLevel.Full) return;
        if (_morphPrepared) { _morphOrigin = origin; return; }
        _host?.UpdateLayout();
        if (_surface is not { ActualWidth: > 0, ActualHeight: > 0 } || !origin.IsLoaded)
        {
            if (_morphQueued) return;
            _morphQueued = true;
            int generation = _morphGeneration;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (generation != _morphGeneration) return;
                _morphQueued = false;
                if (IsOpen && _surface is { ActualWidth: > 0, ActualHeight: > 0 }) MorphFrom(origin);
            }));
            return;
        }
        _motion.Stop();
        if (_scale is not null) _scale.ScaleX = _scale.ScaleY = 1;
        if (_move is not null) _move.X = _move.Y = 0;
        var geometry = GeometryFrom(origin);
        if (geometry is null || _scale is null || _move is null) { Transition(true, false); return; }
        _morphOrigin = origin;
        _morphPrepared = true;
        _morphBody = GetTemplateChild("Body") as ContentPresenter;
        _scale.ScaleX = geometry.Value.ScaleX;
        _scale.ScaleY = geometry.Value.ScaleY;
        _move.X = geometry.Value.MoveX;
        _move.Y = geometry.Value.MoveY;
        _surface.Opacity = 0;
        if (_overlay is not null) _overlay.Opacity = 0;
        if (_morphBody is not null) _morphBody.Opacity = 0;
        TryMorphTransition(true, _version);
    }
    private bool TryMorphTransition(bool open, int version)
    {
        if (!_morphPrepared || _surface is null || _scale is null || _move is null) return false;
        if (open)
        {
            _motion.Double(_scale, ScaleTransform.ScaleXProperty, 1, .4, true, spring: true);
            _motion.Double(_scale, ScaleTransform.ScaleYProperty, 1, .4, true, spring: true);
            _motion.Double(_move, TranslateTransform.XProperty, 0, .4, true, spring: true);
            _motion.Double(_move, TranslateTransform.YProperty, 0, .4, true, spring: true);
            _motion.Double(_surface, OpacityProperty, 1, .16, true, enter: true);
            if (_overlay is not null) _motion.Double(_overlay, OpacityProperty, 1, .24, true, enter: true);
            if (_morphBody is not null) _motion.Double(_morphBody, OpacityProperty, 1, .24, true, delay: .12, enter: true);
            return true;
        }
        if (_morphOrigin is not { IsLoaded: true, IsVisible: true } origin || GeometryFrom(origin) is not { } target) return false;
        _motion.Double(_scale, ScaleTransform.ScaleXProperty, target.ScaleX, .3, true, spring: true);
        _motion.Double(_scale, ScaleTransform.ScaleYProperty, target.ScaleY, .3, true, spring: true);
        _motion.Double(_move, TranslateTransform.XProperty, target.MoveX, .3, true, spring: true);
        _motion.Double(_move, TranslateTransform.YProperty, target.MoveY, .3, true, spring: true);
        if (_overlay is not null) _motion.Double(_overlay, OpacityProperty, 0, .16, true);
        if (_morphBody is not null) _motion.Double(_morphBody, OpacityProperty, 0, .12, true);
        _motion.Double(_surface, OpacityProperty, 0, .1, true, delay: .2, completed: () =>
        {
            if (version == _version && !IsOpen) FinishClose(true);
        });
        return true;
    }
    private MorphGeometry? GeometryFrom(FrameworkElement origin)
    {
        if (_surface is null || _move is null || origin.ActualWidth <= 0 || origin.ActualHeight <= 0 || _surface.ActualWidth <= 0 || _surface.ActualHeight <= 0) return null;
        try
        {
            Rect source = Popover.ScreenBounds(origin);
            Rect current = Popover.ScreenBounds(_surface);
            var dpi = VisualTreeHelper.GetDpi(_surface);
            double baseX = (current.Left + current.Right) / 2 - _move.X * dpi.DpiScaleX;
            double baseY = (current.Top + current.Bottom) / 2 - _move.Y * dpi.DpiScaleY;
            return new MorphGeometry(source.Width / (_surface.ActualWidth * dpi.DpiScaleX),
                source.Height / (_surface.ActualHeight * dpi.DpiScaleY),
                ((source.Left + source.Right) / 2 - baseX) / dpi.DpiScaleX,
                ((source.Top + source.Bottom) / 2 - baseY) / dpi.DpiScaleY);
        }
        catch (InvalidOperationException) { return null; }
    }
    private void ClearMorphOrigin()
    {
        ++_morphGeneration;
        _morphOrigin = null;
        _morphBody = null;
        _morphPrepared = false;
        _morphQueued = false;
    }
}
