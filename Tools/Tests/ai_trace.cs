using System.Linq;
using System.Reflection;
using System.Text;
using HarborRacer.Inputs;
using HarborRacer.Kart;
using HarborRacer.Race;
using UnityEngine;

public static class AiTrace
{
    public static string Run(string who, float fromT, float toT)
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
        var target = racers.First(r => r.racerName == who);
        var drv = target.GetComponent<PathDriverInput>();

        int steps = (int)(toT * 50) + 400;
        for (int i = 1; i <= steps; i++)
        {
            foreach (var r in racers) r.GetComponent<RocketStart>()?.Tick(0.02f);
            rm.Tick(0.02f);
            foreach (var r in racers) fu.Invoke(r.Kart, null);
            Physics.Simulate(0.02f);
            float t = rm.RaceTime;
            if (rm.State == RaceState.Racing && t >= fromT && t <= toT && i % 8 == 0)
            {
                var k = target.Kart; var s = k.State;
                Vector3 node = drv.path.GetNode(drv.nodeIndex).position;
                float ang = Vector3.SignedAngle(k.transform.forward, Vector3.ProjectOnPlane(node - k.transform.position, k.transform.up), k.transform.up);
                sb.AppendLine($"t={t:F1} pos=({k.transform.position.x:F0},{k.transform.position.y:F0},{k.transform.position.z:F0}) real={k.ForwardSpeed:F1} cmd={k.CurrentSpeed:F1} thr={drv.Throttle:F0} brk={drv.Brake:F0} steer={drv.Steer:F2} node={drv.nodeIndex} dist={Vector3.Distance(node, k.transform.position):F0} ang={ang:F0} stun={s.Stunned} jump={s.OnJumpPanel} glide={s.Gliding} gnd={s.Grounded} boost={(k.Boost.Remaining):F1} yaw={k.transform.eulerAngles.y:F0} pitch={k.transform.eulerAngles.x:F0}");
            }
        }
        Physics.simulationMode = SimulationMode.FixedUpdate;
        return sb.ToString();
    }
}
