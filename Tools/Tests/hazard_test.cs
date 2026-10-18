using System.Linq;
using System.Reflection;
using System.Text;
using HarborRacer.Hazards;
using HarborRacer.Inputs;
using HarborRacer.Kart;
using HarborRacer.Race;
using UnityEngine;

public class DriveInput : IKartInput
{
    public float throttle = 1f;
    public float Throttle => throttle;
    public float Brake => 0f;
    public float Steer => 0f;
    public float Pitch => 0f;
    public bool DriftHeld => false;
    public bool DriftPressed => false;
    public bool TrickPressed => false;
    public bool ItemPressed => false;
    public bool ItemBackPressed => false;
    public void EndFixedStep() { }
}

public static class HazardTest
{
    static MethodInfo fu;
    static readonly System.Type[] Skip = { typeof(PlayerKartInput), typeof(KartEffects), typeof(KartDebugHUD), typeof(RaceDebugHUD), typeof(HarborRacer.CameraRig.SimpleChaseCam) };

    static void AwakeAll()
    {
        System.Func<MonoBehaviour, int> order = m => m is CheckpointTrack ? 0 : m is KartController ? 2 : m is RaceManager ? 5 : 3;
        foreach (var m in Object.FindObjectsByType<MonoBehaviour>().OrderBy(order))
        {
            if (m == null || Skip.Contains(m.GetType())) continue;
            if (m.GetType().Namespace == null || !m.GetType().Namespace.StartsWith("HarborRacer")) continue;
            var a = m.GetType().GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
            if (a != null) a.Invoke(m, null);
        }
        fu = typeof(KartController).GetMethod("FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
    }

    static float GroundY(Vector3 p)
    {
        return Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out var h, 200f, HarborRacer.Layers.GroundMask, QueryTriggerInteraction.Ignore) ? h.point.y : p.y;
    }

    static void Put(KartController k, Vector3 pos, Vector3 dir, float speed)
    {
        var rb = k.Body; rb.isKinematic = true;
        dir.y = 0; dir.Normalize();
        pos.y = GroundY(pos) + 1.05f;
        k.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir, Vector3.up));
        rb.isKinematic = false; rb.linearVelocity = dir * speed; rb.rotation = k.transform.rotation;
        k.CurrentSpeed = speed; k.ForwardSpeed = speed; k.controlsLocked = false;
        k.Boost.Remaining = 0; k.Drift.Cancel(); k.Glider.Cancel(); k.AntiGravity.Cancel();
        k.State.Stunned = false; k.State.Star = false; k.State.Bullet = false; k.State.OnJumpPanel = false;
        Physics.SyncTransforms();
    }

    static void Step(KartController k, int n, Trolley[] trolleys = null)
    {
        for (int i = 0; i < n; i++)
        {
            if (trolleys != null) foreach (var t in trolleys) if (t != null) t.Tick(Time.fixedDeltaTime);
            foreach (var rp in Object.FindObjectsByType<RacerProgress>()) { }
            fu.Invoke(k, null);
            Physics.Simulate(Time.fixedDeltaTime);
        }
    }

    public static string Run()
    {
        var sb = new StringBuilder();
        Physics.simulationMode = SimulationMode.Script; Physics.autoSyncTransforms = true;
        AwakeAll();
        var racers = Object.FindObjectsByType<RacerProgress>();
        foreach (var r in racers) { var pk = r.GetComponent<PlayerKartInput>(); if (pk) pk.enabled = false; }
        // park the AI karts far away so they don't interfere
        var player = racers.First(r => r.isPlayer).Kart;
        var drive = new DriveInput(); player.Input = drive;
        foreach (var r in racers.Where(r => !r.isPlayer)) { r.Kart.Body.isKinematic = true; r.transform.position = new Vector3(0, 400, 0); r.Kart.controlsLocked = true; }

        // ---------------- A) crates ----------------
        sb.AppendLine("A) crates: speed cap 5 unless boosting");
        var crates = Object.FindObjectsByType<CrateZone>().Where(c => c.name != "Crate_04" && c.name != "Crate_13").ToArray();   // crash boxes (not barrels)
        var c1 = crates[0]; var c2 = crates[1];
        Put(player, c1.transform.position - Vector3.right * 9f, Vector3.right, 60f);
        Step(player, 14);
        sb.AppendLine($"  plain: {c1.name} broken={c1.Broken} speed after={player.CurrentSpeed:F1} (expect <=~5-ish, was 60)  stillThere={c1.gameObject.activeInHierarchy} visualActive={c1.visual.activeSelf}");
        Put(player, c2.transform.position - Vector3.right * 9f, Vector3.right, 60f);
        player.Boost.AddBoost(3f, BoostSource.Debug);
        Step(player, 14);
        sb.AppendLine($"  boosting: {c2.name} broken={c2.Broken} speed after={player.CurrentSpeed:F1} (expect still high, boost exempts)");

        // ---------------- B) SpinOut / Tumble reactions ----------------
        sb.AppendLine("B) hit reactions");
        Put(player, new Vector3(player.transform.position.x, player.transform.position.y, player.transform.position.z), player.transform.forward, 70f);
        player.Boost.AddBoost(2f, BoostSource.Debug);
        bool applied = player.HitReaction.Apply(HitType.Tumble, player.transform.position + player.transform.right * 3f);
        sb.AppendLine($"  Tumble applied={applied} stunned={player.State.Stunned} boost cancelled={player.Boost.Remaining == 0f}");
        Step(player, 25);
        sb.AppendLine($"  0.5 s later speed={player.CurrentSpeed:F1} (expect ~0), stunLeft={player.HitReaction.StunLeft:F2}");
        bool again = player.HitReaction.Apply(HitType.SpinOut, player.transform.position);
        sb.AppendLine($"  second hit during cooldown/stun applied={again} (expect False)");
        Step(player, 60);
        sb.AppendLine($"  after 1.7 s stunned={player.State.Stunned} (expect False) speed={player.CurrentSpeed:F1} (throttle held -> accelerating again)");
        Put(player, player.transform.position, player.transform.forward, 50f);
        player.State.Star = true;
        sb.AppendLine($"  star kart: Explosion applied={player.HitReaction.Apply(HitType.Explosion, player.transform.position)} (expect False: invulnerable)");
        player.State.Star = false;
        Step(player, 60);   // let the hit cooldown expire
        Put(player, player.transform.position, player.transform.forward, 50f);
        Step(player, 50);
        player.HitReaction.Apply(HitType.Explosion, player.transform.position);
        float vy = player.Body.linearVelocity.y;
        sb.AppendLine($"  Explosion: speed={player.CurrentSpeed:F0} (0 expected) popped up vy={vy:F1} stunned={player.State.Stunned} stunLeft={player.HitReaction.StunLeft:F2} (1.8)");

        // ---------------- C) trolley ----------------
        sb.AppendLine("C) trolley");
        var trolleys = Object.FindObjectsByType<Trolley>();
        var tr = trolleys.First(t => t.name == "Trolley_02");
        Vector3 p0 = tr.transform.position; int n0 = tr.nodeIndex;
        for (int i = 0; i < 100; i++) { tr.Tick(0.02f); Physics.Simulate(0.02f); }
        float moved = Vector3.Distance(tr.transform.position, p0);
        sb.AppendLine($"  moved {moved:F1} u in 2 s (expect ~26 at 13 u/s), node {n0} -> {tr.nodeIndex}");

        // kart rushing at the trolley's side from the front
        Vector3 tf = tr.transform.forward; tf.y = 0; tf.Normalize();
        foreach (var speed in new[] { 70f, 20f })
        {
            var hits = 0;
            System.Action<HitType> h = t => hits++;
            player.HitReaction.Hit += h;
            Put(player, tr.transform.position + tf * 22f, -tf, speed);
            Step(player, 5);            // let hit cooldown from the previous test expire
            player.HitReaction.Step(1f);
            Put(player, tr.transform.position + tf * 22f, -tf, speed);
            for (int i = 0; i < 40; i++) { tr.Tick(0.02f); fu.Invoke(player, null); Physics.Simulate(0.02f); }
            sb.AppendLine($"  kart at {speed} u/s into the trolley: hits={hits} (expect {(speed > 35 ? 1 : 0)}) stunned={player.State.Stunned} speed now={player.CurrentSpeed:F0}");
            player.HitReaction.Hit -= h;
            player.State.Stunned = false;
        }
        // star kart: no hit
        { int hits = 0; System.Action<HitType> h = t => hits++; player.HitReaction.Hit += h;
          Put(player, tr.transform.position + tf * 22f, -tf, 70f); player.State.Star = true; player.HitReaction.Step(1f);
          for (int i = 0; i < 40; i++) { tr.Tick(0.02f); fu.Invoke(player, null); Physics.Simulate(0.02f); }
          sb.AppendLine($"  star kart at 70 u/s: hits={hits} (expect 0)"); player.HitReaction.Hit -= h; player.State.Star = false; }

        // ---------------- D) kart vs kart ----------------
        sb.AppendLine("D) kart-vs-kart bump");
        var ai = racers.First(r => !r.isPlayer).Kart;
        ai.Body.isKinematic = false; ai.controlsLocked = false;
        ai.Input = new DriveInput();
        var spot = new Vector3(player.transform.position.x + 60f, player.transform.position.y, player.transform.position.z);
        Put(player, spot, Vector3.forward, 60f);
        Put(ai, spot + Vector3.right * 2.6f, Vector3.forward, 60f);
        int bumpP = 0, bumpA = 0;
        player.HitReaction.Hit += t => bumpP++; ai.HitReaction.Hit += t => bumpA++;
        player.HitReaction.Step(1f); ai.HitReaction.Step(1f);
        float gap0 = Vector3.Distance(player.transform.position, ai.transform.position);
        for (int i = 0; i < 60; i++) { fu.Invoke(player, null); fu.Invoke(ai, null); Physics.Simulate(0.02f); }
        float gap1 = Vector3.Distance(player.transform.position, ai.transform.position);
        sb.AppendLine($"  two karts side by side: bumps player={bumpP} ai={bumpA} (each at most 1 per 0.75 s), gap {gap0:F1} -> {gap1:F1} u (pushed apart)");
        ai.Body.isKinematic = true; ai.transform.position = new Vector3(0, 400, 0);

        // ---------------- E) spawner ----------------
        sb.AppendLine("E) trolley spawner");
        var rm = Object.FindFirstObjectByType<RaceManager>();
        rm.BeginRace();
        var sp = Object.FindFirstObjectByType<TrolleySpawner>();
        int g = 0; while (rm.State != RaceState.Racing && g++ < 2000) { foreach (var r in rm.Racers) r.GetComponent<RocketStart>()?.Tick(0.02f); rm.Tick(0.02f); sp.Tick(0.02f); }
        sb.AppendLine($"  at GO: spawned={sp.SpawnedCount} (the first tram appears at scene start)");
        for (int i = 0; i < 31 * 50; i++) { rm.Tick(0.02f); sp.Tick(0.02f); }
        sb.AppendLine($"  31 s into the race: spawned={sp.SpawnedCount} (expect 2), trolleys alive={Object.FindObjectsByType<Trolley>().Length}");

        // ---------------- F) cone ----------------
        sb.AppendLine("F) cones are dynamic bodies karts knock away");
        var cone = Object.FindObjectsByType<PylonBody>().First();
        var crb = cone.GetComponent<Rigidbody>();
        for (int i = 0; i < 50; i++) Physics.Simulate(0.02f);     // let the cone settle
        Vector3 cp0 = cone.transform.position;
        Put(player, cp0 - Vector3.right * 22f, Vector3.right, 70f);
        player.HitReaction.Step(1f);
        float minSpeed = 999f;
        for (int i = 0; i < 40; i++) { fu.Invoke(player, null); Physics.Simulate(0.02f); minSpeed = Mathf.Min(minSpeed, player.CurrentSpeed); }
        sb.AppendLine($"  cone moved {Vector3.Distance(cone.transform.position, cp0):F1} u, cone speed now {crb.linearVelocity.magnitude:F1}; kart min commanded speed during the hit {minSpeed:F0} (expect ~70, not slowed)");

        Physics.simulationMode = SimulationMode.FixedUpdate;
        foreach (var r in racers) r.Kart.Body.isKinematic = false;
        return sb.ToString();
    }
}
