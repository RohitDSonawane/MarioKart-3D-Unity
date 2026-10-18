using System.Linq;
using System.Reflection;
using UnityEngine;
using HarborRacer.Kart;
using HarborRacer.Race;

public static class AgBump
{
    public static string Run()
    {
        Physics.SyncTransforms();
        foreach (var m in Object.FindObjectsByType<MonoBehaviour>().OrderBy(m => m is CheckpointTrack ? 0 : m is KartController ? 2 : 3))
        {
            if (m == null || m.GetType().Namespace == null || !m.GetType().Namespace.StartsWith("HarborRacer")) continue;
            var a = m.GetType().GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
            if (a != null) a.Invoke(m, null);
        }
        var k = Object.FindObjectsByType<RacerProgress>().First(r => r.isPlayer).Kart;
        var ag = k.GetComponent<KartAntiGravity>();
        if (ag == null) return "no KartAntiGravity on " + k.name;
        var t = typeof(KartAntiGravity);
        var f = t.GetField("bumpTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        var fu = t.GetMethod("FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        string r = "state=" + (k.State != null) + " boost=" + (k.Boost != null) + " field=" + (f != null) + " fu=" + (fu != null) + "; "; if (k.State == null || k.Boost == null || f == null || fu == null) return r;
        foreach (bool inAg in new[] { true, false })
        {
            foreach (float start in new[] { 0.2f, 1.5f })
            {
                k.State.AntiGravity = inAg; k.Boost.Remaining = start; f.SetValue(ag, 0.3f);
                for (int i = 0; i < 20; i++) fu.Invoke(ag, null);
                r += "antiGravity=" + inAg + ": boost " + start + " -> " + k.Boost.Remaining.ToString("F2") + "\n";
            }
        }
        k.State.AntiGravity = false; k.Boost.Remaining = 0;
        return r;
    }
}
