using System;
using HarborRacer.Inputs;
using HarborRacer.Items;
using HarborRacer.Track;
using UnityEngine;

namespace HarborRacer.Kart
{
    /// <summary>
    /// Arcade kart movement ported from the project's Player.cs (Move / Steer / GroundNormalRotation).
    /// The kart is driven by setting velocity = forward * CurrentSpeed every fixed step (vertical velocity preserved),
    /// plus a constant down acceleration for grip, and is rotated only by script (Rigidbody rotation is frozen).
    /// The same controller runs player and AI karts; the driver supplies an IKartInput.
    /// Optional sibling components hook in: KartBoost, KartDrift, KartTrick, KartGlider, KartAntiGravity,
    /// KartZoneSensor, KartRespawn.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class KartController : MonoBehaviour
    {
        public KartStats stats;
        public Transform groundProbe;
        public bool controlsLocked;            // countdown, respawn, finish autopilot

        [Header("Runtime (read-only)")]
        public float CurrentSpeed;             // commanded forward speed
        public float ForwardSpeed;             // measured forward speed
        public float MaxSpeedNow;
        public float AiSpeedScale = 1f;        // opponent rubber-banding / skill (normal top speed only)
        public float TurnStrength;             // rotate_strength (changes with throttle, drift hop, reverse)

        public KartState State { get; } = new KartState();
        public IKartInput Input { get; set; }
        public KartBoost Boost { get; private set; }
        public KartDrift Drift { get; private set; }
        public KartTrick Trick { get; private set; }
        public KartGlider Glider { get; private set; }
        public KartAntiGravity AntiGravity { get; private set; }
        public KartZoneSensor Zones { get; private set; }
        public KartRespawn Respawn { get; private set; }
        public KartHitReaction HitReaction { get; private set; }
        public ItemHolder Items { get; private set; }
        public CoinWallet Wallet { get; private set; }
        public Rigidbody Body => rb;
        public event Action<Collision> Collided;
        public event Action<float> WallHit;    // speed lost

        Rigidbody rb;
        SphereCollider sphere;
        int stepCount;

        float jumpUp, jumpDown, jumpVy;
        Vector3 jumpHeading;
        int jumpSteps;
        bool jumpAirborne;

        // wall response
        bool wallInContact;
        int lastWallHitStep = -100;
        Vector3 wallPush;
        int wallPushSteps;
        float pushDecay = 0.95f;

        /// <summary>
        /// A decaying sideways nudge added to the velocity each step.
        /// A new push replaces a weaker one.
        /// </summary>
        public void Push(Vector3 initialVelocityNudge, int steps, float decay = 0.95f)
        {
            if (wallPushSteps > 0 && wallPush.sqrMagnitude > initialVelocityNudge.sqrMagnitude) return;
            wallPush = initialVelocityNudge; wallPushSteps = steps; pushDecay = decay;
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            sphere = GetComponent<SphereCollider>();
            if (Input == null) Input = GetComponent<IKartInput>();
            if (stats == null) stats = ScriptableObject.CreateInstance<KartStats>();
            Boost = GetComponent<KartBoost>();
            Drift = GetComponent<KartDrift>();
            Trick = GetComponent<KartTrick>();
            Glider = GetComponent<KartGlider>();
            AntiGravity = GetComponent<KartAntiGravity>();
            Zones = GetComponent<KartZoneSensor>();
            Respawn = GetComponent<KartRespawn>();
            HitReaction = GetComponent<KartHitReaction>();
            Items = GetComponent<ItemHolder>();
            Wallet = GetComponent<CoinWallet>();
            TurnStrength = stats.turnStrength;
        }

        void FixedUpdate()
        {
            stepCount++;
            float dt = Time.fixedDeltaTime;

            Respawn?.Step(dt);
            if (Respawn != null && Respawn.Current == KartRespawn.Phase.Hover)
            {
                Input?.EndFixedStep();     // kinematic hover at the checkpoint: no driving
                return;
            }

            if (rb.isKinematic)                    // parked / scripted body (respawn hover, tooling): velocity cannot be set on it
            {
                Input?.EndFixedStep();
                return;
            }

            (Input as IDrivenInput)?.Tick(dt);     // AI / autopilot compute their controls from the current kart state

            bool throttle = false, brake = false, driftHeld = false, driftPressed = false, trickPressed = false, itemPressed = false, itemBackPressed = false;
            float steer = 0f, pitch = 0f;
            if (Input != null && !controlsLocked)
            {
                throttle = Input.Throttle > 0.1f;
                brake = Input.Brake > 0.1f;
                steer = Input.Steer;
                pitch = Input.Pitch;
                driftHeld = Input.DriftHeld;
                driftPressed = Input.DriftPressed;
                trickPressed = Input.TrickPressed;
                itemPressed = Input.ItemPressed;
                itemBackPressed = Input.ItemBackPressed;
            }
            Items?.Step(dt, itemPressed, itemBackPressed);

            Boost?.Tick(dt);                       // Boost_time countdown at the top of FixedUpdate
            ProbeGround();
            Quaternion rot = rb.rotation;
            ForwardSpeed = Vector3.Dot(rb.linearVelocity, rot * Vector3.forward);

            Zones?.Poll();
            HitReaction?.Step(dt);
            Glider?.Step(dt);
            Drift?.Step(dt, throttle, steer, driftHeld, driftPressed);
            Trick?.Step(trickPressed);
            MaxSpeedNow = ResolveMaxSpeed();

            if (rb.isKinematic)                    // a zone / hit above started a respawn this step: the body is parked now
            {
                Input?.EndFixedStep();
                return;
            }

            HandleWalls(rot);

            ApplyDownforce(rot);
            ApplyVelocity(rot);
            Glider?.AfterVelocity(rb, pitch);
            ApplyWallPush();
            UpdateSpeed(throttle, brake, dt);
            rot = ApplyJumpPanel(rot, dt);
            rot = ApplySteer(rot, steer, throttle);
            if (Glider != null) rot = Glider.AdjustRotation(rot, steer, pitch, rb, dt);
            Drift?.ApplyForces(rb, rot, steer);
            if (AntiGravity != null) rot = AntiGravity.AdjustRotation(rot, dt);
            if (!State.OnJumpPanel && !(Glider != null && Glider.SkipGroundAlign)) rot = AlignToGround(rot, dt);
            rb.rotation = rot;

            Input?.EndFixedStep();
        }

        // ---- jump panel: JUMP_PANEL block ----

        /// <summary>Entering a jump panel: stores the panel's up/down force and boosts for 2 s.</summary>
        public void BeginJumpPanel(float upForce, float downForce, float boostSeconds, float launchSpeed = 0f, Vector3 heading = default)
        {
            if (State.OnJumpPanel || State.Bullet) return;
            // the kart is turned to the panel direction; ignored when it came in the wrong way round
            Vector3 hFlat = Vector3.ProjectOnPlane(heading, Vector3.up);
            Vector3 kFlat = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            jumpHeading = hFlat.sqrMagnitude > 0.01f && Vector3.Dot(hFlat.normalized, kFlat.normalized) > 0.2f ? hFlat.normalized : Vector3.zero;
            State.OnJumpPanel = true;
            jumpUp = upForce; jumpDown = downForce; jumpSteps = 0; jumpAirborne = false; jumpVy = launchSpeed > 0f ? launchSpeed : stats.jumpPanelLaunchSpeed;
            Boost?.AddBoost(boostSeconds, BoostSource.JumpPanel, burst: true);
            Drift?.Cancel();
            if (CurrentSpeed < stats.boostSpeed * 0.9f)
                CurrentSpeed = Mathf.Max(CurrentSpeed, stats.maxSpeed, stats.boostSpeed * 0.9f);
        }

        Quaternion ApplyJumpPanel(Quaternion rot, float dt)
        {
            if (!State.OnJumpPanel) return rot;
            jumpSteps++;
            if (jumpHeading.sqrMagnitude > 0.01f)
            {
                Vector3 f = Vector3.ProjectOnPlane(rot * Vector3.forward, Vector3.up);
                float ang = Vector3.SignedAngle(f, jumpHeading, Vector3.up);
                rot = Quaternion.AngleAxis(Mathf.Clamp(ang, -150f * dt, 150f * dt), Vector3.up) * rot;
            }
            // rb.velocity = forward * currentspeed (vertical velocity is NOT preserved on a panel)
            // our arc: explicit launch speed decaying under a soft gravity
            jumpVy -= stats.jumpPanelGravity * dt;
            Vector3 flat = Vector3.ProjectOnPlane(rot * Vector3.forward, Vector3.up).normalized;
            rb.linearVelocity = flat * CurrentSpeed + Vector3.up * jumpVy;

            // ends the state on the first Ground contact after the panel (the panel itself is not "Ground").
            // Our ramp is part of the road mesh, so require the kart to have left the surface first.
            if (!State.GroundHit || State.GroundDistance > stats.jumpPanelAirborneDistance) jumpAirborne = true;
            bool landed = jumpAirborne && jumpSteps > stats.jumpPanelMinSteps && State.GroundHit
                          && State.GroundDistance <= stats.jumpPanelEndGroundDistance;
            if (landed || jumpSteps > stats.jumpPanelMaxSteps)
                State.OnJumpPanel = false;
            return rot;
        }

        // ---- walls ----

        /// <summary>
        /// Callback-free wall handling: a sphere-cast ahead on the Wall layer. First contact applies the's fence
        /// response (lose 10 speed above 50, small outward nudge); a head-on pin pulls the commanded speed down to reality.
        /// Glancing scrapes keep their speed.
        /// </summary>
        void HandleWalls(Quaternion rot)
        {
            slideNormalValid = false;
            bool wall = WallAhead(rot, out var hit);
            Vector3 fwd = rot * Vector3.forward;
            // A sphere-cast that starts in contact reports a made-up normal (the reverse cast direction), so take the real surface
            // normal from thin rays: along the heading first, then to both sides (a scrape: the wall is beside the kart).
            Vector3 normal = hit.normal;
            bool headOn = false, trusted = false;
            if (wall)
            {
                Vector3 c = transform.TransformPoint(sphere.center);
                float reach = sphere.radius * transform.lossyScale.x + 0.6f;
                if (hit.distance > 0.001f) trusted = true;
                else if (Physics.Raycast(c, fwd, out var ray, reach, 1 << Layers.Wall, QueryTriggerInteraction.Ignore)) { normal = ray.normal; trusted = true; }
                else
                {
                    Vector3 side = rot * Vector3.right;
                    float best = float.MaxValue;
                    foreach (var d in new[] { side, -side, (fwd + side).normalized, (fwd - side).normalized })
                        if (Physics.Raycast(c, d, out var r2, reach, 1 << Layers.Wall, QueryTriggerInteraction.Ignore) && r2.distance < best && Vector3.Dot(r2.normal, fwd) < 0.2f)
                        { best = r2.distance; normal = r2.normal; trusted = true; }
                }
                if (trusted)
                {
                    headOn = Vector3.Dot(normal, fwd) < -0.6f;
                    slideNormal = State.AntiGravity ? normal : Vector3.ProjectOnPlane(normal, Vector3.up);
                    slideNormalValid = slideNormal.sqrMagnitude > 0.0001f;
                    if (slideNormalValid) slideNormal.Normalize();
                }
            }
            // untrusted normal (already touching, nothing found): leave the fence bounce to OnCollisionEnter, which has the true contact normal
            if (wall && trusted && !wallInContact) WallBounce(normal, hit.collider);
            wallInContact = wall;

            // only a near head-on pin drags the commanded speed down; a glancing scrape keeps its speed (see SlideAlongWall)
            if (headOn && CurrentSpeed > ForwardSpeed + 5f)
                CurrentSpeed = Mathf.Lerp(CurrentSpeed, Mathf.Max(ForwardSpeed, 0f), 0.5f);
        }

        Vector3 slideNormal;
        bool slideNormalValid;

        /// <summary>Remove only the into-wall part of the intended velocity so a glancing scrape slides along the wall at nearly full speed
        /// instead of the solver / continuous detection eating about a third of it. A head-on hit still stops the kart.</summary>
        Vector3 SlideAlongWall(Vector3 v)
        {
            if (!slideNormalValid) return v;
            float into = Vector3.Dot(v, slideNormal);
            return into < 0f ? v - slideNormal * into : v;
        }

        /// <summary>: the bounce and the speed loss exist only for colliders tagged "fence" (ZoneKind.Fence); ordinary walls are plain physics.</summary>
        static bool IsFence(Collider col)
        {
            if (col == null) return false;
            var zm = col.GetComponentInParent<ZoneMarker>();
            return zm != null && zm.kind == ZoneKind.Fence;
        }

        void WallBounce(Vector3 normal, Collider col)
        {
            if (!IsFence(col)) return;
            if (stepCount - lastWallHitStep < stats.wallCooldownSteps) return;
            lastWallHitStep = stepCount;
            float lost = 0f;
            if (CurrentSpeed > stats.wallSpeedLossMinSpeed && !State.Bullet) { lost = stats.wallSpeedLoss; CurrentSpeed -= lost; }
            Vector3 n = State.AntiGravity ? normal : Vector3.ProjectOnPlane(normal, Vector3.up);
            if (n.sqrMagnitude > 0.0001f) Push(n.normalized * stats.wallBounceSpeed, stats.wallBounceSteps, stats.wallBounceDecay);
            WallHit?.Invoke(lost);
        }

        void ApplyWallPush()
        {
            if (wallPushSteps <= 0) return;
            rb.linearVelocity += wallPush;
            wallPush *= pushDecay;
            wallPushSteps--;
        }

        bool WallAhead(Quaternion rot, out RaycastHit hit)
        {
            hit = default;
            if (sphere == null || CurrentSpeed <= 0f) return false;
            Vector3 c = transform.TransformPoint(sphere.center);
            float r = sphere.radius * transform.lossyScale.x;
            if (Physics.SphereCast(c, r * 0.95f, rot * Vector3.forward, out hit, 0.6f, 1 << Layers.Wall, QueryTriggerInteraction.Ignore)) return true;
            // Much of the track's scenery (handrails, building fronts, curbs) is on the Drivable layer. Without this the kart stopped dead against it with
            // the commanded speed still at 80+ ("speed cmd 82 / real 0"). Thin rays tell a vertical face from the ground and from ramps.
            Vector3 f = rot * Vector3.forward, up = rot * Vector3.up;
            float reach = r + 0.6f;
            foreach (float ang in DrivableWallRayAngles)
            {
                Vector3 dir = Quaternion.AngleAxis(ang, up) * f;
                if (Physics.Raycast(c, dir, out var rh, reach, 1 << Layers.Drivable, QueryTriggerInteraction.Ignore) && rh.normal.y < 0.5f
                    && rh.collider.GetComponentInParent<KartController>() == null) { hit = rh; return true; }
            }
            return false;
        }

        static readonly float[] DrivableWallRayAngles = { 0f, -20f, 20f };

        // ---- ground ----

        void ProbeGround()
        {
            var origin = groundProbe != null ? groundProbe.position : transform.position;
            float len = Glider != null ? Glider.RayLength(stats.groundRayLength) : stats.groundRayLength;
            bool hit = Physics.Raycast(origin, -transform.up, out var h, len, Layers.GroundMask, QueryTriggerInteraction.Ignore);
            State.GroundHit = hit;
            State.GroundDistance = hit ? h.distance : float.MaxValue;
            State.GroundNormal = hit ? h.normal : Vector3.up;
            State.OnDirt = hit && h.collider.gameObject.layer == Layers.OffRoad;
            State.Grounded = hit && h.distance <= stats.groundedRayDistance;
        }

        void OnCollisionEnter(Collision c)
        {
            if (c.gameObject.layer == Layers.Wall && c.contactCount > 0) WallBounce(c.GetContact(0).normal, c.collider);
            Collided?.Invoke(c);
        }

        // ---- speed ----

        /// <summary>
        /// Max speed for this step. Priority: boost > drift penalty > off-road > normal.
        /// </summary>
        protected virtual float ResolveMaxSpeed()
        {
            if (State.Bullet && Items != null) return Items.bulletSpeed;
            if ((Boost != null && Boost.Boosting) || State.Star) return stats.boostSpeed;
            float max = State.OnDirt ? stats.offRoadMaxSpeed : stats.maxSpeed;
            if (Drift != null) max = Mathf.Min(max, stats.maxSpeed - Drift.MaxSpeedPenalty);
            max *= AiSpeedScale;
            if (Wallet != null) max += Wallet.Count;     // +1 u/s per coin
            return max;
        }

        void ApplyDownforce(Quaternion rot)
        {
            // not while gliding / on a jump panel / inside a CancelDownForce volume
            if (State.Gliding || State.OnJumpPanel || State.CancelDownForce) return;
            float down = stats.downAcceleration;
            if (Respawn != null && Respawn.Current == KartRespawn.Phase.Falling) down *= 2f;   // FellInWater extra pull
            if (State.AntiGravity)
            {
                // AddRelativeForce(down * 5000 * dt) + AddRelativeForce(Physics.gravity * 5): gravity follows the kart's up
                rb.AddForce(rot * Vector3.down * (down + stats.antiGravityExtraDown), ForceMode.Acceleration);
            }
            else rb.AddForce(Vector3.down * down, ForceMode.Acceleration);
        }

        void ApplyVelocity(Quaternion rot)
        {
            if (State.AntiGravity)
            {
                rb.linearVelocity = SlideAlongWall((rot * Vector3.forward) * CurrentSpeed);
            }
            else if (State.Grounded)
            {
                // Driving on ground/ramp: velocity follows the terrain slope
                rb.linearVelocity = SlideAlongWall((rot * Vector3.forward) * CurrentSpeed);
            }
            else
            {
                // Airborne: forward movement on horizontal plane, vertical velocity preserved from physics/impulses
                Vector3 flat = Vector3.ProjectOnPlane(rot * Vector3.forward, Vector3.up).normalized;
                rb.linearVelocity = SlideAlongWall(flat * CurrentSpeed) + Vector3.up * rb.linearVelocity.y;
            }
        }

        void UpdateSpeed(bool throttle, bool brake, float dt)
        {
            bool boosting = Boost != null && Boost.Boosting;
            bool drifting = Drift != null && Drift.IsDrifting;

            // while gliding the speed eases toward max (throttle) or the 65 cruise speed (no throttle)
            if (State.Gliding)
                CurrentSpeed = Mathf.Lerp(CurrentSpeed, throttle ? MaxSpeedNow : stats.glideCruiseSpeed, stats.glideSpeedLerpPerSecond * dt);

            if (throttle)
            {
                CurrentSpeed = Mathf.Lerp(CurrentSpeed, MaxSpeedNow, stats.accelPerSecond * dt);
                if (!drifting && !State.Gliding) TurnStrength = stats.turnStrength;   // rotate_strength = desired unless drifting
            }
            if (brake && !State.Gliding)
            {
                CurrentSpeed = Mathf.Lerp(CurrentSpeed, -MaxSpeedNow / stats.reverseSpeedDivisor, stats.reversePerStep);
                if (ForwardSpeed <= 0f) TurnStrength = stats.reverseTurnStrength;
            }
            if (!throttle)
                CurrentSpeed = Mathf.Lerp(CurrentSpeed, 0f, stats.coastPerStep);

            if (State.OnDirt && !boosting && throttle)
                CurrentSpeed = Mathf.Lerp(CurrentSpeed, MaxSpeedNow, stats.offRoadAccelPerSecond * dt);

            // Boost pushes speed toward boostSpeed even without throttle.
            if (boosting)
                CurrentSpeed = Mathf.Lerp(CurrentSpeed, stats.boostSpeed, stats.boostAccelPerSecond * dt);

            // while hit by a banana/shell the speed lerps to 0 (4.5/s) after all the other speed logic
            if (State.Stunned)
                CurrentSpeed = Mathf.Lerp(CurrentSpeed, 0f, stats.stunSpeedLerpPerSecond * dt);

            State.Reversing = brake && !throttle && ForwardSpeed < 0f && !State.Gliding;
        }

        // ---- steering / orientation ----

        Quaternion ApplySteer(Quaternion rot, float steer, bool throttle)
        {
            if (Mathf.Abs(steer) <= stats.steerDeadzone) return rot;

            float direction, rate;
            if (Drift != null && Drift.IsDrifting)
            {
                direction = Drift.TurnDirection(steer);     // +-2.1 inward / +-0.5 outward
                rate = stats.driftTurnRate;
            }
            else
            {
                direction = steer;
                if (ForwardSpeed >= 40f) rate = stats.turnRateFast;
                else if (ForwardSpeed > 10f) rate = stats.turnRateMedium;
                else if (ForwardSpeed > 3f) rate = stats.turnRateSlow;
                else rate = stats.turnRateStationary;
                if (ForwardSpeed < -5f && !throttle) rate = stats.turnRateReverse;
            }

            float degrees = (TurnStrength + (State.Star ? 15f : 0f)) * direction * rate * stats.turnDegreesPerStepScale;
            return rot * Quaternion.Euler(0f, degrees, 0f);
        }

        Quaternion AlignToGround(Quaternion rot, float dt)
        {
            // only on surfaces with normal.y > 0.5, except in anti-gravity where any slope is accepted
            if (!State.GroundHit || (State.GroundNormal.y <= stats.groundNormalMinY && !State.AntiGravity)) return rot;
            Vector3 up = rot * Vector3.up;
            Quaternion target = Quaternion.FromToRotation(up, State.GroundNormal) * rot;
            return Quaternion.Lerp(rot, target, stats.groundAlignPerSecond * dt);
        }
    }
}
