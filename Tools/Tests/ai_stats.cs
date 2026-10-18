using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarborRacer.Inputs;
using HarborRacer.Kart;
using HarborRacer.Race;
using UnityEngine;
using HarborRacer.Items;

public static class AiStats
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
        var drifts = racers.ToDictionary(r => r, r => new int[4]);
        var used = new Dictionary<ItemId,int>(); var hitsOn = racers.ToDictionary(r => r, r => 0); var hitTypes = new Dictionary<HitType,int>();
        foreach (var r in racers)
        {
            var rr = r;
            rr.Kart.Drift.DriftReleased += t => { if (!rr.isPlayer) drifts[rr][Mathf.Clamp(t,0,3)]++; };
            var h = rr.GetComponent<ItemHolder>(); if (h != null && !rr.isPlayer) h.ItemUsed += id => { used[id] = used.TryGetValue(id, out var c) ? c + 1 : 1; };
            rr.Kart.HitReaction.Hit += ht => { if (ht != HitType.Bump) { hitsOn[rr]++; hitTypes[ht] = hitTypes.TryGetValue(ht, out var c) ? c + 1 : 1; } };
        }
        var wasResp = racers.ToDictionary(r => r, r => false);
        var stuckT = racers.ToDictionary(r => r, r => 0f);
        var events = new List<string>();
        var respCount = racers.ToDictionary(r => r, r => 0);
        int steps = seconds * 50;
        for (int i = 1; i <= steps; i++)
        {
            foreach (var r in racers) r.GetComponent<RocketStart>()?.Tick(0.02f);
            rm.Tick(0.02f);
            foreach (var r in racers) fu.Invoke(r.Kart, null);
            Physics.Simulate(0.02f);
            foreach (var ip in ItemProjectile.All.ToArray()) if (ip != null) ip.Tick(0.02f);
            foreach (var bz in boxes) bz.Tick(0.02f);
            if (rm.State != RaceState.Racing) continue;
            foreach (var r in racers)
            {
                bool resp = r.Kart.Respawn.Respawning;
                if (resp && !wasResp[r])
                {
                    respCount[r]++;
                    var d = r.GetComponent<PathDriverInput>();
                    events.Add($"t={rm.RaceTime:F0}s {r.racerName} RESPAWN at ({r.transform.position.x:F0},{r.transform.position.y:F0},{r.transform.position.z:F0}) cp={r.CheckpointsPassed} node={(d ? d.nodeIndex : -1)} speed={r.Kart.ForwardSpeed:F0}");
                }
                wasResp[r] = resp;
                if (!resp && r.Kart.ForwardSpeed < 3f && !r.Finished) { stuckT[r] += 0.02f; if (stuckT[r] > 2.5f && stuckT[r] < 2.52f) { var d = r.GetComponent<PathDriverInput>(); events.Add($"t={rm.RaceTime:F0}s {r.racerName} STUCK at ({r.transform.position.x:F0},{r.transform.position.y:F0},{r.transform.position.z:F0}) cp={r.CheckpointsPassed} node={(d ? d.nodeIndex : -1)}"); } }
                else stuckT[r] = 0f;
            }
        }
        sb.AppendLine($"after {rm.RaceTime:F0}s state={rm.State}: " + string.Join(" ", rm.Sorted.Select(r => $"{r.racerName}[{(r.Finished ? "FIN " + r.FinishTime.ToString("F0") + "s #" + r.FinishPlace : "cp" + r.CheckpointsPassed + ",L" + r.Lap)},resp{respCount[r]}]")));
        sb.AppendLine("drifts (cancel,tier1,tier2,tier3) per AI: " + string.Join(" ", racers.Where(r => !r.isPlayer).Select(r => r.racerName + "=" + string.Join("/", drifts[r]))));
        sb.AppendLine("items used by AI: " + string.Join(", ", used.OrderByDescending(k => k.Value).Select(k => k.Key + "x" + k.Value)));
        sb.AppendLine("non-bump hits: " + string.Join(", ", hitTypes.Select(k => k.Key + "x" + k.Value)) + " | per racer " + string.Join(" ", racers.Select(r => r.racerName + "=" + hitsOn[r])));
        foreach (var e in events.Take(40)) sb.AppendLine("  " + e);
        Physics.simulationMode = SimulationMode.FixedUpdate;
        return sb.ToString();
    }
}
