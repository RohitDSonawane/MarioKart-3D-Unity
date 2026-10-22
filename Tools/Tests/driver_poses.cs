using System.IO;
using System.Linq;
using System.Text;
using HarborRacer.Kart;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Samples each driver's base clips (idle, turns, item idle) onto the model and renders every kart from the side, to compare the poses.</summary>
public static class DriverPoses
{
    static void Render(string file, Vector3 pos, Vector3 target, float fov)
    {
        var go = new GameObject("TmpPoseCam", typeof(Camera));
        var c = go.GetComponent<Camera>(); c.fieldOfView = fov; c.nearClipPlane = 0.2f; c.farClipPlane = 500f;
        var d = go.AddComponent<UniversalAdditionalCameraData>(); d.renderPostProcessing = false;
        c.transform.position = pos; c.transform.rotation = Quaternion.LookRotation((target - pos).normalized, Vector3.up);
        var rt = new RenderTexture(640, 480, 24) { antiAliasing = 4 }; c.targetTexture = rt; c.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(640, 480, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 640, 480), 0, 0); tex.Apply();
        File.WriteAllBytes(file, tex.EncodeToPNG());
        RenderTexture.active = null; Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(go);
    }

    public static string Run(string outDir, string clipKind)
    {
        Directory.CreateDirectory(outDir);
        var sb = new StringBuilder();
        foreach (var pa in Object.FindObjectsByType<DriverPoseAnimator>(FindObjectsInactive.Include).OrderBy(p => p.transform.root.name))
        {
            var kart = pa.GetComponentInParent<KartController>();
            AnimationClip clip = clipKind == "itemIdle" ? pa.itemIdle : clipKind == "turnLeft" ? pa.turnLeft : pa.idle;
            if (clip == null) { sb.AppendLine(kart.name + " no clip " + clipKind); continue; }
            clip.SampleAnimation(pa.animRoot.gameObject, 0f);
            // body measurements in kart space: renderer bounds of the driver
            var b = new Bounds(); bool first = true;
            foreach (var r in pa.animRoot.GetComponentsInChildren<Renderer>()) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            Vector3 c = kart.transform.InverseTransformPoint(b.center);
            Vector3 s = b.size;
            sb.AppendLine($"{kart.name.PadRight(10)} root={pa.transform.root.name} clip={clip.name.Substring(clip.name.IndexOf('|') + 1)} len={clip.length:F2} boundsCenterLocal=({c.x:F2},{c.y:F2},{c.z:F2}) size=({s.x:F2},{s.y:F2},{s.z:F2}) animRootLocalPos={pa.animRoot.localPosition.ToString("F2")} rot={pa.animRoot.localEulerAngles.ToString("F0")}");
            var p = kart.transform.position; var side = kart.transform.right; var up = Vector3.up;
            Render(Path.Combine(outDir, (kart.name.Replace(" ", "_") + "_" + pa.transform.root.GetInstanceID() + "_" + clipKind) + ".png"), kart.transform.position + side * 5.5f + up * 1.8f, kart.transform.position + up * 1.2f, 40f);
        }
        return sb.ToString();
    }
}
