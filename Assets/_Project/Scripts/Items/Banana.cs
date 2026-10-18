using HarborRacer.Kart;
using UnityEngine;

namespace HarborRacer.Items
{
    /// <summary>Banana: thrown in an arc (or dropped behind), lands and stays. A kart touching it spins out. Shells and other items destroy it.</summary>
    public class Banana : ItemProjectile
    {
        public const float Gravity = 100f;
        Vector3 vel;
        bool landed;

        protected override float HitRadius => 1.3f;
        protected override HitType HitKind => HitType.SpinOut;

        public static Banana Create(KartController owner, Vector3 pos, Vector3 velocity)
        {
            var go = new GameObject("Banana");
            go.layer = Layers.Item;
            go.transform.position = pos;
            var b = go.AddComponent<Banana>(); b.Register();
            b.owner = owner; b.kind = ItemId.Banana; b.vel = velocity; b.ownerGrace = 0.25f;
            ItemVisuals.MakeItemModel(ItemId.Banana, go.transform);          // target ItemBanana model (primitive fallback inside)
            return b;
        }

        protected override void Move(float dt)
        {
            if (age > 60f) { Kill(); return; }
            if (landed) return;
            vel.y -= Gravity * dt;
            vel.x *= 0.995f; vel.z *= 0.995f;
            transform.position += vel * dt;
            if (transform.position.y < -600f) { Kill(); return; }
            if (vel.y < 0f && Physics.Raycast(transform.position + Vector3.up * 0.6f, Vector3.down, out var hit, 0.6f + 0.45f + -vel.y * dt, Layers.GroundMask, QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point + hit.normal * 0.45f;
                transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * transform.rotation;
                landed = true;
            }
        }

        protected override bool Clashes(ItemProjectile other) => !(other is Banana) && !(other is BlueShell) && !(other is BobOmb);
    }
}
