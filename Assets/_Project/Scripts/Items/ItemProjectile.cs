using System.Collections.Generic;
using HarborRacer.Hazards;
using HarborRacer.Kart;
using HarborRacer.Race;
using HarborRacer.Track;
using UnityEngine;

namespace HarborRacer.Items
{
    /// <summary>
    /// Common base for thrown / dropped items. Movement and hit detection are callback-free (overlap queries and
    /// casts) and run from Tick(dt), so they also work under manual physics stepping. Items share one list so they can destroy each
    /// other, and every projectile first asks the target's ItemHolder whether a held-behind item blocks it.
    /// </summary>
    public abstract class ItemProjectile : MonoBehaviour
    {
        public static readonly List<ItemProjectile> All = new List<ItemProjectile>();

        public KartController owner;
        public ItemId kind;
        public float age;
        public float ownerGrace = 0.5f;           // the thrower cannot be hit during this time
        public bool Dead { get; private set; }

        protected Vector3 groundNormal = Vector3.up;
        protected virtual float HitRadius => 1.6f;
        protected virtual HitType HitKind => HitType.Tumble;

        static readonly Collider[] KartBuf = new Collider[16];
        static readonly Collider[] ZoneBuf = new Collider[16];
        static readonly Dictionary<Collider, KartController> KartCache = new Dictionary<Collider, KartController>();

        /// <summary>Clears static state. Domain reload is off for Play mode, so statics would otherwise survive between Play sessions.</summary>
        public static void ResetStatics() { All.Clear(); KartCache.Clear(); itemPath = null; }

        void OnEnable() => Register();
        /// <summary>Called by the Create methods too: OnEnable does not run for components added in edit mode (test harness).</summary>
        public void Register() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }
        void FixedUpdate() => Tick(Time.fixedDeltaTime);

        public void Tick(float dt)
        {
            if (Dead) return;
            age += dt;
            Move(dt);
            if (Dead) return;
            Collide();
        }

        protected abstract void Move(float dt);

        public void Kill()
        {
            if (Dead) return;
            Dead = true;
            All.Remove(this);
            ItemVisuals.Destroy(gameObject);
        }

        // ---- helpers shared by the movers ----

        public static KartController KartOf(Collider c)
        {
            if (!KartCache.TryGetValue(c, out var k) || k == null) { k = c.GetComponentInParent<KartController>(); KartCache[c] = k; }
            return k;
        }

        /// <summary>Snaps to the surface below along the last known normal; returns false when there is none (free fall).</summary>
        protected bool SnapToGround(float hover, float rayUp = 2f, float rayLen = 8f)
        {
            var p = transform.position;
            if (Physics.Raycast(p + groundNormal * rayUp, -groundNormal, out var hit, rayLen, Layers.GroundMask, QueryTriggerInteraction.Ignore)
                || Physics.Raycast(p + Vector3.up * rayUp, Vector3.down, out hit, rayLen, Layers.GroundMask, QueryTriggerInteraction.Ignore))
            {
                groundNormal = Vector3.Slerp(groundNormal, hit.normal, 0.5f).normalized;
                transform.position = hit.point + hit.normal * hover;
                return true;
            }
            return false;
        }

        protected bool WallAhead(Vector3 dir, float dist, float radius, out RaycastHit hit) =>
            Physics.SphereCast(transform.position + groundNormal * 0.2f, radius, dir, out hit, dist, 1 << Layers.Wall, QueryTriggerInteraction.Ignore);

        // ---- collisions ----

        void Collide()
        {
            if (HitKarts()) return;
            ClashWithItems();
            if (!Dead) BreakCrates();
        }

        bool HitKarts()
        {
            int n = Physics.OverlapSphereNonAlloc(transform.position, HitRadius, KartBuf, 1 << Layers.Kart, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var k = KartOf(KartBuf[i]);
                if (k == null) continue;
                if (k == owner && age < ownerGrace) continue;
                if (ContactKart(k)) return true;
            }
            return false;
        }

        /// <summary>Returns true when the projectile was used up on this kart.</summary>
        protected virtual bool ContactKart(KartController k)
        {
            if (k.State.Star || k.State.Bullet) { Kill(); return true; }              // invincible karts just destroy it
            if (k.Items != null && k.Items.TryBlock(this)) { Kill(); return true; }   // held-behind shield
            if (k.HitReaction != null && k.HitReaction.Apply(HitKind, transform.position)) { Kill(); return true; }
            return false;                                                              // immune this instant (cooldown / respawn): pass through
        }

        void ClashWithItems()
        {
            for (int i = All.Count - 1; i >= 0; i--)
            {
                var o = All[i];
                if (o == null) { All.RemoveAt(i); continue; }      // destroyed without Kill (scene reload)
                if (o == this || o.Dead || !Clashes(o)) continue;
                if ((o.transform.position - transform.position).sqrMagnitude > 2.2f * 2.2f) continue;
                o.Kill(); Kill(); return;
            }
        }

        protected virtual bool Clashes(ItemProjectile other) => !(other is BlueShell) && !(other is BobOmb);

        void BreakCrates()
        {
            int n = Physics.OverlapSphereNonAlloc(transform.position, 2f, ZoneBuf, 1 << Layers.Zone, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var crate = ZoneBuf[i].GetComponentInParent<CrateZone>();
                if (crate != null) crate.Break();
            }
        }

        // ---- helpers for the path-following shells ----

        static TrackPath itemPath;
        public static TrackPath ItemPath
        {
            get
            {
                if (itemPath == null) { var go = GameObject.Find("Markers/Paths/ItemPath"); if (go != null) itemPath = go.GetComponent<TrackPath>(); }
                return itemPath;
            }
        }

        /// <summary>Index of the path node nearest to p that lies ahead of it along heading.</summary>
        public static int NodeAhead(TrackPath path, Vector3 p, Vector3 heading)
        {
            int best = 0; float bestScore = float.MaxValue;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 to = path.GetNode(i).position - p;
                float d = to.magnitude;
                float score = d + (Vector3.Dot(to, heading) < 0f ? 150f : 0f);
                if (score < bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        public static RacerProgress RacerOf(KartController k) => k != null ? k.GetComponent<RacerProgress>() : null;
    }
}
