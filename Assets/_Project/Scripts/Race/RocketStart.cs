using HarborRacer.Kart;
using UnityEngine;

namespace HarborRacer.Race
{
    /// <summary>
    /// "before start boost": holding the accelerator during the countdown accumulates time (reset when released);
    /// at GO a hold time between 1 and 2 s grants a 1 s boost. AI karts get a 50 percent chance of a 0.8 s start boost
    /// </summary>
    public class RocketStart : MonoBehaviour
    {
        public KartController kart;
        public float minHold = 1f;
        public float maxHold = 2f;
        public float boostSeconds = 1f;
        public float aiChance = 0.5f;
        public float aiBoostSeconds = 0.8f;

        public float HeldSeconds { get; private set; }

        void Awake() { if (kart == null) kart = GetComponent<KartController>(); }

        void FixedUpdate() => Tick(Time.fixedDeltaTime);

        public void Tick(float dt)
        {
            var rm = RaceManager.Instance;
            if (rm == null || rm.State != RaceState.Countdown) return;
            bool held = kart.Input != null && kart.Input.Throttle > 0.1f;
            HeldSeconds = held ? HeldSeconds + dt : 0f;
        }

        /// <summary>Called by the RaceManager at GO.</summary>
        public void Evaluate()
        {
            bool isPlayer = GetComponent<RacerProgress>() is RacerProgress p && p.isPlayer;
            if (isPlayer)
            {
                if (HeldSeconds > minHold && HeldSeconds < maxHold)
                    kart.Boost?.AddBoost(boostSeconds, BoostSource.StartBoost, burst: true);
            }
            else if (Random.value < aiChance)
                kart.Boost?.AddBoost(aiBoostSeconds, BoostSource.StartBoost, burst: true);
            HeldSeconds = 0f;
        }
    }
}
