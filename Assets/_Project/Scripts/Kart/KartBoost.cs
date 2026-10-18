using System;
using UnityEngine;

namespace HarborRacer.Kart
{
    public enum BoostSource { Drift, Pad, JumpPanel, Glider, GliderTrick, Trick, StartBoost, Mushroom, GoldenMushroom, AntiGravity, Debug }

    /// <summary>
    /// Single source of truth for boosting. Every boost source (drift release, pads, panels, tricks, mushrooms,
    /// rocket start) calls AddBoost. While boosting the kart's max speed is boostSpeed (see KartController).
    /// The controller ticks this once per fixed step.
    /// </summary>
    public class KartBoost : MonoBehaviour
    {
        public float Remaining;
        public bool Boosting => Remaining > 0f;

        /// <summary>Raised when a boost is granted (burst = play the burst effects/sound).</summary>
        public event Action<BoostSource, float, bool> BoostGranted;
        public event Action BoostEnded;

        /// <summary>Grants a boost. Keeps the longer of the remaining time and the new time (never shortens a boost).</summary>
        public void AddBoost(float seconds, BoostSource source, bool burst = true)
        {
            if (seconds <= 0f) return;
            Remaining = Mathf.Max(Remaining, seconds);
            BoostGranted?.Invoke(source, seconds, burst);
        }

        /// <summary>Used when the kart is hit.</summary>
        public void Cancel()
        {
            if (Remaining <= 0f) return;
            Remaining = 0f;
            BoostEnded?.Invoke();
        }

        public void Tick(float dt)
        {
            if (Remaining <= 0f) return;
            Remaining -= dt;
            if (Remaining <= 0f) { Remaining = 0f; BoostEnded?.Invoke(); }
        }
    }
}
