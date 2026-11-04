using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class InViewTitlePage : UserControl
{
    private static readonly Dictionary<string, string> Notes = new()
    {
        ["Blur"] = "Words sharpen into place one after another.",
        ["Word"] = "Each word rises out of its own line.",
        ["Line"] = "Whole lines lift into view from top to bottom.",
        ["Tracking"] = "Letters open from tight spacing to their resting rhythm.",
        ["Wipe"] = "The title is uncovered from left to right.",
    };

    public InViewTitlePage()
    {
        InitializeComponent();
        Title.Lines = ["Make progress", "visible"];
    }

    private void OnReplay(object sender, RoutedEventArgs e) => Title.Replay();

    private void OnVariant(object sender, SelectionChangedEventArgs e)
    {
        if (Title is null || Variants.SelectedValue is not string tag) { return; }
        Title.Variant = Enum.Parse<InViewTitleVariant>(tag);
        Note.Text = Notes[tag];
        Title.Replay();
    }
}
