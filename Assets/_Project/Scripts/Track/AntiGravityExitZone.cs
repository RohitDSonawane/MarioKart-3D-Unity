using HarborRacer.Kart;
using UnityEngine;

namespace HarborRacer.Track
{
    /// <summary>Ref AntiGravityFalse tag: while inside, anti-gravity is switched off (optionally easing the kart upright).</summary>
    public class AntiGravityExitZone : KartZoneBase
    {
        public bool levelOut;

        public override void OnKartStay(KartController k) => k.AntiGravity?.Exit(levelOut);
    }
}
