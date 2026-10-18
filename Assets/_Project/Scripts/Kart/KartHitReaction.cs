using System;
using UnityEngine;

namespace HarborRacer.Kart
{
    public enum HitType { Bump, SpinOut, Tumble, Explosion }

    /// <summary>
    /// Single entry point for everything that hurts a kart: hazards, items, other karts.
    /// SpinOut (banana, cow): stunned 1.5 s, spins. Tumble (shell, trolley): stunned 1.5 s, flips. Explosion: speed 0, popped up,
    /// stunned 1.8 s. Bump: a sideways nudge only. While stunned the commanded speed lerps to 0, boost/drift/trick are
    /// cancelled and drift is blocked. Star, bullet, respawn and a short cooldown make the kart immune.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class KartHitReaction : MonoBehaviour
    {
        public KartController kart;
        public Transform spinPivot;                  // visual-only pivot that spins / flips

        [Header("Ref values")]
        public float stunSeconds = 1.5f;
        public float explosionStunSeconds = 1.8f;
        public float hitCooldown = 0.75f;
        public float bumpSpeedFast = 16f;            // 50000 * 0.016 / 50
        public float bumpSpeedSlow = 11.2f;          // 35000 * 0.016 / 50
        public int bumpSteps = 30;
        public float bumpDecay = 0.95f;
        public float spinSeconds = 1f;
        public float explosionPopSpeed = 15f;

        public float StunLeft { get; private set; }
        public bool IsInvulnerable => kart.State.Star || kart.State.Bullet || (kart.Respawn != null && kart.Respawn.Respawning) || cooldown > 0f;
        public HitType LastHit { get; private set; }
        public bool LastBumpFromLeft { get; private set; }     // which side the last bump came from (the body animator leans away)
        public bool proceduralSpin = true;                        // off when KartBodyAnimator plays the hit clips instead
        public event Action<HitType> Hit;

        float cooldown;
        float visualT, visualDur;
        HitType visualType;
        readonly Collider[] buf = new Collider[16];
        SphereCollider sphere;

        void Awake()
        {
            if (kart == null) kart = GetComponent<KartController>();
            sphere = GetComponent<SphereCollider>();
        }

        // ---- applying hits ----

        /// <summary>Returns true if the hit landed. sourcePos is where it came from (used for the side the kart is nudged to).</summary>
        public bool Apply(HitType type, Vector3 sourcePos)
        {
            if (IsInvulnerable) return false;
            cooldown = hitCooldown;
            LastHit = type;

            if (type == HitType.Bump)
            {
                NudgeAway(sourcePos, kart.CurrentSpeed > 50f ? bumpSpeedFast : bumpSpeedSlow);
                Hit?.Invoke(type);
                return true;
            }

            kart.Wallet?.Lose();                       // a hit costs coins
            StunLeft = type == HitType.Explosion ? explosionStunSeconds : stunSeconds;
            kart.State.Stunned = true;
            kart.Boost?.Cancel();
            kart.Drift?.Cancel();
            kart.Trick?.Cancel();
            kart.Glider?.Cancel();
            if (type == HitType.Tumble) NudgeAway(sourcePos, bumpSpeedFast);
            if (type == HitType.Explosion)
            {
                kart.CurrentSpeed = 0f;
                var v = kart.Body.linearVelocity; v.y = explosionPopSpeed; kart.Body.linearVelocity = v;
            }
            visualType = type; visualT = 0f; visualDur = type == HitType.Explosion ? 1.4f : spinSeconds;
            Hit?.Invoke(type);
            return true;
        }

        void NudgeAway(Vector3 sourcePos, float speed)
        {
            // side = Dot(Cross(forward, toSource), up); push to the opposite side
            Vector3 to = sourcePos - transform.position;
            float side = Vector3.Dot(Vector3.Cross(transform.forward, to), transform.up);
            LastBumpFromLeft = side < 0f;
            Vector3 dir = side < 0f ? transform.right : -transform.right;
            kart.Push(dir * speed, bumpSteps, bumpDecay);
        }

        // ---- per step ----

        public void Step(float dt)
        {
            if (cooldown > 0f) cooldown -= dt;
            if (StunLeft > 0f)
            {
                StunLeft -= dt;
                if (StunLeft <= 0f) { StunLeft = 0f; kart.State.Stunned = false; }
            }
            CheckKartContacts();
        }

        /// <summary>Kart-vs-kart bumps, detected with an overlap query (callback-free). Both karts run this, so both get nudged.</summary>
        void CheckKartContacts()
        {
            if (sphere == null || cooldown > 0f) return;
            Vector3 c = transform.TransformPoint(sphere.center);
            float r = sphere.radius * transform.lossyScale.x * 1.15f;
            int n = Physics.OverlapSphereNonAlloc(c, r, buf, 1 << Layers.Kart, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var other = buf[i].GetComponentInParent<KartController>();
                if (other == null || other == kart) continue;
                // contact within 5 units, not while invulnerable
                if (Vector3.Distance(other.transform.position, transform.position) >= 5f) continue;
                Apply(HitType.Bump, other.transform.position);
                if (other.State.Star || other.State.Bullet)
                {
                    // a starred / bullet kart knocks the other one spinning
                    cooldown = 0f;
                    Apply(HitType.Tumble, other.transform.position);
                }
                break;
            }
        }

        // ---- visuals (spin / flip) ----

        void LateUpdate()
        {
            if (spinPivot == null || !proceduralSpin) return;
            if (visualT >= visualDur && visualDur > 0f) { spinPivot.localRotation = Quaternion.identity; visualDur = 0f; return; }
            if (visualDur <= 0f) return;
            visualT += Time.deltaTime;
            float t = Mathf.Clamp01(visualT / visualDur);
            float ease = t * t * (3f - 2f * t);
            switch (visualType)
            {
                case HitType.SpinOut: spinPivot.localRotation = Quaternion.Euler(0f, 360f * ease, 0f); break;
                case HitType.Tumble: spinPivot.localRotation = Quaternion.Euler(360f * ease, 0f, 0f); break;
                case HitType.Explosion: spinPivot.localRotation = Quaternion.Euler(360f * ease, 360f * ease, 0f); break;
            }
        }
    }
}
