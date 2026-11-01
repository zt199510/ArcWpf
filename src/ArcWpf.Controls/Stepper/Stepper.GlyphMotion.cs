using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace ArcWpf.Controls;

public partial class Stepper
{
    private void UpdateGlyph(ContentPresenter container, int index, int from, bool animate)
    {
        if (_items is null || _items.ItemTemplate.FindName("MarkText", container) is not TextBlock number ||
            _items.ItemTemplate.FindName("CheckPath", container) is not Path check ||
            _items.ItemTemplate.FindName("ErrorPath", container) is not Path error) return;
        if (_renderedMarks.Length != _views.Count) _renderedMarks = new string[_views.Count];
        var view = _views[index];
        string mark = view.Mark;
        bool changed = _renderedMarks[index] != mark;
        double delay = from < 0 ? 0 : CurrentIndex > from && index >= from && index < CurrentIndex
            ? (index - from) * .08 : CurrentIndex < from && index >= CurrentIndex && index < from
                ? (from - 1 - index) * .08 : 0;
        SetGlyph(number, !view.IsComplete && !view.IsError, changed, animate, delay);
        SetGlyph(check, view.IsComplete && !view.IsError, changed, animate, delay);
        SetGlyph(error, view.IsError, changed, animate, delay);
        _renderedMarks[index] = mark;
    }
    private void SetGlyph(FrameworkElement glyph, bool selected, bool changed, bool animate, double delay)
    {
        if (glyph.RenderTransform is not ScaleTransform scale || scale.IsFrozen)
            glyph.RenderTransform = scale = new ScaleTransform(1, 1);
        if (!selected)
        {
            _motion.Double(glyph, OpacityProperty, 0, 0, false);
            _motion.Double(scale, ScaleTransform.ScaleXProperty, .5, 0, false);
            _motion.Double(scale, ScaleTransform.ScaleYProperty, .5, 0, false);
            if (glyph is Path oldPath) _motion.Double(oldPath, Shape.StrokeDashOffsetProperty, 0, 0, false);
            return;
        }
        if (!changed && animate) return;
        bool entering = changed && animate;
        if (entering)
        {
            glyph.Opacity = 0;
            scale.ScaleX = scale.ScaleY = .5;
            if (glyph is Path freshPath) freshPath.StrokeDashOffset = 12;
        }
        _motion.Double(glyph, OpacityProperty, 1, .16, entering, delay: delay, enter: true);
        _motion.Double(scale, ScaleTransform.ScaleXProperty, 1, .26, entering, spring: entering, delay: delay);
        _motion.Double(scale, ScaleTransform.ScaleYProperty, 1, .26, entering, spring: entering, delay: delay);
        if (glyph is Path path)
            _motion.Double(path, Shape.StrokeDashOffsetProperty, 0, .24, entering, delay: delay + .04, enter: true);
    }
    private void UpdateText(ContentPresenter container, int index)
    {
        if (_items is null || _items.ItemTemplate.FindName("TitleText", container) is not TextBlock title ||
            _items.ItemTemplate.FindName("DescriptionText", container) is not TextBlock description) return;
        if (_renderedTitles.Length != _views.Count)
        {
            _renderedTitles = new string[_views.Count];
            _renderedDescriptions = new string[_views.Count];
        }
        string currentTitle = _views[index].Title;
        string currentDescription = _views[index].Description;
        bool descriptionChanged = _renderedDescriptions[index] != currentDescription;
        SetText(title, _renderedTitles[index] != currentTitle);
        SetText(description, descriptionChanged);
        if (_items.ItemTemplate.FindName("DescriptionSlot", container) is Border slot)
            UpdateDescriptionHeight(slot, description, index, descriptionChanged);
        _renderedTitles[index] = currentTitle;
        _renderedDescriptions[index] = currentDescription;
    }
    private void SetText(TextBlock text, bool changed)
    {
        if (text.RenderTransform is not TranslateTransform move || move.IsFrozen)
            text.RenderTransform = move = new TranslateTransform();
        if (text.Effect is not BlurEffect blur || blur.IsFrozen)
            text.Effect = blur = new BlurEffect();
        var level = ArcMotion.GetLevel(this);
        if (!changed && level == ArcMotionLevel.Full) return;
        bool animate = changed && _renderedIndex >= 0 && IsLoaded && level != ArcMotionLevel.Off && !string.IsNullOrEmpty(text.Text);
        bool full = level == ArcMotionLevel.Full;
        if (animate)
        {
            text.Opacity = 0;
            move.Y = full ? 4 : 0;
            blur.Radius = full ? 2 : 0;
        }
        double opacity = string.IsNullOrEmpty(text.Text) ? 0 : 1;
        _motion.Double(text, OpacityProperty, opacity, full ? .24 : .12, animate, enter: true);
        _motion.Double(move, TranslateTransform.YProperty, 0, .24, animate && full, enter: true);
        _motion.Double(blur, BlurEffect.RadiusProperty, 0, .24, animate && full, enter: true);
    }
    private void UpdateDescriptionHeight(Border slot, TextBlock text, int index, bool changed)
    {
        _descriptionSlots.Add(slot);
        if (_descriptionHeights.Length != _views.Count) _descriptionHeights = new double[_views.Count];
        double width = Math.Max(1, slot.ActualWidth > 0 ? slot.ActualWidth : text.ActualWidth);
        text.Measure(new Size(width, double.PositiveInfinity));
        double target = string.IsNullOrEmpty(text.Text) ? 0 : text.DesiredSize.Height;
        var level = ArcMotion.GetLevel(this);
        if (!changed || _renderedIndex < 0 || !IsLoaded || level != ArcMotionLevel.Full)
        {
            if (level != ArcMotionLevel.Full) { slot.BeginAnimation(HeightProperty, null); _descriptionTimelines.Remove(slot); }
            if (!slot.HasAnimatedProperties)
            {
                if (target <= .01) slot.Height = 0;
                else slot.ClearValue(HeightProperty);
            }
            _descriptionHeights[index] = target;
            return;
        }
        double from = double.IsNaN(slot.Height) ? _descriptionHeights[index] : slot.Height;
        // 基础值先交还自然高度，临时时钟结束后不会残留固定尺寸。
        slot.BeginAnimation(HeightProperty, null);
        _descriptionTimelines.Remove(slot);
        if (target <= .01) slot.Height = 0;
        else slot.ClearValue(HeightProperty);
        if (Math.Abs(target - from) >= .5)
        {
            var timeline = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(.4), FillBehavior = FillBehavior.Stop };
            var samples = OwnedMotion.SpringSamples;
            for (int sample = 0; sample < samples.Length; sample++)
                timeline.KeyFrames.Add(new LinearDoubleKeyFrame(from + (target - from) * samples[sample],
                    KeyTime.FromTimeSpan(TimeSpan.FromSeconds(.4 * sample / (samples.Length - 1)))));
            _descriptionTimelines[slot] = timeline;
            timeline.Completed += (_, _) =>
            {
                if (!_descriptionTimelines.TryGetValue(slot, out var active) || !ReferenceEquals(active, timeline)) return;
                slot.BeginAnimation(HeightProperty, null);
                _descriptionTimelines.Remove(slot);
                if (target <= .01) slot.Height = 0;
                else slot.ClearValue(HeightProperty);
            };
            slot.BeginAnimation(HeightProperty, timeline);
        }
        _descriptionHeights[index] = target;
    }
    private void StopDescriptionMotions()
    {
        foreach (var slot in _descriptionSlots)
        {
            slot.BeginAnimation(HeightProperty, null);
            if (slot.Child is TextBlock { Text.Length: > 0 }) slot.ClearValue(HeightProperty);
            else slot.Height = 0;
        }
        _descriptionSlots.Clear();
        _descriptionTimelines.Clear();
    }
}
