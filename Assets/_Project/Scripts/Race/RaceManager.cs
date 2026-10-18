using System;
using System.Collections.Generic;
using HarborRacer.Inputs;
using HarborRacer.Kart;
using HarborRacer.Track;
using UnityEngine;

namespace HarborRacer.Race
{
    public enum RaceState { Waiting, Intro, Countdown, Racing, PlayerFinished, Results }

    public struct RaceResultEntry
    {
        public int Place;
        public string Name;
        public bool Finished;
        public float Time;
        public RacerProgress Racer;
    }

    /// <summary>
    /// One race: intro hold, 3-2-1-GO countdown, racing, the player's finish (autopilot), then results.
    /// Ports the RACE_MANAGER: positions are sorted every 0.1 s by checkpoints passed then distance to the next one;
    /// the race ends for the player when the lap count exceeds the total. Static RACE_STARTED/RACE_COMPLETED become State.
    /// Logic runs in FixedUpdate (deterministic, and testable by stepping physics manually).
    /// </summary>
    public class RaceManager : MonoBehaviour
    {
        public static RaceManager Instance { get; private set; }

        [Header("Setup")]
        public int totalLaps = 3;
        public float introSeconds = 3f;
        public int countdownFrom = 3;
        public float countdownStepSeconds = 1f;
        public float resultsTimeoutAfterPlayerFinish = 20f;
        public float sortInterval = 0.1f;
        public TrackPath raceEndPath;           // the finish-line autopilot route

        public RaceState State { get; private set; } = RaceState.Waiting;
        public float RaceTime { get; private set; }
        public int CountdownValue { get; private set; }               // 3,2,1 then 0 = GO (briefly)
        public float GoDisplayTimer { get; private set; }
        public List<RacerProgress> Racers { get; } = new List<RacerProgress>();
        public List<RacerProgress> Sorted { get; } = new List<RacerProgress>();
        public List<RaceResultEntry> Results { get; } = new List<RaceResultEntry>();
        public RacerProgress Player { get; private set; }
        public bool FinalLap { get; private set; }

        public event Action<RaceState> StateChanged;
        public event Action<int> CountdownTick;                         // 3,2,1,0(GO)
        public event Action<RacerProgress, int> LapCompleted;           // racer, lap just finished
        public event Action FinalLapStarted;                            // player entered the last lap
        public event Action<RacerProgress> RacerFinished;
        public event Action RaceCompleted;                              // results are ready

        readonly Dictionary<RacerProgress, (Action<int> lap, Action fin)> hooks = new Dictionary<RacerProgress, (Action<int> lap, Action fin)>();
        float stateTimer, sortTimer, finishTimer;
        int finishedCount;
        bool started;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start() { BeginRace(); }

        /// <summary>Collect racers, lock them, and begin the intro. Safe to call again (restart in place).</summary>
        public void BeginRace()
        {
            foreach (var kv in hooks) { kv.Key.LapCompleted -= kv.Value.lap; kv.Key.OnFinished -= kv.Value.fin; }
            hooks.Clear();
            Racers.Clear(); Sorted.Clear(); Results.Clear();
            foreach (var r in FindObjectsByType<RacerProgress>())
            {
                Racers.Add(r);
                r.Init(totalLaps);
                r.Kart.controlsLocked = true;
                if (r.isPlayer) Player = r;
                RacerProgress racer = r;
                Action<int> lapHandler = lap => OnLapCompleted(racer, lap);
                Action finHandler = () => OnRacerFinished(racer);
                r.LapCompleted += lapHandler;
                r.OnFinished += finHandler;
                hooks[r] = (lapHandler, finHandler);
            }
            Sorted.AddRange(Racers);
            SortRacers();
            RaceTime = 0f; finishedCount = 0; FinalLap = false; finishTimer = 0f; sortTimer = 0f;
            started = true;
            SetState(RaceState.Intro);
            stateTimer = introSeconds;
        }

        public int NextFinishPlace() => ++finishedCount;

        void FixedUpdate() => Tick(Time.fixedDeltaTime);

        /// <summary>Advance the race by dt seconds (called from FixedUpdate; tests may call it directly).</summary>
        public void Tick(float dt)
        {
            if (!started) return;
            foreach (var r in Racers) r.Tick(dt);

            switch (State)
            {
                case RaceState.Intro:
                    stateTimer -= dt;
                    if (stateTimer <= 0f) StartCountdown();
                    break;
                case RaceState.Countdown:
                    stateTimer -= dt;
                    if (stateTimer <= 0f) AdvanceCountdown();
                    break;
                case RaceState.Racing:
                case RaceState.PlayerFinished:
                    RaceTime += dt;
                    GoDisplayTimer = Mathf.Max(0f, GoDisplayTimer - dt);
                    sortTimer += dt;
                    if (sortTimer >= sortInterval) { sortTimer = 0f; SortRacers(); }
                    if (State == RaceState.PlayerFinished) UpdateAfterPlayerFinish(dt);
                    break;
            }
        }

        // ---- start sequence ----

        void StartCountdown()
        {
            SetState(RaceState.Countdown);
            CountdownValue = countdownFrom;
            stateTimer = countdownStepSeconds;
            CountdownTick?.Invoke(CountdownValue);
        }

        void AdvanceCountdown()
        {
            CountdownValue--;
            if (CountdownValue > 0)
            {
                stateTimer = countdownStepSeconds;
                CountdownTick?.Invoke(CountdownValue);
                return;
            }
            // GO
            CountdownValue = 0;
            GoDisplayTimer = 1.5f;
            CountdownTick?.Invoke(0);
            foreach (var r in Racers) r.Kart.controlsLocked = false;
            SetState(RaceState.Racing);
            foreach (var r in Racers) r.GetComponent<RocketStart>()?.Evaluate();
        }

        // ---- positions ----

        public void SortRacers()
        {
            Sorted.Sort(Compare);
            for (int i = 0; i < Sorted.Count; i++) Sorted[i].Position = i + 1;
        }

        static int Compare(RacerProgress a, RacerProgress b)
        {
            if (a.Finished != b.Finished) return a.Finished ? -1 : 1;
            if (a.Finished) return a.FinishPlace.CompareTo(b.FinishPlace);
            if (a.CheckpointsPassed != b.CheckpointsPassed) return b.CheckpointsPassed.CompareTo(a.CheckpointsPassed);
            return a.DistanceToNext.CompareTo(b.DistanceToNext);
        }

        // ---- laps and finish ----

        void OnLapCompleted(RacerProgress r, int lap)
        {
            LapCompleted?.Invoke(r, lap);
            if (r == Player && r.Lap == totalLaps && !FinalLap)
            {
                FinalLap = true;
                FinalLapStarted?.Invoke();
            }
        }

        void OnRacerFinished(RacerProgress r)
        {
            RacerFinished?.Invoke(r);
            if (r == Player || (Player == null && State == RaceState.Racing))
            {
                StartAutopilot(r);
                SetState(RaceState.PlayerFinished);
                finishTimer = 0f;
            }
            else if (r != Player)
            {
                // AI karts simply keep following their path; nothing to switch.
            }
            if (State == RaceState.PlayerFinished && AllFinished()) CompleteRace();
        }

        void UpdateAfterPlayerFinish(float dt)
        {
            finishTimer += dt;
            if (AllFinished() || finishTimer >= resultsTimeoutAfterPlayerFinish) CompleteRace();
        }

        bool AllFinished()
        {
            foreach (var r in Racers) if (!r.Finished) return false;
            return true;
        }

        void StartAutopilot(RacerProgress r)
        {
            var kart = r.Kart;
            kart.Drift?.Cancel();
            kart.Boost?.Cancel();
            var existing = r.GetComponent<PathDriverInput>();
            var driver = existing != null ? existing : r.gameObject.AddComponent<PathDriverInput>();
            driver.kart = kart;
            driver.path = raceEndPath != null ? raceEndPath : driver.path;
            driver.cruiseSpeed = kart.stats.maxSpeed;
            driver.SnapToPath();
            var real = r.GetComponent<PlayerKartInput>();
            if (real != null) real.enabled = false;
            kart.Input = driver;
            kart.controlsLocked = false;
        }

        void CompleteRace()
        {
            if (State == RaceState.Results) return;
            SortRacers();
            Results.Clear();
            // finished racers by finish place, then unfinished by live position
            foreach (var r in Sorted)
                Results.Add(new RaceResultEntry { Place = r.Position, Name = r.racerName, Finished = r.Finished, Time = r.Finished ? r.FinishTime : 0f, Racer = r });
            SetState(RaceState.Results);
            RaceCompleted?.Invoke();
        }

        void SetState(RaceState s)
        {
            if (State == s) return;
            State = s;
            StateChanged?.Invoke(s);
        }

        /// <summary>Skip the fly-by and go straight to the countdown (no effect once the countdown has started).</summary>
        public void SkipIntro()
        {
            if (State == RaceState.Intro) stateTimer = 0f;
        }

        public void Restart()
        {
            HarborRacer.StaticReset.ResetAll();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0) UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);
            else UnityEngine.SceneManagement.SceneManager.LoadScene(scene.path);     // scene not in the build list (Sandbox, Editor tests)
        }
    }
}
