using System.Diagnostics;
namespace ArcWpf.Controls;
internal sealed class TooltipWarmth
{
    private int _open;
    private double _closed=double.NegativeInfinity;
    internal bool IsWarm(double now)=>_open>0||now-_closed<.3;
    internal void Opened()=>_open++;
    internal void Closed(double now){if(_open>0)_open--;if(_open==0)_closed=now;}
    internal static double Now=>Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
}
