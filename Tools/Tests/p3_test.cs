using System.Linq;
using System.Reflection;
using System.Text;
using HarborRacer.Inputs;
using HarborRacer.Kart;
using HarborRacer.Race;
using HarborRacer.Track;
using UnityEngine;

public static class P3Test
{
    class In : IKartInput
    {
        public float throttle = 1f, brake = 0f, steer = 0f; public bool held, pressed;
        public float Throttle => throttle; public float Brake => brake; public float Steer => steer; public float Pitch => 0f;
        public bool DriftHeld => held; public bool DriftPressed => pressed; public bool TrickPressed => false; public bool ItemPressed => false; public bool ItemBackPressed => false;
        public void EndFixedStep() { pressed = false; }
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
        if (Physics.Raycast(p0 + Vector3.up * 5f, Vector3.down, out var gh0, 20f, HarborRacer.Layers.GroundMask, QueryTriggerInteraction.Ignore)) p0 = gh0.point;
        Vector3 dir = Vector3.ProjectOnPlane(path.GetNode(1).position - path.GetNode(0).position, Vector3.up).normalized;

        // each drift run: teleport back to the start every 20 steps (keeps the kart on the road) but never touch drift state
        System.Func<float, float, float, string> drift = (steer, holdSeconds, speed) =>
        {
            Place(p0, dir, speed); input.throttle = 1; input.steer = steer; input.held = false;
            kart.stats.accelPerSecond = 3f;
            for (int q = 0; q < 12; q++) Step(1);                                  // settle onto the road first
            input.held = true; input.pressed = true;
            float t = 0; bool everDrifting = false; int maxTier = 0;
            for (int i = 0; i < Mathf.RoundToInt(holdSeconds * 50); i++)
            {
                Step(1); t += 0.02f; everDrifting |= kart.Drift.IsDrifting; maxTier = Mathf.Max(maxTier, kart.Drift.Tier);
                if (i % 20 == 19) { var rbv = rb.linearVelocity.magnitude; rb.isKinematic = true; kart.transform.SetPositionAndRotation(p0 + Vector3.up * 0.9f, Quaternion.LookRotation(dir, Vector3.up)); rb.isKinematic = false; rb.linearVelocity = dir * rbv; rb.rotation = kart.transform.rotation; Physics.SyncTransforms(); }
            }
            input.held = false; Step(1);
            kart.stats.accelPerSecond = 0.5f;
            var st = kart.State; return $"[grounded={st.Grounded} gh={st.GroundHit} gd={st.GroundDistance:F2} ny={st.GroundNormal.y:F2} spd={kart.CurrentSpeed:F0} stun={st.Stunned} glide={st.Gliding} jump={st.OnJumpPanel} locked={kart.controlsLocked} hop={kart.Drift.Hopping} dir={kart.Drift.DriftDir} inputSteer={kart.Input.Steer} held={kart.Input.DriftHeld}] drifted={everDrifting} tierReached={maxTier} boostGranted={kart.Boost.Remaining:F2}s";
        };
        sb.AppendLine("T1 tap, no steer (hop only): " + drift(0f, 0.8f, 70f));
        sb.AppendLine("T2a steer, hold 1.0 s (below tier 1): " + drift(1f, 1.0f, 70f));
        sb.AppendLine("T2b steer, hold 1.7 s (tier 1 expects 0.75): " + drift(1f, 1.7f, 70f));
        sb.AppendLine("T2c steer, hold 4.2 s (tier 2 expects 1.5): " + drift(1f, 4.2f, 70f));
        sb.AppendLine("T2d steer, hold 7.2 s (tier 3 expects 2.5): " + drift(-1f, 7.2f, 70f));
        sb.AppendLine("T3 start below 40 u/s (expects no drift): " + drift(1f, 1.2f, 25f));
        // T4 boost stacking and coasting
        Place(p0, dir, 40f); kart.Boost.Remaining = 0; kart.Boost.AddBoost(2f, BoostSource.Mushroom); kart.Boost.AddBoost(1f, BoostSource.Pad); float aa = kart.Boost.Remaining; kart.Boost.AddBoost(3f, BoostSource.Pad); float b = kart.Boost.Remaining;
        sb.AppendLine($"T4 stacking: 2 then +1 -> {aa:F1}; then +3 -> {b:F1} (expects 2.0, 3.0)");
        input.throttle = 0; float v0 = kart.CurrentSpeed; for (int i = 0; i < 50; i++) Step(1); sb.AppendLine($"T4 boost while coasting: speed {v0:F0} -> {kart.CurrentSpeed:F0} after 1 s with throttle off (expects a rise toward 115)");
        kart.Boost.Cancel();
        // T5 cancel on hit
        input.throttle = 1; Place(p0, dir, 70f); for (int q = 0; q < 12; q++) Step(1); input.steer = 1; input.held = true; input.pressed = true; for (int i = 0; i < 45; i++) Step(1);
        bool drifting = kart.Drift.IsDrifting; kart.HitReaction.Apply(HitType.Tumble, p0); Step(2);
        sb.AppendLine($"T5 hit while drifting: was drifting={drifting}, after hit drifting={kart.Drift.IsDrifting} boost={kart.Boost.Remaining:F2}");
        Physics.simulationMode = SimulationMode.FixedUpdate;
        return sb.ToString();
    }
}
