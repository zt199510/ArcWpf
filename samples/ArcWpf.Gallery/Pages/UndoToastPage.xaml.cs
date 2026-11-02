using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class UndoToastPage : UserControl
{
    public sealed record Thread(string From, string Subject, string Preview, string Time, bool Unread, Brush Avatar)
    {
        public string Label => $"{From}, {Subject}";
    }
    private readonly ObservableCollection<Thread> _threads = new();
    public UndoToastPage()
    {
        InitializeComponent();
        Toast.KeySelector = item => ((Thread)item).From;
        Toast.RowShortcuts[Key.E] = "archive";
        Toast.RowShortcuts[Key.Back] = "delete";
        Toast.RowShortcuts[Key.Delete] = "delete";
        Toast.ItemsSource = _threads;
        Fill();
    }
    private static Brush Paint(string a, string b) => new LinearGradientBrush((Color)ColorConverter.ConvertFromString(a), (Color)ColorConverter.ConvertFromString(b), 45);
    private void Fill()
    {
        _threads.Clear();
        _threads.Add(new("Emma Collins", "Brand review moved to Thursday", "I pushed it to 2 pm so Daniel can join from Lisbon.", "9:41", true, Paint("#6D5FD8", "#D9743A")));
        _threads.Add(new("Daniel Kim", "Invoice 2041 for September", "Updated with the corrected VAT number, same total.", "9:12", true, Paint("#2DD4CF", "#B48A8A")));
        _threads.Add(new("Sofia Ramirez", "Offsite rooms are confirmed", "All twelve rooms are held until Friday at noon.", "8:30", false, Paint("#E4B866", "#4C8BE8")));
        _threads.Add(new("Marcus Johnson", "Notes from the checkout crit", "Three small fixes and one bigger question about tax.", "Mon", false, Paint("#35C98F", "#E55A7B")));
        _threads.Add(new("Olivia Bennett", "Signed agreement attached", "Countersigned and filed. Welcome aboard.", "Sun", false, Paint("#A964EE", "#B0A79A")));
        _threads.Add(new("Nathan Cole", "Access to the design repo", "Approved for read access, ping me for write.", "Sat", false, Paint("#5AB98C", "#2FD8BA")));
    }
    private void RefillClick(object sender, RoutedEventArgs e)
    {
        Fill();
        Refill.Visibility = Visibility.Hidden;
    }
    private void Committed(object? sender, System.Collections.Generic.IReadOnlyList<UndoToastEntry> entries) => Refill.Visibility = Visibility.Visible;
}
