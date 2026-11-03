using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class CommandMenuPage : UserControl
{
    public CommandMenuPage()
    {
        InitializeComponent();
        foreach (var (id, label, description, group, icon, shortcut) in new (string, string, string, string, string, string?)[]
        {
            ("new-project", "New project", "Start from a blank setup", "Create", "plus", "N"),
            ("new-document", "New document", "Write a brief or a spec", "Create", "file-text", "⇧ N"),
            ("invite", "Invite teammate", "Send an invite by email", "Create", "user-plus", null),
            ("edit", "Edit project", "Update the project details", "Project", "pencil", "E"),
            ("duplicate", "Duplicate project", "Create a copy with the same setup", "Project", "copy", "D"),
            ("share", "Share project", "Invite someone to collaborate", "Project", "share-2", "S"),
            ("copy-link", "Copy share link", "Anyone with the link can view", "Project", "link", "⇧ C"),
            ("download", "Download brief", "Save the project brief as a PDF", "Project", "download", null),
            ("archive", "Archive project", "Move this project out of the workspace", "Project", "archive", null),
            ("theme", "Toggle theme", "Switch between light and dark", "Preferences", "sun-moon", "T"),
            ("accent", "Change accent color", "Pick the color for buttons and links", "Preferences", "palette", null),
        })
        {
            Stage.Items.Add(new CommandMenuItem { Id = id, Label = label, Description = description, Group = group, Icon = icon, Shortcut = shortcut });
        }
    }
}
