using System.Windows;
using System.Windows.Controls;
namespace ArcWpf.Controls;
/// <summary>Simple vertical form layout that preserves native field controls and bindings.</summary>
public class FormLayout:Panel
{
 public static readonly DependencyProperty SpacingProperty=DependencyProperty.Register(nameof(Spacing),typeof(double),typeof(FormLayout),new FrameworkPropertyMetadata(20d,FrameworkPropertyMetadataOptions.AffectsMeasure|FrameworkPropertyMetadataOptions.AffectsArrange),v=>v is double d&&double.IsFinite(d)&&d>=0);
 public static readonly DependencyProperty MaxContentWidthProperty=DependencyProperty.Register(nameof(MaxContentWidth),typeof(double),typeof(FormLayout),new FrameworkPropertyMetadata(540d,FrameworkPropertyMetadataOptions.AffectsMeasure|FrameworkPropertyMetadataOptions.AffectsArrange),v=>v is double d&&double.IsFinite(d)&&d>0);
 public double Spacing{get=>(double)GetValue(SpacingProperty);set=>SetValue(SpacingProperty,value);}public double MaxContentWidth{get=>(double)GetValue(MaxContentWidthProperty);set=>SetValue(MaxContentWidthProperty,value);}
 protected override Size MeasureOverride(Size available){double width=Math.Min(available.Width,MaxContentWidth),height=0;foreach(UIElement c in InternalChildren){if(c.Visibility==Visibility.Collapsed)continue;c.Measure(new Size(width,double.PositiveInfinity));height+=c.DesiredSize.Height;if(height>0)height+=Spacing;}if(height>0)height-=Spacing;return new Size(width,height);}
 protected override Size ArrangeOverride(Size final){double width=Math.Min(final.Width,MaxContentWidth),y=0;foreach(UIElement c in InternalChildren){if(c.Visibility==Visibility.Collapsed)continue;c.Arrange(new Rect(0,y,width,c.DesiredSize.Height));y+=c.DesiredSize.Height+Spacing;}return new Size(width,Math.Max(0,y-Spacing));}
}
