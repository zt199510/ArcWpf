using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace ArcWpf.Controls;

/// <summary>
/// One row of the default <see cref="TreeView"/> template. It runs the row's own transitions (hover and selection
/// color, the chevron turn, the folder glyph swap) and reports itself to UI Automation as a tree item.
/// Named parts it drives when present: Hover, Label, LabelStrong, FolderBase, FolderStrong, Folder, FolderOpen, Chevron.
/// </summary>
public sealed class TreeViewRow : System.Windows.Controls.Button
{
    private TreeRow? _row;
    private bool _shownExpanded, _shownSelected;

    internal TreeRow? Row => _row;

    public TreeViewRow()
    {
        DataContextChanged += (_, e) => Attach(e.NewValue as TreeRow);
        // The parts exist once the row template has been laid out, so a realized row lands on its state here.
        Loaded += (_, _) => { Attach(DataContext as TreeRow); Snap(); };
        Unloaded += (_, _) => { Detach(); Stop(); };
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TreeViewRowAutomationPeer(this);

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsMouseOverProperty) Paint(Moving);
        else if (e.Property == ArcMotion.LevelProperty && ArcMotion.GetLevel(this) != ArcMotionLevel.Full) Stop();
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        Snap();
    }

    private bool Moving => IsLoaded && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;

    private void Attach(TreeRow? row)
    {
        if (ReferenceEquals(row, _row)) return;
        Detach();
        _row = row;
        if (_row is not null) _row.PropertyChanged += RowChanged;
        Snap();
    }

    private void Detach()
    {
        if (_row is not null) _row.PropertyChanged -= RowChanged;
        _row = null;
    }

    private void RowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(TreeRow.IsExpanded) or nameof(TreeRow.IsSelected))) return;
        bool expanded = _shownExpanded, selected = _shownSelected;
        Paint(Moving);
        if (UIElementAutomationPeer.FromElement(this) is not { } peer) return;
        if (expanded != _shownExpanded && AutomationPeer.ListenerExists(AutomationEvents.PropertyChanged))
            peer.RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty, State(expanded), State(_shownExpanded));
        if (selected != _shownSelected && AutomationPeer.ListenerExists(AutomationEvents.PropertyChanged))
            peer.RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, selected, _shownSelected);
    }

    internal ExpandCollapseState State(bool expanded) => _row is not { HasChildren: true } ? ExpandCollapseState.LeafNode : expanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

    /// <summary>Lands every part on the row's current state without motion.</summary>
    private void Snap()
    {
        Stop();
        _shownExpanded = _row?.IsExpanded ?? false;
        _shownSelected = _row?.IsSelected ?? false;
        Paint(false);
        if (Part("Chevron") is { } chevron) chevron.RenderTransform = new RotateTransform(_shownExpanded ? 90 : 0);
        if (Part("Folder") is { } folder) { folder.Opacity = _shownExpanded ? 0 : 1; folder.RenderTransform = Transform.Identity; folder.Effect = null; }
        if (Part("FolderOpen") is { } open) { open.Opacity = _shownExpanded ? 1 : 0; open.RenderTransform = Transform.Identity; open.Effect = null; }
    }

    private void Paint(bool animate)
    {
        bool selected = _row?.IsSelected ?? false, expanded = _row?.IsExpanded ?? false;
        // .item:hover and [aria-selected]: surface-muted fill, foreground label; a selected closed folder turns foreground too.
        TreeRowMotion.Fade(Part("Hover"), IsMouseOver ? 1 : 0, Tween.Fast, animate);
        bool strong = IsMouseOver || selected;
        TreeRowMotion.Fade(Part("LabelStrong"), strong ? 1 : 0, Tween.Fast, animate);
        TreeRowMotion.Fade(Part("Label"), strong ? 0 : 1, Tween.Fast, animate);
        TreeRowMotion.Fade(Part("FolderStrong"), selected ? 1 : 0, Tween.Fast, animate);
        TreeRowMotion.Fade(Part("FolderBase"), selected ? 0 : 1, Tween.Fast, animate);
        _shownSelected = selected;
        if (expanded == _shownExpanded) return;
        _shownExpanded = expanded;
        TreeRowMotion.Turn(Part("Chevron"), expanded ? 90 : 0, animate);
        TreeRowMotion.Swap(Part(expanded ? "Folder" : "FolderOpen"), Part(expanded ? "FolderOpen" : "Folder"), animate);
    }

    internal void Stop()
    {
        foreach (var name in (string[])["Hover", "Label", "LabelStrong", "FolderBase", "FolderStrong", "Folder", "FolderOpen", "Chevron", "Selection", "Branch"])
            if (Part(name) is { } part) TreeRowMotion.Settle(part);
    }

    internal FrameworkElement? Part(string name) => TreeRowMotion.Find(this, name);
}

/// <summary>tree-view.tsx motion for one row, on parts the row and its container own.</summary>
internal static class TreeRowMotion
{
    // motion-tokens stagger.item.
    public const double ItemStagger = .035;
    private static readonly DependencyProperty[] Owned = [UIElement.OpacityProperty, FrameworkElement.HeightProperty];

    /// <summary>A delayed tween: holds <paramref name="from"/> through the delay, then eases to <paramref name="to"/>; the base value takes over when it ends.</summary>
    private static AnimationTimeline Frames(double from, double to, double delay, double seconds, IEasingFunction ease, FillBehavior fill = FillBehavior.Stop)
    {
        var timeline = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(delay + seconds), FillBehavior = fill };
        timeline.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        if (delay > 0) timeline.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(delay))));
        timeline.KeyFrames.Add(new EasingDoubleKeyFrame(to, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(delay + seconds)), ease));
        return timeline;
    }

    // A critically damped spring with this visual duration, sampled over twice that window as Tween.Spring does.
    private static AnimationTimeline Spring(double from, double to, double visualDuration, double delay = 0, FillBehavior fill = FillBehavior.Stop)
        => Frames(from, to, delay, visualDuration * 2, ArcEasing.Spring(visualDuration, visualDuration * 2), fill);

    /// <summary>Sets the opacity a part rests at; with <paramref name="animate"/> it eases there from where it is now.</summary>
    public static void Fade(FrameworkElement? part, double to, double seconds, bool animate)
    {
        if (part is null) return;
        double from = part.Opacity;
        part.BeginAnimation(UIElement.OpacityProperty, null);
        part.Opacity = to;
        if (animate && Math.Abs(from - to) > .001) part.BeginAnimation(UIElement.OpacityProperty, Frames(from, to, 0, seconds, ArcEasing.Standard));
    }

    /// <summary>The chevron turns on the snappy spring from wherever it is.</summary>
    public static void Turn(FrameworkElement? chevron, double angle, bool animate)
    {
        if (chevron is null) return;
        double from = chevron.RenderTransform is RotateTransform current ? current.Angle : angle;
        var rotate = new RotateTransform(angle);
        chevron.RenderTransform = rotate;
        if (animate && Math.Abs(from - angle) > .01) rotate.BeginAnimation(RotateTransform.AngleProperty, Spring(from, angle, Tween.Snappy));
    }

    /// <summary>FolderIcon popLayout: the new glyph scales up from .6 out of a CSS 2px blur (radius 4, as ArcEffects maps it) while the old one shrinks and fades.</summary>
    public static void Swap(FrameworkElement? outgoing, FrameworkElement? incoming, bool animate)
    {
        if (outgoing is not null)
        {
            double from = outgoing.Opacity;
            Settle(outgoing); outgoing.Opacity = 0;
            if (animate && from > .001)
            {
                outgoing.BeginAnimation(UIElement.OpacityProperty, Frames(from, 0, 0, Tween.Instant, ArcEasing.Standard));
                Scale(outgoing, Frames(1, .6, 0, Tween.Instant, ArcEasing.Standard));
                Blur(outgoing, 0, 4, Tween.Instant, ArcEasing.Standard);
            }
        }
        if (incoming is not null)
        {
            double from = incoming.Opacity;
            Settle(incoming); incoming.Opacity = 1;
            if (animate && from < .999)
            {
                incoming.BeginAnimation(UIElement.OpacityProperty, Frames(from, 1, 0, Tween.Fast, ArcEasing.Enter));
                Scale(incoming, Spring(.6, 1, Tween.Snappy));
                Blur(incoming, 4, 0, Tween.Fast, ArcEasing.Enter);
            }
        }
    }

    private static void Scale(FrameworkElement part, AnimationTimeline timeline)
    {
        var scale = new ScaleTransform();
        part.RenderTransformOrigin = new Point(.5, .5);
        part.RenderTransform = scale;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, timeline);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, timeline);
    }

    private static void Blur(FrameworkElement part, double from, double to, double seconds, IEasingFunction ease)
    {
        var blur = new BlurEffect { Radius = to };
        part.Effect = blur;
        var timeline = Frames(from, to, 0, seconds, ease);
        // The effect goes once the blur ends, so a resting glyph is never rendered through it.
        timeline.Completed += (_, _) => { if (ReferenceEquals(part.Effect, blur)) part.Effect = null; };
        blur.BeginAnimation(BlurEffect.RadiusProperty, timeline);
    }

    /// <summary>Drops a part's running motion; its resting values stay.</summary>
    public static void Settle(FrameworkElement part)
    {
        part.BeginAnimation(UIElement.OpacityProperty, null);
        if (part.RenderTransform is { IsFrozen: false } transform)
        {
            foreach (var property in (DependencyProperty[])[RotateTransform.AngleProperty, ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty, TranslateTransform.XProperty, TranslateTransform.YProperty])
                if (property.OwnerType.IsInstanceOfType(transform)) transform.BeginAnimation(property, null);
        }
        if (part.RenderTransform is ScaleTransform or TranslateTransform) part.RenderTransform = Transform.Identity;
        part.Effect = null;
    }

    /// <summary>
    /// A mounting row: its height opens on the smooth spring while the row fades in and slides 6 DIPs from the left after
    /// its stagger, and its branch line draws down. The container is the ItemsControl's own presenter, so nothing here
    /// touches a template or consumer value.
    /// </summary>
    public static void Enter(TreeRow row, FrameworkElement container, double delay)
    {
        double height = container.ActualHeight;
        if (height <= 0) return;
        row.NaturalHeight = height;
        var token = row.Motion = new object();
        container.ClipToBounds = true;
        var open = Spring(0, height, Tween.Smooth);
        open.Completed += (_, _) => { if (ReferenceEquals(row.Motion, token)) container.ClipToBounds = false; };
        container.BeginAnimation(FrameworkElement.HeightProperty, open);
        container.BeginAnimation(UIElement.OpacityProperty, Frames(0, 1, delay, Tween.Fast, ArcEasing.Enter));
        var slide = new TranslateTransform();
        container.RenderTransform = slide;
        slide.BeginAnimation(TranslateTransform.XProperty, Spring(-6, 0, Tween.Smooth, delay));
        if (row.Depth > 1 && Find(container, "Branch") is { Visibility: Visibility.Visible } branch)
        {
            var draw = new ScaleTransform();
            branch.RenderTransformOrigin = new Point(0, 0);
            branch.RenderTransform = draw;
            draw.BeginAnimation(ScaleTransform.ScaleYProperty, Frames(0, 1, delay, Tween.Standard, ArcEasing.Enter));
            branch.BeginAnimation(UIElement.OpacityProperty, Frames(0, 1, delay, Tween.Standard, ArcEasing.Enter));
        }
    }

    /// <summary>A row whose branch closed: it fades and shifts 4 DIPs left while its height folds, then <paramref name="done"/> removes it.</summary>
    public static void Leave(TreeRow row, FrameworkElement container, Action done)
    {
        double height = container.ActualHeight;
        if (!container.HasAnimatedProperties) row.NaturalHeight = height;
        var token = row.Motion = new object();
        container.IsHitTestVisible = false;
        container.ClipToBounds = true;
        var fold = Spring(height, 0, Tween.Smooth, fill: FillBehavior.HoldEnd);
        fold.Completed += (_, _) => { if (ReferenceEquals(row.Motion, token)) done(); };
        container.BeginAnimation(FrameworkElement.HeightProperty, fold);
        container.BeginAnimation(UIElement.OpacityProperty, Frames(container.Opacity, 0, 0, Tween.Instant, ArcEasing.Standard, FillBehavior.HoldEnd));
        var slide = container.RenderTransform is TranslateTransform { IsFrozen: false } current ? current : new TranslateTransform();
        double x = slide.X;
        container.RenderTransform = slide;
        slide.BeginAnimation(TranslateTransform.XProperty, Frames(x, -4, 0, Tween.Instant, ArcEasing.Standard, FillBehavior.HoldEnd));
    }

    /// <summary>A leaving row whose branch opened again before it was gone grows back from where it is.</summary>
    public static void Revive(TreeRow row, FrameworkElement container)
    {
        double height = container.ActualHeight, opacity = container.Opacity, target = row.NaturalHeight > 0 ? row.NaturalHeight : height;
        var token = row.Motion = new object();
        container.IsHitTestVisible = true;
        var open = Spring(height, target, Tween.Smooth);
        open.Completed += (_, _) => { if (ReferenceEquals(row.Motion, token)) container.ClipToBounds = false; };
        container.BeginAnimation(FrameworkElement.HeightProperty, open);
        container.BeginAnimation(UIElement.OpacityProperty, Frames(opacity, 1, 0, Tween.Fast, ArcEasing.Enter));
        if (container.RenderTransform is TranslateTransform { IsFrozen: false } slide)
        {
            double x = slide.X;
            slide.BeginAnimation(TranslateTransform.XProperty, Spring(x, 0, Tween.Smooth));
        }
    }

    /// <summary>Where a row's selection fill sits now, motion included, in the rows host.</summary>
    public static Point? SelectionPoint(FrameworkElement container, Visual host)
        => Find(container, "Selection") is { IsVisible: true } fill ? fill.TranslatePoint(new Point(), (UIElement)host) : null;

    /// <summary>
    /// The shared layoutId fill: the new row's fill starts where the old one was and settles on the morph spring. Its row
    /// drops below the others meanwhile, so the fill passes under their labels as it does in the source.
    /// </summary>
    public static void Glide(FrameworkElement container, Visual host, Point from)
    {
        if (Find(container, "Selection") is not { } fill) return;
        double shownY = fill.RenderTransform is TranslateTransform current ? current.Y : 0;
        var to = fill.TranslatePoint(new Point(), (UIElement)host);
        double offset = from.Y - (to.Y - shownY);
        var slide = new TranslateTransform();
        fill.RenderTransform = slide;
        if (Math.Abs(offset) < .5) return;
        Panel.SetZIndex(container, -1);
        var glide = Spring(offset, 0, Tween.Morph);
        glide.Completed += (_, _) => { if (ReferenceEquals(fill.RenderTransform, slide)) container.ClearValue(Panel.ZIndexProperty); };
        slide.BeginAnimation(TranslateTransform.YProperty, glide);
    }

    /// <summary>Ends a container's enter, exit or glide at its resting layout.</summary>
    public static void Stop(FrameworkElement container)
    {
        foreach (var property in Owned) container.BeginAnimation(property, null);
        container.ClipToBounds = false;
        container.IsHitTestVisible = true;
        container.RenderTransform = Transform.Identity;
        container.ClearValue(Panel.ZIndexProperty);
        if (FindRow(container) is { } row) row.Stop();
        else foreach (var name in (string[])["Selection", "Branch"]) if (Find(container, name) is { } part) Settle(part);
    }

    private static TreeViewRow? FindRow(FrameworkElement container) => TreeView.FindVisual(container, item => item is TreeViewRow) as TreeViewRow;
    /// <summary>A named element of the row template, looked up in the template's own name scope so it works before layout.</summary>
    internal static FrameworkElement? Find(FrameworkElement element, string name)
    {
        var presenter = element as ContentPresenter ?? element.TemplatedParent as ContentPresenter;
        return presenter?.ContentTemplate?.FindName(name, presenter) as FrameworkElement;
    }
}

internal sealed class TreeViewAutomationPeer(TreeView owner) : FrameworkElementAutomationPeer(owner), ISelectionProvider
{
    private TreeView Tree => (TreeView)Owner;
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tree;
    protected override string GetClassNameCore() => nameof(TreeView);
    public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.Selection ? this : base.GetPattern(patternInterface);

    // The visible rows are the tree's items; a custom template without TreeViewRow keeps the default walk.
    protected override List<AutomationPeer>? GetChildrenCore()
    {
        var rows = Tree.RealizedRows().Select(row => CreatePeerForElement(row)).OfType<AutomationPeer>().ToList();
        return rows.Count > 0 ? rows : base.GetChildrenCore();
    }

    bool ISelectionProvider.CanSelectMultiple => false;
    bool ISelectionProvider.IsSelectionRequired => false;
    IRawElementProviderSimple[] ISelectionProvider.GetSelection()
        => Tree.RealizedRows().Where(row => row.Row?.IsSelected == true).Select(row => ProviderFromPeer(CreatePeerForElement(row))).ToArray();
}

internal sealed class TreeViewRowAutomationPeer(TreeViewRow owner) : FrameworkElementAutomationPeer(owner), IExpandCollapseProvider, ISelectionItemProvider
{
    private TreeViewRow Item => (TreeViewRow)Owner;
    private TreeView? Tree
    {
        get
        {
            DependencyObject? current = Item;
            while (current is not null and not TreeView) current = VisualTreeHelper.GetParent(current);
            return current as TreeView;
        }
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TreeItem;
    protected override string GetClassNameCore() => nameof(TreeViewRow);
    // The label is the row's name; its two stacked text layers are drawing, not content.
    protected override List<AutomationPeer>? GetChildrenCore() => null;
    protected override int GetPositionInSetCore() => Item.Row?.Position ?? base.GetPositionInSetCore();
    protected override int GetSizeOfSetCore() => Item.Row?.SetSize ?? base.GetSizeOfSetCore();

    public override object? GetPattern(PatternInterface patternInterface) => patternInterface switch
    {
        PatternInterface.ExpandCollapse when Item.Row is { HasChildren: true } => this,
        PatternInterface.SelectionItem => this,
        PatternInterface.Invoke => null,
        _ => base.GetPattern(patternInterface)
    };

    ExpandCollapseState IExpandCollapseProvider.ExpandCollapseState => Item.State(Item.Row?.IsExpanded ?? false);
    void IExpandCollapseProvider.Expand() => Toggle(true);
    void IExpandCollapseProvider.Collapse() => Toggle(false);
    private void Toggle(bool open)
    {
        if (!IsEnabled()) throw new ElementNotEnabledException();
        if (Item.Row is not { } row || Tree is not { } tree) throw new InvalidOperationException("The row is not in a tree.");
        tree.ExpandFromAutomation(row, open);
    }

    bool ISelectionItemProvider.IsSelected => Item.Row?.IsSelected ?? false;
    IRawElementProviderSimple? ISelectionItemProvider.SelectionContainer => Tree is { } tree && CreatePeerForElement(tree) is { } peer ? ProviderFromPeer(peer) : null;
    void ISelectionItemProvider.Select()
    {
        if (!IsEnabled()) throw new ElementNotEnabledException();
        if (Item.Row is { } row) Tree?.SelectFromAutomation(row);
    }
    void ISelectionItemProvider.AddToSelection()
    {
        // Single selection: adding is only possible while nothing else is selected.
        if (Tree?.SelectedNode is { } selected && selected != Item.Row?.Node) throw new InvalidOperationException("The tree selects one row at a time.");
        ((ISelectionItemProvider)this).Select();
    }
    void ISelectionItemProvider.RemoveFromSelection()
    {
        if (Item.Row?.IsSelected == true) throw new InvalidOperationException("The tree keeps its selection until another row is selected.");
    }
}
