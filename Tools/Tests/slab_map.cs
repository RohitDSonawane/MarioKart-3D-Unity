using System.Text;
using UnityEngine;

/// <summary>ASCII map around the harbor deck slab: S = only the invisible slab is there, G = real ground (other mesh) at the same height, . = nothing, W = visible water surface only.</summary>
public static class SlabMap
{
    public static string Run(float x0, float x1, float z0, float z1, float step)
    {
        Physics.SyncTransforms();
        var sb = new StringBuilder();
        for (float z = z1; z >= z0; z -= step)
        {
            for (float x = x0; x <= x1; x += step)
            {
                bool slab = false, real = false;
                foreach (var h in Physics.RaycastAll(new Vector3(x, -80f, z), Vector3.down, 60f, HarborRacer.Layers.GroundMask, QueryTriggerInteraction.Ignore))
                {
                    if (Mathf.Abs(h.point.y + 109f) > 4f) continue;
                    if (h.collider.name == "HarborDeckFill") slab = true; else real = true;
                }
                sb.Append(real ? 'G' : slab ? 'S' : '.');
            }
            sb.AppendLine("  z=" + z.ToString("F0"));
        }
        sb.AppendLine("x from " + x0 + " to " + x1 + " step " + step);
        return sb.ToString();
    }
}
