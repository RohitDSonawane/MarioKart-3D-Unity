using HarborRacer.Kart;
using UnityEngine;

namespace HarborRacer.Track
{
    /// <summary>Ref CancelDownForce: inside this volume the kart's constant down acceleration is switched off.</summary>
    public class CancelDownForceZone : KartZoneBase
    {
        public override void OnKartEnter(KartController k) => k.State.CancelDownForce = true;
        public override void OnKartExit(KartController k) => k.State.CancelDownForce = false;
    }
}
