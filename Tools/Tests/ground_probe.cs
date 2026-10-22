using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>Lists every ground collider hit by a vertical ray through each probe point (x z pairs), top to bottom.</summary>
public static class GroundProbe
{
    public static string Run(float[] xz)
    {
        Physics.SyncTransforms();
        var sb = new StringBuilder();
        for (int i = 0; i + 1 < xz.Length; i += 2)
        {
            var hits = Physics.RaycastAll(new Vector3(xz[i], 200f, xz[i + 1]), Vector3.down, 500f, ~0, QueryTriggerInteraction.Ignore).OrderByDescending(h => h.point.y);
            sb.AppendLine($"({xz[i]:F0},{xz[i + 1]:F0}): " + string.Join(" | ", hits.Select(h => $"y={h.point.y:F0} {h.collider.name} L{h.collider.gameObject.layer} n.y={h.normal.y:F2}")));
        }
        return sb.ToString();
    }
}
