using HarborRacer.Kart;
using HarborRacer.Race;
using HarborRacer.Track;
using UnityEngine;

namespace HarborRacer.Items
{
    /// <summary>Straight-flying shell that hugs the ground and bounces off walls. Tumbles whatever it hits.</summary>
    public class GreenShell : ItemProjectile
    {
        public float speed = 95f;                  // kart max + 10
        public int maxBounces = 5;
        public float lifetime = 20f;
        protected Vector3 dir;
        int bounces;
        float fall;
        Transform spin;

        public static GreenShell Create(KartController owner, Vector3 pos, Vector3 direction)
        {
            var go = new GameObject("GreenShell");
            go.layer = Layers.Item;
            go.transform.position = pos;
            var s = go.AddComponent<GreenShell>(); s.Register();
            s.owner = owner; s.kind = ItemId.GreenShell; s.dir = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            s.spin = ItemVisuals.MakeItemModel(ItemId.GreenShell, go.transform);
            return s;
        }

        protected override void Move(float dt)
        {
            if (age > lifetime) { Kill(); return; }
            dir = Vector3.ProjectOnPlane(dir, groundNormal).normalized;
            if (WallAhead(dir, speed * dt + 0.3f, 0.5f, out var wall))
            {
                var n = Vector3.ProjectOnPlane(wall.normal, Vector3.up).normalized;
                if (n.sqrMagnitude > 0.01f) dir = Vector3.Reflect(dir, n);
                if (++bounces > maxBounces) { Kill(); return; }
            }
            transform.position += dir * speed * dt;
            if (SnapToGround(0.7f)) fall = 0f;
            else { fall += Banana.Gravity * dt; transform.position += Vector3.down * fall * dt; if (transform.position.y < -600f) Kill(); }
            if (spin != null) spin.Rotate(0f, 720f * dt, 0f);
        }
    }
}
