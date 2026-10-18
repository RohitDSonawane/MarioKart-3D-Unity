using HarborRacer.Kart;
using UnityEngine;

namespace HarborRacer.Track
{
    /// <summary>Ref GliderPanel (the blue dash panel on a glider ramp): leaving it gives a 2 s boost (and lift if already gliding).</summary>
    public class GliderRampZone : KartZoneBase
    {
        public override void OnKartExit(KartController k)
        {
            if (k.Glider != null) k.Glider.OnRampExit();
            else k.Boost?.AddBoost(2f, BoostSource.Glider, burst: true);
        }
    }
}
