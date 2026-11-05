using System.Windows.Controls;
using ArcWpf.AiAssistant.Backend;
using ArcWpf.Controls;

namespace ArcWpf.AiAssistant;

/// <summary>What a chat keeps while another one is on screen.</summary>
internal sealed class Conversation(ConversationItem item, Lazy<IAssistantSession> session)
{
    public ConversationItem Item { get; } = item;
    public IAssistantSession Session => session.Value;
    public StackPanel Thread { get; } = new();
    public List<ArtifactItem> Artifacts { get; } = [];
    public List<PromptAttachment> Files { get; } = [];
    public List<PromptAttachment> DraftFiles { get; } = [];
    public string Draft { get; set; } = "";
    public bool PaneOpen { get; set; }

    public TurnView? Last => Thread.Children.Count > 0 ? (TurnView)Thread.Children[Thread.Children.Count - 1] : null;
    public bool IsEmpty => Thread.Children.Count == 0;
}
