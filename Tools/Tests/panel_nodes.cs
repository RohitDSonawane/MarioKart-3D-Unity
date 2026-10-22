using System.Linq;
using System.Text;
using HarborRacer.Track;
using UnityEngine;

/// <summary>Lists every jump panel with its size, launch speed and the racing-line node nearest to it (and that node's lateral offset from the panel).</summary>
public static class PanelNodes
{
    public static string Run()
    {
        var sb = new StringBuilder();
        var path = GameObject.Find("Markers/Paths/AI/ComputerDriverPATH Toad").GetComponent<TrackPath>();
        foreach (var jp in Object.FindObjectsByType<JumpPanelZone>().OrderBy(p => p.name))
        {
            var bc = jp.GetComponent<BoxCollider>();
            Vector3 size = bc ? Vector3.Scale(bc.size, jp.transform.lossyScale) : Vector3.zero;
            int bi = 0; float best = float.MaxValue;
            for (int i = 0; i < path.Count; i++) { float d = (path.GetNode(i).position - jp.transform.position).sqrMagnitude; if (d < best) { best = d; bi = i; } }
            Vector3 np = path.GetNode(bi).position, rel = np - jp.transform.position;
            float lateral = Vector3.Dot(rel, jp.transform.right), along = Vector3.Dot(rel, jp.transform.forward);
            sb.AppendLine($"{jp.name} at ({jp.transform.position.x:F0},{jp.transform.position.y:F0},{jp.transform.position.z:F0}) fwd=({jp.transform.forward.x:F2},{jp.transform.forward.z:F2}) box {size.x:F0}x{size.y:F0}x{size.z:F0} launch {jp.launchSpeed}  nearest node {bi} at ({np.x:F0},{np.y:F0},{np.z:F0}) lateral {lateral:F1} along {along:F1}");
        }
        return sb.ToString();
    }
}
