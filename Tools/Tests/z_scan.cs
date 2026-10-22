using System.Text;
using UnityEngine;

/// <summary>For a fixed x, scans z and prints the ground collider and height under each point; runs of identical results are merged.</summary>
public static class ZScan
{
    public static string Run(float x, float z0, float z1, float step)
    {
        Physics.SyncTransforms();
        var sb = new StringBuilder(); string last = null; float runStart = z0;
        for (float z = z0; z <= z1 + 0.01f; z += step)
        {
            string s = "none";
            if (Physics.Raycast(new Vector3(x, -60f, z), Vector3.down, out var h, 150f, HarborRacer.Layers.GroundMask, QueryTriggerInteraction.Ignore)) s = h.collider.name + " y=" + Mathf.Round(h.point.y);
            if (s != last) { if (last != null) sb.AppendLine($"z {runStart:F0}..{z - step:F0}: {last}"); last = s; runStart = z; }
        }
        sb.AppendLine($"z {runStart:F0}..{z1:F0}: {last}");
        return sb.ToString();
    }
}
