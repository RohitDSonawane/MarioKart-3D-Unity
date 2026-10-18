using HarborRacer.Kart;
using UnityEngine;

namespace HarborRacer.Track
{
    /// <summary>Ref "Boost" surface: while on it the kart gets a boost (Boost_time = 2 refreshed each step).</summary>
    public class BoostPadZone : KartZoneBase
    {
        public float boostSeconds = 2f;

        public override void OnKartEnter(KartController k) => k.Boost?.AddBoost(boostSeconds, BoostSource.Pad, burst: true);
        public override void OnKartStay(KartController k) => k.Boost?.AddBoost(boostSeconds, BoostSource.Pad, burst: false);
    }
}
