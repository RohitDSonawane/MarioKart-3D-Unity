using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public static class NullMats
{
    public static string Run()
    {
        var sb = new StringBuilder();
        var groups = new Dictionary<string, int>();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
        {
            if (!r.sharedMaterials.Any(m => m == null)) continue;
            string root = r.transform.root.name;
            string key = r.GetType().Name + " | " + r.name.Substring(0, Mathf.Min(r.name.Length, 28)) + " | root " + (root.StartsWith("Carried_") ? "Carried_*" : root) + " | parent " + (r.transform.parent ? r.transform.parent.name : "-");
            groups[key] = groups.TryGetValue(key, out var c) ? c + 1 : 1;
        }
        foreach (var kv in groups.OrderByDescending(k => k.Value)) sb.AppendLine(kv.Value + "x " + kv.Key);
        // the item box particle systems: what do they draw
        var box = GameObject.Find("ItemBoxes/ItemBox_01");
        if (box != null)
            foreach (var ps in box.GetComponentsInChildren<ParticleSystem>(true))
            {
                var pr = ps.GetComponent<ParticleSystemRenderer>();
                sb.AppendLine("  ItemBox_01 PS " + ps.name + " renderMode=" + pr.renderMode + " mat=" + (pr.sharedMaterial ? pr.sharedMaterial.name : "NULL") + " startSize=" + ps.main.startSize.constantMax.ToString("F1") + " playing=" + ps.isPlaying + " scaling=" + ps.main.scalingMode);
            }
        return sb.ToString();
    }
}
