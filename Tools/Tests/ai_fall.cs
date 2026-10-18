using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarborRacer.Inputs;
using HarborRacer.Kart;
using HarborRacer.Race;
using HarborRacer.Hazards;
using UnityEngine;

public static class AiFall
{
    public static string Run(int seconds)
    {
        var sb = new StringBuilder();
        var rm = Object.FindFirstObjectByType<RaceManager>();
        var skip = new System.Type[] { typeof(PlayerKartInput), typeof(KartEffects), typeof(KartDebugHUD), typeof(RaceDebugHUD), typeof(HarborRacer.CameraRig.KartCameraRig), typeof(HarborRacer.UI.RaceHud) };
        foreach (var m in Object.FindObjectsByType<MonoBehaviour>().OrderBy(m => m is CheckpointTrack ? 0 : m is KartController ? 2 : m is RaceManager ? 5 : 3))
        {
            if (m == null || skip.Contains(m.GetType())) continue;
            if (m.GetType().Namespace == null || !m.GetType().Namespace.StartsWith("HarborRacer")) continue;
            var a = m.GetType().GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
            if (a != null) a.Invoke(m, null);
        }
        foreach (var m in Object.FindObjectsByType<PathDriverInput>()) typeof(PathDriverInput).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(m, null);
        foreach (var k in Object.FindObjectsByType<RacerProgress>()) { var pk = k.GetComponent<PlayerKartInput>(); if (pk) pk.enabled = false; }
        Physics.simulationMode = SimulationMode.Script; Physics.autoSyncTransforms = true;
        rm.BeginRace();
        var fu = typeof(KartController).GetMethod("FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        var racers = rm.Racers;
        var player = rm.Player;
        var pd = player.gameObject.AddComponent<PathDriverInput>(); pd.kart = player.Kart;
        pd.path = GameObject.Find("Markers/Paths/MarioRaceEndPath").GetComponent<HarborRacer.Track.TrackPath>(); pd.SnapToPath(); player.Kart.Input = pd;

        var slow = new Dictionary<RacerProgress, float>();
        var reported = new Dictionary<RacerProgress, float>();
        var resp = new Dictionary<RacerProgress, bool>();
        var ring = new Dictionary<RacerProgress, List<string>>();
        int steps = seconds * 50;
        for (int i = 1; i <= steps; i++)
        {
            foreach (var r in racers) r.GetComponent<RocketStart>()?.Tick(0.02f);
            rm.Tick(0.02f);
            foreach (var r in racers) fu.Invoke(r.Kart, null);
            Physics.Simulate(0.02f);
            if (rm.State != RaceState.Racing) continue;
            float t = rm.RaceTime;
            foreach (var r in racers)
            {
                var k = r.Kart;
                bool rs = k.Respawn.Respawning;
                if (i % 12 == 0 && !rs)
                {
                    if (!ring.TryGetValue(r, out var rl)) ring[r] = rl = new List<string>();
                    var dd = r.GetComponent<PathDriverInput>();
                    rl.Add("t=" + t.ToString("F1") + " (" + k.transform.position.x.ToString("F0") + "," + k.transform.position.y.ToString("F0") + "," + k.transform.position.z.ToString("F0") + ") v=" + k.ForwardSpeed.ToString("F0") + " node=" + dd.nodeIndex + " gnd=" + k.State.Grounded + " st=" + dd.Steer.ToString("F2") + " boost=" + k.Boost.Remaining.ToString("F1"));
                    if (rl.Count > 14) rl.RemoveAt(0);
                }
                if (resp.TryGetValue(r, out var was) && !was && rs && ring.ContainsKey(r))
                {
                    foreach (var line in ring[r]) sb.AppendLine("      " + r.racerName + " " + line);
                    sb.AppendLine($"t={t:F0} {r.racerName} RESPAWN begins at ({k.transform.position.x:F0},{k.transform.position.y:F0},{k.transform.position.z:F0}) cp={r.CheckpointsPassed}");
                }
                resp[r] = rs;
                if (rs || k.controlsLocked) { slow[r] = 0f; continue; }
                float v = Mathf.Abs(k.ForwardSpeed);
                slow[r] = v < 6f ? (slow.TryGetValue(r, out var s0) ? s0 : 0f) + 0.02f : 0f;
                if (slow[r] > 1.5f && (!reported.TryGetValue(r, out var last) || t - last > 20f))
                {
                    reported[r] = t;
                    var pos = k.transform.position;
                    var cols = Physics.OverlapSphere(pos + Vector3.up * 0.5f, 4f, ~0, QueryTriggerInteraction.Ignore).Where(c => c.transform.root != k.transform.root).Select(c => c.gameObject.name + "[L" + c.gameObject.layer + "]").Distinct().Take(6);
                    var others = racers.Where(o => o != r && Vector3.Distance(o.transform.position, pos) < 8f).Select(o => o.racerName + " " + Vector3.Distance(o.transform.position, pos).ToString("F1") + "u spd " + o.Kart.ForwardSpeed.ToString("F0"));
                    var tr = Object.FindObjectsByType<Trolley>().Where(x => Vector3.Distance(x.transform.position, pos) < 45f).Select(x => "trolley " + Vector3.Distance(x.transform.position, pos).ToString("F0") + "u");
                    var drv = r.GetComponent<PathDriverInput>();
                    sb.AppendLine($"t={t:F0} {r.racerName} STUCK ({pos.x:F0},{pos.y:F0},{pos.z:F0}) cp={r.CheckpointsPassed} node={drv.nodeIndex} cmd={k.CurrentSpeed:F0} stun={k.State.Stunned} star={k.State.Star} touching: {string.Join(",", cols)} | karts: {string.Join(",", others)} | {string.Join(",", tr)}");
                }
            }
        }
        Physics.simulationMode = SimulationMode.FixedUpdate;
        sb.AppendLine("end: " + string.Join(" ", racers.Select(r => r.racerName + "[cp" + r.CheckpointsPassed + ",L" + r.Lap + "]")));
        return sb.ToString();
    }
}
