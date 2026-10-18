using System.Linq;
using System.Reflection;
using System.Text;
using HarborRacer.Inputs;
using HarborRacer.Kart;
using HarborRacer.Race;
using HarborRacer.Track;
using UnityEngine;

public static class RampTrace
{
    class In : IKartInput
    {
        public float throttle = 1f, brake = 0f, steer = 0f;
        public float Throttle => throttle; public float Brake => brake; public float Steer => steer; public float Pitch => 0f;
        public bool DriftHeld => false; public bool DriftPressed => false; public bool TrickPressed => false; public bool ItemPressed => false; public bool ItemBackPressed => false;
        public void EndFixedStep() { }
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
        Vector3 dir = Vector3.ProjectOnPlane(path.GetNode(1).position - path.GetNode(0).position, Vector3.up).normalized;

        Vector3 from = new Vector3(212f, -40f, -115f), to = new Vector3(150f, -11f, -76f), to2 = new Vector3(99f, 5f, -85f);
        foreach (float sp in new[] { 40f, 60f, 85f })
        {
            Vector3 d0 = Vector3.ProjectOnPlane(to - from, Vector3.up).normalized;
            Physics.Raycast(from + Vector3.up * 30f, Vector3.down, out var g0, 80f, HarborRacer.Layers.GroundMask);
            Place(g0.point, d0, sp); input.throttle = 1f;
            string log = "start speed " + sp + ":";
            bool fell = false;
            for (int i = 0; i < 400; i++)
            {
                Vector3 tgt = Vector3.Distance(kart.transform.position, to) > 12f && kart.transform.position.x > 120f ? to : to2;
                float ang = Vector3.SignedAngle(kart.transform.forward, Vector3.ProjectOnPlane(tgt - kart.transform.position, Vector3.up), Vector3.up);
                input.steer = Mathf.Clamp(ang / 25f, -1f, 1f);
                Step(1);
                if (i % 12 == 0)
                {
                    bool gr = Physics.Raycast(kart.transform.position + Vector3.up, Vector3.down, out var gh, 6f, HarborRacer.Layers.GroundMask);
                    log += " [" + (i * 0.02f).ToString("F1") + "s (" + kart.transform.position.x.ToString("F0") + "," + kart.transform.position.y.ToString("F0") + "," + kart.transform.position.z.ToString("F0") + ") v" + kart.ForwardSpeed.ToString("F0") + " " + (gr ? gh.collider.name.Substring(0, System.Math.Min(10, gh.collider.name.Length)) : "AIR") + (kart.State.OnDirt ? " dirt" : "") + "]";
                }
                if (kart.transform.position.y < -60f && kart.transform.position.x < 160f) { fell = true; log += " FELL at (" + kart.transform.position.x.ToString("F0") + "," + kart.transform.position.y.ToString("F0") + "," + kart.transform.position.z.ToString("F0") + ")"; break; }
                if (kart.transform.position.x < 80f) break;
            }
            sb.AppendLine(log + (fell ? "" : " reached deck end"));
        }
        Physics.simulationMode = SimulationMode.FixedUpdate;
        return sb.ToString();
    }
}
