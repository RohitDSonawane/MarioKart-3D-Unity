using HarborRacer.Kart;
using UnityEngine;

namespace HarborRacer.Track
{
    /// <summary>
    /// while gliding (or in anti-gravity) extra down acceleration pulls the kart toward the track.
    /// force 15000 = 15000 * 0.02 = 300 u/s2.
    /// </summary>
    public class DownForceZone : KartZoneBase
    {
        public float force = 15000f;
        public bool relative;

        public override void OnKartStay(KartController k)
        {
            if (!k.State.Gliding && !k.State.AntiGravity) return;
            Vector3 dir = (relative || k.State.AntiGravity) ? -k.transform.up : Vector3.down;
            k.Body.AddForce(dir * (force * Time.fixedDeltaTime), ForceMode.Acceleration);
        }
    }
}
