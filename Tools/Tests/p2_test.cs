using System.Linq;
using System.Reflection;
using System.Text;
using HarborRacer.Inputs;
using HarborRacer.Kart;
using HarborRacer.Race;
using HarborRacer.Track;
using UnityEngine;

public static class P2Test
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

        // A. top speed on road (straight run ~ 120 u: use the harbor straight, 7 s)
        Place(p0, dir, 0f); float top = 0, t85 = -1;
        for (int i = 0; i < 700; i++) { Step(1); top = Mathf.Max(top, kart.ForwardSpeed); if (t85 < 0 && kart.ForwardSpeed >= 80f) t85 = i * 0.02f; if (Vector3.Distance(kart.transform.position, p0) > 300f) break; }
        sb.AppendLine($"A road top speed: max real {top:F1} (stat 85); time to 80 u/s: {(t85 < 0 ? "not reached (road too short)" : t85.ToString("F1") + " s")}; commanded {kart.CurrentSpeed:F1}");

        // B. off-road cap: find an OffRoad surface (grass at the harbor entrance)
        Vector3 grass = default; bool found = false;
        for (float x = 632; x < 700 && !found; x += 4) if (Physics.Raycast(new Vector3(x, -90, 152), Vector3.down, out var h, 40, ~0, QueryTriggerInteraction.Ignore) && h.collider.gameObject.layer == HarborRacer.Layers.OffRoad) { grass = h.point; found = true; }
        if (found) { Place(grass, Vector3.left, 60f); float maxOff = 0; for (int i = 0; i < 100; i++) { Step(1); if (kart.State.OnDirt) maxOff = kart.ForwardSpeed; } sb.AppendLine($"B off-road: onDirt={kart.State.OnDirt} speed after 2 s from 60 = {maxOff:F1} (cap 30, off-road accel lerp 3/s)"); }
        else sb.AppendLine("B off-road: no grass patch found for the probe");

        // C. reverse
        Place(p0, dir, 0f); input.throttle = 0; input.brake = 1; float rev = 0; for (int i = 0; i < 400; i++) { Step(1); rev = Mathf.Min(rev, kart.ForwardSpeed); }
        sb.AppendLine($"C reverse top: {rev:F1} (plan ~ -47, as built -40)"); input.brake = 0; input.throttle = 1;

        // D. steering rate by speed band (full lock, 1 s, yaw change)
        foreach (float sp in new[] { 2f, 6f, 20f, 60f, 85f })
        {
            Place(p0, dir, sp); input.steer = 1f; float y0 = kart.transform.eulerAngles.y;
            kart.stats.accelPerSecond = 0f; float spd = sp;                      // hold speed: only steering is measured
            for (int i = 0; i < 50; i++) { kart.CurrentSpeed = spd; Step(1); }
            kart.stats.accelPerSecond = 0.5f;
            sb.AppendLine($"D full steer 1 s at {sp} u/s -> {Mathf.DeltaAngle(y0, kart.transform.eulerAngles.y):F0} deg");
        }
        input.steer = 0f; input.throttle = 1f;

        // E. wall slide: temporary wall at a 10 degree angle beside the road; compare default friction vs frictionless
        float[] res = new float[2];
        for (int variant = 0; variant < 2; variant++)
        {
            Place(p0, dir, 60f);
            var wall = new GameObject("TmpWall"); wall.layer = HarborRacer.Layers.Wall;
            var bc = wall.AddComponent<BoxCollider>(); bc.size = new Vector3(0.5f, 8f, 120f);
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            wall.transform.position = p0 + dir * 70f + right * 2.4f + Vector3.up * 3f;
            wall.transform.rotation = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0, 6f, 0);        // converges slowly toward the kart's line
            var sc = kart.GetComponent<SphereCollider>();
            var mat = new PhysicsMaterial("fr0") { dynamicFriction = 0, staticFriction = 0, frictionCombine = PhysicsMaterialCombine.Minimum };
            var prev = sc.sharedMaterial; if (variant == 1) { sc.sharedMaterial = mat; bc.sharedMaterial = mat; }
            Physics.SyncTransforms(); input.steer = 0f;
            float sum = 0; int cnt = 0;
            for (int i = 0; i < 200; i++) { Step(1); if (i > 40) { sum += kart.ForwardSpeed; cnt++; } }
            res[variant] = sum / Mathf.Max(1, cnt);
            sc.sharedMaterial = prev; Object.DestroyImmediate(wall);
        }
        sb.AppendLine($"E wall scrape avg real speed: default friction {res[0]:F1} vs frictionless {res[1]:F1} (commanded speed ~85 on a straight)");

        Physics.simulationMode = SimulationMode.FixedUpdate;
        return sb.ToString();
    }
}
