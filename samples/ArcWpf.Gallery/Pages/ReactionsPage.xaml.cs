using System.Windows.Controls; using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class ReactionsPage : UserControl { public ReactionsPage(){InitializeComponent(); Main.Options.Add(new ReactionOption{Id="👍",Label="Like"}); Main.Options.Add(new ReactionOption{Id="❤️",Label="Love"}); Main.Options.Add(new ReactionOption{Id="🎉",Label="Celebrate"}); Main.Values.Add(new Reaction{Id="👍",Count=3}); Status.Text="3 reactions";} private void Changed(object? s,ReactionChangedEventArgs e)=>Status.Text=$"{e.Reactions.Sum(r=>r.Count)} reactions"; }
