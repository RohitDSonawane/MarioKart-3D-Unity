using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public static class BadShaders
{
    public static string Run()
    {
        var sb = new StringBuilder();
        var seen = new HashSet<string>();
        int total = 0, bad = 0;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
        {
            foreach (var m in r.sharedMaterials)
            {
                total++;
                string why = null;
                if (m == null) why = "NULL material";
                else if (m.shader == null) why = "null shader";
                else if (!m.shader.isSupported) why = "unsupported shader " + m.shader.name;
                else if (m.shader.name.Contains("InternalError") || m.shader.name.StartsWith("Hidden/")) why = "shader " + m.shader.name;
                else if (m.shader.name.StartsWith("Legacy Shaders") || m.shader.name.StartsWith("Particles/") || m.shader.name.StartsWith("Mobile/") || m.shader.name.StartsWith("Sprites/") && false) why = "built-in shader " + m.shader.name;
                if (why == null) continue;
                bad++;
                string key = why + " | " + (m != null ? m.name : "") + " | " + r.GetType().Name;
                if (seen.Add(key)) sb.AppendLine(key + "  e.g. on " + GetPath(r.transform) + " active=" + r.gameObject.activeInHierarchy);
            }
        }
        sb.Insert(0, "renderers' materials " + total + ", suspicious " + bad + "\n");
        return sb.ToString();
    }
    static string GetPath(Transform t) { var s = t.name; int n = 0; while (t.parent != null && n++ < 4) { t = t.parent; s = t.name + "/" + s; } return s; }
}
