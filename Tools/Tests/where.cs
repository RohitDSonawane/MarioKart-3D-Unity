using System.Linq;
using System.Text;
using UnityEngine;
using HarborRacer.Race;
public static class Where
{
    public static string Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine("playing=" + Application.isPlaying + " scene dirty=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty);
        var rm = Object.FindFirstObjectByType<RaceManager>();
        if (rm != null) sb.AppendLine("state=" + rm.State + " raceTime=" + rm.RaceTime.ToString("F1"));
        foreach (var r in Object.FindObjectsByType<RacerProgress>().OrderBy(x => x.racerName))
            sb.AppendLine(r.racerName.PadRight(10) + " player=" + r.isPlayer + " pos=" + r.transform.position.ToString("F0") + " active=" + r.gameObject.activeInHierarchy + " kin=" + r.GetComponent<Rigidbody>().isKinematic);
        var slots = Object.FindObjectsByType<HarborRacer.Track.ZoneMarker>(FindObjectsInactive.Include).Where(z => z.kind == HarborRacer.Track.ZoneKind.StartSlot).OrderBy(z => z.index);
        foreach (var z in slots) sb.AppendLine("slot " + z.index + " " + z.transform.position.ToString("F0"));
        return sb.ToString();
    }
}
