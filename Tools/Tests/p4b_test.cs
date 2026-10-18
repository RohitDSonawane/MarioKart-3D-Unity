using System.Linq;
using System.Reflection;
using System.Text;
using HarborRacer.Inputs;
using HarborRacer.Kart;
using HarborRacer.Race;
using HarborRacer.Track;
using UnityEngine;

public static class P4bTest
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



        Vector3 sea = default; bool foundSea = false;
        for (float sx = 150; sx < 900 && !foundSea; sx += 25) for (float sz = 380; sz < 700 && !foundSea; sz += 25)
            if (!Physics.Raycast(new Vector3(sx, 600, sz), Vector3.down, 1200f, ~0, QueryTriggerInteraction.Ignore)) { sea = new Vector3(sx, 0, sz); foundSea = true; }
        sb.AppendLine("open sea point: " + (foundSea ? sea.ToString() : "none"));
        foreach (float h in new[] { -150f, -50f, 50f, 150f, 300f, 500f })
        {
            for (int tries = 0; tries < 1; tries++)
            {
                Place(p0, dir, 0f);
                rb.isKinematic = true; kart.transform.position = new Vector3(sea.x, h, sea.z); rb.isKinematic = false; rb.linearVelocity = Vector3.zero; Physics.SyncTransforms();
                float tStart = -1, tFree = -1, minY = 1e9f, maxV = 0;
                for (int i = 0; i < 400; i++) { Step(1); minY = Mathf.Min(minY, kart.transform.position.y); maxV = Mathf.Max(maxV, -rb.linearVelocity.y); if (kart.Respawn.Respawning && tStart < 0) tStart = i * 0.02f; if (tStart >= 0 && !kart.Respawn.Respawning && tFree < 0) { tFree = i * 0.02f; break; } }
                var pos = kart.transform.position; bool onRoad = Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out var rh, 8f, HarborRacer.Layers.GroundMask, QueryTriggerInteraction.Ignore);
                sb.AppendLine($"drop from y={h}: peak fall speed {maxV:F0} u/s ({maxV * 0.02f:F1} u/step); respawn began {(tStart < 0 ? "NEVER" : tStart.ToString("F2") + "s")}, control back {(tFree < 0 ? "-" : tFree.ToString("F2") + "s")}; lowest y {minY:F0}; ended ({pos.x:F0},{pos.y:F0},{pos.z:F0}) onGround={onRoad}");
            }
        }
        Physics.simulationMode = SimulationMode.FixedUpdate;
        return sb.ToString();
    }
}
