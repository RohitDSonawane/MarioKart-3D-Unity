using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public static class Floaters2
{
    public static string Run()
    {
        var sb = new StringBuilder();
        var temp = new List<MeshCollider>();
        // every visible mesh that is not a spectator gets a temporary collider so the ray sees what the player sees
        foreach (var mf in Object.FindObjectsByType<MeshFilter>())
        {
            if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;
            if (mf.transform.root.name == "Spectators" || mf.GetComponentInParent<Animator>() != null) continue;
            var mr = mf.GetComponent<MeshRenderer>(); if (mr == null || !mr.enabled) continue;
            var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; temp.Add(mc);
        }
        Physics.SyncTransforms();
        var root = GameObject.Find("Spectators");
        int floating = 0; var rows = new List<string>();
        for (int i = 0; i < root.transform.childCount; i++)
        {
            var t = root.transform.GetChild(i); Vector3 p = t.position;
            bool hit = Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var h, 80f, ~0, QueryTriggerInteraction.Ignore);
            float gap = hit ? p.y - h.point.y : 999f;
            if (gap > 2.8f) { floating++; rows.Add(t.name + " gap " + (hit ? gap.ToString("F1") : "none") + (hit ? " over " + h.collider.name.Substring(0, Mathf.Min(22, h.collider.name.Length)) : "") + " at " + p.ToString("F0")); }
        }
        sb.AppendLine("temp colliders " + temp.Count + "; toads still floating (>2.8 u): " + floating + " of " + root.transform.childCount);
        foreach (var r in rows) sb.AppendLine("  " + r);
        foreach (var mc in temp) Object.DestroyImmediate(mc);
        return sb.ToString();
    }
}
