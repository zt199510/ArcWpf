using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class LiquidTabBarPage : UserControl
{
    private static readonly Dictionary<string, string> Notes = new() { ["home"] = "Your feed", ["saved"] = "3 saved places", ["inbox"] = "2 unread messages", ["profile"] = "Your profile" };

    public LiquidTabBarPage()
    {
        InitializeComponent();
        Bar.Tabs =
        [
            new() { Id = "home", Text = "Home", Icon = "house" },
            new() { Id = "saved", Text = "Saved", Icon = "bookmark" },
            new() { Id = "inbox", Text = "Inbox", Icon = "message-circle", Count = 2 },
            new() { Id = "profile", Text = "Profile", Icon = "user" },
        ];
    }

    private void OnTab(object? sender, string id)
    {
        Note.Text = Notes[id];
        Note.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, new Duration(System.TimeSpan.FromSeconds(.24))));
    }

    private void OnMode(object sender, SelectionChangedEventArgs e)
    {
        if (Bar is null) { return; }
        Bar.ShowLabels = Modes.SelectedValue as string switch { "all" => LiquidTabLabels.All, "none" => LiquidTabLabels.None, _ => LiquidTabLabels.Selected };
    }
}
