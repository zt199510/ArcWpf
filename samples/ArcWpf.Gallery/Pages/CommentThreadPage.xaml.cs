using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class CommentThreadPage : UserControl
{
    private static readonly CommentAuthor Emma = new() { Id = "emma", Name = "Emma Collins" }, Jasmine = new() { Id = "jasmine", Name = "Jasmine Brooks" }, Marcus = new() { Id = "marcus", Name = "Marcus Johnson" }, Daniel = new() { Id = "daniel", Name = "Daniel Kim" }, Chloe = new() { Id = "chloe", Name = "Chloe Nguyen" };

    public CommentThreadPage()
    {
        InitializeComponent();
        Thread.CurrentUser = Emma;
        Thread.People = [Emma, Jasmine, Marcus, Daniel, Chloe];
        Thread.Comments = Start();
        Thread.CommentsChanged += (_, e) => Thread.Comments = e.Comments;
    }

    private void OnReset(object sender, RoutedEventArgs e)
    {
        Thread.IsResolved = false;
        Thread.Comments = Start();
    }

    private static IReadOnlyList<ThreadComment> Start() =>
    [
        new ThreadComment
        {
            Id = "c1", Author = Jasmine, CreatedAt = "2h", Body = "Can we try a shorter headline here? @Emma Collins had a version that fit on one line at 390 px.",
            Reactions = [new CommentReaction("👍", ["emma", "marcus"])],
            Replies =
            [
                new ThreadComment { Id = "c2", Author = Emma, CreatedAt = "1h", Body = "Yes, “Plan less. Ship more.” Dropping it into the file now.", Reactions = [new CommentReaction("🎉", ["jasmine"])] },
                new ThreadComment { Id = "c3", Author = Marcus, CreatedAt = "48m", Body = "Works for me. It also saves the second line on mobile." },
            ],
        },
        new ThreadComment { Id = "c4", Author = Daniel, CreatedAt = "30m", Body = "Once this lands I'll update the social image copy to match. @Chloe Nguyen can you check the click through numbers after?", Reactions = [new CommentReaction("👀", ["chloe"])] },
    ];
}
