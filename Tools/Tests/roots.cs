using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Roots
{
    public static string Run()
    {
        var sb = new StringBuilder();
        var sc = SceneManager.GetActiveScene();
        sb.AppendLine("scene " + sc.name + " dirty=" + sc.isDirty);
        foreach (var go in sc.GetRootGameObjects())
        {
            int missing = 0;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) missing += UnityEditor.GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            sb.AppendLine(go.name + " kids=" + go.transform.childCount + " active=" + go.activeSelf + " missingScripts=" + missing + " flags=" + go.hideFlags);
        }
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.name == "RedShell" || t.name == "GreenShell" || t.name == "BlueShell" || t.name == "BobOmb" || t.name == "Banana" || t.name.StartsWith("Tmp"))
                sb.AppendLine("STRAY " + t.name + " parent=" + (t.parent ? t.parent.name : "root") + " pos=" + t.position.ToString("F0"));
        return sb.ToString();
    }
}
