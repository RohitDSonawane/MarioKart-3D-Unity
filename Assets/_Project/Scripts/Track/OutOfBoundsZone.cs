using HarborRacer.Kart;
using UnityEngine;

namespace HarborRacer.Track
{
    /// <summary>Ref OutOfBounds / Water tags: touching this sends the kart back to the last checkpoint.</summary>
    public class OutOfBoundsZone : KartZoneBase
    {
        public RespawnReason reason = RespawnReason.OutOfBounds;
        public override void OnKartEnter(KartController k) => k.Respawn?.Begin(reason);
    }
}
