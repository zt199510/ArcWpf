using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcWpf.Controls;

// date-range-picker.tsx Rolling: words roll one by one in their own slots; a leaving word pops out of flow
// (here into a zero-size Canvas) so the next one never waits for space. Words that stay the same hold still.
internal sealed class RollingText : StackPanel
{
    private readonly List<(Grid Slot, Canvas Leaving, TextBlock Word)> _slots = [];
    public string Text { get; private set; } = "";

    public RollingText() { Orientation = Orientation.Horizontal; }

    public void Set(string text, int direction, ArcMotionLevel level, bool animate)
    {
        if (text == Text && _slots.Count > 0) return;
        Text = text;
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        bool motion = animate && IsLoaded && level != ArcMotionLevel.Off;
        while (_slots.Count > words.Length)
        {
            var last = _slots[^1];
            Children.Remove(last.Slot);
            _slots.RemoveAt(_slots.Count - 1);
        }
        for (int i = 0; i < words.Length; i++)
        {
            if (i < _slots.Count)
            {
                var (slot, leaving, old) = _slots[i];
                if (old.Text == words[i]) continue;
                var word = Word(words[i]);
                slot.Children.Add(word);
                _slots[i] = (slot, leaving, word);
                slot.Children.Remove(old);
                if (!motion) continue;
                leaving.Children.Add(old);
                Roll(word, old, direction, level, () => leaving.Children.Remove(old));
                continue;
            }
            var leavingLayer = new Canvas { ClipToBounds = false };
            var fresh = Word(words[i]);
            var newSlot = new Grid { Margin = new Thickness(i == 0 ? 0 : .28 * Em, 0, 0, 0), Children = { leavingLayer, fresh } };
            Children.Add(newSlot);
            _slots.Add((newSlot, leavingLayer, fresh));
            if (motion) Roll(fresh, null, direction, level, null);
        }
    }

    private double Em => System.Windows.Documents.TextElement.GetFontSize(this);

    private static TextBlock Word(string text) => new() { Text = text, RenderTransform = new TranslateTransform() };

    // A later range rises from below, an earlier one drops from above; reduced motion uses short fades.
    private void Roll(TextBlock word, TextBlock? old, int direction, ArcMotionLevel level, Action? done)
    {
        double em = Em;
        if (level == ArcMotionLevel.Full)
        {
            TimePicker.Fly(word, 0, 1, direction * .45 * em, 0, 2, 0, .26, ArcEasing.Enter, null);
            if (old is not null) TimePicker.Fly(old, 1, 0, 0, direction * -.45 * em, 0, 2, .14, ArcEasing.Standard, done);
        }
        else
        {
            TimePicker.Fly(word, 0, 1, 0, 0, 0, 0, .12, ArcEasing.Standard, null);
            if (old is not null) TimePicker.Fly(old, 1, 0, 0, 0, 0, 0, .08, ArcEasing.Standard, done);
        }
    }
}
