using System.Windows.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class CardStackPage : UserControl
{
    public sealed record Trip(string Code, string Dates, string Name, string Country, string Pitch, string Travel, string Weather, string Price);

    private readonly System.Collections.Generic.List<string> _shortlist = [];

    public CardStackPage()
    {
        InitializeComponent();
        Stack.LabelSelector = item => ((Trip)item).Name;
        Stack.Decided += (_, e) => { if (e.Decision == ArcWpf.Controls.CardDecision.Right) { _shortlist.Add(((Trip)e.Item).Name); } };
        Stack.Undone += (_, e) => _shortlist.Remove(((Trip)e.Item).Name);
        Stack.ResetCompleted += (_, _) => _shortlist.Clear();
        Stack.EmptyText = () => _shortlist.Count == 0 ? ("Nothing shortlisted", "Every destination was skipped.") : ("Shortlist ready", string.Join(", ", _shortlist));
        Stack.ItemsSource = new object[]
        {
            new Trip("LIS", "14\u201317 Oct", "Lisbon", "Portugal", "Tiled hills, long lunches, and a riverside workshop space for 18.", "3h 35m direct from Berlin", "22\u00B0 and mostly sunny", "From \u20AC780 per person"),
            new Trip("CPH", "21\u201324 Oct", "Copenhagen", "Denmark", "Harbour saunas, bikes for everyone, and a studio loft in N\u00F8rrebro.", "1h 10m direct from Berlin", "11\u00B0 with light rain", "From \u20AC1,140 per person"),
            new Trip("OPO", "14\u201317 Oct", "Porto", "Portugal", "A river-view warehouse by day and dinners in Ribeira by night.", "3h 20m direct from Berlin", "19\u00B0 and clear", "From \u20AC690 per person"),
            new Trip("KIX", "3\u20139 Nov", "Kyoto", "Japan", "Temple mornings, ryokan evenings, and a long flight to earn them.", "15h 40m with one stop", "17\u00B0 and dry", "From \u20AC2,380 per person"),
            new Trip("EDI", "28\u201331 Oct", "Edinburgh", "Scotland", "Old Town walks and a converted church for the team sessions.", "2h 05m direct from Berlin", "10\u00B0 and breezy", "From \u20AC920 per person"),
            new Trip("SVQ", "21\u201324 Oct", "Seville", "Spain", "Courtyard sessions under orange trees and very late tapas.", "3h 30m direct from Berlin", "26\u00B0 and sunny", "From \u20AC740 per person"),
        };
    }
}
