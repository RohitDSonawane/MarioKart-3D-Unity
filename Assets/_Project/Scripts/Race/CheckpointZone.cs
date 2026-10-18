using HarborRacer.Kart;
using HarborRacer.Track;
using UnityEngine;

namespace HarborRacer.Race
{
    /// <summary>A checkpoint trigger across the road. Racers must hit them in order.</summary>
    public class CheckpointZone : KartZoneBase
    {
        public int index;

        public override void OnKartEnter(KartController kart)
        {
            var progress = kart.GetComponent<RacerProgress>();
            if (progress != null) progress.OnCheckpointEntered(index);
        }
    }
}
