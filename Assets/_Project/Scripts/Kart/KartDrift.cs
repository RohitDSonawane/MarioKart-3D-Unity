using System;
using UnityEngine;

namespace HarborRacer.Kart
{
    /// <summary>
    /// arcade-style drift ported from the Player.Drift/Steer: hop on press, lock a direction, hold with steering
    /// above 40 speed to charge three tiers (blue/orange/pink), release for a mini-turbo.
    /// Driven by KartController each fixed step; visuals (hop + body yaw) run in LateUpdate.
    /// </summary>
    public class KartDrift : MonoBehaviour
    {
        public KartController kart;
        public KartBoost boost;
        public Transform visual;                 // body root that hops and yaws

        // ---- state ----
        public int DriftDir { get; private set; }          // -1 left, +1 right, 0 none
        public bool IsDrifting { get; private set; }       // drift flags (steering effects, speed penalty, body yaw)
        public bool Landed { get; private set; }           // hop has landed: lateral force active
        public bool Hopping { get; private set; }
        public float DriftTime { get; private set; }
        public int Tier { get; private set; }               // 0..3
        public float TurnStrength => kart.TurnStrength;

        public event Action<int> DriftStarted;              // direction
        public event Action<int> TierChanged;               // new tier (1..3)
        public event Action<int> DriftReleased;             // tier reached when released (0 = no boost)

        float hopT;                                          // fixed-step hop timer (physics)
        float hopVisualT;                                    // frame-rate hop timer (visual)
        bool hopVisualActive;
        Vector3 visualBasePos;
        Quaternion visualBaseRot;
        AnimationCurve hopCurve;
        bool wasHeld;

        void Awake()
        {
            if (kart == null) kart = GetComponent<KartController>();
            if (boost == null) boost = GetComponent<KartBoost>();
            if (visual != null) { visualBasePos = visual.localPosition; visualBaseRot = visual.localRotation; }
        }

        void BuildCurve()
        {
            // rise 0.62 by 0.1s, land 0.2167, small squash 0.3, settle 0.333, end 0.433
            var s = kart.stats;
            float h = s.driftHopHeight;
            hopCurve = new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.1f, h), new Keyframe(s.driftHopLandTime, 0f),
                new Keyframe(0.3f, -0.06f), new Keyframe(0.3333f, 0f), new Keyframe(s.driftHopDuration, 0f));
            for (int i = 0; i < hopCurve.length; i++) hopCurve.SmoothTangents(i, 0.2f);
        }

        // ---------------------------------------------------------------- fixed step

        /// <summary>Called by KartController every fixed step, after the ground probe and before max speed is resolved.</summary>
        public void Step(float dt, bool throttle, float steer, bool held, bool pressed)
        {
            var s = kart.stats;
            var st = kart.State;
            bool steering = Mathf.Abs(steer) > s.steerDeadzone;
            bool blocked = st.Gliding || st.OnJumpPanel || st.Stunned;

            // --- hop && !glider && !jumpPanel && onGround
            bool onGround = st.GroundHit && st.GroundDistance <= 1.2f && st.GroundNormal.y > s.groundNormalMinY;
            if (pressed && !blocked && onGround)
            {
                Hopping = true; Landed = false; hopT = 0f;
                hopVisualActive = true; hopVisualT = 0f;
                DriftDir = steering ? (steer > 0f ? 1 : -1) : 0;
                kart.TurnStrength = s.driftHopTurnStrength;
            }
            if (Hopping)
            {
                hopT += dt;
                if (!Landed && hopT >= s.driftHopLandTime) Landed = true;
                if (hopT >= s.driftHopDuration) Hopping = false;
            }

            // choose a direction late if the hop was made without steering
            if (held && DriftDir == 0 && steering && (Hopping || Landed)) DriftDir = steer > 0f ? 1 : -1;

            // --- drift condition
            bool cond = held && st.Grounded && kart.CurrentSpeed > s.driftMinSpeed && steering && DriftDir != 0 && !blocked;

            if (cond)
            {
                if (!IsDrifting) { IsDrifting = true; DriftStarted?.Invoke(DriftDir); }
                kart.TurnStrength = Mathf.Lerp(kart.TurnStrength, s.turnStrength, s.driftTurnStrengthRamp * dt);
                DriftTime += dt;
                int tier = DriftTime >= s.driftTier3Time ? 3 : DriftTime >= s.driftTier2Time ? 2 : DriftTime >= s.driftTier1Time ? 1 : 0;
                if (tier != Tier) { Tier = tier; if (tier > 0) TierChanged?.Invoke(tier); }
            }
            else if (kart.CurrentSpeed < s.driftMinSpeed || blocked)
            {
                CancelInternal();                    // speed < 40 resets flags and time
            }

            // releasing the accelerator cancels the drift. No boost.
            if (!throttle && IsDrifting) CancelInternal();

            if (!held)
            {
                if (wasHeld) Release();
                else { IsDrifting = false; DriftDir = 0; }
            }
            wasHeld = held;
        }

        void Release()
        {
            var s = kart.stats;
            int tier = DriftTime >= s.driftTier3Time ? 3 : DriftTime >= s.driftTier2Time ? 2 : DriftTime >= s.driftTier1Time ? 1 : 0;
            if (tier > 0 && boost != null)
            {
                float secs = tier == 3 ? s.driftTier3Boost : tier == 2 ? s.driftTier2Boost : s.driftTier1Boost;
                boost.AddBoost(secs, BoostSource.Drift, burst: true);
            }
            if (IsDrifting || tier > 0) DriftReleased?.Invoke(tier);
            ResetState();
        }

        /// <summary>Hit, glider, jump panel, respawn, race end. Never grants a boost.</summary>
        public void Cancel() => CancelInternal();

        void CancelInternal()
        {
            if (IsDrifting) DriftReleased?.Invoke(0);
            ResetState();
        }

        void ResetState()
        {
            IsDrifting = false; DriftDir = 0; DriftTime = 0f; Tier = 0; Landed = false;
        }

        /// <summary>Steer value that actually turns the kart while drifting.</summary>
        public float TurnDirection(float steer)
        {
            var s = kart.stats;
            bool inward = steer * DriftDir > 0f;
            return DriftDir * (inward ? s.driftTurnInward : s.driftTurnOutward);
        }

        /// <summary>Lateral slide and extra downforce. Called after steering.</summary>
        public void ApplyForces(Rigidbody rb, Quaternion rot, float steer)
        {
            if (!IsDrifting) return;
            var s = kart.stats;
            if (!kart.State.Gliding)
                rb.AddForce(rot * Vector3.down * s.driftExtraDownAccel, ForceMode.Acceleration);
            if (Landed && Mathf.Abs(steer) > s.steerDeadzone)
                rb.AddForce(rot * Vector3.right * (-DriftDir) * s.driftLateralAccel, ForceMode.Acceleration);
        }

        public float MaxSpeedPenalty => IsDrifting ? kart.stats.driftMaxSpeedPenalty : 0f;

        // ---------------------------------------------------------------- visuals

        void LateUpdate()
        {
            if (visual == null || kart == null) return;
            if (hopCurve == null) BuildCurve();
            var s = kart.stats;

            float yOffset = 0f;
            if (hopVisualActive)
            {
                hopVisualT += Time.deltaTime;
                yOffset = hopCurve.Evaluate(hopVisualT);
                if (hopVisualT >= s.driftHopDuration) { hopVisualActive = false; yOffset = 0f; }
            }
            visual.localPosition = visualBasePos + Vector3.up * yOffset;

            float targetYaw = (IsDrifting && DriftDir != 0) ? DriftDir * s.driftBodyYaw : 0f;
            Quaternion target = visualBaseRot * Quaternion.Euler(0f, targetYaw, 0f);
            visual.localRotation = Quaternion.Slerp(visual.localRotation, target, s.driftBodyYawLerp * Time.deltaTime);
        }
    }
}
