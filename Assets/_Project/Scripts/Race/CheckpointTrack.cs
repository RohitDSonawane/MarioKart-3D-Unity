using System.Collections.Generic;
using UnityEngine;

namespace HarborRacer.Race
{
    /// <summary>
    /// Ordered checkpoints around the track.
    /// Gives progress-to-next-checkpoint distances for position sorting and the respawn / wrong-way logic.
    /// </summary>
    public class CheckpointTrack : MonoBehaviour
    {
        public static CheckpointTrack Current { get; private set; }

        public List<Transform> points = new List<Transform>();
        public Transform finishLine;

        public int Count => points.Count;

        void Awake()
        {
            Current = this;
            if (points.Count == 0)
                foreach (Transform t in transform) points.Add(t);
        }

        public Transform Get(int index)
        {
            int n = points.Count;
            return points[((index % n) + n) % n];
        }

        /// <summary>Track direction at a checkpoint: from the previous checkpoint toward this one.</summary>
        public Vector3 Direction(int next)
        {
            Vector3 d = Get(next).position - Get(next - 1).position;
            return d.sqrMagnitude < 0.0001f ? Get(next).forward : d.normalized;
        }

        /// <summary>
        /// how far the racer still has to travel to the next checkpoint, measured along the
        /// track direction (projection of the vector to the checkpoint onto prev->next).
        /// </summary>
        public float DistanceToNext(Vector3 position, int next)
        {
            Vector3 dir = Get(next).position - Get(next - 1).position;
            Vector3 toGoal = position - Get(next).position;
            return Vector3.Project(toGoal, dir).magnitude;
        }
    }
}
