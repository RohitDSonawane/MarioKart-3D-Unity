using HarborRacer.Kart;
using HarborRacer.Race;
using HarborRacer.Track;
using UnityEngine;

namespace HarborRacer.Items
{
    /// <summary>
    /// Blue shell: flies above the track along the item path at 150 u/s ignoring everyone, homes on whoever is in first place,
    /// then dives and explodes (radius 8, hits everyone inside). Only one may be active at a time (ItemHolder re-rolls otherwise).
    /// </summary>
    public class BlueShell : ItemProjectile
    {
        public float speed = 150f, diveSpeed = 120f, cruiseHeight = 10f, homeRange = 70f, blastRadius = 8f;
        TrackPath path;
        int node = -1;
        bool diving;
        Transform ball;

        public static bool IsActive
        {
            get { foreach (var p in All) if (p is BlueShell && !p.Dead) return true; return false; }
        }

        public KartController Target { get; private set; }

        public static BlueShell Create(KartController owner)
        {
            var go = new GameObject("BlueShell");
            go.layer = Layers.Item;
            go.transform.position = owner.transform.position + Vector3.up * 3f;
            var s = go.AddComponent<BlueShell>(); s.Register();
            s.owner = owner; s.kind = ItemId.BlueShell; s.ownerGrace = 99f;
            s.ball = ItemVisuals.MakeItemModel(ItemId.BlueShell, go.transform);   // target spiny shell model
            s.path = ItemPath;
            return s;
        }

        protected override float HitRadius => 2.2f;
        protected override bool Clashes(ItemProjectile other) => false;

        protected override bool ContactKart(KartController k)
        {
            if (!diving) return false;
            Explode(); return true;
        }

        protected override void Move(float dt)
        {
            if (age > 45f) { Explode(); return; }
            Target = FirstPlace();
            if (Target == null) { if (age > 3f) Kill(); return; }

            Vector3 pos = transform.position, tp = Target.transform.position;
            float flat = Vector3.ProjectOnPlane(tp - pos, Vector3.up).magnitude;

            if (diving)
            {
                transform.position = Vector3.MoveTowards(pos, tp, diveSpeed * dt);
                if ((tp - transform.position).magnitude < 2.5f) Explode();
            }
            else
            {
                Vector3 goal;
                if (path == null || flat < homeRange) goal = tp + Vector3.up * 8f;
                else
                {
                    if (node < 0) node = NodeAhead(path, pos, Target.transform.position - pos);
                    goal = path.GetNode(node).position + Vector3.up * cruiseHeight;
                    if (Vector3.ProjectOnPlane(goal - pos, Vector3.up).magnitude < 20f) node = (node + 1) % path.Count;
                }
                transform.position = Vector3.MoveTowards(pos, goal, speed * dt);
                if (flat < 6f && pos.y > tp.y + 3f) diving = true;
            }
            if (ball != null) ball.Rotate(0f, 540f * dt, 0f);
        }

        static KartController FirstPlace()
        {
            var rm = RaceManager.Instance;
            if (rm == null) return null;
            foreach (var r in rm.Sorted) if (!r.Finished && r.Position == 1) return r.Kart;
            return rm.Sorted.Count > 0 ? rm.Sorted[0].Kart : null;
        }

        void Explode()
        {
            ExplosionBlast.Detonate(transform.position, blastRadius, owner);
            Kill();
        }
    }
}
