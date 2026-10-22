using System.Linq;
using System.Text;
using HarborRacer.Track;
using UnityEngine;

/// <summary>Lists the Water / OutOfBounds zones with their world bounds, and the extent of all ground colliders.</summary>
public static class WaterZones
{
    public static string Run()
    {
        var sb = new StringBuilder();
        foreach (var m in Object.FindObjectsByType<ZoneMarker>(FindObjectsInactive.Include).Where(z => z.kind == ZoneKind.Water || z.kind.ToString().Contains("Out")))
        {
            var bc = m.GetComponent<BoxCollider>(); if (bc == null) continue;
            var b = bc.bounds;
            sb.AppendLine($"{m.kind} {m.name} center ({b.center.x:F0},{b.center.y:F0},{b.center.z:F0}) size ({b.size.x:F0},{b.size.y:F0},{b.size.z:F0}) x[{b.min.x:F0}..{b.max.x:F0}] y[{b.min.y:F0}..{b.max.y:F0}] z[{b.min.z:F0}..{b.max.z:F0}]");
        }
        Bounds all = new Bounds(); bool first = true;
        foreach (var c in Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Exclude)) if (c.gameObject.layer == HarborRacer.Layers.Drivable || c.gameObject.layer == HarborRacer.Layers.OffRoad) { if (first) { all = c.bounds; first = false; } else all.Encapsulate(c.bounds); }
        sb.AppendLine($"ground collider extent x[{all.min.x:F0}..{all.max.x:F0}] y[{all.min.y:F0}..{all.max.y:F0}] z[{all.min.z:F0}..{all.max.z:F0}]");
        return sb.ToString();
    }
}
