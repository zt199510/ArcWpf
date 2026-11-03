using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class BillingTogglePage : UserControl
{
    private readonly List<(BillingPrice Price, TextBlock Note, double Monthly, double Yearly)> _plans = [];

    public BillingTogglePage()
    {
        InitializeComponent();
        Toggle.Options = new[]
        {
            new BillingToggleOption { Value = "monthly", Label = "Monthly" },
            new BillingToggleOption { Value = "yearly", Label = "Yearly", Badge = "Save 20%", ActiveBadge = "Saving 20%" },
        };
        Add("Solo", "One person, every device", 10, 8);
        Add("Team", "Shared spaces for up to 20", 20, 16);
        Add("Company", "SSO, audit log, and admin roles", 40, 32);
        Apply();
    }

    private void Add(string name, string detail, double monthly, double yearly)
    {
        var title = new TextBlock { Text = name, FontSize = 14, FontWeight = FontWeights.Medium, LineHeight = 19.6, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, Margin = new Thickness(0, 0, 0, 2) };
        title.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.Foreground");
        var sub = new TextBlock { Text = detail, FontSize = 12, LineHeight = 16.8, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, TextTrimming = TextTrimming.CharacterEllipsis };
        sub.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
        var price = new BillingPrice { Currency = "$", Period = "/ mo", AmountSize = 28, HorizontalAlignment = HorizontalAlignment.Right };
        var note = new TextBlock { FontSize = 12, LineHeight = 16.8, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, Margin = new Thickness(0, 2, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
        note.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
        var right = new StackPanel { Children = { price, note }, VerticalAlignment = VerticalAlignment.Top };
        DockPanel.SetDock(right, Dock.Right);
        var row = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 16, 0, _plans.Count == 2 ? 0 : 16), Child = new DockPanel { Children = { right, new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { title, sub } } } } };
        row.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderSubtle");
        Rows.Children.Add(row);
        _plans.Add((price, note, monthly, yearly));
    }

    private void OnPeriod(object? sender, EventArgs e) => Apply();

    private void Apply()
    {
        bool yearly = Toggle.Value == "yearly";
        foreach (var (price, note, monthly, year) in _plans)
        {
            price.Amount = yearly ? year : monthly;
            price.Was = yearly ? monthly : null;
            note.Text = yearly ? $"${year * 12:0} billed yearly" : "Billed monthly";
        }
    }
}
