using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using ArcWpf.Controls;
using ArcButton = ArcWpf.Controls.Button;
namespace ArcWpf.Gallery.Pages;

// The registry rating demo: a rating of 3 or less asks for a note after 240 ms, higher thanks the person; "Not now" skips, a sent note can be edited.
public partial class RatingPage : UserControl
{
    private readonly DispatcherTimer _settle = new() { Interval = TimeSpan.FromMilliseconds(240) };
    private string? _tone;
    private string _phase = "auto", _note = "";
    private string? _shown;

    public RatingPage()
    {
        InitializeComponent();
        _settle.Tick += (_, _) => { _settle.Stop(); _tone = Handoff.Value <= 3 ? "low" : "high"; Render(); };
    }

    private void RatingChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _settle.Stop();
        if (e.NewValue == 0) { _tone = null; _phase = "auto"; _note = ""; Render(); return; }
        _settle.Start();
    }

    private string? View => _tone is null ? null : _phase is "sent" or "note" ? _phase : _phase == "skipped" ? "thanks" : _tone == "low" || _note.Trim().Length > 0 ? "note" : "thanks";

    private void Render()
    {
        var view = View;
        if (view == _shown) return;
        _shown = view;
        foreach (UIElement old in Follow.Children.Cast<UIElement>().ToArray()) Follow.Children.Remove(old);
        if (view is null) return;
        // .ratingDemoView: padding 31 4 4 with a hairline 16 below the stars; it settles in from y -8 with a subtle blur.
        var body = new Grid { Margin = new Thickness(4, 31, 4, 4) };
        var rule = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(4, 16, 4, 0) };
        rule.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.BorderSubtle");
        Follow.Children.Add(rule);
        body.Children.Add(view switch
        {
            "thanks" => Line(new Run("Thanks, Emma will see your rating"), "Add a note", () => Go("note")),
            "sent" => Line(new Bold(new Run("Note sent to Emma")) { FontWeight = FontWeights.Medium }, "Edit note", () => Go("note")),
            _ => Note(),
        });
        Follow.Children.Add(body);
        body.Opacity = 0;
        body.RenderTransform = new TranslateTransform(0, -8);
        var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
        body.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(1, TimeSpan.FromSeconds(.24)) { BeginTime = TimeSpan.FromSeconds(.06), EasingFunction = ease });
        ((TranslateTransform)body.RenderTransform).BeginAnimation(TranslateTransform.YProperty, new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromSeconds(.5)) { BeginTime = TimeSpan.FromSeconds(.06), EasingFunction = ease });
    }

    private void Go(string phase) { _phase = phase; Render(); }

    private static FrameworkElement Line(Inline copy, string action, Action click)
    {
        var row = new DockPanel();
        var button = new ArcButton { Content = action, Variant = ButtonVariant.Ghost, Size = ControlSize.Small, Margin = new Thickness(12, 0, 0, 0) };
        button.Click += (_, _) => click();
        DockPanel.SetDock(button, Dock.Right);
        row.Children.Add(button);
        var text = new TextBlock(copy) { VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, FontSize = 14 };
        text.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextSecondary");
        row.Children.Add(text);
        return row;
    }

    private FrameworkElement Note()
    {
        var form = new StackPanel();
        var field = new Textarea { Label = _phase == "auto" ? "What would have made it smoother?" : "Anything Emma should know?", Placeholder = "Missing states, unclear specs, timing", MinLines = 3, Text = _note };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var skip = new ArcButton { Content = "Not now", Variant = ButtonVariant.Ghost, Size = ControlSize.Small, Margin = new Thickness(0, 0, 8, 0) };
        var send = new ArcButton { Content = "Send note", Size = ControlSize.Small, IsEnabled = _note.Trim().Length > 0 };
        field.TextChanged += (_, _) => { _note = field.Text; send.IsEnabled = _note.Trim().Length > 0; };
        skip.Click += (_, _) => Go("skipped");
        send.Click += (_, _) => { if (_note.Trim().Length > 0) Go("sent"); };
        actions.Children.Add(skip);
        actions.Children.Add(send);
        form.Children.Add(field);
        form.Children.Add(actions);
        field.Loaded += (_, _) => field.Focus();
        return form;
    }
}
