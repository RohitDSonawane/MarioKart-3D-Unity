using System.Linq;
using System.Reflection;
using System.Text;
using HarborRacer.Inputs;
using HarborRacer.Kart;
using HarborRacer.Race;
using HarborRacer.Track;
using UnityEngine;

/// <summary>Drives the player-side kart straight through a jump panel from a start distance at a given speed and logs the run.</summary>
public static class PanelRun
{
    public static string Run(string panelName, float runUp, float speed, float lateral, int seconds)
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
        foreach (var r in rm.Racers) if (!r.isPlayer) r.gameObject.SetActive(false);
        var player = rm.Player; var kart = player.Kart;
        var pk = player.GetComponent<PlayerKartInput>(); if (pk) pk.enabled = false;
        var panel = Object.FindObjectsByType<JumpPanelZone>().First(p => p.name == panelName);
        Vector3 f = panel.transform.forward; f.y = 0; f.Normalize(); Vector3 right = Vector3.Cross(Vector3.up, f);
        Vector3 start = panel.transform.position - f * runUp + right * lateral;
        Physics.Raycast(start + Vector3.up * 6f, Vector3.down, out var gh, 30f, HarborRacer.Layers.GroundMask, QueryTriggerInteraction.Ignore);
        player.transform.SetPositionAndRotation(gh.point + Vector3.up * 1.1f, Quaternion.LookRotation(f, Vector3.up));
        kart.Body.position = player.transform.position; kart.Body.rotation = player.transform.rotation; Physics.SyncTransforms();
        kart.CurrentSpeed = speed; kart.Body.linearVelocity = f * speed;
        var input = new ScriptedInput { throttle = 1f }; kart.Input = input;
        var fu = typeof(KartController).GetMethod("FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        for (int i = 0; i < 3 * 50 && rm.State != RaceState.Racing; i++) rm.Tick(0.02f);     // through the countdown
        foreach (var r in rm.Racers) r.Kart.controlsLocked = false;
        for (int i = 0; i <= seconds * 50; i++)
        {
            fu.Invoke(kart, null); Physics.Simulate(0.02f);
            if (i % 10 == 0)
            {
                var st = kart.State;
                sb.AppendLine($"t={i * 0.02f:F1} pos=({kart.transform.position.x:F0},{kart.transform.position.y:F1},{kart.transform.position.z:F0}) real={kart.ForwardSpeed:F0} cmd={kart.CurrentSpeed:F0} gnd={st.Grounded} jumpPanel={st.OnJumpPanel} glide={st.Gliding} stun={st.Stunned} locked={kart.controlsLocked}");
            }
        }
        Physics.simulationMode = SimulationMode.FixedUpdate;
        return sb.ToString();
    }

    class ScriptedInput : IKartInput
    {
        public float throttle;
        public float Throttle => throttle; public float Brake => 0f; public float Steer => 0f;
        public bool DriftHeld => false; public bool DriftPressed => false; public bool TrickPressed => false;
        public bool ItemPressed => false; public bool ItemBackPressed => false; public float Pitch => 0f;
        public void EndFixedStep() { }
    }
}
