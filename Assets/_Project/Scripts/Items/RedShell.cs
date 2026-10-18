using HarborRacer.Kart;
using HarborRacer.Race;
using HarborRacer.Track;
using UnityEngine;

namespace HarborRacer.Items
{
    /// <summary>
    /// Red shell: follows the item path ahead of the thrower and locks onto a racer ahead of the thrower that comes within lock range
    /// Thrown backwards it behaves like a green shell.
    /// </summary>
    public class RedShell : GreenShell
    {
        public float chaseSpeed = 120f;
        public float lockRange = 30f;
        public bool homing = true;
        TrackPath path;
        int node = -1;
        KartController target;
        Transform ball;

        public static RedShell CreateRed(KartController owner, Vector3 pos, Vector3 direction, bool homingEnabled)
        {
            var go = new GameObject("RedShell");
            go.layer = Layers.Item;
            go.transform.position = pos;
            var s = go.AddComponent<RedShell>(); s.Register();
            s.owner = owner; s.kind = ItemId.RedShell; s.dir = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            s.homing = homingEnabled; s.speed = homingEnabled ? s.chaseSpeed : 95f;
            s.ball = ItemVisuals.MakeItemModel(ItemId.RedShell, go.transform);
            s.path = ItemPath;
            return s;
        }

        public KartController Target => target;

        protected override void Move(float dt)
        {
            if (!homing || path == null) { base.Move(dt); return; }
            if (age > 15f) { Kill(); return; }
            if (node < 0) node = NodeAhead(path, transform.position, dir);

            if (target == null || target.Respawn != null && target.Respawn.Respawning) target = FindTarget();

            Vector3 pos = transform.position;
            Vector3 goal = target != null ? target.transform.position : path.GetNode(node).position;
            Vector3 flatTo = Vector3.ProjectOnPlane(goal - pos, groundNormal);
            if (target == null && flatTo.magnitude < 14f) { node = (node + 1) % path.Count; flatTo = Vector3.ProjectOnPlane(path.GetNode(node).position - pos, groundNormal); }

            if (flatTo.sqrMagnitude > 0.01f)
                dir = Vector3.RotateTowards(Vector3.ProjectOnPlane(dir, groundNormal).normalized, flatTo.normalized, (target != null ? 9f : 5f) * dt, 0f);

            if (WallAhead(dir, chaseSpeed * dt + 0.3f, 0.5f, out _)) { Kill(); return; }   // wall contact ends the chase
            transform.position += dir * chaseSpeed * dt;
            if (!SnapToGround(0.7f)) { Kill(); return; }
            if (ball != null) ball.Rotate(0f, 720f * dt, 0f);
        }

        KartController FindTarget()
        {
            var rm = RaceManager.Instance;
            if (rm == null) return null;
            var me = RacerOf(owner);
            KartController best = null; float bestD = lockRange;
            foreach (var r in rm.Racers)
            {
                if (r.Kart == owner || r.Finished) continue;
                if (me != null && r.Position >= me.Position) continue;          // only racers ahead of the thrower
                float d = Vector3.Distance(r.transform.position, transform.position);
                if (d < bestD) { bestD = d; best = r.Kart; }
            }
            return best;
        }
    }
}
