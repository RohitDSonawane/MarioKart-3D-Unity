using UnityEngine;

namespace HarborRacer.Track
{
    /// <summary>A waypoint along a TrackPath. Drift hints come from the DriftLeft/DriftRight tags.</summary>
    public class Waypoint : MonoBehaviour
    {
        public DriftHint driftHint;
    }
}
