using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarborRacer.Inputs;
using HarborRacer.Kart;
using HarborRacer.Race;
using HarborRacer.Track;
using UnityEngine;

/// <summary>
/// Drives one kart into the walls beside the racing line: at every node of the used path, on both sides, aimed at the nearest wall at several
/// angles and speeds, with no steering correction. Reports runs where the kart climbs the wall, falls off the track, respawns, or ends far from the line.
/// </summary>
public static class WallSweep
{
    class Input : IKartInput
    {
        public float Throttle => 1f; public float Brake => 0f; public float Steer => 0f; public float Pitch => 0f;
        public bool DriftHeld => false; public bool DriftPressed => false; public bool TrickPressed => false; public bool ItemPressed => false; public bool ItemBackPressed => false;
        public void EndFixedStep() { }
    }

    static readonly int Solid = (1 << HarborRacer.Layers.Wall) | (1 << HarborRacer.Layers.Drivable);

    public static string Run(int pathIndex, float speed, int firstNode, int lastNode, int stepNode)
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
        Physics.simulationMode = SimulationMode.Script; Physics.autoSyncTransforms = true;
        rm.BeginRace();
        var testRacer = rm.Racers.First(r => !r.isPlayer && r.racerName == "Toad");
        foreach (var r in rm.Racers) if (r != testRacer) r.gameObject.SetActive(false);
        var player = testRacer; var kart = player.Kart;
        var path = GameObject.Find("Markers/Paths/AI").GetComponentsInChildren<TrackPath>(true).OrderBy(p => p.name).ToArray()[pathIndex];
        var drv = testRacer.GetComponent<PathDriverInput>(); drv.path = path; drv.altPaths = null;
        var fu = typeof(KartController).GetMethod("FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        for (int i = 0; i < 400 && rm.State != RaceState.Racing; i++) rm.Tick(0.02f);
        foreach (var r in rm.Racers) r.Kart.controlsLocked = false;
        int runs = 0, bad = 0, n = path.Count;
        var kinds = new Dictionary<string, int>();
        for (int node = firstNode; node <= lastNode && node < n; node += stepNode)
        {
            Vector3 a = path.GetNode(node).position, b = path.GetNode((node + 1) % n).position;
            Vector3 tang = Vector3.ProjectOnPlane(b - a, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, tang);
            if (!Physics.Raycast(a + Vector3.up * 6f, Vector3.down, out var gh, 40f, HarborRacer.Layers.GroundMask, QueryTriggerInteraction.Ignore)) continue;
            foreach (int side in new[] { -1, 1 })
            {
                // the nearest vertical face on this side, at kart height
                Vector3 dirSide = right * side;
                if (!Physics.Raycast(gh.point + Vector3.up * 1.4f, dirSide, out var wh, 30f, Solid, QueryTriggerInteraction.Ignore) || wh.normal.y > 0.7f) continue;
                float gap = wh.distance;
                foreach (float ang in new[] { 12f, 30f, 55f })
                {
                    // start beside the wall, a few units short of it, heading along the line turned toward the wall
                    Vector3 start = gh.point + dirSide * Mathf.Max(0f, gap - 4f) - tang * 6f;
                    if (!Physics.Raycast(start + Vector3.up * 6f, Vector3.down, out var sg, 20f, HarborRacer.Layers.GroundMask, QueryTriggerInteraction.Ignore)) continue;
                    Vector3 heading = (Quaternion.AngleAxis(ang * side, Vector3.up) * tang).normalized;
                    // wait out any respawn from the previous run
                    for (int w = 0; w < 200 && kart.Respawn.Respawning; w++) { fu.Invoke(kart, null); Physics.Simulate(0.02f); }
                    var rot = Quaternion.LookRotation(heading, Vector3.up);
                    player.transform.SetPositionAndRotation(sg.point + Vector3.up * 1.1f, rot);
                    kart.Body.position = player.transform.position; kart.Body.rotation = rot;
                    kart.Body.linearVelocity = heading * speed; kart.Body.angularVelocity = Vector3.zero;
                    kart.CurrentSpeed = speed; kart.ForwardSpeed = speed;
                    kart.State.OnJumpPanel = false; kart.State.Stunned = false; kart.State.Gliding = false; kart.State.AntiGravity = false;
                    kart.Zones?.Clear(); Physics.SyncTransforms(); drv.SnapToPath();
                    float startY = sg.point.y, minY = 1e9f, maxY = -1e9f, minUp = 1f, maxSide = -1e9f, maxAbove = 0f; bool resp = false; Vector3 lost = Vector3.zero; bool lostSet = false; bool groundAtResp = false; int tunnels = 0; string tunnelWith = ""; Vector3 prevPos = player.transform.position; float respT = 0f; Vector3 end = Vector3.zero; float avg = 0f;
                    int steps = 150;
                    for (int s = 0; s < steps; s++)
                    {
                        fu.Invoke(kart, null); Physics.Simulate(0.02f);
                        var p = player.transform.position; end = p;
                        if ((p - prevPos).sqrMagnitude > 0.0001f && Physics.Linecast(prevPos + Vector3.up * 0.9f, p + Vector3.up * 0.9f, out var th, Solid, QueryTriggerInteraction.Ignore) && th.normal.y < 0.5f && th.collider.GetComponentInParent<KartController>() == null) { tunnels++; tunnelWith = th.collider.name + " L" + th.collider.gameObject.layer + " at (" + th.point.x.ToString("F0") + "," + th.point.y.ToString("F0") + "," + th.point.z.ToString("F0") + ")"; }
                        prevPos = p;
                        maxSide = Mathf.Max(maxSide, Vector3.Dot(p - gh.point, dirSide));
                        if (Physics.Raycast(p + Vector3.up * 1f, Vector3.down, out var under, 60f, HarborRacer.Layers.GroundMask, QueryTriggerInteraction.Ignore)) maxAbove = Mathf.Max(maxAbove, p.y - under.point.y); else maxAbove = Mathf.Max(maxAbove, 60f);
                        if (!lostSet && maxAbove >= 6f) { lostSet = true; lost = p; }
                        minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); minUp = Mathf.Min(minUp, player.transform.up.y); avg += kart.ForwardSpeed;
                        if (kart.Respawn.Respawning && !resp) { resp = true; respT = s * 0.02f; groundAtResp = Physics.Raycast(p + Vector3.up * 1f, Vector3.down, 3f, HarborRacer.Layers.GroundMask, QueryTriggerInteraction.Ignore); break; }
                    }
                    avg /= steps; runs++;
                    string problem = null;
                    if (resp) problem = groundAtResp ? $"OOB-ZONE respawn after {respT:F1}s (on the road)" : $"FELL and respawned after {respT:F1}s";
                    else if (tunnels > 0) problem = $"TUNNELLED through {tunnelWith} ({tunnels} steps)";
                    else if (maxAbove > 4f) { var nearW = Physics.OverlapSphere(lost, 14f, 1 << HarborRacer.Layers.Wall, QueryTriggerInteraction.Ignore).Select(c => c.name).Distinct().Take(3); problem = $"LEFT THE ROAD at ({lost.x:F0},{lost.y:F0},{lost.z:F0}), {maxAbove:F0} u above ground; walls near: [{string.Join(", ", nearW)}]"; }
                    else if (minUp < 0.6f) problem = $"TILTED to up.y {minUp:F2}";
                    if (problem != null)
                    {
                        bad++; kinds[problem.Split(' ')[0]] = kinds.TryGetValue(problem.Split(' ')[0], out var c) ? c + 1 : 1;
                        sb.AppendLine($"node {node} side {(side < 0 ? "L" : "R")} angle {ang:F0} speed {speed:F0} wall {wh.collider.name} (L{wh.collider.gameObject.layer}, n.y {wh.normal.y:F2}, {gap:F0} u away) at ({sg.point.x:F0},{sg.point.y:F0},{sg.point.z:F0}): {problem}, avg speed {avg:F0}");
                    }
                }
            }
        }
        Physics.simulationMode = SimulationMode.FixedUpdate;
        sb.AppendLine($"runs {runs}, problems {bad}: " + string.Join(", ", kinds.Select(k => k.Key + " x" + k.Value)));
        return sb.ToString();
    }
}
