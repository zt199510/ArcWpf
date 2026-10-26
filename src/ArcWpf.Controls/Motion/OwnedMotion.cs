using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace ArcWpf.Controls;

// Owns only dedicated internal visual properties, never public consumer bindings.
internal sealed class OwnedMotion
{
    private readonly Dictionary<(DependencyObject,DependencyProperty), Entry> _active = new();
    private sealed record Entry(object Target, Action Stop);
    internal bool IsRunning => _active.Count != 0;
    internal void Double(DependencyObject target, DependencyProperty property, double value, double seconds, bool animate, bool spring=false, Action? completed=null, double delay=0, bool enter=false)
    {
        var key=(target,property);
        if(animate && _active.TryGetValue(key,out var current) && Equals(current.Target,value)) return;
        double from=(double)target.GetValue(property);
        Cancel(key); target.SetValue(property,value);
        if(!animate || Math.Abs(from-value)<.0001){completed?.Invoke();return;}
        var timeline=new DoubleAnimationUsingKeyFrames { Duration=TimeSpan.FromSeconds(seconds+delay), FillBehavior=FillBehavior.Stop };
        if(delay>0)timeline.KeyFrames.Add(new DiscreteDoubleKeyFrame(from,KeyTime.FromTimeSpan(TimeSpan.Zero)));
        if(spring)
        {
            double[] samples=[0,.0213,.0739,.1443,.2235,.305,.3849,.4606,.5305,.594,.6508,.7011,.7452,.7836,.8168,.8454,.8699,.8907,.9084,.9234,.9361,.9467,.9557,.9632,.9694,.9747,.979,.9826,.9857,.9882,.9902,.992,.9934,.9946,.9955,.9963,1];
            for(int i=0;i<samples.Length;i++)timeline.KeyFrames.Add(new LinearDoubleKeyFrame(from+(value-from)*samples[i],KeyTime.FromTimeSpan(TimeSpan.FromSeconds(delay+seconds*i/(samples.Length-1)))));
        }
        else
        {
            timeline.KeyFrames.Add(new DiscreteDoubleKeyFrame(from,KeyTime.FromTimeSpan(TimeSpan.FromSeconds(delay))));
            timeline.KeyFrames.Add(new SplineDoubleKeyFrame(value,KeyTime.FromTimeSpan(TimeSpan.FromSeconds(seconds+delay)),enter?new KeySpline(.16,1,.3,1):new KeySpline(.22,1,.36,1)));
        }
        Start(key,value,timeline,completed);
    }
    internal void Color(SolidColorBrush brush, System.Windows.Media.Color value, double seconds, bool animate)
    {
        var key=((DependencyObject)brush,SolidColorBrush.ColorProperty);
        if(animate && _active.TryGetValue(key,out var current) && Equals(current.Target,value))return;
        var from=brush.Color; Cancel(key); brush.Color=value;
        if(!animate || from==value)return;
        var timeline=new ColorAnimationUsingKeyFrames { Duration=TimeSpan.FromSeconds(seconds),FillBehavior=FillBehavior.Stop };
        timeline.KeyFrames.Add(new DiscreteColorKeyFrame(from,KeyTime.FromTimeSpan(TimeSpan.Zero)));
        timeline.KeyFrames.Add(new SplineColorKeyFrame(value,KeyTime.FromTimeSpan(TimeSpan.FromSeconds(seconds)),new KeySpline(.22,1,.36,1)));
        Start(key,value,timeline);
    }
    private static void Begin(DependencyObject target,DependencyProperty property,AnimationTimeline? animation)
    { if(target is UIElement ui)ui.BeginAnimation(property,animation); else ((Animatable)target).BeginAnimation(property,animation); }
    private void Start((DependencyObject target,DependencyProperty property) key,object value,AnimationTimeline animation,Action? completed=null)
    {
        var entry=new Entry(value,()=>Begin(key.target,key.property,null)); _active[key]=entry;
        animation.Completed+=(_,_)=> { if(_active.TryGetValue(key,out var active) && ReferenceEquals(active,entry)){entry.Stop();_active.Remove(key);completed?.Invoke();} };
        Begin(key.target,key.property,animation);
    }
    private void Cancel((DependencyObject,DependencyProperty) key) { if(_active.Remove(key,out var old))old.Stop(); }
    internal void Stop() { foreach(var entry in _active.Values)entry.Stop(); _active.Clear(); }
}
