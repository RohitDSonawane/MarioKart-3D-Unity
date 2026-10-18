using UnityEngine;

namespace HarborRacer.Kart
{
    /// <summary>
    /// Kart tuning (ScriptableObject, editable live in play mode). Defaults are the values read from the
    /// project's Toad Harbor scene and Player.cs: max 85, boost 115, turn strength 25, off-road 30.
    /// Rates named "PerStep" were written as per-FixedUpdate lerp factors in the (fixed dt 0.02).
    /// </summary>
    [CreateAssetMenu(menuName = "HarborRacer/Kart Stats", fileName = "KartStats")]
    public class KartStats : ScriptableObject
    {
        [Header("Speed")]
        public float maxSpeed = 85f;
        public float boostSpeed = 115f;
        public float offRoadMaxSpeed = 30f;
        public float reverseSpeedDivisor = 1.6f;      // reverse max = maxSpeed / this

        [Header("Acceleration (lerp factors)")]
        public float accelPerSecond = 0.5f;           // Lerp(cur, max, 0.5 * dt)
        public float offRoadAccelPerSecond = 3f;      // on dirt with throttle
        public float coastPerStep = 0.01f;            // Lerp(cur, 0, 0.01) each step with no throttle
        public float reversePerStep = 0.03f;          // Lerp(cur, -max/1.6, 0.03) each step while braking

        [Header("Gravity / grounding")]
        public float downAcceleration = 100f;         // AddForce(down * 5000 * dt, Acceleration)
        public float groundRayLength = 2f;
        public float groundedRayDistance = 1.5f;      // ray hit within this counts as grounded
        public float groundAlignPerSecond = 7.5f;
        public float groundNormalMinY = 0.5f;

        [Header("Boost")]
        public float boostAccelPerSecond = 4f;        // Lerp(cur, boost, 5*dt); applied even without throttle

        [Header("Drift")]
        public float driftMinSpeed = 40f;             // CurrentSpeed needed to start/keep a drift
        public float driftHopLandTime = 0.2167f;      // hop anim landing event ("is_drifting")
        public float driftHopDuration = 0.4333f;
        public float driftHopHeight = 0.62f;          // visual rise, in Visual local units
        public float driftTier1Time = 1.5f;           // seconds of steering while drifting
        public float driftTier2Time = 4f;
        public float driftTier3Time = 7f;
        public float driftTier1Boost = 0.75f;
        public float driftTier2Boost = 1.5f;
        public float driftTier3Boost = 2.5f;
        public float driftTurnInward = 2.1f;          // steer toward the drift direction
        public float driftTurnOutward = 0.5f;         // steer against it (or none)
        public float driftTurnRate = 1.2f;
        public float driftHopTurnStrength = 5f;       // turn strength right after the hop
        public float driftTurnStrengthRamp = 3f;      // per second, back to full strength
        public float driftLateralAccel = 1000f;       // AddForce(right * 50000 * dt, Acceleration)
        public float driftExtraDownAccel = 200f;      // -up * 10000 * dt
        public float driftMaxSpeedPenalty = 10f;
        public float driftBodyYaw = 20f;
        public float driftBodyYawLerp = 8f;

        [Header("Jump panel")]
        public float jumpPanelForwardAccel = 1200f;   // AddForce(forward * 60000 * dt, Acceleration)
        public float jumpPanelLerpPerSecond = 1.1f;   // upforce lerps toward downforce
        public float jumpPanelLaunchSpeed = 26f;      // initial upward speed off a panel (our arc: peak ~ v^2 / 2g)
        public float jumpPanelGravity = 72f;          // gravity while on the panel arc (softer than the 100 u/s^2 ground pull)
        public int jumpPanelMinSteps = 15;            // ignore ground contact right after take-off (0.3 s)
        public float jumpPanelEndGroundDistance = 0.95f;
        public float jumpPanelAirborneDistance = 1.5f; // ground probe farther than this = left the surface
        public int jumpPanelMaxSteps = 200;            // fallback: end after 4 s no matter what

        [Header("Trick")]
        public float trickMinSpeed = 40f;
        public float trickForceStart = 6500f;         // x trickFrameDt = up acceleration
        public float trickForceStep = 300f;
        public float trickFrameDt = 0.016f;
        public int trickSteps = 30;
        public float trickLandBoost = 0.9f;

        [Header("Wall / fence")]
        public float wallBounceSpeed = 8f;            // initial outward nudge (u/s), decays
        public float wallBounceDecay = 0.95f;
        public int wallBounceSteps = 60;
        public float wallSpeedLoss = 10f;             // applied when speed > wallSpeedLossMinSpeed
        public float wallSpeedLossMinSpeed = 50f;
        public int wallCooldownSteps = 15;

        [Header("Hits")]
        public float stunSpeedLerpPerSecond = 4.5f;   // Lerp(currentspeed, 0, 4.5 * dt) while hit

        [Header("Respawn")]
        public float waterFallSeconds = 0.5f;         // wait 0.5 s before teleport (water)
        public float respawnHoverSeconds = 0.5f;      // kinematic for 0.5 s after teleport
        public float respawnHoverHeight = 1.0f;

        [Header("Glider")]
        public float glideTrickMinSpeed = 60f;        // above this the launch becomes a glider trick
        public float glideCruiseSpeed = 65f;          // speed target when the throttle is released
        public float glideSpeedLerpPerSecond = 2f;
        public float glideVerticalDamp = 0.75f;       // vy *= per step
        public float glideVerticalDampClimb = 0.45f;  // while holding "climb"
        public float glideDiveAngle = 25f;
        public float glideClimbAngle = -25f;
        public float glideClimbMaxAngle = -20f;
        public float glidePitchLerp = 1.5f;
        public float glideDiveDownAccel = 40f;        // 2000 * 0.02
        public float glideClimbUpAccel = 30f;         // 1500 * 0.02, first 6 s only
        public float glideClimbMaxTime = 6f;
        public float glideBankAngle = 40f;
        public float glideBankLerp = 3f;
        public float glideBankSmoothTime = 0.1f;
        public float glideTurnStrength = 15f;
        public float glideTrickOpenDelay = 0.45f;
        public float glideTrickBoostDelay = 0.8f;
        public float glideTrickBoost = 0.5f;
        public float glideArmDelay = 1f;              // landing armed after this (non-trick)
        public float glideTrickArmDelay = 2.5f;       // landing armed after this (trick glide)
        public float glideMaxSeconds = 12f;           // fail-safe: auto-close
        public float glideRampBoost = 2f;             // leaving a glider ramp
        public float glideLiftStart = 20000f;         // impulse force, decays by glideLiftStep per step
        public float glideLiftStep = 300f;
        public int glideLiftSteps = 60;
        public float glideLiftToVelocity = 0.00032f;  // force * 0.016 (frame dt) / mass 50
        public float glideGroundRayLength = 3f;
        public float glideLandRayLength = 6f;         // stronger re-alignment right after landing
        public float glideLandAlignSeconds = 0.6f;
        public float glideNoAlignAfter = 3f;          // ground alignment stops after 3 s of gliding

        [Header("Anti-gravity")]
        public float antiGravityExtraDown = 10f;      // Physics.gravity * 5 relative
        public float antiGravityExitLevelSeconds = 1.5f;
        public float antiGravityLevelLerp = 3f;
        public float antiGravityBumpDelay = 0.3f;     // wait after a kart bump in anti-gravity...
        public float antiGravityBumpBoost = 1.25f;    // ... then boost this long if under 1 s remains

        [Header("Steering")]
        public float turnStrength = 25f;            // desired_rotate_strength (Toad Harbor scene)
        public float reverseTurnStrength = 120f;
        public float turnDegreesPerStepScale = 0.025f;
        public float steerDeadzone = 0.1f;
        public float turnRateStationary = 0f;         // speed < 3
        public float turnRateSlow = 0.5f;             // 3..10
        public float turnRateMedium = 1.3f;           // 10..40
        public float turnRateFast = 1.75f;            // >= 40
        public float turnRateReverse = -0.5f;         // speed < -5 and not on throttle
    }
}
