using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HarborRacer.Items;
using HarborRacer.Kart;
using HarborRacer.Track;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Renders staged action shots of the track to PNG files (edit mode): kart packs, flips, orbiting shells, an explosion, the glider.</summary>
public static class Screenshots
{
    static TrackPath path;
    static KartController[] karts;

    class Shot
    {
        public string name; public int node; public Vector3 cam;     // cam: x = side, y = up, z = back (relative to the lead kart heading)
        public float fov = 55f, lookAhead = 0f, lookUp = 1f; public System.Action<Vector3, Vector3> stage; public bool aerial;
    }

    static Vector3 Ground(Vector3 p)
    {
        if (Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out var h, 80f, HarborRacer.Layers.GroundMask, QueryTriggerInteraction.Ignore)) return h.point;
        return p;
    }

    static void SetGlider(KartController k, bool open)
    {
        foreach (var tr in k.GetComponentsInChildren<Transform>(true))
            if (tr.name == "Glider") { tr.gameObject.SetActive(open); if (open) tr.localScale = Vector3.one * 0.1f; }
    }

    static void ResetPoses()
    {
        foreach (var k in karts)
        {
            var ba = k.transform.Find("BodyAnim"); if (ba) { ba.localPosition = Vector3.zero; ba.localRotation = Quaternion.identity; }
            var ent = k.GetComponent<ItemEntourage>(); if (ent) ent.Clear();
            SetGlider(k, false);
        }
        foreach (var b in Object.FindObjectsByType<BlastVisual>()) Object.DestroyImmediate(b.gameObject);
    }

    /// <summary>Lines the eight karts up around a path node in two columns, facing along the path.</summary>
    static void Pack(int node, out Vector3 leadPos, out Vector3 fwd)
    {
        int n = path.Count;
        Vector3 a = path.GetNode(node).position, b = path.GetNode((node + 1) % n).position;
        fwd = Vector3.ProjectOnPlane(b - a, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        leadPos = Ground(a);
        for (int i = 0; i < karts.Length; i++)
        {
            Vector3 p = a - fwd * (i / 2 * 11f) + right * (i % 2 == 0 ? -4.2f : 4.2f) + fwd * ((i % 2) * 3f);
            p = Ground(p);
            Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out var h, 20f, HarborRacer.Layers.GroundMask, QueryTriggerInteraction.Ignore);
            Vector3 up = h.collider ? h.normal : Vector3.up;
            karts[i].transform.SetPositionAndRotation(p + up * 1.0f, Quaternion.LookRotation(Vector3.ProjectOnPlane(fwd, up), up));
        }
        Physics.SyncTransforms();
    }

    static void Render(string file, Vector3 pos, Vector3 target, float fov)
    {
        var go = new GameObject("TmpShotCam", typeof(Camera));
        var c = go.GetComponent<Camera>();
        c.fieldOfView = fov; c.nearClipPlane = 0.3f; c.farClipPlane = 2500f; c.allowHDR = true;
        var d = go.AddComponent<UniversalAdditionalCameraData>(); d.renderPostProcessing = true; d.antialiasing = AntialiasingMode.None;
        c.transform.position = pos; c.transform.rotation = Quaternion.LookRotation((target - pos).normalized, Vector3.up);
        var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        c.targetTexture = rt; c.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); tex.Apply();
        File.WriteAllBytes(file, tex.EncodeToPNG());
        RenderTexture.active = null; Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(go);
    }

    static readonly int Block = ~((1 << HarborRacer.Layers.Kart) | (1 << HarborRacer.Layers.Zone) | (1 << HarborRacer.Layers.Item) | (1 << HarborRacer.Layers.ItemBox) | (1 << HarborRacer.Layers.Hazard));

    /// <summary>The requested camera offset if it is in free space with a clear line to the kart, else the nearest rotation of it around the kart that is.</summary>
    static Vector3 PickCamera(Vector3 anchor, Vector3 fwd, Vector3 off)
    {
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        Vector3 local = fwd * -off.z + right * off.x;           // horizontal part, behind (+z) the kart
        foreach (float ang in new float[] { 0, 25, -25, 50, -50, 80, -80, 110, -110, 140, -140, 180 })
            foreach (float shrink in new float[] { 1f, 0.7f, 0.5f, 1.5f, 2.2f })
            {
                Vector3 pos = anchor + Quaternion.AngleAxis(ang, Vector3.up) * local * shrink + Vector3.up * off.y * Mathf.Lerp(1f, shrink, 0.5f);
                if (Physics.CheckSphere(pos, 0.9f, Block, QueryTriggerInteraction.Ignore)) continue;
                if (Physics.Linecast(anchor + Vector3.up * 1.6f, pos, Block, QueryTriggerInteraction.Ignore)) continue;
                return pos;
            }
        return anchor + local * 0.4f + Vector3.up * off.y * 0.6f;
    }

    public static string Run(string outDir)
    {
        Directory.CreateDirectory(outDir);
        path = GameObject.Find("Markers/Paths/AI/ComputerDriverPATH Toad").GetComponent<TrackPath>();
        karts = Object.FindObjectsByType<KartController>(FindObjectsInactive.Include).OrderBy(k => k.name == "Kart" ? 0 : 1).ThenBy(k => k.name).ToArray();
        var sb = new StringBuilder();

        var shots = new List<Shot>
        {
            new Shot { name = "01_pack_chase", node = 1, cam = new Vector3(2.5f, 3.2f, 9f), fov = 60f, lookAhead = 14f },
            new Shot { name = "02_pack_low_front", node = 3, cam = new Vector3(5f, 0.8f, -16f), fov = 50f, lookAhead = -2f, lookUp = 1.2f },
            new Shot { name = "03_harbor_aerial", node = 0, cam = new Vector3(30f, 60f, 70f), fov = 55f, lookAhead = -8f, lookUp = 0f, aerial = true },
            new Shot { name = "04_flip_in_the_pack", node = 4, cam = new Vector3(5f, 2.2f, 8.5f), fov = 62f, lookAhead = 0f, lookUp = 2f,
                stage = (lead, fwd) => { var ba = karts[0].transform.Find("BodyAnim"); ba.localPosition = new Vector3(0, 3.2f, 0); ba.localRotation = Quaternion.Euler(-118f, 12f, 0f); } },
            new Shot { name = "05_shell_orbit", node = 7, cam = new Vector3(0f, 2.6f, 9f), fov = 58f, lookAhead = 3f,
                stage = (lead, fwd) => { var e = karts[0].GetComponent<ItemEntourage>(); e.Show(ItemId.GreenShell, 3, EntourageMode.Orbit); e.Place(); var e2 = karts[3].GetComponent<ItemEntourage>(); e2.Show(ItemId.RedShell, 3, EntourageMode.Orbit); e2.Place(); } },
            new Shot { name = "06_explosion", node = 3, cam = new Vector3(7f, 3f, 11f), fov = 60f, lookAhead = 1f, lookUp = 2f,
                stage = (lead, fwd) => { var k = karts[0]; var ba = k.transform.Find("BodyAnim"); ba.localPosition = new Vector3(0, 2.2f, 0); ba.localRotation = Quaternion.Euler(-70f, 140f, 25f);
                    BlastVisual.Spawn(k.transform.position + Vector3.up * 2.6f + k.transform.right * 2.8f - k.transform.forward * 1.5f, 3f); var blast = GameObject.Find("Blast"); if (blast) blast.transform.GetChild(0).localScale = Vector3.one * 4.2f; } },
            new Shot { name = "07_glider_over_the_harbor", node = 18, cam = new Vector3(9f, 3f, 16f), fov = 60f, lookAhead = 2f, lookUp = 4f,
                stage = (lead, fwd) => { foreach (var i in new[] { 0, 1 }) { var k = karts[i]; k.transform.position += Vector3.up * (i == 0 ? 7f : 4.5f); SetGlider(k, true);
                    k.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(-12f, 0f, i == 0 ? 14f : -10f); } } },
            new Shot { name = "08_drift_lean", node = 13, cam = new Vector3(-3.5f, 1.8f, 7f), fov = 62f, lookAhead = 2f,
                stage = (lead, fwd) => { var ba = karts[0].transform.Find("BodyAnim"); ba.localRotation = Quaternion.Euler(0f, 24f, -9f); var ba2 = karts[1].transform.Find("BodyAnim"); ba2.localRotation = Quaternion.Euler(0f, -22f, 8f); } },
            new Shot { name = "09_ramp_climb", node = 17, cam = new Vector3(-6f, 4f, 14f), fov = 62f, lookAhead = 6f },
            new Shot { name = "10_spin_out", node = 22, cam = new Vector3(4f, 3.2f, 10f), fov = 62f, lookAhead = 2f,
                stage = (lead, fwd) => { var ba = karts[0].transform.Find("BodyAnim"); ba.localPosition = new Vector3(0, 0.9f, 0); ba.localRotation = Quaternion.Euler(-14f, 215f, 0f); var ba2 = karts[1].transform.Find("BodyAnim"); ba2.localRotation = Quaternion.Euler(-10f, 80f, 0f); } },
            new Shot { name = "11_wide_overpass", node = 20, cam = new Vector3(85f, 70f, 70f), fov = 50f, lookAhead = 0f, lookUp = 0f, aerial = true },
            new Shot { name = "13_hero_front_mario", node = 2, cam = new Vector3(3.2f, 1.5f, -7.5f), fov = 45f, lookAhead = 0f, lookUp = 1.3f },
            new Shot { name = "12_hero_low_angle", node = 4, cam = new Vector3(-3.2f, 0.55f, -9f), fov = 48f, lookAhead = 0f, lookUp = 1.1f },
        };

        foreach (var s in shots)
        {
            ResetPoses();
            Pack(s.node, out var lead, out var fwd);
            s.stage?.Invoke(lead, fwd);
            Vector3 anchor = karts[0].transform.position;
            Vector3 pos = PickCamera(anchor, fwd, s.cam);
            Vector3 target = anchor + fwd * s.lookAhead + Vector3.up * s.lookUp;
            Render(Path.Combine(outDir, s.name + ".png"), pos, target, s.fov);
            sb.AppendLine(s.name + " node " + s.node + " at " + anchor.ToString("F0"));
        }
        ResetPoses();
        return sb.ToString();
    }
}
