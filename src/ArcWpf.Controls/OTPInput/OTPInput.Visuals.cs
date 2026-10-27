using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ArcWpf.Controls;
public partial class OTPInput
{
    private static readonly DependencyProperty ErrorBrushProperty=DependencyProperty.Register("ErrorBrush",typeof(Brush),typeof(OTPInput),new PropertyMetadata(null));
    private readonly OwnedMotion _motion=new();
    private Border? _ring;
    private TranslateTransform? _ringMove, _shake;
    private ScaleTransform? _ringScale;
    private SolidColorBrush? _ringBrush;
    private PasswordMessage? _description, _error;
    private bool _ringShown;
    internal bool IsMotionRunning => _motion.IsRunning || Digits.Any(d=>d.IsMotionRunning) || (_description?.IsMotionRunning??false) || (_error?.IsMotionRunning??false) || (_shake?.HasAnimatedProperties??false);
    private bool Full => IsLoaded && IsVisible && IsEnabled && ArcMotion.GetLevel(this)==ArcMotionLevel.Full;
    private void InitializeVisuals()
    {
        _ring=GetTemplateChild("PART_Ring") as Border; _ringShown=false;
        if(_ring is not null) { _ringMove=new(); _ringScale=new(.94,.94); var group=new TransformGroup(); group.Children.Add(_ringScale); group.Children.Add(_ringMove); _ring.RenderTransform=group; _ring.RenderTransformOrigin=new(.5,.5); _ring.Opacity=0; _ringBrush=new(); _ringBrush.Changed+=(_,_)=>_ring?.InvalidateVisual(); _ring.BorderBrush=_ringBrush; }
        _description=_error=null;
        if(GetTemplateChild("PART_DescriptionHost") is Panel hint) { _description=new(.12); hint.Children.Add(_description); }
        if(GetTemplateChild("PART_ErrorHost") is Panel error) { _error=new(.12); error.Children.Add(_error); }
        if(GetTemplateChild("PART_InputHost") is FrameworkElement host) { _shake=new(); host.RenderTransform=_shake; }
    }
    private void Refresh(bool animate)
    {
        foreach(var d in Digits)
        {
            d.IsReadOnly=IsReadOnly; d.HasError=!string.IsNullOrEmpty(ErrorMessage);
            AutomationProperties.SetName(d,$"{Label}, digit {(int)d.Tag+1} of {Length}");
            AutomationProperties.SetHelpText(d,string.Join(" ",new[]{Description,ErrorMessage}.Where(s=>!string.IsNullOrEmpty(s))));
        }
        _description?.SetText(Description??"",animate); _error?.SetText(ErrorMessage??"",animate);
        if(_ringBrush is not null) { var source=!string.IsNullOrEmpty(ErrorMessage)?GetValue(ErrorBrushProperty):BorderBrush; if(source is SolidColorBrush brush) _motion.Color(_ringBrush,brush.Color,.16,animate && Full); }
        UpdateRing(animate);
    }
    private void LayoutSlots()
    {
        if(_row is null) return;
        double available=Math.Max(0,ActualWidth);
        double gap=Length<=1?0:Math.Min(8,available/(Length*2));
        _row.Width=Math.Min(42*Length+gap*(Length-1),available);
        for(int i=1;i<_row.ColumnDefinitions.Count;i+=2) _row.ColumnDefinitions[i].Width=new GridLength(gap);
        _row.UpdateLayout(); UpdateRing(false);
    }
    private void UpdateRing(bool animate)
    {
        if(_ring is null || _ringMove is null || _ringScale is null || _row is null) return;
        var active=Digits.FirstOrDefault(d=>d.IsKeyboardFocused);
        bool shown=active is not null && IsEnabled && IsVisible;
        if(active is not null)
        {
            double x=active.TranslatePoint(new Point(),_row).X;
            _ring.Width=active.ActualWidth;
            _motion.Double(_ringMove,TranslateTransform.XProperty,x,.26,animate && Full && _ringShown,spring:true);
        }
        _motion.Double(_ring,OpacityProperty,shown?1:0,.16,animate && Full);
        _motion.Double(_ringScale,ScaleTransform.ScaleXProperty,shown?1:.94,.26,animate && Full,spring:true);
        _motion.Double(_ringScale,ScaleTransform.ScaleYProperty,shown?1:.94,.26,animate && Full,spring:true);
        _ringShown=shown;
    }
    private void Shake()
    {
        if(_shake is null || !Full) return;
        var animation=new DoubleAnimationUsingKeyFrames { Duration=TimeSpan.FromSeconds(.36),FillBehavior=FillBehavior.Stop };
        double[] offsets=[0,-6,5,-3,2,0];
        for(int i=0;i<offsets.Length;i++) animation.KeyFrames.Add(new SplineDoubleKeyFrame(offsets[i],KeyTime.FromTimeSpan(TimeSpan.FromSeconds(.36*i/(offsets.Length-1))),new KeySpline(.22,1,.36,1)));
        var target=_shake;
        animation.Completed+=(_,_)=>target.BeginAnimation(TranslateTransform.XProperty,null);
        target.BeginAnimation(TranslateTransform.XProperty,animation);
    }
    private void StopMotion()
    { _motion.Stop(); foreach(var d in Digits)d.Stop(); _description?.Stop(); _error?.Stop(); _shake?.BeginAnimation(TranslateTransform.XProperty,null); }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if(e.Property==BorderBrushProperty || e.Property==ErrorBrushProperty) Refresh(true);
        if(e.Property==ArcMotion.LevelProperty) { StopMotion(); Refresh(false); }
    }
}



