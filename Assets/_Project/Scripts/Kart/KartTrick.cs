using System;
using UnityEngine;

namespace HarborRacer.Kart
{
    /// <summary>
    /// inside a TrickZone, pressing the trick key above 40 speed gives a small upward kick and
    /// a boost (min 0.9 s) when the kart lands. Trick animations cycle 0,1,2 (JumpTrick1, JumpTrick2, GliderTrick).
    /// </summary>
    public class KartTrick : MonoBehaviour
    {
        public KartController kart;
        public int NextTrickIndex { get; private set; }
        public event Action<int> TrickPerformed;
        public event Action TrickLanded;

        float force;
        int stepsLeft;
        bool airborneSeen;

        void Awake() { if (kart == null) kart = GetComponent<KartController>(); }

        public void Step(bool trickPressed)
        {
            var s = kart.stats;
            var st = kart.State;

            if (trickPressed && st.TrickAvailable && kart.ForwardSpeed > s.trickMinSpeed && !st.Gliding)
            {
                st.TrickAvailable = false;
                st.TrickPending = true;
                airborneSeen = false;
                force = s.trickForceStart;
                stepsLeft = s.trickSteps;
                int idx = NextTrickIndex;
                NextTrickIndex = (NextTrickIndex + 1) % 3;
                TrickPerformed?.Invoke(idx);
            }

            if (stepsLeft > 0)
            {
                if (st.OnJumpPanel) stepsLeft = 0;      // loop stops when a jump panel takes over
                else
                {
                    kart.Body.AddForce(kart.transform.up * (force * s.trickFrameDt), ForceMode.Acceleration);
                    if (force >= s.trickForceStep) force -= s.trickForceStep;
                    stepsLeft--;
                }
            }

            if (st.TrickPending)
            {
                if (!st.Grounded) airborneSeen = true;
                else if (airborneSeen)
                {
                    kart.Boost?.AddBoost(s.trickLandBoost, BoostSource.Trick, burst: true);
                    st.TrickPending = false;
                    TrickLanded?.Invoke();
                }
            }
        }

        public void Cancel() { stepsLeft = 0; kart.State.TrickPending = false; }
    }
}
