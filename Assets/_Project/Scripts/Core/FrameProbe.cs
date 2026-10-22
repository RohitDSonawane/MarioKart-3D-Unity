using System.IO;
using System.Text;
using HarborRacer.Inputs;
using HarborRacer.Items;
using HarborRacer.Kart;
using HarborRacer.Race;
using HarborRacer.Track;
using UnityEngine;

namespace HarborRacer
{
    /// <summary>
    /// Opt-in frame-time recorder for built players: start the exe with -perfprobe and it writes perf_probe.txt next to the log (frame time,
    /// CPU/GPU split from FrameTimingManager, GC collections, every frame over 33 ms with the race state). -perfprobe-auto also lets the
    /// player kart follow the racing line, so a whole race can be sampled without a controller. Does nothing otherwise.
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    public class FrameProbe : MonoBehaviour
    {
        const float SpikeMs = 33f;
        StringBuilder log = new StringBuilder();
        FrameTiming[] timings = new FrameTiming[1];
        float windowT, windowWorst; int windowFrames, gc0, spikes; float elapsed; bool auto, autoDone; float nextHit = 20f; double tUpdate, tBeforeRender, tEnd, prevEnd; float phaseA, phaseB, phaseC; int hitIdx; string path;
        float[] all = new float[40000]; int count; float nextItem = 12f; int itemIdx; string lastAction = "-"; float lastActionT;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = System.Environment.GetCommandLineArgs();
            bool probe = false, auto = false;
            foreach (var a in args) { if (a == "-perfprobe") probe = true; if (a == "-perfprobe-auto") { probe = true; auto = true; } }
            if (!probe) return;
            Application.runInBackground = true;
            var go = new GameObject("FrameProbe"); DontDestroyOnLoad(go);
            var fp = go.AddComponent<FrameProbe>(); fp.auto = auto;
            fp.path = Path.Combine(Application.dataPath, "..", "perf_probe.txt");
            fp.log.AppendLine($"gpu {SystemInfo.graphicsDeviceName} api {SystemInfo.graphicsDeviceType} res {Screen.width}x{Screen.height} vsync {QualitySettings.vSyncCount} target {Application.targetFrameRate} quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
            fp.gc0 = System.GC.CollectionCount(0);
        }

        System.Collections.IEnumerator Start()
        {
            Application.onBeforeRender += () => tBeforeRender = Time.realtimeSinceStartupAsDouble;
            var eof = new WaitForEndOfFrame();
            while (true)
            {
                yield return eof;
                tEnd = Time.realtimeSinceStartupAsDouble;
                phaseA = (float)((tUpdate - prevEnd) * 1000.0); phaseB = (float)((tBeforeRender - tUpdate) * 1000.0); phaseC = (float)((tEnd - tBeforeRender) * 1000.0);
                prevEnd = tEnd;
                if (phaseA + phaseB + phaseC > SpikeMs && elapsed > 3f)
                    log.AppendLine($"    phases: before-Update (physics/engine) {phaseA:F0} ms, Update..render {phaseB:F0} ms, render+present {phaseC:F0} ms");
            }
        }

        void Update()
        {
            tUpdate = Time.realtimeSinceStartupAsDouble;
            float dt = Time.unscaledDeltaTime * 1000f; elapsed += Time.unscaledDeltaTime;
            if (count < all.Length) all[count++] = dt;
            windowT += dt; windowFrames++; windowWorst = Mathf.Max(windowWorst, dt);
            var rm = RaceManager.Instance;
            if (auto && !autoDone && rm != null && rm.Player != null) EnableAutopilot(rm);
            if (auto && autoDone && rm.State == RaceState.Racing && elapsed > nextItem)
            {
                nextItem = elapsed + 4f;
                var ids = (ItemId[])System.Enum.GetValues(typeof(ItemId));
                var id = ids[itemIdx++ % ids.Length];
                if (id != ItemId.None)
                {
                    var holder = rm.Player.GetComponent<ItemHolder>(); var sw = System.Diagnostics.Stopwatch.StartNew();
                    holder.Give(id); holder.Step(0.02f, true, false);
                    lastAction = id.ToString(); lastActionT = elapsed;
                    log.AppendLine($"  item {id}: Give+Use took {sw.Elapsed.TotalMilliseconds:F1} ms at t={elapsed:F1}s");
                }
            }
            if (auto && autoDone && rm.State == RaceState.Racing && elapsed > nextHit)
            {
                nextHit = elapsed + 7f;
                var types = new[] { HitType.SpinOut, HitType.Tumble, HitType.Explosion };
                var ht = types[hitIdx++ % types.Length]; var pk2 = rm.Player.transform;
                rm.Player.Kart.HitReaction.Apply(ht, pk2.position + pk2.forward * 3f);
                lastAction = ht.ToString(); lastActionT = elapsed;
            }
            if (dt > SpikeMs && elapsed > 3f)
            {
                spikes++;
                var p = rm != null && rm.Player != null ? rm.Player.transform.position : Vector3.zero;
                log.AppendLine($"  SPIKE t={elapsed:F1}s {dt:F0} ms frame {Time.frameCount} race {(rm != null ? rm.State + " " + rm.RaceTime.ToString("F1") : "-")} player ({p.x:F0},{p.y:F0},{p.z:F0}) gc0+{System.GC.CollectionCount(0) - gc0} lastItem {lastAction} {elapsed - lastActionT:F1}s ago");
            }
            if (windowT >= 2000f)
            {
                FrameTimingManager.CaptureFrameTimings();
                uint n = FrameTimingManager.GetLatestTimings(1, timings);
                string split = n > 0 ? $" cpu {timings[0].cpuFrameTime:F1} gpu {timings[0].gpuFrameTime:F1} ms" : "";
                log.AppendLine($"t={elapsed:F0}s avg {windowT / windowFrames:F1} ms ({1000f * windowFrames / windowT:F0} fps) worst {windowWorst:F0} ms{split} gc0 {System.GC.CollectionCount(0) - gc0} heap {System.GC.GetTotalMemory(false) / 1048576}MB");
                windowT = 0; windowFrames = 0; windowWorst = 0;
                File.WriteAllText(path, log.ToString());
            }
            if (elapsed > 150f) Finish();
        }

        void EnableAutopilot(RaceManager rm)
        {
            autoDone = true;
            var player = rm.Player; var pk = player.GetComponent<PlayerKartInput>(); if (pk) pk.enabled = false;
            var pd = player.gameObject.AddComponent<PathDriverInput>(); pd.kart = player.Kart;
            pd.path = GameObject.Find("Markers/Paths/AI/ComputerDriverPATH Toad").GetComponent<TrackPath>(); pd.SnapToPath();
            pd.racerAI = true; pd.profile = AIProfile.ForName("Standin"); pd.hopMinDistanceFromHuman = 0f; player.Kart.Input = pd;
        }

        void Finish()
        {
            System.Array.Sort(all, 0, count);
            log.AppendLine($"SUMMARY {count} frames, spikes>{SpikeMs}ms {spikes}: p50 {all[count / 2]:F1} p95 {all[(int)(count * 0.95)]:F1} p99 {all[(int)(count * 0.99)]:F1} p999 {all[(int)(count * 0.999)]:F1} max {all[count - 1]:F0} ms");
            File.WriteAllText(path, log.ToString());
            Application.Quit();
        }
    }
}
