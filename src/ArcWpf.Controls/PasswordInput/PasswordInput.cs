using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;

namespace ArcWpf.Controls;

[TemplatePart(Name = "PART_PasswordBox", Type = typeof(PasswordBox))]
[TemplatePart(Name = "PART_RevealBox", Type = typeof(PasswordRevealBox))]
[TemplatePart(Name = "PART_RevealToggle", Type = typeof(PasswordRevealToggle))]
public partial class PasswordInput : Control
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(PasswordInput), new PropertyMetadata(""));
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(PasswordInput), new PropertyMetadata("", StateChanged));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(PasswordInput), new PropertyMetadata("", MessageChanged));
    public static readonly DependencyProperty ErrorMessageProperty = DependencyProperty.Register(nameof(ErrorMessage), typeof(string), typeof(PasswordInput), new PropertyMetadata("", MessageChanged));
    public static readonly DependencyProperty IsPasswordRevealedProperty = DependencyProperty.Register(nameof(IsPasswordRevealed), typeof(bool), typeof(PasswordInput), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, RevealChanged, (d, v) => ((PasswordInput)d)._composing ? ((PasswordInput)d)._revealedState : (bool)v && ((PasswordInput)d).IsEnabled));
    public static readonly DependencyProperty MaxLengthProperty = DependencyProperty.Register(nameof(MaxLength), typeof(int), typeof(PasswordInput), new PropertyMetadata(0), v => (int)v >= 0);
    public static readonly DependencyProperty PasswordCharProperty = DependencyProperty.Register(nameof(PasswordChar), typeof(char), typeof(PasswordInput), new PropertyMetadata('●'));
    public static readonly DependencyProperty ShowPasswordTextProperty = DependencyProperty.Register(nameof(ShowPasswordText), typeof(string), typeof(PasswordInput), new PropertyMetadata("Show password", StateChanged));
    public static readonly DependencyProperty HidePasswordTextProperty = DependencyProperty.Register(nameof(HidePasswordText), typeof(string), typeof(PasswordInput), new PropertyMetadata("Hide password", StateChanged));
    private static readonly DependencyPropertyKey HasPasswordPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasPassword), typeof(bool), typeof(PasswordInput), new PropertyMetadata(false));
    public static readonly DependencyProperty HasPasswordProperty = HasPasswordPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey HasErrorPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasError), typeof(bool), typeof(PasswordInput), new PropertyMetadata(false));
    public static readonly DependencyProperty HasErrorProperty = HasErrorPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey EffectiveErrorMessagePropertyKey = DependencyProperty.RegisterReadOnly(nameof(EffectiveErrorMessage), typeof(string), typeof(PasswordInput), new PropertyMetadata(""));
    public static readonly DependencyProperty EffectiveErrorMessageProperty = EffectiveErrorMessagePropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey IsPlaceholderVisiblePropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsPlaceholderVisible), typeof(bool), typeof(PasswordInput), new PropertyMetadata(false));
    public static readonly DependencyProperty IsPlaceholderVisibleProperty = IsPlaceholderVisiblePropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey RevealButtonTextPropertyKey = DependencyProperty.RegisterReadOnly(nameof(RevealButtonText), typeof(string), typeof(PasswordInput), new PropertyMetadata("Show password"));
    public static readonly DependencyProperty RevealButtonTextProperty = RevealButtonTextPropertyKey.DependencyProperty;
    public static readonly RoutedEvent PasswordChangedEvent = EventManager.RegisterRoutedEvent(nameof(PasswordChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PasswordInput));
    public event RoutedEventHandler PasswordChanged { add => AddHandler(PasswordChangedEvent, value); remove => RemoveHandler(PasswordChangedEvent, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public string ErrorMessage { get => (string)GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }
    public bool IsPasswordRevealed { get => (bool)GetValue(IsPasswordRevealedProperty); set => SetValue(IsPasswordRevealedProperty, value); }
    public int MaxLength { get => (int)GetValue(MaxLengthProperty); set => SetValue(MaxLengthProperty, value); }
    public char PasswordChar { get => (char)GetValue(PasswordCharProperty); set => SetValue(PasswordCharProperty, value); }
    public string ShowPasswordText { get => (string)GetValue(ShowPasswordTextProperty); set => SetValue(ShowPasswordTextProperty, value); }
    public string HidePasswordText { get => (string)GetValue(HidePasswordTextProperty); set => SetValue(HidePasswordTextProperty, value); }
    public bool HasPassword => (bool)GetValue(HasPasswordProperty);
    public bool HasError => (bool)GetValue(HasErrorProperty);
    public string EffectiveErrorMessage => (string)GetValue(EffectiveErrorMessageProperty);
    public bool IsPlaceholderVisible => (bool)GetValue(IsPlaceholderVisibleProperty);
    public string RevealButtonText => (string)GetValue(RevealButtonTextProperty);

    /// <summary>Explicit plaintext access, like PasswordBox.Password; this is deliberately not a dependency property.</summary>
    public string Password
    {
        get { VerifyAccess(); return ReadPlaintext(); }
        set
        {
            VerifyAccess(); var next = new SecureString(); foreach (char c in value ?? "") next.AppendChar(c);
            ReplaceSecret(next); SyncEditors(); NotifyPasswordChanged();
        }
    }
    /// <summary>Returns an independent copy. The caller must dispose it.</summary>
    public SecureString SecurePassword { get { VerifyAccess(); return _secret.Copy(); } }
    public void SetSecurePassword(SecureString password) { VerifyAccess(); ArgumentNullException.ThrowIfNull(password); ReplaceSecret(password.Copy()); SyncEditors(); NotifyPasswordChanged(); }
    public void Clear() => Password = "";
    public void SelectAll() { if (IsPasswordRevealed) _reveal?.SelectAll(); else _password?.SelectAll(); }
    public bool FocusEditor() => IsEnabled && (IsPasswordRevealed ? _reveal?.Focus() : _password?.Focus()) == true;

    private SecureString _secret = new();
    private PasswordBox? _password;
    private PasswordRevealBox? _reveal;
    private PasswordRevealToggle? _toggle;
    private FrameworkElement? _label;
    private bool _syncing, _composing, _revealedState, _pointerToggle, _releasing;
    private INotifyCollectionChanged? _errors;
    private DispatcherOperation? _compositionEnd;
    static PasswordInput() => DefaultStyleKeyProperty.OverrideMetadata(typeof(PasswordInput), new FrameworkPropertyMetadata(typeof(PasswordInput)));
    public PasswordInput()
    {
        Loaded += (_, _) => { ObserveErrors(); UpdateMessages(false); UpdateState(); SyncEditors(); UpdateColors(false); };
        Unloaded += (_, _) => { ConcealAndStop(); ObserveErrors(false); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) ConcealAndStop(); };
        AddHandler(TextCompositionManager.PreviewTextInputStartEvent, new TextCompositionEventHandler(CompositionStarted), true);
        AddHandler(TextCompositionManager.PreviewTextInputUpdateEvent, new TextCompositionEventHandler(CompositionStarted), true);
        AddHandler(TextCompositionManager.PreviewTextInputEvent, new TextCompositionEventHandler(CompositionEnded), true);
    }
    private string ReadPlaintext()
    {
        IntPtr data = Marshal.SecureStringToBSTR(_secret);
        try { return Marshal.PtrToStringBSTR(data); } finally { Marshal.ZeroFreeBSTR(data); }
    }
    private void ReplaceSecret(SecureString next) { next.MakeReadOnly(); _secret.Dispose(); _secret = next; UpdateState(); }
    private void NotifyPasswordChanged() => RaiseEvent(new RoutedEventArgs(PasswordChangedEvent, this));
    private void HiddenChanged(object sender, RoutedEventArgs e)
    {
        if (_syncing || _password is null) return;
        ReplaceSecret(_password.SecurePassword);
        if (IsPasswordRevealed && _reveal is not null) { _syncing = true; try { _reveal.Text = ReadPlaintext(); } finally { _syncing = false; } }
        NotifyPasswordChanged(); e.Handled = true;
    }
    private void VisibleChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing || _reveal is null || !IsPasswordRevealed) return;
        var next = new SecureString(); foreach (char c in _reveal.Text) next.AppendChar(c); ReplaceSecret(next);
        _syncing = true; try { if (_password is not null) _password.Password = _reveal.Text; } finally { _syncing = false; }
        NotifyPasswordChanged(); e.Handled = true;
    }
    private void SyncEditors()
    {
        _syncing = true;
        try
        {
            if (_password is not null) _password.Password = ReadPlaintext();
            if (_reveal is not null) _reveal.Text = IsPasswordRevealed ? ReadPlaintext() : "";
        }
        finally { _syncing = false; }
        ShowEditor(false);
    }
    private static void StateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((PasswordInput)d).UpdateState();
    private static void MessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((PasswordInput)d).UpdateMessages(((PasswordInput)d).IsLoaded);
    private static void RevealChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var input = (PasswordInput)d; input._revealedState = (bool)e.NewValue;
        bool focused = input._password?.IsKeyboardFocused == true || input._reveal?.IsKeyboardFocused == true;
        input._syncing = true;
        try { if (input._reveal is not null) { input._reveal.Text = input.IsPasswordRevealed ? input.ReadPlaintext() : ""; input._reveal.CaretIndex = input._reveal.Text.Length; } }
        finally { input._syncing = false; }
        input.ShowEditor(focused); input.UpdateState(); input.AnimateResolve();
    }
    private void ShowEditor(bool focus)
    {
        if (_password is not null) _password.Visibility = IsPasswordRevealed ? Visibility.Collapsed : Visibility.Visible;
        if (_reveal is not null) _reveal.Visibility = IsPasswordRevealed ? Visibility.Visible : Visibility.Collapsed;
        if (focus) { FocusEditor(); if (!IsPasswordRevealed && _password is not null) EditingCommands.MoveToDocumentEnd.Execute(null, _password); }
    }
    private void UpdateState()
    {
        if (_secret is null) return;
        SetValue(HasPasswordPropertyKey, _secret.Length > 0);
        SetValue(IsPlaceholderVisiblePropertyKey, !HasPassword && !_composing && !string.IsNullOrEmpty(Placeholder));
        SetValue(RevealButtonTextPropertyKey, (IsPasswordRevealed ? HidePasswordText : ShowPasswordText) ?? "");
        if (_toggle is not null) _toggle.IsEnabled = IsEnabled && !_composing;
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e); if (_secret is null) return;
        if (e.Property == IsEnabledProperty && !IsEnabled) ConcealAndStop();
        if (e.Property == IsEnabledProperty) UpdateState();
        if (e.Property == Validation.ErrorsProperty || e.Property == Validation.HasErrorProperty) { ObserveErrors(IsLoaded); UpdateMessages(IsLoaded); }
        if (e.Property == BorderBrushProperty) UpdateColors(IsLoaded);
        if (e.Property == ArcMotion.LevelProperty) { StopMotion(); UpdateColors(false); UpdateMessages(false); }
    }
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnGotKeyboardFocus(e); if (ReferenceEquals(e.NewFocus, this)) FocusEditor(); }
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    { base.OnLostKeyboardFocus(e); if (_composing && !Popover.IsWithin(e.NewFocus as DependencyObject, this)) FinishComposition(); }
    private void CompositionStarted(object sender, TextCompositionEventArgs e) { _composing = true; UpdateState(); }
    private void CompositionEnded(object sender, TextCompositionEventArgs e)
    { _compositionEnd?.Abort(); _compositionEnd = Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(FinishComposition)); }
    private void FinishComposition() { _compositionEnd = null; _composing = false; CoerceValue(IsPasswordRevealedProperty); UpdateState(); }
    private void TogglePointer(object sender, MouseButtonEventArgs e) => _pointerToggle = true;
    private void ToggleKey(object sender, KeyEventArgs e) => _pointerToggle = false;
    private void ToggleClicked(object sender, RoutedEventArgs e) { if (_pointerToggle) FocusEditor(); _pointerToggle = false; e.Handled = true; }
    private void LabelClicked(object sender, MouseButtonEventArgs e) { if (FocusEditor()) e.Handled = true; }
    private void ConcealAndStop()
    {
        _compositionEnd?.Abort(); _compositionEnd = null; _composing = false;
        SetCurrentValue(IsPasswordRevealedProperty, false); CoerceValue(IsPasswordRevealedProperty);
        _syncing = true; try { if (_reveal is not null) _reveal.Clear(); } finally { _syncing = false; }
        StopMotion(); UpdateState();
    }
    public override void OnApplyTemplate()
    {
        ReleaseParts(); base.OnApplyTemplate();
        _password = GetTemplateChild("PART_PasswordBox") as PasswordBox; _reveal = GetTemplateChild("PART_RevealBox") as PasswordRevealBox;
        _toggle = GetTemplateChild("PART_RevealToggle") as PasswordRevealToggle; _label = GetTemplateChild("PART_Label") as FrameworkElement;
        if (_password is not null) _password.PasswordChanged += HiddenChanged;
        if (_reveal is not null) _reveal.TextChanged += VisibleChanged;
        if (_toggle is not null) { _toggle.Click += ToggleClicked; _toggle.PreviewMouseLeftButtonDown += TogglePointer; _toggle.PreviewKeyDown += ToggleKey; }
        if (_label is not null) _label.MouseLeftButtonDown += LabelClicked;
        InitializeVisuals(); SyncEditors(); UpdateState(); UpdateMessages(false); UpdateColors(false);
    }
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate) { ReleaseParts(); base.OnTemplateChanged(oldTemplate, newTemplate); }
    private void ReleaseParts()
    {
        if (_releasing) return; _releasing = true;
        try
        {
            if (_password is not null || _reveal is not null) ConcealAndStop();
            if (_password is not null) { _password.PasswordChanged -= HiddenChanged; _password.Clear(); }
            if (_reveal is not null) { _reveal.TextChanged -= VisibleChanged; _reveal.Clear(); }
            if (_toggle is not null) { _toggle.Click -= ToggleClicked; _toggle.PreviewMouseLeftButtonDown -= TogglePointer; _toggle.PreviewKeyDown -= ToggleKey; }
            if (_label is not null) _label.MouseLeftButtonDown -= LabelClicked;
            _password = null; _reveal = null; _toggle = null; _label = null; ReleaseVisuals();
        }
        finally { _releasing = false; }
    }
    private void ObserveErrors(bool observe = true)
    {
        var errors = observe ? Validation.GetErrors(this) as INotifyCollectionChanged : null;
        if (ReferenceEquals(errors, _errors)) return;
        if (_errors is not null) CollectionChangedEventManager.RemoveHandler(_errors, ErrorsChanged);
        _errors = errors; if (_errors is not null) CollectionChangedEventManager.AddHandler(_errors, ErrorsChanged);
    }
    private void ErrorsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateMessages(IsLoaded);
    protected override AutomationPeer OnCreateAutomationPeer() => new PasswordInputPeer(this);
    private sealed class PasswordInputPeer(PasswordInput owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(PasswordInput);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetNameCore() => string.IsNullOrEmpty(base.GetNameCore()) ? owner.Label : base.GetNameCore();
        protected override string GetHelpTextCore() => string.Join(" ", new[] { owner.Description, owner.EffectiveErrorMessage }.Where(s => !string.IsNullOrWhiteSpace(s)));
        protected override List<AutomationPeer>? GetChildrenCore()
        {
            var children = new List<AutomationPeer>();
            UIElement? editor = owner.IsPasswordRevealed ? owner._reveal : owner._password;
            if (editor is not null && UIElementAutomationPeer.CreatePeerForElement(editor) is { } peer) children.Add(peer);
            if (owner._toggle is not null && UIElementAutomationPeer.CreatePeerForElement(owner._toggle) is { } toggle) children.Add(toggle);
            return children;
        }
    }
}
