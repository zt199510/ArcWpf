using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class MentionInputPage : UserControl
{
    public MentionInputPage()
    {
        InitializeComponent();
        foreach (var (id, name, role, from, to) in new[]
        {
            ("emma", "Emma Collins", "Product designer", "#7A5CE0", "#E9A15B"), ("marcus", "Marcus Johnson", "Frontend engineer", "#35C7A0", "#F0628C"),
            ("jasmine", "Jasmine Brooks", "Design lead", "#6FD8A0", "#E96FD0"), ("olivia", "Olivia Bennett", "Platform engineer", "#C0B8A0", "#8A5CE0"),
            ("sofia", "Sofia Ramirez", "Operations lead", "#E3C24A", "#4C6FE0"), ("ryan", "Ryan Sullivan", "Account executive", "#35C7A0", "#E96F9A"),
        })
            Input.People.Add(new MentionPerson { Id = id, Name = name, Role = role, Avatar = Portrait(from, to) });
        Input.Channels.Add(new MentionChannel { Id = "design", Name = "design", Description = "Design reviews and critique" });
        Input.Channels.Add(new MentionChannel { Id = "launch", Name = "launch", Description = "Launch planning" });
        Input.Channels.Add(new MentionChannel { Id = "support", Name = "support", Description = "Customer questions" });
        Input.TextChanged += (_, _) =>
        {
            Post.IsEnabled = Input.Text.Trim().Length > 0;
            int people = 0;
            foreach (var token in Input.Mentions) if (token.Kind == MentionKind.Person) people++;
            Notify.Text = people == 0 ? "Enter to post" : $"Notifies {people} {(people == 1 ? "person" : "people")}";
        };
        Input.Submitted += (_, _) => Send();
    }
    private void OnAt(object sender, RoutedEventArgs e) => Input.InsertTrigger('@');
    private void OnHash(object sender, RoutedEventArgs e) => Input.InsertTrigger('#');
    private void OnPost(object sender, RoutedEventArgs e) => Send();
    private void Send()
    {
        if (Input.Text.Trim().Length == 0) return;
        Input.Clear();
    }
    // The live demo's portraits are its own images, so these are neutral gradient placeholders.
    private static ImageSource Portrait(string from, string to)
    {
        var image = new DrawingImage(new GeometryDrawing(new LinearGradientBrush((Color)ColorConverter.ConvertFromString(from), (Color)ColorConverter.ConvertFromString(to), 45), null, new RectangleGeometry(new Rect(0, 0, 28, 28))));
        image.Freeze();
        return image;
    }
}
