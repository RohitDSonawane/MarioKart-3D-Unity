using System.Linq;
using System.Text;
using HarborRacer.Track;
using UnityEngine;

/// <summary>Lists the gameplay zones (kind, size, centre) within a radius of a point.</summary>
public static class ZonesNear
{
    public static string Run(float x, float y, float z, float radius)
    {
        var sb = new StringBuilder();
        var c = new Vector3(x, y, z);
        foreach (var m in Object.FindObjectsByType<ZoneMarker>(FindObjectsInactive.Include).OrderBy(m => (m.transform.position - c).sqrMagnitude))
        {
            if ((m.transform.position - c).magnitude > radius) break;
            var bc = m.GetComponent<BoxCollider>();
            var size = bc ? Vector3.Scale(bc.size, m.transform.lossyScale) : Vector3.zero;
            sb.AppendLine($"{m.kind} {m.name} at ({m.transform.position.x:F0},{m.transform.position.y:F0},{m.transform.position.z:F0}) size {size.x:F0}x{size.y:F0}x{size.z:F0} active={m.gameObject.activeInHierarchy} layer={m.gameObject.layer}");
        }
        return sb.ToString();
    }
}
