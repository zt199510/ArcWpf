using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

[TemplatePart(Name = "PART_Row", Type = typeof(Grid))]
[TemplatePart(Name = "PART_Ring", Type = typeof(Border))]
public partial class OTPInput : Control
{
    public static readonly DependencyProperty LengthProperty = DependencyProperty.Register(nameof(Length), typeof(int), typeof(OTPInput), new PropertyMetadata(6, LengthChanged), v => (int)v is >= 1 and <= 12);
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(OTPInput), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, ValueChangedCore, (d,v) => new string(((string?)v ?? "").Where(IsDigit).Take(((OTPInput)d).Length).ToArray())));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(OTPInput), new PropertyMetadata("", StateChanged));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(OTPInput), new PropertyMetadata("", StateChanged));
    public static readonly DependencyProperty ErrorMessageProperty = DependencyProperty.Register(nameof(ErrorMessage), typeof(string), typeof(OTPInput), new PropertyMetadata("", ErrorChanged));
    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(OTPInput), new PropertyMetadata(false, StateChanged));
    private static readonly DependencyPropertyKey IsCompletePropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsComplete), typeof(bool), typeof(OTPInput), new PropertyMetadata(false));
    public static readonly DependencyProperty IsCompleteProperty = IsCompletePropertyKey.DependencyProperty;
    public static readonly RoutedEvent ValueChangedEvent = EventManager.RegisterRoutedEvent(nameof(ValueChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(OTPInput));
    public static readonly RoutedEvent CompletedEvent = EventManager.RegisterRoutedEvent(nameof(Completed), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(OTPInput));
    public event RoutedEventHandler ValueChanged { add => AddHandler(ValueChangedEvent,value); remove => RemoveHandler(ValueChangedEvent,value); }
    public event RoutedEventHandler Completed { add => AddHandler(CompletedEvent,value); remove => RemoveHandler(CompletedEvent,value); }
    public int Length { get => (int)GetValue(LengthProperty); set => SetValue(LengthProperty,value); }
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty,value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty,value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty,value); }
    public string ErrorMessage { get => (string)GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty,value); }
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty,value); }
    public bool IsComplete => (bool)GetValue(IsCompleteProperty);
    internal readonly List<OTPDigit> Digits = [];
    private Grid? _row;
    private bool _sync;
    private string _previous = "";
    static OTPInput() { DefaultStyleKeyProperty.OverrideMetadata(typeof(OTPInput), new FrameworkPropertyMetadata(typeof(OTPInput))); }
    public OTPInput()
    {
        SetResourceReference(ErrorBrushProperty,"ArcWpf.Brush.Danger");
        Loaded += (_,_) => { Refresh(false); LayoutSlots(); };
        Unloaded += (_,_) => StopMotion();
        IsVisibleChanged += (_,_) => { if (!IsVisible) StopMotion(); else Refresh(false); };
        IsEnabledChanged += (_,_) => { Refresh(false); if (!IsEnabled) StopMotion(); };
        SizeChanged += (_,_) => LayoutSlots();
        IsKeyboardFocusWithinChanged += (_,_) => UpdateRing(true);
    }
    public void Clear() => SetCurrentValue(ValueProperty, "");
    public void FocusDigit(int index) { if (Digits.Count > 0 && IsEnabled) { var digit=Digits[Math.Clamp(index,0,Digits.Count-1)]; digit.Focus(); digit.SelectAll(); } }
    private static bool IsDigit(char c) => c is >= '0' and <= '9';
    private static void LengthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    { var c=(OTPInput)d; c.CoerceValue(ValueProperty); c.SetValue(IsCompletePropertyKey,c.Value.Length==c.Length); c.BuildDigits(); }
    private static void ValueChangedCore(DependencyObject d, DependencyPropertyChangedEventArgs e)
    { var c=(OTPInput)d; c.SetValue(IsCompletePropertyKey,c.Value.Length==c.Length); c.SyncDigits(); c.RaiseEvent(new RoutedEventArgs(ValueChangedEvent,c)); }
    private static void StateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((OTPInput)d).Refresh(true);
    private static void ErrorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    { var c=(OTPInput)d; c.Refresh(true); if (!string.IsNullOrEmpty((string?)e.NewValue)) c.Shake(); if(AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged) && UIElementAutomationPeer.FromElement(c) is {} peer) peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged); }
    public override void OnApplyTemplate()
    {
        StopMotion(); ReleaseDigits(); _description?.Detach(); _error?.Detach();
        base.OnApplyTemplate(); _row=GetTemplateChild("PART_Row") as Grid;
        InitializeVisuals(); BuildDigits(); Refresh(false);
    }
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    {
        StopMotion(); ReleaseDigits(); _description?.Detach(); _error?.Detach();
        _row=null; _ring=null; _description=_error=null;
        base.OnTemplateChanged(oldTemplate,newTemplate);
    }
    private void ReleaseDigits()
    {
        foreach(var digit in Digits) { digit.TextChanged-=Edited; digit.PreviewKeyDown-=DigitKeyDown; digit.PreviewTextInput-=DigitTextInput; digit.PreviewMouseLeftButtonDown-=DigitMouseDown; digit.GotKeyboardFocus-=Focused; DataObject.RemovePastingHandler(digit,Paste); digit.Stop(); }
        Digits.Clear(); _row?.Children.Clear(); _row?.ColumnDefinitions.Clear();
    }
    private void BuildDigits()
    {
        if (_row is null) return;
        int focus=Digits.FindIndex(x=>x.IsKeyboardFocused); ReleaseDigits();
        for(int i=0;i<Length;i++)
        {
            if(i>0) _row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(8) });
            _row.ColumnDefinitions.Add(new ColumnDefinition());
            var digit=new OTPDigit { Tag=i, MaxLength=1 };
            InputMethod.SetIsInputMethodEnabled(digit,false);
            digit.TextChanged+=Edited; digit.PreviewKeyDown+=DigitKeyDown; digit.PreviewTextInput+=DigitTextInput; digit.PreviewMouseLeftButtonDown+=DigitMouseDown; digit.GotKeyboardFocus+=Focused;
            DataObject.AddPastingHandler(digit,Paste);
            Grid.SetColumn(digit,i*2); _row.Children.Add(digit); Digits.Add(digit);
        }
        _previous=""; SyncDigits(); Refresh(false); LayoutSlots();
        if(focus>=0) FocusDigit(focus);
    }
    private void SyncDigits()
    {
        bool wave=Enumerable.Range(0,Length).Count(i=>(_previous.ElementAtOrDefault(i))!=(Value.ElementAtOrDefault(i)))>1;
        _sync=true;
        try { for(int i=0;i<Digits.Count;i++) { Digits[i].WaveDelay=wave ? i*.035 : 0; string next=i<Value.Length ? Value[i].ToString() : ""; if(Digits[i].Text!=next) Digits[i].SetCurrentValue(TextBox.TextProperty,next); } }
        finally { _sync=false; _previous=Value; }
    }
    private void Edited(object sender, TextChangedEventArgs e)
    {
        if(_sync) return;
        var digit=(OTPDigit)sender;
        if(IsReadOnly || !IsEnabled) { SyncDigits(); return; }
        ApplyInput((int)digit.Tag,digit.Text);
    }
    internal void ApplyInput(int index,string raw)
    {
        if(!IsEnabled || IsReadOnly) return;
        index=Math.Clamp(index,0,Math.Min(Value.Length,Length-1));
        string clean=new(raw.Where(IsDigit).Take(Length-index).ToArray());
        if(clean.Length==0 && raw.Length>0) { SyncDigits(); return; }
        string next=clean.Length==0 ? (index<Value.Length ? Value.Remove(index,1) : Value) : (Value[..index]+clean+Value[Math.Min(Value.Length,index+clean.Length)..]);
        string before=Value; SetCurrentValue(ValueProperty,next); SyncDigits();
        if(clean.Length>0) FocusDigit(Math.Min(index+clean.Length,Length-1)); else FocusDigit(index);
        if(Value!=before && IsComplete) RaiseEvent(new RoutedEventArgs(CompletedEvent,this));
    }
    private void DigitTextInput(object sender, TextCompositionEventArgs e)
    { e.Handled=true; if(!string.IsNullOrEmpty(e.Text)) ApplyInput((int)((OTPDigit)sender).Tag,e.Text); }
    private void DigitMouseDown(object sender, MouseButtonEventArgs e)
    { FocusDigit((int)((OTPDigit)sender).Tag); e.Handled=true; }
    protected override Size MeasureOverride(Size constraint)
    {
        if(_row is not null) _row.Width=Math.Min(42*Length+8*(Length-1),double.IsFinite(constraint.Width)?Math.Max(0,constraint.Width):double.PositiveInfinity);
        return base.MeasureOverride(constraint);
    }
    private void Paste(object sender, DataObjectPastingEventArgs e)
    {
        e.CancelCommand();
        if(e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText) && e.SourceDataObject.GetData(DataFormats.UnicodeText) is string {Length:>0} pasted) ApplyInput((int)((OTPDigit)sender).Tag,pasted);
    }
    private void DigitKeyDown(object sender,KeyEventArgs e)
    {
        int index=(int)((OTPDigit)sender).Tag;
        switch(e.Key)
        {
            case Key.Left: FocusDigit(index-1); e.Handled=true; break;
            case Key.Right: FocusDigit(index+1); e.Handled=true; break;
            case Key.Home: FocusDigit(0); e.Handled=true; break;
            case Key.End: FocusDigit(Math.Min(Value.Length,Length-1)); e.Handled=true; break;
            case Key.Back: if(!IsReadOnly) { int at=index<Value.Length?index:Math.Max(0,Math.Min(index-1,Value.Length-1)); ApplyInput(at,""); } e.Handled=true; break;
            case Key.Delete: ApplyInput(index,""); e.Handled=true; break;
        }
    }
    private void Focused(object sender,KeyboardFocusChangedEventArgs e) { ((OTPDigit)sender).SelectAll(); UpdateRing(true); }
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnGotKeyboardFocus(e); if(ReferenceEquals(e.NewFocus,this)) FocusDigit(Math.Min(Value.Length,Length-1)); }
    protected override AutomationPeer OnCreateAutomationPeer() => new OTPPeer(this);
    private sealed class OTPPeer(OTPInput owner) : FrameworkElementAutomationPeer(owner)
    { protected override string GetClassNameCore()=>nameof(OTPInput); protected override AutomationControlType GetAutomationControlTypeCore()=>AutomationControlType.Group; protected override string GetNameCore()=>string.IsNullOrEmpty(base.GetNameCore())?owner.Label:base.GetNameCore(); protected override string GetHelpTextCore()=>string.Join(" ",new[]{owner.Description,owner.ErrorMessage}.Where(s=>!string.IsNullOrEmpty(s))); }
}


