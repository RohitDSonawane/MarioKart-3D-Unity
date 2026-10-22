using System.Text;
using HarborRacer.Track;
using UnityEngine;

/// <summary>ASCII map of the ground around a point: X = ground within yTol of refY, o = ground at another height, . = nothing; digits mark racing-line nodes.</summary>
public static class DeckMap
{
    public static string Run(float cx, float cz, float half, float step, float refY, float yTol, int firstNode, int lastNode)
    {
        Physics.SyncTransforms();
        var path = GameObject.Find("Markers/Paths/AI/ComputerDriverPATH Toad").GetComponent<TrackPath>();
        var sb = new StringBuilder();
        int n = (int)(half * 2 / step) + 1;
        var grid = new char[n, n];
        for (int zi = 0; zi < n; zi++)
            for (int xi = 0; xi < n; xi++)
            {
                float x = cx - half + xi * step, z = cz + half - zi * step;
                char c = '.';
                var hits = Physics.RaycastAll(new Vector3(x, refY + 120f, z), Vector3.down, 300f, HarborRacer.Layers.GroundMask, QueryTriggerInteraction.Ignore);
                foreach (var h in hits) { if (Mathf.Abs(h.point.y - refY) <= yTol) { c = 'X'; break; } c = 'o'; }
                grid[zi, xi] = c;
            }
        for (int i = firstNode; i <= lastNode && i < path.Count; i++)
        {
            var p = path.GetNode(i).position;
            int xi = Mathf.RoundToInt((p.x - (cx - half)) / step), zi = Mathf.RoundToInt((cz + half - p.z) / step);
            sb.AppendLine($"node {i} at ({p.x:F0},{p.y:F0},{p.z:F0})");
            if (xi >= 0 && xi < n && zi >= 0 && zi < n) grid[zi, xi] = (char)('0' + (i % 10));
        }
        for (int zi = 0; zi < n; zi++) { for (int xi = 0; xi < n; xi++) sb.Append(grid[zi, xi]); sb.AppendLine("  z=" + (cz + half - zi * step).ToString("F0")); }
        sb.AppendLine("x from " + (cx - half) + " to " + (cx + half) + ", step " + step);
        return sb.ToString();
    }
}
