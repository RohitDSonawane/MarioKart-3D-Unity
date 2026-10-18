using System.Linq;
using System.Reflection;
using System.Text;
using HarborRacer.Inputs;
using HarborRacer.Kart;
using HarborRacer.Race;
using HarborRacer.Track;
using UnityEngine;

public static class P2Wall
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

        string[] names = { "plain wall 6deg scrape", "fence 6deg scrape", "plain wall head-on", "fence head-on" };
        for (int v = 0; v < 4; v++)
        {
            Place(p0, dir, 60f); input.steer = 0f; input.throttle = 1f;
            var wall = new GameObject("TmpWall"); wall.layer = HarborRacer.Layers.Wall;
            var bc = wall.AddComponent<BoxCollider>(); bc.size = new Vector3(0.5f, 8f, 120f);
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            bool head = v >= 2;
            if (!head) { wall.transform.position = p0 + dir * 70f + right * 2.4f + Vector3.up * 3f; wall.transform.rotation = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0, 6f, 0); }
            else { wall.transform.position = p0 + dir * 60f + Vector3.up * 3f; wall.transform.rotation = Quaternion.LookRotation(right, Vector3.up); }
            if (v % 2 == 1) { var zm = wall.AddComponent<HarborRacer.Track.ZoneMarker>(); zm.kind = HarborRacer.Track.ZoneKind.Fence; }
            Physics.SyncTransforms();
            float sum = 0, minCmd = 999, endCmd = 0; int cnt = 0; float maxFwd = 0;
            for (int i = 0; i < 250; i++)
            {
                Step(1);
                if (i > 40) { sum += kart.ForwardSpeed; cnt++; }
                if (i == 100 && head) { /* turn away from the wall after being pinned */ input.steer = -1f; }
                maxFwd = Mathf.Max(maxFwd, kart.ForwardSpeed); minCmd = Mathf.Min(minCmd, kart.CurrentSpeed); endCmd = kart.CurrentSpeed;
            }
            sb.AppendLine($"{names[v]}: avg real {sum / Mathf.Max(1, cnt):F1}, min commanded {minCmd:F1}, final commanded {endCmd:F1}, max real {maxFwd:F1}");
            Object.DestroyImmediate(wall);
        }

        Physics.simulationMode = SimulationMode.FixedUpdate;
        return sb.ToString();
    }
}
