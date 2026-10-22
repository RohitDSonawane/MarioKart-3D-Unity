using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Renders the scene from a camera placed behind and above a ground point, looking along a yaw (degrees), to a PNG.</summary>
public static class SpotView
{
    public static string Run(float x, float z, float yaw, float back, float up, float lookDown, string file)
    {
        Physics.SyncTransforms();
        Physics.Raycast(new Vector3(x, 50f, z), Vector3.down, out var h, 200f, HarborRacer.Layers.GroundMask, QueryTriggerInteraction.Ignore);
        Vector3 p = h.point, fwd = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
        var go = new GameObject("TmpSpotCam", typeof(Camera)); var c = go.GetComponent<Camera>(); c.fieldOfView = 70; c.nearClipPlane = 0.2f; c.farClipPlane = 1500f;
        var d = go.AddComponent<UniversalAdditionalCameraData>(); d.renderPostProcessing = true;
        c.transform.position = p - fwd * back + Vector3.up * up; c.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(lookDown, 0, 0);
        var rt = new RenderTexture(1280, 720, 24) { antiAliasing = 4 }; c.targetTexture = rt; c.Render(); RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
        File.WriteAllBytes(file, tex.EncodeToPNG()); RenderTexture.active = null; Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(go);
        return "ground y " + h.point.y.ToString("F1") + " on " + (h.collider ? h.collider.name : "none");
    }
}
