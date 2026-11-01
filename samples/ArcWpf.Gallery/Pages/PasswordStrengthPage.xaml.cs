using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;

// The registry demo: "Suggest one" cycles three passphrases and types them in one character every 42 ms (instantly with reduced motion);
// Save refuses below "Good" and puts the reason on the field.
public partial class PasswordStrengthPage : UserControl
{
    private static readonly string[] Suggestions = ["Juniper-Harbor-48", "Copper-Lantern-73", "Meadow-Signal-26"];
    private readonly DispatcherTimer _typing = new() { Interval = TimeSpan.FromMilliseconds(42) };
    private readonly Func<Task> _save = () => Task.Delay(900);
    private int _next, _typed;
    private string _target = "";
    private bool _typingNow;

    public PasswordStrengthPage()
    {
        InitializeComponent();
        _typing.Tick += (_, _) =>
        {
            _typed++;
            Type(_target[.._typed]);
            if (_typed >= _target.Length) _typing.Stop();
        };
    }

    private void Type(string value) { _typingNow = true; Field.Password = value; _typingNow = false; }

    private void PasswordEdited(object sender, RoutedEventArgs e)
    {
        if (_typingNow) return;
        _typing.Stop();
        Field.Error = "";
    }

    private void SuggestClick(object sender, RoutedEventArgs e)
    {
        _typing.Stop();
        _target = Suggestions[_next++ % Suggestions.Length];
        Field.Error = "";
        Field.IsRevealed = true;
        if (ArcMotion.GetLevel(this) != ArcMotionLevel.Full) { Type(_target); return; }
        _typed = 0;
        Type("");
        _typing.Start();
    }

    // Click runs before ActionButton reads Action, so a refused save never enters the pending phase.
    private void SaveClick(object sender, RoutedEventArgs e)
    {
        var strength = PasswordStrength.EstimateStrength(Field.Password);
        if (strength.Level >= 3) { Save.Action = _save; return; }
        Save.Action = null;
        Field.Error = Field.Password.Length == 0 ? "Enter a password first." : strength.Label == "Too short" ? "Too short to save. Use at least 8 characters." : "Too weak to save. Meet at least three requirements.";
    }
}
