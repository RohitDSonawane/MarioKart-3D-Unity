using System.Linq;
using System.Reflection;
using System.Text;
using HarborRacer.Inputs;
using HarborRacer.Kart;
using HarborRacer.Race;
using HarborRacer.Track;
using UnityEngine;

public static class PanelTest
{
    class In : IKartInput
    {
        public float throttle = 1f, brake = 0f, steer = 0f;
        public float Throttle => throttle; public float Brake => brake; public float Steer => steer; public float Pitch => 0f;
        public bool DriftHeld => false; public bool DriftPressed => false; public bool TrickPressed => false; public bool ItemPressed => false; public bool ItemBackPressed => false;
        public void EndFixedStep() { }
    }

    static MethodInfo fu;
    static KartController kart;
    static Rigidbody rb;
    static StringBuilder sb = new StringBuilder();

    static void Step(int n) { for (int i = 0; i < n; i++) { fu.Invoke(kart, null); Physics.Simulate(0.02f); } }

    static void Place(Vector3 pos, Vector3 dir, float speed)
    {
        rb.isKinematic = true;
        kart.transform.SetPositionAndRotation(pos + Vector3.up * 1.2f, Quaternion.LookRotation(dir, Vector3.up));
        rb.isKinematic = false; rb.linearVelocity = dir * speed; rb.rotation = kart.transform.rotation; kart.CurrentSpeed = speed;
        kart.controlsLocked = false; kart.Boost.Remaining = 0; kart.State.Stunned = false; kart.Zones.Clear();
        if (kart.Respawn.Respawning) typeof(KartRespawn).GetMethod("Finish", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(kart.Respawn, null);
    }

    public static string Run()
    {
        sb.Clear();
        Physics.SyncTransforms();
        var skip = new System.Type[] { typeof(PlayerKartInput), typeof(KartEffects), typeof(KartDebugHUD), typeof(RaceDebugHUD), typeof(HarborRacer.CameraRig.KartCameraRig), typeof(HarborRacer.UI.RaceHud) };
        foreach (var m in Object.FindObjectsByType<MonoBehaviour>().OrderBy(m => m is CheckpointTrack ? 0 : m is KartController ? 2 : 3))
        {
            if (m == null || skip.Contains(m.GetType())) continue;
            if (m.GetType().Namespace == null || !m.GetType().Namespace.StartsWith("HarborRacer")) continue;
            var a = m.GetType().GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
            if (a != null) a.Invoke(m, null);
        }
        foreach (var r in Object.FindObjectsByType<RacerProgress>()) { var pk = r.GetComponent<PlayerKartInput>(); if (pk) pk.enabled = false; if (!r.isPlayer) { r.Kart.Body.isKinematic = true; r.transform.position = new Vector3(0, 900, 0); r.Kart.controlsLocked = true; } }
        fu = typeof(KartController).GetMethod("FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        Physics.simulationMode = SimulationMode.Script;
        kart = Object.FindObjectsByType<RacerProgress>().First(r => r.isPlayer).Kart; rb = kart.Body;
        var input = new In(); kart.Input = input;

        // long straight on the road: first segment of the Peach AI line
        var path = GameObject.Find("Markers/Paths/AI/ComputerDriverPATH Toad").GetComponent<TrackPath>();
        Vector3 p0 = path.GetNode(0).position + Vector3.up * 0.3f;
        Vector3 dir = Vector3.ProjectOnPlane(path.GetNode(1).position - path.GetNode(0).position, Vector3.up).normalized;

        var panels = Object.FindObjectsByType<HarborRacer.Track.JumpPanelZone>().OrderBy(p => p.transform.position.x).ToArray();
        foreach (var jp in panels)
        {
            Vector3 pdir = Vector3.ProjectOnPlane(jp.transform.forward, Vector3.up).normalized;
            foreach (float sign in new[] { 1f, -1f })
            foreach (float sp in new[] { 40f, 80f })
            {
                Vector3 d = pdir * sign;
                Vector3 pos = jp.transform.position - d * 28f;
                if (!Physics.Raycast(pos + Vector3.up * 20f, Vector3.down, out var gh, 60f, HarborRacer.Layers.GroundMask)) { continue; }
                Place(gh.point, d, sp); input.steer = 0f; input.throttle = 1f;
                float startY = kart.transform.position.y; float minY = 999, maxY = -999; bool onPanel = false, resp = false; int landStep = -1; float maxDist = 0;
                for (int i = 0; i < 250; i++)
                {
                    Step(1);
                    if (kart.State.OnJumpPanel) onPanel = true;
                    if (kart.Respawn.Respawning) { resp = true; break; }
                    float y = kart.transform.position.y; minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                    maxDist = Mathf.Max(maxDist, Vector3.Distance(kart.transform.position, gh.point));
                }
                bool hasGround = Physics.Raycast(kart.transform.position + Vector3.up, Vector3.down, out var eh, 40f, HarborRacer.Layers.GroundMask);
                sb.AppendLine(jp.name + " (" + jp.transform.position.x.ToString("F0") + "," + jp.transform.position.y.ToString("F0") + "," + jp.transform.position.z.ToString("F0") + ") dir " + (sign > 0 ? "fwd" : "back") + " speed " + sp + ": onPanel=" + onPanel + " respawn=" + resp + " y " + startY.ToString("F1") + " min " + minY.ToString("F1") + " max " + maxY.ToString("F1") + " end " + kart.transform.position.y.ToString("F1") + " groundBelowEnd=" + (hasGround ? eh.collider.name.Substring(0, System.Math.Min(18, eh.collider.name.Length)) + "@" + eh.point.y.ToString("F1") : "none") + " travelled " + maxDist.ToString("F0"));
            }
        }
        Physics.simulationMode = SimulationMode.FixedUpdate;
        return sb.ToString();
    }
}
