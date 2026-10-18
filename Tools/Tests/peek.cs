using System.Linq;
using System.Text;
using UnityEngine;
using HarborRacer.Race;

public static class Peek
{
    public static string Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine("isPlaying=" + Application.isPlaying + " timeScale=" + Time.timeScale + " time=" + Time.time.ToString("F1") + " unscaled=" + Time.unscaledTime.ToString("F1") + " fixedDelta=" + Time.fixedDeltaTime + " frame=" + Time.frameCount);
        var rm = Object.FindFirstObjectByType<RaceManager>();
        if (rm == null) { sb.AppendLine("no RaceManager"); return sb.ToString(); }
        sb.AppendLine("state=" + rm.State + " introSeconds=" + rm.introSeconds + " raceTime=" + rm.RaceTime.ToString("F1") + " racers=" + rm.Racers.Count + " player=" + (rm.Player != null ? rm.Player.racerName : "null"));
        foreach (var r in rm.Racers.Take(3)) sb.AppendLine("  " + r.racerName + " pos=" + r.transform.position.ToString("F0") + " locked=" + r.Kart.controlsLocked + " kin=" + r.Kart.Body.isKinematic);
        var cam = Object.FindFirstObjectByType<HarborRacer.CameraRig.KartCameraRig>();
        sb.AppendLine("cameraRig=" + (cam != null) + (cam != null ? " camPos=" + cam.Cam.transform.position.ToString("F0") : ""));
        var timelines = Object.FindObjectsByType<UnityEngine.Playables.PlayableDirector>(FindObjectsInactive.Include);
        sb.AppendLine("playableDirectors=" + timelines.Length);
        return sb.ToString();
    }
}
