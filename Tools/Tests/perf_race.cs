using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarborRacer.Inputs;
using HarborRacer.Kart;
using HarborRacer.Race;
using UnityEngine;
using HarborRacer.Items;

public static class PerfRace
{
    public static string Run(int seconds)
    {
        var sb = new StringBuilder();
        var rm = Object.FindFirstObjectByType<RaceManager>();
        var skip = new System.Type[] { typeof(PlayerKartInput), typeof(KartEffects), typeof(KartDebugHUD), typeof(RaceDebugHUD), typeof(HarborRacer.CameraRig.SimpleChaseCam) };
        foreach (var m in Object.FindObjectsByType<MonoBehaviour>().OrderBy(m => m is CheckpointTrack ? 0 : m is KartController ? 2 : m is RaceManager ? 5 : 3))
        {
            if (m == null || skip.Contains(m.GetType())) continue;
            if (m.GetType().Namespace == null || !m.GetType().Namespace.StartsWith("HarborRacer")) continue;
            var a = m.GetType().GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
            if (a != null) a.Invoke(m, null);
        }
        foreach (var k in Object.FindObjectsByType<RacerProgress>()) { var pk = k.GetComponent<PlayerKartInput>(); if (pk) pk.enabled = false; }
        Physics.simulationMode = SimulationMode.Script; Physics.autoSyncTransforms = true;
        rm.BeginRace();
        var fu = typeof(KartController).GetMethod("FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        var racers = rm.Racers;
        var player = rm.Player;
        var pd = player.gameObject.AddComponent<PathDriverInput>(); pd.kart = player.Kart;
        pd.path = GameObject.Find("Markers/Paths/MarioRaceEndPath").GetComponent<HarborRacer.Track.TrackPath>(); pd.SnapToPath(); pd.hopMinDistanceFromHuman = 0f; pd.path = GameObject.Find("Markers/Paths/AI/ComputerDriverPATH Toad").GetComponent<HarborRacer.Track.TrackPath>(); pd.SnapToPath(); pd.racerAI = true; pd.profile = AIProfile.ForName("Standin"); player.Kart.Input = pd;
        var boxes = Object.FindObjectsByType<ItemBoxZone>();
        var names = new[] { "rm.Tick+rocket", "kart FixedUpdate x8", "Physics.Simulate", "item projectiles", "item boxes", "AI+other Update" };
        var tot = new double[names.Length]; var mx = new double[names.Length]; var gc0 = System.GC.CollectionCount(0); long mem0 = System.GC.GetTotalMemory(false);
        var frames = new List<(double ms, int step, string worst)>();
        var sw = new System.Diagnostics.Stopwatch();
        double T() { double t = sw.Elapsed.TotalMilliseconds; sw.Restart(); return t; }
        int steps = seconds * 50;
        for (int i = 1; i <= steps; i++)
        {
            var cur = new double[names.Length];
            sw.Restart();
            foreach (var r in racers) r.GetComponent<RocketStart>()?.Tick(0.02f);
            rm.Tick(0.02f); cur[0] = T();
            foreach (var r in racers) { var t0 = System.Diagnostics.Stopwatch.StartNew(); var h0 = r.GetComponent<ItemHolder>(); var it0 = h0 ? h0.Current : ItemId.None; bool rsp0 = r.Kart.Respawn.Respawning; fu.Invoke(r.Kart, null); double d = t0.Elapsed.TotalMilliseconds; if (d > 8) sb.AppendLine($"  spike step {i}: {r.racerName} FixedUpdate {d:F0} ms, item {it0}->{(h0 ? h0.Current : ItemId.None)} state {(h0 ? h0.State.ToString() : "-")} respawning {rsp0}->{r.Kart.Respawn.Respawning} speed {r.Kart.ForwardSpeed:F0} grounded {r.Kart.State.Grounded}"); }
            cur[1] = T();
            Physics.Simulate(0.02f); cur[2] = T();
            foreach (var ip in ItemProjectile.All.ToArray()) if (ip != null) ip.Tick(0.02f); cur[3] = T();
            foreach (var bz in boxes) bz.Tick(0.02f); cur[4] = T();
            double all = 0; int wi = 0;
            for (int k = 0; k < cur.Length; k++) { tot[k] += cur[k]; mx[k] = System.Math.Max(mx[k], cur[k]); all += cur[k]; if (cur[k] > cur[wi]) wi = k; }
            frames.Add((all, i, names[wi]));
        }
        var s = frames.Select(f => f.ms).OrderBy(x => x).ToArray();
        sb.AppendLine($"{steps} steps, state={rm.State}. avg {s.Average():F2} ms, p50 {s[s.Length/2]:F2}, p95 {s[(int)(s.Length*0.95)]:F2}, p99 {s[(int)(s.Length*0.99)]:F2}, max {s.Last():F2} ms; GC gen0 collections {System.GC.CollectionCount(0)-gc0}, managed mem {mem0/1048576}MB -> {System.GC.GetTotalMemory(false)/1048576}MB");
        for (int k = 0; k < names.Length; k++) sb.AppendLine($"  {names[k],-22} avg {tot[k]/steps:F3} ms  max {mx[k]:F2} ms");
        sb.AppendLine("worst steps: " + string.Join(" | ", frames.OrderByDescending(f => f.ms).Take(12).Select(f => $"#{f.step} {f.ms:F1}ms ({f.worst})")));
        Physics.simulationMode = SimulationMode.FixedUpdate;
        return sb.ToString();
    }
}
