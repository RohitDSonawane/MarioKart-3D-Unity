using System.Linq;
using System.Reflection;
using System.Text;
using HarborRacer.Inputs;
using HarborRacer.Kart;
using HarborRacer.Race;
using HarborRacer.Track;
using UnityEngine;

public static class RespawnTest
{
    public static string Run()
    {
        var sb = new StringBuilder();
        Physics.SyncTransforms();
        var skip = new System.Type[] { typeof(PlayerKartInput), typeof(KartEffects), typeof(KartDebugHUD), typeof(RaceDebugHUD), typeof(HarborRacer.CameraRig.KartCameraRig), typeof(HarborRacer.UI.RaceHud) };
        foreach (var m in Object.FindObjectsByType<MonoBehaviour>().OrderBy(m => m is CheckpointTrack ? 0 : m is KartController ? 2 : 3))
        {
            if (m == null || skip.Contains(m.GetType())) continue;
            if (m.GetType().Namespace == null || !m.GetType().Namespace.StartsWith("HarborRacer")) continue;
            var a = m.GetType().GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
            if (a != null) a.Invoke(m, null);
        }
        foreach (var m in Object.FindObjectsByType<PathDriverInput>()) typeof(PathDriverInput).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(m, null);
        foreach (var r in Object.FindObjectsByType<RacerProgress>()) { var pk = r.GetComponent<PlayerKartInput>(); if (pk) pk.enabled = false; }
        var fu = typeof(KartController).GetMethod("FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        Physics.simulationMode = SimulationMode.Script;
        var ai = Object.FindObjectsByType<RacerProgress>().First(r => !r.isPlayer);
        var kart = ai.Kart; var drv = ai.GetComponent<PathDriverInput>();
        foreach (var r in Object.FindObjectsByType<RacerProgress>()) if (r != ai) { r.Kart.Body.isKinematic = true; r.transform.position = new Vector3(0, 900, 0); }
        kart.controlsLocked = false;
        int nodeBefore = drv.nodeIndex;
        // drop the AI kart into the sea far from where its target node is
        var rb = kart.Body;
        rb.isKinematic = true; kart.transform.position = new Vector3(300f, -150f, 300f); rb.position = kart.transform.position; rb.isKinematic = false;
        Physics.SyncTransforms();
        bool began = false; bool teleported = false; int steps = 0;
        kart.Respawn.Teleported += () => teleported = true;
        for (int i = 0; i < 400; i++)
        {
            rb.linearVelocity = new Vector3(0, i < 20 ? -30f : rb.linearVelocity.y, 0);
            fu.Invoke(kart, null); Physics.Simulate(0.02f); steps++;
            if (kart.Respawn.Respawning) began = true;
            if (began && !kart.Respawn.Respawning) break;
        }
        Vector3 fwd = kart.transform.forward;
        var n = drv.GetType().GetField("path", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
        var path = (TrackPath)n.GetValue(drv);
        Vector3 toNode = Vector3.ProjectOnPlane(path.GetNode(drv.nodeIndex).position - kart.transform.position, Vector3.up).normalized;
        sb.AppendLine($"respawn began={began} teleported={teleported} steps={steps} pos={kart.transform.position.ToString("F0")} nodeBefore={nodeBefore} nodeAfter={drv.nodeIndex}");
        sb.AppendLine($"facing vs direction to target node: {Vector3.Angle(Vector3.ProjectOnPlane(fwd, Vector3.up), toNode):F0} deg; dist to target node {Vector3.Distance(kart.transform.position, path.GetNode(drv.nodeIndex).position):F0}");
        Physics.simulationMode = SimulationMode.FixedUpdate;
        return sb.ToString();
    }
}
