using System.Linq;
using System.Reflection;
using System.Text;
using HarborRacer.Inputs;
using HarborRacer.Kart;
using HarborRacer.Race;
using HarborRacer.Track;
using UnityEngine;

public static class Angle02
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

        var jp = Object.FindObjectsByType<HarborRacer.Track.JumpPanelZone>().First(p => p.name == "JumpPanel_02");
        Vector3 pdir = Vector3.ProjectOnPlane(jp.transform.forward, Vector3.up).normalized;
        Vector3 pright = Vector3.Cross(Vector3.up, pdir);
        int ok = 0, total = 0;
        foreach (float yaw in new[] { 0f, -10f, 10f, -20f, 20f })
        foreach (float lat in new[] { 0f, -6f, 6f })
        foreach (float sp in new[] { 25f, 60f, 85f })
        {
            Vector3 d = Quaternion.AngleAxis(yaw, Vector3.up) * pdir;
            Vector3 pos = jp.transform.position - pdir * 30f + pright * lat;
            if (!Physics.Raycast(pos + Vector3.up * 20f, Vector3.down, out var gh, 60f, HarborRacer.Layers.GroundMask)) continue;
            Place(gh.point, d, sp); input.steer = 0f; input.throttle = 1f;
            bool resp = false;
            for (int i = 0; i < 300; i++) { Step(1); if (kart.Respawn.Respawning) { resp = true; break; } }
            bool onRoad = Physics.Raycast(kart.transform.position + Vector3.up, Vector3.down, out var eh, 6f, HarborRacer.Layers.GroundMask) && kart.transform.position.y > 10f;
            total++; if (!resp && onRoad) ok++;
            if (resp || !onRoad) sb.AppendLine("FAIL yaw " + yaw + " lat " + lat + " speed " + sp + " respawn=" + resp + " end " + kart.transform.position.ToString("F0"));
        }
        sb.AppendLine("jump panel 02 approaches: " + ok + " of " + total + " landed on the road");
        Physics.simulationMode = SimulationMode.FixedUpdate;
        return sb.ToString();
    }
}
