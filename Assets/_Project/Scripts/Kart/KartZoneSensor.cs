using System.Collections.Generic;
using HarborRacer.Track;
using UnityEngine;

namespace HarborRacer.Kart
{
    /// <summary>
    /// Callback-free zone detection: each fixed step an overlap-sphere on the Zone layer finds the KartZoneBase components
    /// the kart is inside, and enter/stay/exit are derived by diffing with the previous step. Works under manual physics
    /// stepping (edit-mode tests) as well as in play mode. The sphere matches the kart's body collider.
    /// </summary>
    public class KartZoneSensor : MonoBehaviour
    {
        public KartController kart;
        public Vector3 localCenter = new Vector3(0f, 0.41f, -0.09f);
        public float localRadius = 1.64f;

        readonly Collider[] buffer = new Collider[64];
        HashSet<KartZoneBase> current = new HashSet<KartZoneBase>();
        HashSet<KartZoneBase> next = new HashSet<KartZoneBase>();
        readonly List<KartZoneBase> scratch = new List<KartZoneBase>();
        static readonly Dictionary<Collider, KartZoneBase> Cache = new Dictionary<Collider, KartZoneBase>();

        public static void ResetStatics() => Cache.Clear();

        public IReadOnlyCollection<KartZoneBase> Current => current;

        public void Poll()
        {
            if (kart == null) return;
            Vector3 c = transform.TransformPoint(localCenter);
            float r = localRadius * transform.lossyScale.x;
            int n = Physics.OverlapSphereNonAlloc(c, r, buffer, 1 << Layers.Zone, QueryTriggerInteraction.Collide);

            next.Clear();
            for (int i = 0; i < n; i++)
            {
                var col = buffer[i];
                if (!Cache.TryGetValue(col, out var zone) || zone == null)
                {
                    zone = col.GetComponentInParent<KartZoneBase>();
                    Cache[col] = zone;
                }
                if (zone != null && zone.isActiveAndEnabled) next.Add(zone);
            }

            foreach (var z in next)
                if (!current.Contains(z)) z.OnKartEnter(kart);
            foreach (var z in next) z.OnKartStay(kart);

            scratch.Clear();
            foreach (var z in current)
                if (z == null || !next.Contains(z)) scratch.Add(z);
            foreach (var z in scratch) if (z != null) z.OnKartExit(kart);

            (current, next) = (next, current);
        }

        /// <summary>Clears all zone state (respawn / teleport) so exits fire cleanly.</summary>
        public void Clear()
        {
            scratch.Clear();
            foreach (var z in current) scratch.Add(z);
            current.Clear();
            foreach (var z in scratch) if (z != null) z.OnKartExit(kart);
        }
    }
}
