using System.Reflection;
using System.Windows.Media;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class LucideTests
{
    // Joining an icon's paths into one string turns each later "m" into a move relative to the last point, which throws that piece off the canvas.
    [TestMethod]
    public void EveryGlyphStaysOnItsCanvas() => Sta.Run(() =>
    {
        var off = new List<string>();
        foreach (var field in typeof(Lucide).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).Where(f => f.IsLiteral && f.FieldType == typeof(string)))
        {
            var bounds = Geometry.Parse((string)field.GetRawConstantValue()!).Bounds;
            if (bounds.Left < -1 || bounds.Top < -1 || bounds.Right > 25 || bounds.Bottom > 25) { off.Add($"{field.Name} {bounds}"); }
        }
        Assert.AreEqual(0, off.Count, string.Join("; ", off));
    });
}
