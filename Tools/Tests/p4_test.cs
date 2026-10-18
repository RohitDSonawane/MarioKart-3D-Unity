using System.Linq;
using System.Reflection;
using System.Text;
using HarborRacer.Inputs;
using HarborRacer.Kart;
using HarborRacer.Race;
using HarborRacer.Track;
using UnityEngine;

public static class P4Test
{
    class In : IKartInput
    {
        public float throttle = 1f, brake = 0f, steer = 0f; public bool held, pressed, trick;
        public float Throttle => throttle; public float Brake => brake; public float Steer => steer; public float Pitch => 0f;
        public bool DriftHeld => held; public bool DriftPressed => pressed; public bool TrickPressed => trick; public bool ItemPressed => false; public bool ItemBackPressed => false;
        public void EndFixedStep() { pressed = false; trick = false; }
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


        // 1. jump panel 02 (51,14,-194): run-up along its heading, record state, trick, landing boost
        var panel = Object.FindObjectsByType<JumpPanelZone>().First(z => z.name == "JumpPanel_02");
        Vector3 pdir = Vector3.ProjectOnPlane(panel.transform.forward, Vector3.up).normalized;
        Vector3 start = panel.transform.position - pdir * 40f; if (Physics.Raycast(start + Vector3.up * 8f, Vector3.down, out var gh, 30f, HarborRacer.Layers.GroundMask, QueryTriggerInteraction.Ignore)) start = gh.point;
        Place(start, pdir, 75f); for (int i = 0; i < 8; i++) Step(1);
        bool onPanel = false, trickSeen = false, tricked = false; float airSteps = 0, landBoost = 0; Vector3 takeoff = default, land = default; int respawns = 0; float peak = -1e9f, y0 = 0;
        for (int i = 0; i < 400; i++)
        {
            if (kart.State.TrickAvailable) { trickSeen = true; if (!tricked && kart.State.OnJumpPanel) { input.trick = true; tricked = true; } }
            Step(1);
            if (kart.State.OnJumpPanel) { if (!onPanel) { onPanel = true; takeoff = kart.transform.position; y0 = takeoff.y; } airSteps++; peak = Mathf.Max(peak, kart.transform.position.y - y0); }
            else if (onPanel && land == default) { land = kart.transform.position; landBoost = kart.Boost.Remaining; }
            if (kart.Respawn.Respawning) { respawns++; break; }
            if (land != default && i > 0 && airSteps > 0 && Vector3.Distance(kart.transform.position, land) > 5f) break;
        }
        sb.AppendLine($"1 jump panel 02: triggered={onPanel} trickZoneSeen={trickSeen} trickPressed={tricked} airtime={airSteps * 0.02f:F2}s flight={(land == default ? 0 : Vector3.Distance(takeoff, land)):F0}u peak+{peak:F1} boostAfterLanding={landBoost:F2}s respawn={respawns}");

        // 2. out of bounds / water: drop into the sea and time the respawn
        Place(new Vector3(206f, -105f, 324f), dir, 0f); var cp = kart.GetComponent<RacerProgress>();
        rb.isKinematic = true; kart.transform.position = new Vector3(260f, -300f, 330f); rb.isKinematic = false; Physics.SyncTransforms();
        float tStart = -1, tFree = -1; int steps = 0;
        for (int i = 0; i < 250; i++) { Step(1); steps++; if (kart.Respawn.Respawning && tStart < 0) tStart = i * 0.02f; if (tStart >= 0 && !kart.Respawn.Respawning && tFree < 0) { tFree = i * 0.02f; break; } }
        var pos = kart.transform.position; bool onRoad = Physics.Raycast(pos + Vector3.up, Vector3.down, out var rh, 4f, HarborRacer.Layers.GroundMask, QueryTriggerInteraction.Ignore) && rh.collider.gameObject.layer == HarborRacer.Layers.Drivable;
        sb.AppendLine($"2 sea drop: respawn began {tStart:F2}s, control back {tFree:F2}s (spec ~1.5 s total); landed at ({pos.x:F0},{pos.y:F0},{pos.z:F0}) onDrivableRoad={onRoad}");

        // 3. head-on wall at 70 u/s (temporary wall on the Wall layer)
        Place(p0, dir, 70f); for (int i = 0; i < 6; i++) Step(1);
        var wall = new GameObject("TmpWall"); wall.layer = HarborRacer.Layers.Wall; var bc = wall.AddComponent<BoxCollider>(); bc.size = new Vector3(20f, 8f, 1f);
        wall.transform.position = p0 + dir * 22f + Vector3.up * 3f; wall.transform.rotation = Quaternion.LookRotation(dir, Vector3.up); Physics.SyncTransforms();
        float minSpeed = 999, maxBack = 0, spBefore = kart.ForwardSpeed; for (int i = 0; i < 150; i++) { Step(1); minSpeed = Mathf.Min(minSpeed, kart.ForwardSpeed); }
        Object.DestroyImmediate(wall);
        sb.AppendLine($"3 head-on wall: speed before {spBefore:F0}, lowest after {minSpeed:F0}, final commanded {kart.CurrentSpeed:F0} (spec: lose 10 and bounce away, no run-away)");
        Physics.simulationMode = SimulationMode.FixedUpdate;
        return sb.ToString();
    }
}
