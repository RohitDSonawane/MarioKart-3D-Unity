using HarborRacer.Kart;
using HarborRacer.Track;
using UnityEngine;

namespace HarborRacer.Race
{
    /// <summary>The finish line: counts a lap only when every checkpoint has been visited.</summary>
    public class LapLineZone : KartZoneBase
    {
        public override void OnKartEnter(KartController kart)
        {
            var progress = kart.GetComponent<RacerProgress>();
            if (progress != null) progress.OnFinishLine();
        }
    }
}
