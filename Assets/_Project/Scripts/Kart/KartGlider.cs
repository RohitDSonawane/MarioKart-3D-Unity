using System;
using UnityEngine;

namespace HarborRacer.Kart
{
    /// <summary>
    /// Glider ported from the Player (OnTriggerExit GliderPanel / GliderPanelFly, gliderMovements, Move glide block).
    /// A launch zone starts the glide (a "glider trick" when fast), the ramp exit gives a boost and a lift, and the kart then
    /// banks and pitches with the input until it lands. Timers run in fixed steps.
    /// </summary>
    public class KartGlider : MonoBehaviour
    {
        public KartController kart;
        public Transform gliderVisual;               // wings; scaled 0 (closed) -> 1 (open)
        public float visualOpenSpeed = 5f;

        public bool TrickGlide { get; private set; }
        public bool Open { get; private set; }
        public float GlidingTime { get; private set; }
        public float AngleX => angleX;
        public float AngleZ => angleZ;

        public event Action Opened;
        public event Action Closed;
        public event Action<int> GlideTrick;

        float angleX, angleZ;
        float startT;
        bool armed, trickBoosted;
        int trickIdx;
        float smoothSteer, smoothVel;
        float landAlignTimer;
        float liftForce; int liftSteps;
        Vector3 visualBaseScale = Vector3.one;
        float visualBlend;

        void Awake()
        {
            if (kart == null) kart = GetComponent<KartController>();
            if (gliderVisual != null) { visualBaseScale = gliderVisual.localScale; gliderVisual.localScale = Vector3.zero; }
        }

        /// <summary>Length of the ground probe ray.</summary>
        public float RayLength(float normalLength)
        {
            if (kart.State.Gliding) return kart.stats.glideGroundRayLength;
            if (landAlignTimer > 0f) return kart.stats.glideLandRayLength;
            return normalLength;
        }

        /// <summary>Ground alignment is skipped after 3 s of gliding.</summary>
        public bool SkipGroundAlign => kart.State.Gliding && GlidingTime > kart.stats.glideNoAlignAfter;

        // ---- zone entry points ----

        /// <summary>Leaving a GliderPanelFly trigger: set the glide angles and open the glider.</summary>
        public void StartGlide(float angleZDeg, float angleXDeg)
        {
            angleZ = angleZDeg; angleX = angleXDeg;
            var s = kart.stats; var st = kart.State;
            if (st.Gliding) return;

            kart.Drift?.Cancel();
            st.Gliding = true;
            startT = 0f; GlidingTime = 0f; armed = false; trickBoosted = false; smoothSteer = 0f; smoothVel = 0f;
            if (kart.CurrentSpeed <= s.glideTrickMinSpeed)
            {
                TrickGlide = false; SetOpen(true);
            }
            else
            {
                TrickGlide = true; SetOpen(false);
                trickIdx = UnityEngine.Random.Range(0, 2);
            }
        }

        /// <summary>Leaving a GliderPanel ramp: boost, and a decaying upward lift if already gliding.</summary>
        public void OnRampExit()
        {
            var s = kart.stats;
            kart.Boost?.AddBoost(s.glideRampBoost, BoostSource.Glider, burst: true);
            if (kart.State.Gliding) { liftForce = s.glideLiftStart; liftSteps = s.glideLiftSteps; }
        }

        // ---- per fixed step ----

        float stallT;

        public void Step(float dt)
        {
            var s = kart.stats; var st = kart.State;
            if (landAlignTimer > 0f) landAlignTimer -= dt;
            if (!st.Gliding) return;

            startT += dt; GlidingTime += dt;
            if (TrickGlide)
            {
                if (!Open && startT >= s.glideTrickOpenDelay) SetOpen(true);
                if (!trickBoosted && startT >= s.glideTrickBoostDelay)
                {
                    trickBoosted = true;
                    kart.Boost?.AddBoost(s.glideTrickBoost, BoostSource.GliderTrick, burst: true);
                    GlideTrick?.Invoke(trickIdx);
                }
            }
            float armAt = TrickGlide ? s.glideTrickArmDelay : s.glideArmDelay;
            if (!armed && startT >= armAt) armed = true;

            // pinned against an obstacle: nothing is moving, so end the glide instead of waiting for the fail-safe
            stallT = armed && kart.ForwardSpeed < 3f ? stallT + dt : 0f;

            // landing + fail-safe
            if ((armed && st.Grounded) || startT > s.glideMaxSeconds || st.OnJumpPanel || stallT > 1f) Close();
        }

        /// <summary>Applied right after the normal velocity assignment: vertical damping, lift impulse.</summary>
        public void AfterVelocity(Rigidbody rb, float pitchInput)
        {
            if (!kart.State.Gliding) return;
            var s = kart.stats;
            Vector3 v = rb.linearVelocity;
            if (liftSteps > 0)
            {
                v += Vector3.up * (liftForce * s.glideLiftToVelocity);
                if (liftForce >= s.glideLiftStep) liftForce -= s.glideLiftStep;
                liftSteps--;
            }
            v.y *= pitchInput > 0.5f ? s.glideVerticalDampClimb : s.glideVerticalDamp;
            rb.linearVelocity = v;
        }

        /// <summary>: pitch with the stick, bank with the steering. Returns the new rotation.</summary>
        public Quaternion AdjustRotation(Quaternion rot, float steer, float pitchInput, Rigidbody rb, float dt)
        {
            if (!kart.State.Gliding) return rot;
            var s = kart.stats;

            smoothSteer = Mathf.SmoothDamp(smoothSteer, -steer, ref smoothVel, s.glideBankSmoothTime, Mathf.Infinity, dt);
            kart.TurnStrength = s.glideTurnStrength;

            Vector3 e = rot.eulerAngles;
            float targetX = angleX;
            if (pitchInput < -0.5f)                                 // "up" = dive
            {
                targetX = s.glideDiveAngle + angleX;
                rb.AddForce(Vector3.down * s.glideDiveDownAccel, ForceMode.Acceleration);
            }
            else if (pitchInput > 0.5f)                             // "down" = climb
            {
                float cur = e.x > 180f ? e.x - 360f : e.x;
                targetX = cur > s.glideClimbMaxAngle + angleX ? s.glideClimbAngle + angleX : cur;
                if (GlidingTime < s.glideClimbMaxTime)
                    rb.AddForce(rot * Vector3.up * s.glideClimbUpAccel, ForceMode.Acceleration);
            }
            rot = Quaternion.SlerpUnclamped(rot, Quaternion.Euler(targetX, e.y, e.z), s.glidePitchLerp * dt);

            Vector3 e2 = rot.eulerAngles;
            Quaternion bank = Quaternion.Euler(e2.x, e2.y, angleZ + s.glideBankAngle * smoothSteer);
            return Quaternion.SlerpUnclamped(rot, bank, s.glideBankLerp * dt);
        }

        void Close()
        {
            kart.State.Gliding = false;
            TrickGlide = false; liftSteps = 0;
            landAlignTimer = kart.stats.glideLandAlignSeconds;
            SetOpen(false);
        }

        /// <summary>Hit, respawn, race end.</summary>
        public void Cancel()
        {
            if (kart.State.Gliding) Close();
        }

        void SetOpen(bool open)
        {
            if (Open == open) return;
            Open = open;
            if (open) Opened?.Invoke(); else Closed?.Invoke();
        }

        // ---- visuals ----

        void LateUpdate()
        {
            if (gliderVisual == null) return;
            visualBlend = Mathf.MoveTowards(visualBlend, Open ? 1f : 0f, visualOpenSpeed * Time.deltaTime);
            gliderVisual.localScale = visualBaseScale * visualBlend;
            if (gliderVisual.gameObject.activeSelf != visualBlend > 0.01f) gliderVisual.gameObject.SetActive(visualBlend > 0.01f);
        }
    }
}
