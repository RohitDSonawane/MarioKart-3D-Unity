using System.Collections.Generic;
using HarborRacer.Hazards;
using HarborRacer.Kart;
using UnityEngine;

namespace HarborRacer.Items
{
    /// <summary>Bob-omb / blue shell blast. Applied instantly here; Star, Bullet and respawn immunity live in KartHitReaction.</summary>
    public static class ExplosionBlast
    {
        public static int Detonate(Vector3 pos, float radius, KartController owner)
        {
            int hits = 0;
            var seen = new HashSet<KartController>();
            foreach (var c in Physics.OverlapSphere(pos, radius, 1 << Layers.Kart, QueryTriggerInteraction.Ignore))
            {
                var k = ItemProjectile.KartOf(c);
                if (k == null || !seen.Add(k) || k.HitReaction == null) continue;
                if (k.HitReaction.Apply(HitType.Explosion, pos)) hits++;
            }

            for (int i = ItemProjectile.All.Count - 1; i >= 0; i--)
            {
                var p = ItemProjectile.All[i];
                if (p == null) { ItemProjectile.All.RemoveAt(i); continue; }
                if (!(p is BlueShell) && (p.transform.position - pos).sqrMagnitude < radius * radius) p.Kill();
            }

            foreach (var c in Physics.OverlapSphere(pos, radius, 1 << Layers.Zone, QueryTriggerInteraction.Collide))
            {
                var crate = c.GetComponentInParent<CrateZone>();
                if (crate != null) crate.Break();
            }

            if (Application.isPlaying)
            {
                Audio.AudioService.Instance?.Play3D(Audio.AudioService.Instance?.library?.explosion, pos, 1f, 10f, 100f);
                BlastVisual.Spawn(pos, radius);
            }
            return hits;
        }
    }

}
