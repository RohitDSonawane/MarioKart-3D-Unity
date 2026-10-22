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

/// <summary>
/// Audits the racing lines. Static part: for every node of every AI path, is there road under it, is it floating, is the segment to the
/// next node blocked by a wall or steep face, does the ground jump, how wide is the road. Dynamic part (Drive): one kart alone follows
/// a path for a lap and reports stalls, falls and teleports.
/// </summary>
public static class PathAudit
{
    static readonly int Solid = (1 << HarborRacer.Layers.Wall) | (1 << HarborRacer.Layers.Drivable);

    static bool Ground(Vector3 p, out RaycastHit h) =>
        Physics.Raycast(p + Vector3.up * 8f, Vector3.down, out h, 40f, HarborRacer.Layers.GroundMask, QueryTriggerInteraction.Ignore);

    /// <summary>mode 0: static audit of all paths; mode 1: one kart drives path pathIndex (0-6) for up to maxSeconds.</summary>
    public static string Run(int mode, int pathIndex, int maxSeconds) => mode == 0 ? Static() : Drive(pathIndex, maxSeconds);

    static string Static()
    {
        Physics.SyncTransforms();
        var sb = new StringBuilder();
        var root = GameObject.Find("Markers/Paths/AI");
        int issues = 0;
        foreach (var tp in root.GetComponentsInChildren<TrackPath>(true))
        {
            int n = tp.Count; float len = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = tp.GetNode(i).position, b = tp.GetNode((i + 1) % n).position;
                len += (b - a).magnitude;
                var flags = new List<string>();
                if (!Ground(a, out var ha)) flags.Add("NO GROUND UNDER NODE");
                else
                {
                    float above = a.y - ha.point.y;
                    if (above > 4f) flags.Add($"node floats {above:F1} u above the ground");
                    if (above < -0.5f) flags.Add($"node {-above:F1} u below the ground");
                    if (ha.collider.gameObject.layer != HarborRacer.Layers.Drivable && ha.collider.gameObject.layer != HarborRacer.Layers.OffRoad) flags.Add("ground is layer " + ha.collider.gameObject.layer + " (" + ha.collider.name + ")");
                    if (ha.collider.gameObject.layer == HarborRacer.Layers.OffRoad) flags.Add("node is on OFF-ROAD ground (" + ha.collider.name + ")");
                }
                // the segment: walls / vertical faces across it at kart height, and a ground gap along it
                Vector3 d = b - a; float dl = d.magnitude;
                if (dl > 0.1f)
                {
                    Vector3 dir = d / dl;
                    if (Physics.SphereCast(a + Vector3.up * 1.6f, 0.8f, dir, out var wh, dl, Solid, QueryTriggerInteraction.Ignore) && wh.normal.y < 0.55f && wh.collider.GetComponentInParent<KartController>() == null)
                        flags.Add($"segment BLOCKED at {wh.distance:F0}/{dl:F0} u by {wh.collider.name} (layer {wh.collider.gameObject.layer}, n.y {wh.normal.y:F2})");
                    int gaps = 0; float firstGap = -1f; float prevY = float.NaN; float maxStep = 0f;
                    for (float s = 0f; s <= dl; s += 4f)
                    {
                        Vector3 q = a + dir * s;
                        if (!Ground(q, out var hq)) { gaps++; if (firstGap < 0f) firstGap = s; continue; }
                        if (!float.IsNaN(prevY)) maxStep = Mathf.Max(maxStep, Mathf.Abs(hq.point.y - prevY));
                        prevY = hq.point.y;
                    }
                    if (gaps > 0) flags.Add($"{gaps} GAP sample(s) along the segment (first at {firstGap:F0} u) - jump or hole");
                    if (maxStep > 5f) flags.Add($"ground steps {maxStep:F1} u within 4 u along the segment");
                }
                // road width across the line at the node
                var right = Vector3.Cross(Vector3.up, new Vector3(d.x, 0f, d.z).normalized);
                string width = ""; int onRoad = 0;
                foreach (float o in new float[] { -12f, -9f, -6f, -3f, 0f, 3f, 6f, 9f, 12f })
                {
                    bool g = Ground(a + right * o, out var hw) && Mathf.Abs(hw.point.y - a.y) < 6f;
                    width += g ? "X" : "."; if (g) onRoad++;
                }
                if (onRoad < 5 || width[4] == '.') flags.Add("narrow / edge: road across the line [" + width + "]");
                if (dl > 160f) flags.Add($"nodes {dl:F0} u apart");
                if (flags.Count > 0) { issues += flags.Count; sb.AppendLine($"{tp.name} node {i} ({a.x:F0},{a.y:F0},{a.z:F0}): " + string.Join(" | ", flags)); }
            }
            sb.AppendLine($"-- {tp.name}: {n} nodes, {len:F0} u long");
        }
        sb.AppendLine("static issues: " + issues);
        return sb.ToString();
    }

    /// <summary>One kart alone follows the given path for a lap. pathIndex selects among the AI paths (0-6).</summary>
    static string Drive(int pathIndex, int maxSeconds)
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
        var paths = GameObject.Find("Markers/Paths/AI").GetComponentsInChildren<TrackPath>(true).OrderBy(p => p.name).ToArray();
        var path = paths[pathIndex];
        var racer = rm.Racers.First(r => !r.isPlayer && r.racerName == "Toad");
        foreach (var r in rm.Racers) if (r != racer) r.gameObject.SetActive(false);        // the test kart drives alone
        var kart = racer.Kart; var drv = racer.GetComponent<PathDriverInput>();
        drv.path = path; drv.altPaths = null;
        Vector3 p0 = path.GetNode(0).position, p1 = path.GetNode(1).position;
        Vector3 fwd = Vector3.ProjectOnPlane(p1 - p0, Vector3.up).normalized;
        Physics.Raycast(p0 + Vector3.up * 6f, Vector3.down, out var gh, 30f, HarborRacer.Layers.GroundMask, QueryTriggerInteraction.Ignore);
        racer.transform.SetPositionAndRotation(gh.point + Vector3.up * 1.1f, Quaternion.LookRotation(fwd, Vector3.up));
        kart.Body.position = racer.transform.position; kart.Body.rotation = racer.transform.rotation; Physics.SyncTransforms();
        drv.SnapToPath();
        var fu = typeof(KartController).GetMethod("FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        var npF = typeof(PathDriverInput).GetField("noProgressTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        Vector3 last = racer.transform.position; bool fellOnce = false; float slow = 0f; bool reported = false; int hops = 0, falls = 0, resp = 0; bool wasResp = false;
        int startNode = drv.nodeIndex, maxNode = 0, wraps = 0, lastNode = drv.nodeIndex; float topSpeed = 0f; var stallNodes = new List<int>();
        float t = 0f; int steps = maxSeconds * 50;
        for (int i = 0; i < steps; i++)
        {
            rm.Tick(0.02f);
            racer.GetComponent<RocketStart>()?.Tick(0.02f);
            fu.Invoke(kart, null);
            Physics.Simulate(0.02f);
            foreach (var ip in ItemProjectile.All.ToArray()) if (ip != null) ip.Tick(0.02f);
            if (rm.State != RaceState.Racing) continue;
            t = rm.RaceTime;
            var pos = racer.transform.position;
            topSpeed = Mathf.Max(topSpeed, kart.ForwardSpeed);
            if (drv.nodeIndex < lastNode - 10) wraps++;
            lastNode = drv.nodeIndex;
            bool r2 = kart.Respawn.Respawning; if (r2) fellOnce = false;
            if (r2 && !wasResp) { resp++; sb.AppendLine($"  RESPAWN t={t:F1}s at ({pos.x:F0},{pos.y:F0},{pos.z:F0}) node {drv.nodeIndex}"); }
            wasResp = r2;
            float d = (pos - last).magnitude; last = pos;
            if (d > 20f && !r2) { hops++; sb.AppendLine($"  TELEPORT/HOP t={t:F1}s {d:F0} u to ({pos.x:F0},{pos.y:F0},{pos.z:F0}) node {drv.nodeIndex}"); }
            if (pos.y < path.GetNode(drv.nodeIndex).position.y - 60f && !r2 && !fellOnce) { fellOnce = true; falls++; sb.AppendLine($"  FELL t={t:F1}s at ({pos.x:F0},{pos.y:F0},{pos.z:F0}) node {drv.nodeIndex}"); }
            slow = kart.ForwardSpeed < 3f && !r2 ? slow + 0.02f : 0f;
            if (slow > 1.5f && !reported)
            {
                reported = true; stallNodes.Add(drv.nodeIndex);
                var hits = Physics.OverlapSphere(pos + racer.transform.up * 0.4f, 2.3f, ~0, QueryTriggerInteraction.Ignore).Where(c => c.attachedRigidbody != kart.Body).Select(c => c.name + "(L" + c.gameObject.layer + ")").Distinct().Take(5);
                sb.AppendLine($"  STALL t={t:F1}s at ({pos.x:F0},{pos.y:F0},{pos.z:F0}) node {drv.nodeIndex} speed cmd {kart.CurrentSpeed:F0} touching [{string.Join(", ", hits)}]");
            }
            if (slow < 0.02f) reported = false;
            if (wraps >= 1) break;
        }
        sb.Insert(0, $"{path.name}: lap {(wraps >= 1 ? "completed" : "NOT completed")} in {t:F1}s, top speed {topSpeed:F0}, stalls {stallNodes.Count}, hops {hops}, falls {falls}, respawns {resp}\n");
        Physics.simulationMode = SimulationMode.FixedUpdate;
        return sb.ToString();
    }
}
