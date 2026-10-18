using HarborRacer.Kart;
using UnityEngine;

namespace HarborRacer.Track
{
    /// <summary>
    /// Base for gameplay zones the kart can overlap (boost pads, jump panels, trick zones, out-of-bounds...).
    /// Zone colliders are triggers on the Zone layer; KartZoneSensor polls overlaps each fixed step and calls these.
    /// </summary>
    public abstract class KartZoneBase : MonoBehaviour
    {
        public virtual void OnKartEnter(KartController kart) { }
        public virtual void OnKartStay(KartController kart) { }
        public virtual void OnKartExit(KartController kart) { }
    }
}
