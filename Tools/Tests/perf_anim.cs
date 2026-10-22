using System.Linq;
using System.Text;
using UnityEngine;

public static class PerfAnim
{
    public static string Run()
    {
        var sb = new StringBuilder();
        var an = Object.FindObjectsByType<Animator>(FindObjectsInactive.Exclude);
        sb.AppendLine("animators by cullingMode: " + string.Join(", ", an.GroupBy(a => a.cullingMode).Select(g => g.Key + " x" + g.Count())));
        sb.AppendLine("animators by owner root: " + string.Join(", ", an.GroupBy(a => a.transform.root.name + "/" + (a.transform.parent ? a.transform.parent.name : "")).OrderByDescending(g => g.Count()).Take(8).Select(g => g.Key + " x" + g.Count())));
        var sk = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Exclude);
        sb.AppendLine("skinned updateWhenOffscreen: " + sk.Count(s => s.updateWhenOffscreen) + "/" + sk.Length + "; quality " + string.Join(",", sk.GroupBy(s => s.quality).Select(g => g.Key + "x" + g.Count())) + "; bones max " + sk.Max(s => s.bones.Length) + "; verts sum " + sk.Where(s => s.sharedMesh).Sum(s => (long)s.sharedMesh.vertexCount));
        sb.AppendLine("skinned by root: " + string.Join(", ", sk.GroupBy(s => s.transform.root.name).OrderByDescending(g => g.Count()).Take(6).Select(g => g.Key + " x" + g.Count())));
        var ps = Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude);
        sb.AppendLine("particle systems playing " + ps.Count(p => p.isPlaying) + "/" + ps.Length + ", playOnAwake " + ps.Count(p => p.main.playOnAwake) + ", max particles sum " + ps.Sum(p => p.main.maxParticles) + ", with collision/trails/lights: " + ps.Count(p => p.collision.enabled) + "/" + ps.Count(p => p.trails.enabled) + "/" + ps.Count(p => p.lights.enabled));
        sb.AppendLine("ps by root: " + string.Join(", ", ps.GroupBy(p => p.transform.root.name).OrderByDescending(g => g.Count()).Take(6).Select(g => g.Key + " x" + g.Count())));
        var au = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude);
        sb.AppendLine("audio: playing " + au.Count(a => a.isPlaying) + "/" + au.Length + ", loops " + au.Count(a => a.loop) + ", spatial " + au.Count(a => a.spatialBlend > 0.5f));
        var mb = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude);
        sb.AppendLine("monobehaviours " + mb.Length + ": " + string.Join(", ", mb.GroupBy(m => m.GetType().Name).OrderByDescending(g => g.Count()).Take(14).Select(g => g.Key + " x" + g.Count())));
        var rb = Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude);
        sb.AppendLine("rigidbodies: kinematic " + rb.Count(r => r.isKinematic) + " cont-detect " + string.Join(",", rb.GroupBy(r => r.collisionDetectionMode).Select(g => g.Key + "x" + g.Count())) + " interp " + string.Join(",", rb.GroupBy(r => r.interpolation).Select(g => g.Key + "x" + g.Count())));
        return sb.ToString();
    }
}
