using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ArcWpf.Controls;

// Native editing and automation remain on the TextBox; only the display copy animates.
public sealed class OTPDigit : TextBox
{
    private static readonly DependencyPropertyKey HasDigitPropertyKey=DependencyProperty.RegisterReadOnly(nameof(HasDigit),typeof(bool),typeof(OTPDigit),new PropertyMetadata(false));
    public static readonly DependencyProperty HasDigitProperty=HasDigitPropertyKey.DependencyProperty;
    public bool HasDigit => (bool)GetValue(HasDigitProperty);
    public static readonly DependencyProperty HasErrorProperty=DependencyProperty.Register(nameof(HasError),typeof(bool),typeof(OTPDigit),new PropertyMetadata(false));
    public bool HasError { get=>(bool)GetValue(HasErrorProperty); set=>SetValue(HasErrorProperty,value); }
    internal double WaveDelay { get; set; }
    private readonly OwnedMotion _motion=new();
    private Border? _surface;
    private Grid? _glyphs;
    private SolidColorBrush? _border,_background;
    private TextBlock? _current;
    private string _display="";
    internal bool IsMotionRunning=>_motion.IsRunning;
    private bool Animate=>IsLoaded && IsVisible && IsEnabled && ArcMotion.GetLevel(this)!=ArcMotionLevel.Off;
    static OTPDigit() { DefaultStyleKeyProperty.OverrideMetadata(typeof(OTPDigit),new FrameworkPropertyMetadata(typeof(OTPDigit))); }
    public OTPDigit()
    {
        TextChanged+=(_,_)=>{ SetValue(HasDigitPropertyKey,Text.Length>0); UpdateGlyph(); };
        Unloaded+=(_,_)=>Stop();
        IsVisibleChanged+=(_,_)=>{ if(!IsVisible) Stop(); };
        IsEnabledChanged+=(_,_)=>{ Stop(); Colors(false); };
    }
    public override void OnApplyTemplate()
    { Stop(); if(_border is not null) _border.Changed-=BrushChanged; if(_background is not null) _background.Changed-=BrushChanged; base.OnApplyTemplate(); _surface=GetTemplateChild("PART_Surface") as Border; _glyphs=GetTemplateChild("PART_Glyphs") as Grid; _current=null; _display=""; _border=_background=null; Colors(false); UpdateGlyph(); }
    // WPF's rounded Border caches its drawing; invalidate it as the private animated brushes change.
    private void BrushChanged(object? sender, EventArgs e) => _surface?.InvalidateVisual();
    private void Colors(bool animate)
    {
        if(_surface is null) return;
        if(BorderBrush is SolidColorBrush b) { if(_border is null) { _border=new(b.Color); _border.Changed+=BrushChanged; } _surface.BorderBrush=_border; _motion.Color(_border,b.Color,.16,animate && Animate && ArcMotion.GetLevel(this)==ArcMotionLevel.Full); }
        if(Background is SolidColorBrush bg) { if(_background is null) { _background=new(bg.Color); _background.Changed+=BrushChanged; } _surface.Background=_background; _motion.Color(_background,bg.Color,.16,animate && Animate && ArcMotion.GetLevel(this)==ArcMotionLevel.Full); }
    }
    private void UpdateGlyph()
    {
        if(_glyphs is null || _display==Text) return;
        _display=Text; bool animate=Animate, full=animate && ArcMotion.GetLevel(this)==ArcMotionLevel.Full;
        if(_current is {} old)
        {
            var transforms=(TransformGroup)old.RenderTransform; var scale=(ScaleTransform)transforms.Children[1];
            _motion.Double(scale,ScaleTransform.ScaleXProperty,.9,.12,full); _motion.Double(scale,ScaleTransform.ScaleYProperty,.9,.12,full);
            _motion.Double((BlurEffect)old.Effect,BlurEffect.RadiusProperty,2,.12,full);
            _motion.Double(old,OpacityProperty,0,.12,full,completed:()=>_glyphs?.Children.Remove(old));
        }
        _current=null;
        if(Text.Length==0) return;
        var move=new TranslateTransform(0,full?FontSize*.35:0); var scaleNew=new ScaleTransform(1,1); var transform=new TransformGroup(); transform.Children.Add(move); transform.Children.Add(scaleNew);
        var blur=new BlurEffect { Radius=full?4:0 };
        var glyph=new TextBlock { Text=Text,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,RenderTransform=transform,RenderTransformOrigin=new(.5,.5),Effect=blur,Opacity=animate?0:1,IsHitTestVisible=false };
        glyph.SetResourceReference(TextBlock.ForegroundProperty,"ArcWpf.Brush.Foreground");
        _current=glyph; _glyphs.Children.Add(glyph);
        _motion.Double(move,TranslateTransform.YProperty,0,.24,full,delay:WaveDelay,enter:true);
        _motion.Double(blur,BlurEffect.RadiusProperty,0,.24,full,delay:WaveDelay,enter:true);
        _motion.Double(glyph,OpacityProperty,1,full?.24:.12,animate,delay:full?WaveDelay:0,enter:true);
    }
    internal void Stop()
    { _motion.Stop(); if(_glyphs is not null) foreach(var child in _glyphs.Children.Cast<UIElement>().Where(c=>c!=_current).ToArray()) _glyphs.Children.Remove(child); }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    { base.OnPropertyChanged(e); if(e.Property==BackgroundProperty || e.Property==BorderBrushProperty) Colors(true); if(e.Property==ArcMotion.LevelProperty) Stop(); }
}
