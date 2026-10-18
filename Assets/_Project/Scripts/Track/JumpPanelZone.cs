using HarborRacer.Kart;
using UnityEngine;

namespace HarborRacer.Track
{
    /// <summary>
    /// entering starts the jump-panel state with this panel's up/down force values and a 2 s boost.
    /// upForce is negative = upward push; it lerps toward downForce (positive = downward pull) at 2.5/s.
    /// </summary>
    public class JumpPanelZone : KartZoneBase
    {
        public float upForce = -250000f;
        public float downForce = 200000f;
        public float boostSeconds = 2f;
        public float launchSpeed;            // upward launch speed; 0 = KartStats default. The importer aims it at the road the racing line lands on.

        public override void OnKartEnter(KartController k) => k.BeginJumpPanel(upForce, downForce, boostSeconds, launchSpeed, transform.forward);
    }
}
