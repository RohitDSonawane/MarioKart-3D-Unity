using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarborRacer.Inputs;
using HarborRacer.Kart;
using HarborRacer.Race;
using HarborRacer.Items;
using UnityEngine;

/// <summary>Logs what an AI kart is touching when it stalls (speed under 3 for 1 s) or falls below the track.</summary>
public static class AiWedge
{
    // which lateral offsets (-9..+9 u) still have road under them: X = road, . = void
    static string Edge(Transform tr)
    {
        var s = new StringBuilder();
        foreach (var o in new[] { -9f, -6f, -3f, 0f, 3f, 6f, 9f })
        {
            var from = tr.position + tr.right * o + Vector3.up * 3f;
            s.Append(Physics.Raycast(from, Vector3.down, 8f, HarborRacer.Layers.GroundMask, QueryTriggerInteraction.Ignore) ? 'X' : '.');
        }
        return s.ToString();
    }

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
        pd.path = GameObject.Find("Markers/Paths/MarioRaceEndPath").GetComponent<HarborRacer.Track.TrackPath>(); pd.SnapToPath(); player.Kart.Input = pd;
        var hist = racers.ToDictionary(r => r, r => new Queue<string>());
        var avF = typeof(PathDriverInput).GetField("avoidOffset", BindingFlags.NonPublic | BindingFlags.Instance);
        var slow = racers.ToDictionary(r => r, r => 0f);
        var reported = racers.ToDictionary(r => r, r => false);
        var fellReported = racers.ToDictionary(r => r, r => false);
        int steps = seconds * 50, reports = 0;
        for (int i = 1; i <= steps; i++)
        {
            foreach (var r in racers) r.GetComponent<RocketStart>()?.Tick(0.02f);
            rm.Tick(0.02f);
            foreach (var r in racers) fu.Invoke(r.Kart, null);
            Physics.Simulate(0.02f);
            foreach (var ip in ItemProjectile.All.ToArray()) if (ip != null) ip.Tick(0.02f);
            if (rm.State != RaceState.Racing) continue;
            foreach (var r in racers)
            {
                var k = r.Kart; var tr = r.transform; var d = r.GetComponent<PathDriverInput>();
                if (k.Respawn.Respawning) { slow[r] = 0f; reported[r] = false; continue; }
                if (i % 5 == 0)
                {
                    var q = hist[r]; q.Enqueue($"t={rm.RaceTime:F1} ({tr.position.x:F0},{tr.position.y:F0},{tr.position.z:F0}) edge[{Edge(tr)}] yaw={tr.eulerAngles.y:F0} v={k.ForwardSpeed:F0} cmd={k.CurrentSpeed:F0} steer={k.Input.Steer:F2} node={d.nodeIndex} avoid={(float)avF.GetValue(d):F1} drift={(k.Drift != null && k.Drift.IsDrifting)} boost={(k.Boost != null ? k.Boost.Remaining : 0f):F1}");
                    if (q.Count > 20) q.Dequeue();
                }
                slow[r] = k.ForwardSpeed < 3f ? slow[r] + 0.02f : 0f;
                if (!fellReported[r] && d.path.Count > 0 && tr.position.y < -150f && reports < 14)
                {
                    fellReported[r] = true; reports++;
                    foreach (var hline in hist[r]) sb.AppendLine("    " + hline);
                    sb.AppendLine($"FALL t={rm.RaceTime:F1}s {r.racerName} at ({tr.position.x:F0},{tr.position.y:F0},{tr.position.z:F0}) node={d.nodeIndex} nodeY={d.path.GetNode(d.nodeIndex).position.y:F0} v={k.ForwardSpeed:F0}");
                }
                if (fellReported[r] && tr.position.y > -140f) fellReported[r] = false;
                if (slow[r] > 1.2f && !reported[r] && reports < 12)
                {
                    reported[r] = true; reports++;
                    var o = tr.position + Vector3.up * 0.5f;
                    var hits = Physics.OverlapSphere(tr.position + tr.up * 0.4f, 2.3f, ~0, QueryTriggerInteraction.Ignore).Where(c => c.attachedRigidbody == null || c.attachedRigidbody != k.Body).Select(c => c.name + "(L" + c.gameObject.layer + ")").Distinct().Take(8);
                    string fwdHit = Physics.Raycast(o, tr.forward, out var h, 12f, ~0, QueryTriggerInteraction.Ignore) ? h.collider.name + " L" + h.collider.gameObject.layer + " @" + h.distance.ToString("F1") + " n.y=" + h.normal.y.ToString("F2") : "none";
                    Vector3 nodePos = d.path.GetNode(d.nodeIndex).position;
                    foreach (var hline in hist[r]) sb.AppendLine("    " + hline);
                    sb.AppendLine($"WEDGE t={rm.RaceTime:F1}s {r.racerName} at ({tr.position.x:F0},{tr.position.y:F0},{tr.position.z:F0}) yaw={tr.eulerAngles.y:F0} node={d.nodeIndex} nodeDist={(nodePos - tr.position).magnitude:F0} nodeAng={Vector3.SignedAngle(tr.forward, Vector3.ProjectOnPlane(nodePos - tr.position, Vector3.up), Vector3.up):F0} steer={k.Input.Steer:F2} cmd={k.CurrentSpeed:F1} touching=[{string.Join(", ", hits)}] fwdRay={fwdHit}");
                }
            }
        }
        Physics.simulationMode = SimulationMode.FixedUpdate;
        return sb.ToString() + "done, race time " + rm.RaceTime.ToString("F0");
    }
}
