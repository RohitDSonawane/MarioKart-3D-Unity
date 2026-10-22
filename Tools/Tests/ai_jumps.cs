using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarborRacer.Inputs;
using HarborRacer.Kart;
using HarborRacer.Race;
using HarborRacer.Items;
using UnityEngine;

/// <summary>Flags any AI kart that moves more than 20 u in one 0.02 s step (max real speed is ~2.6 u per step): teleports and hops.</summary>
public static class AiJumps
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
        // the human drives the racing line like an AI here (the real game has no driver on it)
        var pd = player.gameObject.AddComponent<PathDriverInput>(); pd.kart = player.Kart;
        pd.path = GameObject.Find("Markers/Paths/MarioRaceEndPath").GetComponent<HarborRacer.Track.TrackPath>(); pd.SnapToPath(); player.Kart.Input = pd;

        var last = racers.ToDictionary(r => r, r => r.transform.position);
        var npF = typeof(PathDriverInput).GetField("noProgressTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        var wasResp = racers.ToDictionary(r => r, r => false);
        int jumps = 0;
        int steps = seconds * 50;
        for (int i = 1; i <= steps; i++)
        {
            foreach (var r in racers) r.GetComponent<RocketStart>()?.Tick(0.02f);
            rm.Tick(0.02f);
            var before = racers.ToDictionary(r => r, r => npF != null && r.GetComponent<PathDriverInput>() != null ? (float)npF.GetValue(r.GetComponent<PathDriverInput>()) : 0f);
            foreach (var r in racers) fu.Invoke(r.Kart, null);
            Physics.Simulate(0.02f);
            foreach (var ip in ItemProjectile.All.ToArray()) if (ip != null) ip.Tick(0.02f);
            foreach (var r in racers)
            {
                var p = r.transform.position;
                float d = (p - last[r]).magnitude;
                bool resp = r.Kart.Respawn.Respawning;
                if (d > 20f && jumps < 40)
                {
                    jumps++;
                    var drv = r.GetComponent<PathDriverInput>();
                    sb.AppendLine($"JUMP t={rm.RaceTime:F2}s state={rm.State} {r.racerName} {d:F1} u  ({last[r].x:F0},{last[r].y:F0},{last[r].z:F0}) -> ({p.x:F0},{p.y:F0},{p.z:F0}) node={(drv ? drv.nodeIndex : -1)} noProgressBefore={before[r]:F2} respawning={resp} speed={r.Kart.ForwardSpeed:F0}");
                }
                if (resp && !wasResp[r]) sb.AppendLine($"  respawn t={rm.RaceTime:F2}s {r.racerName}");
                wasResp[r] = resp;
                last[r] = p;
            }
        }
        sb.AppendLine("done: " + jumps + " jumps in " + seconds + " s, race time " + rm.RaceTime.ToString("F1"));
        Physics.simulationMode = SimulationMode.FixedUpdate;
        return sb.ToString();
    }
}
