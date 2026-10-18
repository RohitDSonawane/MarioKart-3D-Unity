using HarborRacer.Inputs;
using HarborRacer.Kart;
using HarborRacer.Race;
using UnityEngine;

namespace HarborRacer.Items
{
    /// <summary>
    /// Opponent item logic. Called every fixed step by ItemHolder for non-player racers; returns the same two
    /// button presses a human would make. A random 0.3-1.0 s reaction delay follows every new item.
    /// </summary>
    public class AIItemBrain
    {
        readonly ItemHolder holder;
        readonly KartController kart;
        readonly RacerProgress racer;
        readonly System.Random rng;
        float held, react, spacing, goldenTap, patience;

        public AIItemBrain(ItemHolder h, KartController k, RacerProgress r)
        {
            holder = h; kart = k; racer = r;
            rng = new System.Random((r != null ? r.racerName : "ai").GetHashCode() ^ 0x5bd1);
        }

        public void Reset()
        {
            held = 0f; spacing = 0f; goldenTap = 0f;
            react = 0.3f + (float)rng.NextDouble() * 0.7f;
            patience = 8f + (float)rng.NextDouble() * 7f;            // banana / hold-behind timer
        }

        public void Decide(float dt, out bool use, out bool back)
        {
            use = back = false;
            held += dt; react -= dt; spacing -= dt; goldenTap -= dt;
            if (react > 0f) return;

            var id = holder.Current;
            switch (id)
            {
                case ItemId.Star: case ItemId.Coin: case ItemId.BlueShell:
                    use = true; break;
                case ItemId.BulletBill:
                    use = kart.AntiGravity == null || kart.AntiGravity.CanUseBullet; break;

                case ItemId.Mushroom: case ItemId.TripleMushroom:
                    if (spacing <= 0f && (OnStraight() || held > 7f)) { use = true; spacing = 2.5f; }
                    break;
                case ItemId.GoldenMushroom:
                    if (holder.GoldenLeft <= 0f) { if (OnStraight() || held > 4f) use = true; }
                    else if (goldenTap <= 0f) { use = true; goldenTap = 0.6f; }
                    break;

                case ItemId.Banana: case ItemId.TripleBanana:
                    DecideBanana(ref use, ref back); break;

                case ItemId.GreenShell: case ItemId.TripleGreen:
                    DecideShell(ref use, ref back, Target(60f, 10f) != null); break;
                case ItemId.RedShell: case ItemId.TripleRed:
                    DecideShell(ref use, ref back, AheadByPosition(80f) != null); break;
                case ItemId.BobOmb:
                    DecideShell(ref use, ref back, Target(40f, 25f) != null); break;
            }
        }

        void DecideBanana(ref bool use, ref bool back)
        {
            bool triple = holder.Current == ItemId.TripleBanana;
            if (!triple && !holder.HoldingBehind) { back = true; return; }          // drag it behind as a shield
            if (spacing > 0f) return;
            if (Behind(25f) != null || held > patience) { back = true; spacing = 1f; }
        }

        void DecideShell(ref bool use, ref bool back, bool targetAhead)
        {
            if (spacing > 0f) return;
            bool triple = ItemInfo.Uses(holder.Current) == 3;
            if (targetAhead) { use = true; spacing = 1f; return; }
            if (!triple && !holder.HoldingBehind && held > 1.2f) { back = true; return; }   // hold behind as defence
            if (Behind(30f) != null && held > 5f) { back = true; spacing = 1f; return; }     // someone is on our tail
            if (held > 22f) { use = true; spacing = 1f; }                                     // never sit on it forever
        }

        // ---- sensing ----

        bool OnStraight()
        {
            var d = kart.GetComponent<PathDriverInput>();
            if (d == null || d.path == null || d.DriftHeld) return !(d != null && d.DriftHeld);
            for (int i = 0; i < 3; i++)
            {
                var wp = d.path.GetNode(d.nodeIndex + i).GetComponent<HarborRacer.Track.Waypoint>();
                if (wp != null && wp.driftHint != HarborRacer.Track.DriftHint.None) return false;
            }
            return true;
        }

        /// <summary>A racer within range in front of the kart (cone half-angle in degrees).</summary>
        KartController Target(float range, float coneDeg)
        {
            var rm = RaceManager.Instance;
            if (rm == null) return null;
            float cos = Mathf.Cos(coneDeg * Mathf.Deg2Rad);
            foreach (var r in rm.Racers)
            {
                if (r.Kart == kart || r.Finished) continue;
                Vector3 to = r.transform.position - kart.transform.position;
                if (to.magnitude < range && Vector3.Dot(to.normalized, kart.transform.forward) > cos) return r.Kart;
            }
            return null;
        }

        KartController Behind(float range)
        {
            var rm = RaceManager.Instance;
            if (rm == null) return null;
            foreach (var r in rm.Racers)
            {
                if (r.Kart == kart || r.Finished) continue;
                Vector3 to = r.transform.position - kart.transform.position;
                if (to.magnitude < range && Vector3.Dot(to.normalized, kart.transform.forward) < -0.6f) return r.Kart;
            }
            return null;
        }

        /// <summary>A racer ahead in the standings and within range (red shells find it on their own).</summary>
        KartController AheadByPosition(float range)
        {
            var rm = RaceManager.Instance;
            if (rm == null || racer == null) return null;
            foreach (var r in rm.Racers)
            {
                if (r.Kart == kart || r.Finished || r.Position >= racer.Position) continue;
                if (Vector3.Distance(r.transform.position, kart.transform.position) < range &&
                    Vector3.Dot((r.transform.position - kart.transform.position).normalized, kart.transform.forward) > 0.3f) return r.Kart;
            }
            return null;
        }
    }
}
