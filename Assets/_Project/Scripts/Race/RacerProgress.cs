using System;
using HarborRacer.Kart;
using UnityEngine;

namespace HarborRacer.Race
{
    /// <summary>
    /// Per-racer lap and checkpoint state. Lap starts at 1; a lap is counted when the finish line is crossed
    /// with every checkpoint visited in order; finishing happens when the lap count exceeds the race's lap total.
    /// </summary>
    [RequireComponent(typeof(KartController))]
    public class RacerProgress : MonoBehaviour
    {
        public string racerName = "Racer";
        public bool isPlayer;

        public int Lap { get; private set; } = 1;
        public int NextCheckpoint { get; private set; }
        public int CheckpointsPassed { get; private set; }       // monotonic: used for position sorting
        public float DistanceToNext { get; private set; }
        public int Position { get; set; }                         // live 1..N (set by RaceManager)
        public bool Finished { get; private set; }
        public int FinishPlace { get; private set; }
        public float FinishTime { get; private set; }
        public bool WrongWay { get; private set; }
        public KartController Kart { get; private set; }

        public event Action<int> LapCompleted;                    // the lap just finished (1-based)
        public event Action OnFinished;                           // raised once when the racer finishes

        bool[] visited;
        float wrongWayTimer;
        int totalLaps = 3;

        void Awake()
        {
            Kart = GetComponent<KartController>();
        }

        public void Init(int laps)
        {
            totalLaps = laps;
            var track = CheckpointTrack.Current;
            visited = new bool[track != null ? track.Count : 0];
            Lap = 1; NextCheckpoint = 0; CheckpointsPassed = 0; Finished = false; FinishPlace = 0; FinishTime = 0f;
        }

        /// <summary>only the checkpoint the racer is waiting for counts. Out-of-order triggers are ignored.</summary>
        public void OnCheckpointEntered(int index)
        {
            if (Finished || visited == null || visited.Length == 0) return;
            if (index != NextCheckpoint) return;
            visited[index] = true;
            NextCheckpoint = (index + 1) % visited.Length;
            CheckpointsPassed++;
        }

        /// <summary>Crossing the line counts a lap only if every checkpoint was visited this lap.</summary>
        public void OnFinishLine()
        {
            if (Finished || visited == null || visited.Length == 0) return;
            for (int i = 0; i < visited.Length; i++) if (!visited[i]) return;

            Array.Clear(visited, 0, visited.Length);
            int done = Lap;
            Lap++;
            LapCompleted?.Invoke(done);
            if (Lap > totalLaps) Finish();
        }

        void Finish()
        {
            Finished = true;
            var rm = RaceManager.Instance;
            FinishTime = rm != null ? rm.RaceTime : 0f;
            FinishPlace = rm != null ? rm.NextFinishPlace() : 1;
            OnFinished?.Invoke();
        }

        /// <summary>Called by the RaceManager every fixed step: distance to the next checkpoint and wrong-way detection.</summary>
        public void Tick(float dt)
        {
            var track = CheckpointTrack.Current;
            if (track == null || track.Count == 0 || Finished) return;
            DistanceToNext = track.DistanceToNext(transform.position, NextCheckpoint);

            Vector3 dir = Vector3.ProjectOnPlane(track.Direction(NextCheckpoint), Vector3.up);
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            bool backwards = dir.sqrMagnitude > 0.01f && fwd.sqrMagnitude > 0.01f
                             && Vector3.Dot(dir.normalized, fwd.normalized) < -0.5f && Kart != null && Kart.ForwardSpeed > 5f;
            wrongWayTimer = backwards ? wrongWayTimer + dt : 0f;
            bool wrong = wrongWayTimer >= 2f;
            if (wrong != WrongWay) { WrongWay = wrong; }
        }

        /// <summary>Checkpoint a respawn should use: the last one passed.</summary>
        public Transform LastCheckpoint
        {
            get
            {
                var track = CheckpointTrack.Current;
                if (track == null || track.Count == 0) return null;
                return track.Get(NextCheckpoint - 1);
            }
        }
    }
}
