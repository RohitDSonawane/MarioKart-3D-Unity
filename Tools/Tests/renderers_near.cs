using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>Lists visible renderers whose bounds overlap a box: name, centre and size.</summary>
public static class RenderersNear
{
    public static string Run(float cx, float cy, float cz, float hx, float hy, float hz)
    {
        var box = new Bounds(new Vector3(cx, cy, cz), new Vector3(hx * 2f, hy * 2f, hz * 2f));
        var sb = new StringBuilder();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude).Where(r => r.bounds.Intersects(box)).OrderBy(r => r.bounds.size.sqrMagnitude).Take(40))
        {
            var b = r.bounds;
            sb.AppendLine($"{r.name.PadRight(46).Substring(0, 46)} x[{b.min.x:F0}..{b.max.x:F0}] y[{b.min.y:F0}..{b.max.y:F0}] z[{b.min.z:F0}..{b.max.z:F0}]");
        }
        return sb.ToString();
    }
}
