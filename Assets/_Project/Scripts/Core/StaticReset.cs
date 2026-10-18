using HarborRacer.Items;
using HarborRacer.Kart;
using UnityEngine;

namespace HarborRacer
{
    /// <summary>
    /// This project enters Play mode without a domain reload, so static fields keep their values from the previous session (stale item lists,
    /// cached colliders, cached sprites). Runs before the first scene loads and clears every static cache.
    /// </summary>
    public static class StaticReset
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => ResetAll();

        /// <summary>Also called before a scene restart: the old scene's karts, colliders and projectiles are about to be destroyed.</summary>
        public static void ResetAll()
        {
            ItemProjectile.ResetStatics();
            KartZoneSensor.ResetStatics();
            KartRespawn.ResetCache();
            ItemVisuals.ResetStatics();
        }
    }
}
