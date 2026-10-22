using System.IO;
using HarborRacer.Track;
using UnityEngine;

/// <summary>Renders a top-down picture of an area (orthographic camera) with the racing-line nodes drawn as red spheres, to a PNG.</summary>
public static class TopDown
{
    public static string Run(float cx, float cz, float half, float camY, string file)
    {
        var root = new GameObject("TmpTopDown");
        var path = GameObject.Find("Markers/Paths/AI/ComputerDriverPATH Toad").GetComponent<TrackPath>();
        for (int i = 0; i < path.Count; i++)
        {
            var p = path.GetNode(i).position;
            if (Mathf.Abs(p.x - cx) > half || Mathf.Abs(p.z - cz) > half) continue;
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere); s.transform.SetParent(root.transform); s.transform.position = p + Vector3.up * 40f; s.transform.localScale = Vector3.one * 3f;
            Object.DestroyImmediate(s.GetComponent<Collider>());
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); mat.color = Color.red; s.GetComponent<Renderer>().sharedMaterial = mat;
        }
        var camGo = new GameObject("TmpTopCam", typeof(Camera)); camGo.transform.SetParent(root.transform);
        var c = camGo.GetComponent<Camera>(); c.orthographic = true; c.orthographicSize = half; c.farClipPlane = 600f; c.nearClipPlane = 0.5f;
        c.transform.position = new Vector3(cx, camY, cz); c.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.1f, 0.1f, 0.3f);
        var rt = new RenderTexture(1000, 1000, 24); c.targetTexture = rt; c.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1000, 1000, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1000, 1000), 0, 0); tex.Apply();
        File.WriteAllBytes(file, tex.EncodeToPNG());
        RenderTexture.active = null; Object.DestroyImmediate(root); rt.Release();
        return "wrote " + file;
    }
}
