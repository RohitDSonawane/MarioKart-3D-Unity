using HarborRacer.Kart;
using UnityEngine;

namespace HarborRacer.Track
{
    /// <summary>Ref TrickCollider: while inside, pressing the trick key performs a trick jump.</summary>
    public class TrickZone : KartZoneBase
    {
        public override void OnKartEnter(KartController k) => k.State.TrickAvailable = true;
        public override void OnKartExit(KartController k) => k.State.TrickAvailable = false;
    }
}
