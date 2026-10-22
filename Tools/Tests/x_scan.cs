using System.Text;
using UnityEngine;

/// <summary>For a fixed z, scans x and prints which ground collider (and height) lies under each point; runs of identical results are merged.</summary>
public static class XScan
{
    public static string Run(float z, float x0, float x1, float step)
    {
        Physics.SyncTransforms();
        var sb = new StringBuilder(); string last = null; float runStart = x0;
        for (float x = x0; x <= x1 + 0.01f; x += step)
        {
            string s = "none";
            if (Physics.Raycast(new Vector3(x, -60f, z), Vector3.down, out var h, 120f, HarborRacer.Layers.GroundMask, QueryTriggerInteraction.Ignore)) s = h.collider.name + " y=" + Mathf.Round(h.point.y);
            if (s != last) { if (last != null) sb.AppendLine($"x {runStart:F0}..{x - step:F0}: {last}"); last = s; runStart = x; }
        }
        sb.AppendLine($"x {runStart:F0}..{x1:F0}: {last}");
        return sb.ToString();
    }
}
