using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarborRacer.Inputs;
using HarborRacer.Items;
using HarborRacer.Kart;
using HarborRacer.Race;
using HarborRacer.Track;
using UnityEngine;

public static class ItemTest
{
    class TestInput : IKartInput
    {
        public float throttle = 1f, steer = 0f;
        public bool use, back;
        public float Throttle => throttle; public float Brake => 0f; public float Steer => steer; public float Pitch => 0f;
        public bool DriftHeld => false; public bool DriftPressed => false; public bool TrickPressed => false;
        public bool ItemPressed => use; public bool ItemBackPressed => back;
        public void EndFixedStep() { use = false; back = false; }
    }

    static StringBuilder sb;
    static List<RacerProgress> racers;
    static MethodInfo fu;
    static RaceManager rm;

    static void Log(string name, bool ok, string detail = "") => sb.AppendLine($"{(ok ? "PASS" : "FAIL")}  {name}  {detail}");

    static void Step(int n, System.Action each = null)
    {
        for (int i = 0; i < n; i++)
        {
            foreach (var r in racers) fu.Invoke(r.Kart, null);
            Physics.Simulate(0.02f);
            foreach (var p in ItemProjectile.All.ToArray()) if (p != null) p.Tick(0.02f);
            each?.Invoke();
        }
    }

    static void Place(RacerProgress r, Vector3 pos, Vector3 dir, float speed = 0f)
    {
        var k = r.Kart; var rb = k.Body;
        rb.isKinematic = true;
        k.transform.SetPositionAndRotation(pos + Vector3.up * 1.2f, Quaternion.LookRotation(dir, Vector3.up));
        rb.isKinematic = false; rb.linearVelocity = dir * speed; rb.rotation = k.transform.rotation;
        k.CurrentSpeed = speed; k.controlsLocked = false;
        k.Boost.Remaining = 0; k.State.Stunned = false; k.State.Star = false; k.State.Bullet = false;
        k.HitReaction.Step(5f); // drains cooldown
        k.Zones.Clear();
        if (k.Respawn.Respawning) typeof(KartRespawn).GetMethod("Finish", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(k.Respawn, null);
    }

    static void ClearItems() { foreach (var p in ItemProjectile.All.ToArray()) if (p != null) p.Kill(); }

    public static string Run()
    {
        sb = new StringBuilder();
        Physics.SyncTransforms();
        var skip = new System.Type[] { typeof(PlayerKartInput), typeof(HarborRacer.Kart.KartEffects), typeof(KartDebugHUD), typeof(RaceDebugHUD), typeof(HarborRacer.CameraRig.SimpleChaseCam) };
        foreach (var m in Object.FindObjectsByType<MonoBehaviour>().OrderBy(m => m is CheckpointTrack ? 0 : m is KartController ? 2 : m is RaceManager ? 5 : 3))
        {
            if (m == null || skip.Contains(m.GetType())) continue;
            if (m.GetType().Namespace == null || !m.GetType().Namespace.StartsWith("HarborRacer")) continue;
            var a = m.GetType().GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
            if (a != null) a.Invoke(m, null);
        }
        foreach (var r in Object.FindObjectsByType<RacerProgress>()) { var pk = r.GetComponent<PlayerKartInput>(); if (pk) pk.enabled = false; var pd = r.GetComponent<PathDriverInput>(); if (pd) pd.enabled = false; }
        Physics.simulationMode = SimulationMode.Script;
        rm = Object.FindFirstObjectByType<RaceManager>();
        rm.BeginRace();
        fu = typeof(KartController).GetMethod("FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        racers = rm.Racers.ToList();
        var A = rm.Player; var B = racers.First(r => !r.isPlayer);
        foreach (var r in racers) if (r != A && r != B) { r.Kart.Body.isKinematic = true; r.transform.position = new Vector3(0, 800 + racers.IndexOf(r) * 20, 0); r.Kart.controlsLocked = true; r.Kart.State.Star = false; }
        var inA = new TestInput(); var inB = new TestInput { throttle = 0f };
        A.Kart.Input = inA; B.Kart.Input = inB;
        var hA = A.GetComponent<ItemHolder>(); var hB = B.GetComponent<ItemHolder>();
        B.isPlayer = true;      // keep the placeholder AI brain out of the way; B is driven by the test

        var path = GameObject.Find("Markers/Paths/AI/ComputerDriverPATHPeach").GetComponent<TrackPath>();
        Vector3 p0 = path.GetNode(0).position + Vector3.up * 0.3f;
        Vector3 dir = Vector3.ProjectOnPlane(path.GetNode(1).position - path.GetNode(0).position, Vector3.up).normalized;
        Vector3 Ahead(float d) => p0 + dir * d;

        // ---- 1. distribution table ----
        var rng = new System.Random(7);
        var dist = new StringBuilder();
        bool distOk = true;
        for (int pos = 1; pos <= 8; pos++)
        {
            var counts = new Dictionary<ItemId, int>();
            for (int i = 0; i < 1000; i++) { var id = ItemDistribution.Roll(pos, rng); counts[id] = counts.TryGetValue(id, out var c) ? c + 1 : 1; }
            var row = ItemDistribution.Row(pos); int total = row.Sum(e => e.weight);
            foreach (var e in row) { float exp = 1000f * e.weight / total; if (!counts.TryGetValue(e.id, out var got) || Mathf.Abs(got - exp) > 90f) distOk = false; }
            if (counts.Keys.Any(k => !row.Any(e => e.id == k))) distOk = false;
            dist.Append($"[{pos}:" + string.Join(",", counts.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}={kv.Value}")) + "] ");
        }
        Log("1000 rolls per position match the table (+-9%)", distOk, dist.ToString());
        var blueRolls = 0; for (int i = 0; i < 500; i++) if (ItemDistribution.Roll(7, rng, true, blueShellActive: true) == ItemId.BlueShell) blueRolls++;
        Log("no second blue shell while one is active", blueRolls == 0);
        var bulletRolls = 0; for (int i = 0; i < 500; i++) if (ItemDistribution.Roll(8, rng, canBullet: false) == ItemId.BulletBill) bulletRolls++;
        Log("no bullet where it is forbidden", bulletRolls == 0);

        // ---- 2. item box flow ----
        var box = Object.FindObjectsByType<ItemBoxZone>().OrderBy(z => Vector3.Distance(z.transform.position, p0)).First();
        Place(A, box.transform.position - Vector3.up * 0.4f, dir, 0f); inA.throttle = 0f;
        hA.Give(ItemId.None);
        A.Position = 4;
        Step(2);
        Log("box touched: breaks and starts roulette", box.Broken && hA.State == HolderState.Rolling, $"state={hA.State} broken={box.Broken}");
        int tick = 0;
        while (hA.State == HolderState.Rolling && tick < 400) { Step(1); box.Tick(0.02f); tick++; }
        Log("roulette lasts ~4 s and ends with an item", tick >= 190 && tick <= 215 && hA.State == HolderState.Holding, $"ticks={tick} item={hA.Current}");
        Log("box respawned after 1 s", !box.Broken, $"broken={box.Broken}");
        hA.Give(ItemId.None); inA.throttle = 1f;

        // ---- 3. mushroom family + coin ----
        Place(A, p0, dir, 40f);
        hA.Give(ItemId.Mushroom); inA.use = true; Step(2);
        Log("mushroom: boost 2 s, item consumed", A.Kart.Boost.Boosting && hA.State == HolderState.Empty, $"boost={A.Kart.Boost.Remaining:F2}");
        hA.Give(ItemId.TripleMushroom);
        inA.use = true; Step(1); inA.use = true; Step(1);
        Log("triple mushroom: 2 of 3 uses spent", hA.UsesLeft == 1 && hA.Current == ItemId.TripleMushroom, $"uses={hA.UsesLeft}");
        inA.use = true; Step(1);
        Log("triple mushroom: empty after 3", hA.State == HolderState.Empty);
        A.Kart.Boost.Remaining = 0;
        hA.Give(ItemId.GoldenMushroom);
        int boosts = 0; for (int i = 0; i < 260; i++) { if (i % 30 == 0) inA.use = true; Step(1); if (A.Kart.Boost.Remaining > 1.9f) boosts++; }
        Log("golden mushroom: unlimited boosts within 10 s", boosts >= 5, $"boostsSeen={boosts} left={hA.GoldenLeft:F1}");
        Step(280);
        Log("golden mushroom ends after 10 s", hA.State == HolderState.Empty && hA.GoldenLeft <= 0f);
        var wallet = A.GetComponent<CoinWallet>(); wallet.Clear();
        hA.Give(ItemId.Coin); inA.use = true; Step(2);
        Log("coin item: +1 coin", wallet.Count == 1, $"coins={wallet.Count}");
        for (int i = 0; i < 12; i++) wallet.Add(1);
        Log("coins cap at 10", wallet.Count == 10);
        Place(A, p0, dir, 80f); A.Kart.Boost.Remaining = 0; Step(120);
        Log("10 coins: top speed 95 u/s", Mathf.Abs(A.Kart.MaxSpeedNow - 95f) < 0.5f, $"max={A.Kart.MaxSpeedNow}");
        Place(A, p0, dir, 40f);
        A.Kart.HitReaction.Apply(HitType.Tumble, p0 + dir * 5f);
        Log("a hit costs 3 coins", wallet.Count == 7, $"coins={wallet.Count}");
        wallet.Clear();

        // ---- 4. banana ----
        ClearItems(); Place(A, p0, dir, 0f); Place(B, Ahead(40f), dir, 0f); inA.throttle = 0f;
        hA.Give(ItemId.Banana); inA.use = true; Step(60);
        var ban = ItemProjectile.All.OfType<Banana>().FirstOrDefault();
        Log("banana thrown forward lands on the road", ban != null && Vector3.Distance(ban.transform.position, p0) > 10f, ban != null ? $"landed at {Vector3.Distance(ban.transform.position, p0):F0}u ahead" : "none");
        Place(B, ban != null ? ban.transform.position : Ahead(30f), dir, 0f); Step(3);
        Log("kart on a banana spins out", B.Kart.State.Stunned && B.Kart.HitReaction.LastHit == HitType.SpinOut, $"stunned={B.Kart.State.Stunned} hit={B.Kart.HitReaction.LastHit}");
        Log("banana is gone after the hit", !ItemProjectile.All.OfType<Banana>().Any());
        Step(100);

        // ---- 5. hold behind ----
        ClearItems(); Place(A, Ahead(20f), dir, 0f); Place(B, p0, dir, 0f);
        hA.Give(ItemId.Banana); inA.back = true; Step(2);
        Log("back on a single banana holds it behind", hA.HoldingBehind && hA.State == HolderState.Holding);
        hB.Give(ItemId.GreenShell); inB.use = true; Step(40);
        Log("held banana blocks a shell from behind", A.Kart.State.Stunned == false && hA.State == HolderState.Empty, $"stunned={A.Kart.State.Stunned} item={hA.State}");

        // ---- 6. green shell + bounce ----
        ClearItems(); Place(A, p0, dir, 0f); Place(B, Ahead(60f), dir, 0f); Step(5);
        hA.Give(ItemId.GreenShell); inA.use = true; Step(70);
        Log("green shell knocks the kart ahead (tumble)", B.Kart.State.Stunned && B.Kart.HitReaction.LastHit == HitType.Tumble, $"stunned={B.Kart.State.Stunned} hit={B.Kart.HitReaction.LastHit}");
        Step(100);

        // ---- 7. triple green (orbit) ----
        ClearItems(); Place(A, p0, dir, 0f); Place(B, Ahead(60f), dir, 0f);
        hA.Give(ItemId.TripleGreen);
        var ent = A.GetComponent<ItemEntourage>();
        Log("triple green: 3 orbiting shells", hA.UsesLeft == 3 && ent.Count == 3 && ent.Mode == EntourageMode.Orbit, $"uses={hA.UsesLeft} visuals={ent.Count}");
        inA.use = true; Step(2);
        Log("firing spends one", hA.UsesLeft == 2 && ent.Count == 2 && ItemProjectile.All.OfType<GreenShell>().Count() == 1);
        hA.Give(ItemId.None);

        // ---- 8. red shell homing ----
        ClearItems(); Place(A, p0, dir, 0f); Place(B, Ahead(25f), dir, 0f); Step(5);
        A.Position = 2; B.Position = 1;
        hA.Give(ItemId.RedShell); inA.use = true; Step(60);
        Log("red shell locks on and hits the racer ahead", B.Kart.State.Stunned, $"stunned={B.Kart.State.Stunned}");
        Step(100);

        // ---- 9. bob-omb ----
        ClearItems(); Place(A, p0, dir, 0f); Place(B, Ahead(32f), dir, 0f); Step(5);
        hA.Give(ItemId.BobOmb); inA.use = true; Step(240);
        Log("bob-omb explodes and the nearby kart is blown up", B.Kart.HitReaction.LastHit == HitType.Explosion, $"hit={B.Kart.HitReaction.LastHit} bombs left={ItemProjectile.All.OfType<BobOmb>().Count()}");
        Step(120);

        // ---- 10. star ----
        ClearItems(); Place(A, p0, dir, 40f); Place(B, Ahead(300f), dir, 0f); inA.throttle = 1f;
        hA.Give(ItemId.Star); inA.use = true; Step(2);
        Log("star: invincible with boost-level top speed", A.Kart.State.Star && A.Kart.MaxSpeedNow >= 115f, $"star={A.Kart.State.Star} max={A.Kart.MaxSpeedNow}");
        Banana.Create(B.Kart, A.transform.position + dir * 6f, Vector3.zero); Step(60);
        Log("star ignores bananas", !A.Kart.State.Stunned);
        float t = 1.24f; while (A.Kart.State.Star && t < 12f) { Step(5); t += 0.1f; }
        Log("star lasts ~7.5 s", t > 7.2f && t < 7.9f, $"t={t:F1}");

        // ---- 11. bullet bill ----
        ClearItems(); Place(A, p0 - dir * 2f, dir, 30f); Place(B, Ahead(300f), dir, 0f);
        var startPos = A.transform.position;
        hA.Give(ItemId.BulletBill); inA.use = true; Step(2);
        Log("bullet: active, kart hidden", A.Kart.State.Bullet && A.Kart.GetComponentsInChildren<Renderer>().Count(r => r.enabled) <= 2);
        var trace = new List<string>(); System.Func<int> bnode = () => A.GetComponent<PathDriverInput>() != null ? A.GetComponent<PathDriverInput>().nodeIndex : -1; float maxSp = 0; int steps = 0; string why = ""; Vector3 lastPos = A.transform.position; while (A.Kart.State.Bullet && steps < 800) { Step(1); maxSp = Mathf.Max(maxSp, A.Kart.ForwardSpeed); steps++; if (A.Kart.Respawn.Respawning && why == "") why = $"respawn at step {steps} pos={A.transform.position}"; if (A.Kart.State.Bullet) lastPos = A.transform.position; if (steps % 12 == 0) trace.Add($"({A.transform.position.x:F0},{A.transform.position.y:F0},{A.transform.position.z:F0}) v={A.Kart.ForwardSpeed:F0} gnd={A.Kart.State.Grounded} nd={bnode()}"); }
        sb.AppendLine("  trace: " + string.Join(" | ", trace));
        Log("bullet: ~11 s, ~130 u/s, ends at 70", steps > 520 && steps < 580 && maxSp > 100f && Mathf.Abs(A.Kart.CurrentSpeed - 70f) < 6f, $"steps={steps} maxSpeed={maxSp:F0} speedNow={A.Kart.CurrentSpeed:F0} input restored={(A.Kart.Input == inA)} why={why} lastPos={lastPos} left={hA.BulletLeft:F1}");

        // ---- 12. blue shell ----
        ClearItems(); Place(A, p0, dir, 0f); Place(B, Ahead(140f), dir, 0f);
        A.Position = 2; B.Position = 1; rm.Sorted.Clear(); rm.Sorted.Add(B); rm.Sorted.Add(A);
        hA.Give(ItemId.BlueShell); inA.use = true; Step(2); sb.AppendLine($"  blue: hA.State={hA.State} cur={hA.Current} canAct={hA.CanAct} bullet={A.Kart.State.Bullet} stunned={A.Kart.State.Stunned} locked={A.Kart.controlsLocked} shells={ItemProjectile.All.Count}");
        Log("only one blue shell may be active", BlueShell.IsActive);
        bool bExploded = false; B.Kart.HitReaction.Hit += (ht) => { if (ht == HitType.Explosion) bExploded = true; sb.AppendLine("  B got hit: " + ht); }; sb.AppendLine($"  B before blue: star={B.Kart.State.Star} bullet={B.Kart.State.Bullet} resp={B.Kart.Respawn.Respawning} stunned={B.Kart.State.Stunned} invuln={B.Kart.HitReaction.IsInvulnerable}");
        var btr = new List<string>(); for (int q = 0; q < 300; q++) { Step(1); if (q % 20 == 0) { var bs = ItemProjectile.All.OfType<BlueShell>().FirstOrDefault(); btr.Add(bs == null ? "gone" : $"({bs.transform.position.x:F0},{bs.transform.position.y:F0},{bs.transform.position.z:F0}) tgt={(bs.Target != null)}"); } }
        sb.AppendLine("  blue trace: B at " + B.transform.position + " | " + string.Join(" | ", btr));
        Log("blue shell explodes on first place", bExploded && !BlueShell.IsActive, $"hit={B.Kart.HitReaction.LastHit} active={BlueShell.IsActive}");

        ClearItems();
        Physics.simulationMode = SimulationMode.FixedUpdate;
        return sb.ToString();
    }
}
