using HarborRacer.Kart;
using UnityEngine;

namespace HarborRacer.Items
{
    /// <summary>
    /// Bob-omb: thrown in a high arc (or dropped), bounces once, then walks toward the closest kart while flashing.
    /// Explodes after 4 s or when it touches a kart (radius 10 blast, Explosion hit on everyone inside).
    /// </summary>
    public class BobOmb : ItemProjectile
    {
        public float fuse = 4f, walkSpeed = 22f, blastRadius = 10f, seekRange = 70f;
        Vector3 vel;
        bool walking, bounced;
        Renderer body;
        Material[] origMats;
        float flash;

        protected override float HitRadius => 2.2f;

        public static BobOmb Create(KartController owner, Vector3 pos, Vector3 velocity)
        {
            var go = new GameObject("BobOmb");
            go.layer = Layers.Item;
            go.transform.position = pos;
            var b = go.AddComponent<BobOmb>(); b.Register();
            b.owner = owner; b.kind = ItemId.BobOmb; b.vel = velocity; b.ownerGrace = 0.6f;
            var t = ItemVisuals.MakeItemModel(ItemId.BobOmb, go.transform);
            b.body = t.GetComponentInChildren<Renderer>();
            if (b.body != null) b.origMats = b.body.sharedMaterials;
            return b;
        }

        protected override bool Clashes(ItemProjectile other) => false;

        protected override bool ContactKart(KartController k)
        {
            Explode();
            return true;
        }

        protected override void Move(float dt)
        {
            fuse -= dt;
            if (fuse <= 0f) { Explode(); return; }
            flash += dt;
            if (body != null && origMats != null)
            {
                bool on = (int)(flash / 0.2f) % 2 == 0 && age > 2f;          // flashes red in the last 2 s, keeping its textures otherwise
                if (on) { var m = new Material[origMats.Length]; var red = ItemVisuals.Mat(new Color(0.9f, 0.1f, 0.1f), true); for (int q = 0; q < m.Length; q++) m[q] = red; body.sharedMaterials = m; }
                else body.sharedMaterials = origMats;
            }

            if (!walking)
            {
                vel.y -= Banana.Gravity * dt;
                transform.position += vel * dt;
                if (vel.y < 0f && Physics.Raycast(transform.position + Vector3.up * 0.6f, Vector3.down, out var hit, 0.6f + 0.55f - vel.y * dt, Layers.GroundMask, QueryTriggerInteraction.Ignore))
                {
                    transform.position = hit.point + hit.normal * 0.55f;
                    groundNormal = hit.normal;
                    if (!bounced) { bounced = true; vel = new Vector3(vel.x * 0.4f, -vel.y * 0.35f, vel.z * 0.4f); }
                    else walking = true;
                }
                if (transform.position.y < -600f) Kill();
                return;
            }

            var closest = Closest();
            if (closest != null)
            {
                Vector3 to = Vector3.ProjectOnPlane(closest.transform.position - transform.position, groundNormal);
                if (to.sqrMagnitude > 0.01f) transform.position += to.normalized * walkSpeed * dt;
            }
            if (!SnapToGround(0.55f)) { vel = Vector3.zero; walking = false; }
        }

        KartController Closest()
        {
            KartController best = null; float bestD = seekRange * seekRange;
            foreach (var c in Physics.OverlapSphere(transform.position, seekRange, 1 << Layers.Kart, QueryTriggerInteraction.Ignore))
            {
                var k = KartOf(c);
                if (k == null) continue;
                float d = (k.transform.position - transform.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = k; }
            }
            return best;
        }

        void Explode()
        {
            ExplosionBlast.Detonate(transform.position, blastRadius, owner);
            Kill();
        }
    }
}
