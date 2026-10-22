using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>Lists every collider within a radius of a point: name, layer, trigger flag, kind, size, and whether the point lies above it.</summary>
public static class CollidersNear
{
    public static string Run(float x, float y, float z, float radius)
    {
        Physics.SyncTransforms();
        var sb = new StringBuilder();
        foreach (var c in Physics.OverlapSphere(new Vector3(x, y, z), radius, ~0, QueryTriggerInteraction.Collide).OrderBy(c => (c.bounds.center - new Vector3(x, y, z)).sqrMagnitude).Take(30))
        {
            var b = c.bounds;
            string kind = c is MeshCollider mc ? "Mesh" + (mc.convex ? "(convex)" : "") : c.GetType().Name.Replace("Collider", "");
            sb.AppendLine($"{c.name.PadRight(48).Substring(0, 48)} layer {c.gameObject.layer,2} trigger={c.isTrigger} {kind} center ({b.center.x:F0},{b.center.y:F0},{b.center.z:F0}) size ({b.size.x:F0},{b.size.y:F0},{b.size.z:F0})");
        }
        // vertical ray list straight down through the pad centre and 3 u either side
        foreach (float o in new[] { -6f, -3f, 0f, 3f, 6f })
        {
            var from = new Vector3(x + o * 0.7f, y + 20f, z - o * 0.7f);
            var hits = Physics.RaycastAll(from, Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore).OrderByDescending(h => h.point.y);
            sb.AppendLine($"ray offset {o:F0}: " + string.Join(" | ", hits.Select(h => $"y={h.point.y:F1} {h.collider.name} L{h.collider.gameObject.layer}")));
        }
        return sb.ToString();
    }
}
