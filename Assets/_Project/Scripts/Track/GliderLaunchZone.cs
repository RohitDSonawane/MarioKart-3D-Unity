using HarborRacer.Kart;
using UnityEngine;

namespace HarborRacer.Track
{
    /// <summary>Ref GliderPanelFly: leaving this trigger starts the glide with the zone's glide angles (GetGlideAngle).</summary>
    public class GliderLaunchZone : KartZoneBase
    {
        public float glideAngleZ;     // roll the kart settles at
        public float glideAngleX;     // pitch offset

        public override void OnKartExit(KartController k) => k.Glider?.StartGlide(glideAngleZ, glideAngleX);
    }
}
