using HarborRacer.Kart;
using HarborRacer.Race;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace HarborRacer.CameraRig
{
    /// <summary>
    /// Full race camera. Hierarchy: rig (follows the kart) -> Arm (local offset, boost pull) ->
    /// ShakePivot -> Camera. Modes: normal chase, boost pull-in with FOV kick, anti-gravity (rig along the kart's up, arm rolls with
    /// the zone), gliding (slow level-out), trick pending (level horizon), jump panel (lazy), Bullet Bill (pulled back), respawn
    /// (frozen while falling), intro fly-in, finish orbit, first-person (C) and look-back (hold B). A spherecast pulls the camera in
    /// when geometry gets between it and the kart.
    /// </summary>
    public class KartCameraRig : MonoBehaviour
    {
        public Transform target;

        [Header("Ref values")]
        public float heightOffset = 1.8f;
        public Vector3 normalPos = new Vector3(0f, 0.7f, -6.75f);
        public float normalPitch = 2f;
        public Vector3 boostPos = new Vector3(0f, 1f, -8f);                 // scene values of the Camerafollow
        public Vector3 bulletPos = new Vector3(0f, 1.5f, -5.5f);
        public float antiGravityPosY = 0.4f;
        public float normalFov = 60f, boostFov = 60f, raceEndFov = 60f;      // the never changes the FOV while boosting or at the finish
        public float rotateLerpPerSecond = 3f;
        public float antiGravityGraceSeconds = 3f;

        [Header("Collision")]
        public float collisionRadius = 0.35f;
        public float minDistance = 1.5f;

        public CameraShake Shake { get; } = new CameraShake();
        public bool FirstPerson { get; private set; }
        public bool LookBack { get; private set; }
        public Camera Cam => cam;

        Transform arm, pivot;
        Camera cam;
        KartController kart;
        KartRespawn respawn;
        RacerProgress racer;
        float antiGravTimer, finishTime, introTime, introBlend, fov;
        Vector3 armPos;
        Quaternion armRot = Quaternion.identity;
        bool wasGrounded = true;
        float airTime;
        int mask;

        void Awake()
        {
            mask = (1 << Layers.Wall) | (1 << Layers.Drivable) | (1 << Layers.Scenery);
        }

        void Start()
        {
            arm = new GameObject("Arm").transform; arm.SetParent(transform, false);
            pivot = new GameObject("ShakePivot").transform; pivot.SetParent(arm, false);
            cam = GetComponentInChildren<Camera>();
            if (cam == null)
            {
                var go = new GameObject("Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.transform.SetParent(pivot, false);
            cam.nearClipPlane = 0.3f; cam.farClipPlane = 2500f;
            var urp = cam.GetUniversalAdditionalCameraData(); urp.renderPostProcessing = true; urp.antialiasing = AntialiasingMode.None;                // MSAA from the pipeline asset already smooths edges
            fov = normalFov; cam.fieldOfView = fov;
            armPos = normalPos; armRot = Quaternion.Euler(normalPitch, 0f, 0f);
            Bind();
        }

        void Bind()
        {
            if (target == null) return;
            kart = target.GetComponent<KartController>();
            respawn = target.GetComponent<KartRespawn>();
            racer = target.GetComponent<RacerProgress>();
            Snap();
            if (respawn != null) respawn.Teleported += Snap;
            if (kart != null)
            {
                if (kart.HitReaction != null) kart.HitReaction.Hit += OnHit;
                if (kart.Boost != null) kart.Boost.BoostGranted += OnBoost;
                kart.WallHit += OnWall;
            }
        }

        void OnDestroy()
        {
            if (respawn != null) respawn.Teleported -= Snap;
            if (kart != null)
            {
                if (kart.HitReaction != null) kart.HitReaction.Hit -= OnHit;
                if (kart.Boost != null) kart.Boost.BoostGranted -= OnBoost;
                kart.WallHit -= OnWall;
            }
        }

        void OnHit(HitType t)
        {
            if (t == HitType.Explosion) Shake.Add(CameraShake.Explosion);
            else if (t != HitType.Bump) Shake.Add(CameraShake.Hit);
        }
        void OnBoost(BoostSource s, float seconds, bool burst) { if (burst) Shake.Add(CameraShake.BoostBurst); }
        void OnWall(float lost) => Shake.Add(CameraShake.WallBump);

        void Snap()
        {
            if (target == null) return;
            transform.position = target.position + Vector3.up * heightOffset;
            transform.rotation = target.rotation;
        }

        void LateUpdate()
        {
            if (target == null || cam == null) return;
            float dt = Time.deltaTime;
            if (kart == null) { kart = target.GetComponent<KartController>(); if (kart == null) return; }

            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool toggleView = (kb != null && kb.cKey.wasPressedThisFrame) || (pad != null && pad.rightStickButton.wasPressedThisFrame);
            if (toggleView) FirstPerson = !FirstPerson;
            LookBack = (kb != null && kb.bKey.isPressed) || (pad != null && pad.buttonEast.isPressed);

            var rm = RaceManager.Instance;
            bool intro = rm != null && rm.State == RaceState.Intro;
            bool finished = racer != null && racer.Finished;
            if (intro) { introTime += dt; introBlend = 1f; }
            else if (introBlend > 0f) introBlend = Mathf.MoveTowards(introBlend, 0f, dt / 0.5f);

            bool falling = respawn != null && respawn.Current == KartRespawn.Phase.Falling;
            if (falling) return;                                    // the camera holds still while the kart falls

            var s = kart.State;
            if (s.AntiGravity) antiGravTimer = antiGravityGraceSeconds; else if (antiGravTimer > 0f) antiGravTimer -= dt;
            bool antiGrav = antiGravTimer > 0f;
            bool gliding = s.Gliding;
            bool boosting = (kart.Boost != null && kart.Boost.Boosting) || s.TrickPending;

            // landing shake
            if (s.GroundHit && s.GroundDistance < 1.2f) { if (!wasGrounded && airTime > 0.35f) Shake.Add(CameraShake.Landing); wasGrounded = true; airTime = 0f; }
            else { wasGrounded = false; airTime += dt; }

            // ---- rig position / rotation ----
            Vector3 up = (gliding || antiGrav) ? target.up : Vector3.up;
            transform.position = target.position + up * heightOffset;
            if (gliding && kart.Glider != null)
                Slerp(Quaternion.Euler(kart.Glider.AngleX, target.eulerAngles.y, kart.Glider.AngleZ), 1f, dt);
            else if (s.TrickPending && !antiGrav)
                Slerp(Quaternion.Euler(0f, target.eulerAngles.y, 0f), 3f, dt);
            else if (s.OnJumpPanel)
                Slerp(target.rotation, 0.4f, dt);
            else
                Slerp(target.rotation, rotateLerpPerSecond, dt);

            // ---- arm target ----
            Vector3 wantPos = normalPos; Quaternion wantRot = Quaternion.Euler(normalPitch, 0f, 0f); float rate = 4f, wantFov = normalFov;
            if (s.Bullet) { wantPos = bulletPos; rate = 6f; }
            else if (boosting) { wantPos = boostPos; rate = 4f; wantFov = boostFov; }
            if (antiGrav && kart.AntiGravity != null && kart.AntiGravity.RotateCamera)
                wantRot = Quaternion.Euler(normalPitch + kart.AntiGravity.CameraRotX, 0f, kart.AntiGravity.CameraRotZ);
            if (antiGrav && kart.AntiGravity != null && kart.AntiGravity.RotateCamera) wantPos.y = antiGravityPosY;
            if (finished) { wantPos = normalPos; wantFov = raceEndFov; rate = 3f; }
            armPos = Vector3.Lerp(armPos, wantPos, rate * dt);
            armRot = Quaternion.Slerp(armRot, wantRot, 3f * dt);
            fov = Mathf.Lerp(fov, wantFov, 4f * dt);
            cam.fieldOfView = fov;

            arm.localPosition = armPos;
            arm.localRotation = armRot;

            Vector3 camWorld; Quaternion camRot;
            if (FirstPerson && !intro && !finished)
            {
                camWorld = target.position + target.up * 1.15f + target.forward * 0.25f;
                camRot = target.rotation;
            }
            else if (LookBack && !intro && !finished)
            {
                camWorld = target.position + target.up * 1.5f + target.forward * 6f;
                camRot = target.rotation * Quaternion.Euler(4f, 180f, 0f);
            }
            else
            {
                // pull the camera in when level geometry sits between the rig and the camera
                Vector3 from = transform.position;
                Vector3 want = arm.position;
                Vector3 dir = want - from; float dist = dir.magnitude;
                if (dist > 0.01f && Physics.SphereCast(from, collisionRadius, dir / dist, out var hit, dist, mask, QueryTriggerInteraction.Ignore))
                {
                    float d = Mathf.Max(minDistance, hit.distance - 0.1f);
                    arm.position = from + dir / dist * Mathf.Min(d, dist);
                }
                camWorld = arm.position; camRot = arm.rotation;

                if (intro || introBlend > 0f) IntroPose(rm, intro, ref camWorld, ref camRot);
                if (finished) FinishPose(dt, ref camWorld, ref camRot);
            }

            Shake.Tick(dt);
            pivot.position = camWorld; pivot.rotation = camRot * Quaternion.Euler(0f, 0f, Shake.Roll);
            pivot.position += pivot.TransformDirection(Shake.Offset);
            cam.transform.localPosition = Vector3.zero; cam.transform.localRotation = Quaternion.identity;
        }

        void Slerp(Quaternion want, float perSecond, float dt) => transform.rotation = Quaternion.Slerp(transform.rotation, want, perSecond * dt);

        /// <summary>Intro: sweeps from in front of the kart round to the chase position, then blends into the normal camera.</summary>
        void IntroPose(RaceManager rm, bool intro, ref Vector3 pos, ref Quaternion rot)
        {
            float len = rm != null ? Mathf.Max(0.5f, rm.introSeconds) : 3f;
            float t = intro ? Mathf.Clamp01(introTime / len) : 1f;
            float ease = t * t * (3f - 2f * t);
            float angle = Mathf.Lerp(150f, 0f, ease);
            Vector3 center = target.position + Vector3.up * 1.2f;
            Vector3 offset = Quaternion.AngleAxis(angle, Vector3.up) * (-target.forward * Mathf.Lerp(11f, 7.5f, ease)) + Vector3.up * Mathf.Lerp(3.5f, 2.2f, ease);
            Vector3 introPos = center + offset;
            Quaternion introRot = Quaternion.LookRotation(center - introPos, Vector3.up);
            float w = intro ? 1f : introBlend;
            pos = Vector3.Lerp(pos, introPos, w);
            rot = Quaternion.Slerp(rot, introRot, w);
        }

        /// <summary>Finish: swing to the front of the kart and orbit slowly.</summary>
        void FinishPose(float dt, ref Vector3 pos, ref Quaternion rot)
        {
            finishTime += dt;
            float swing = Mathf.Clamp01(finishTime / 1.5f);
            float angle = Mathf.Lerp(180f, 150f, swing) + finishTime * 12f;
            Vector3 center = target.position + Vector3.up * 1f;
            Vector3 offset = Quaternion.AngleAxis(angle, Vector3.up) * (-target.forward * 7.5f) + Vector3.up * 2.4f;
            Vector3 p = center + offset;
            pos = Vector3.Lerp(pos, p, swing);
            rot = Quaternion.Slerp(rot, Quaternion.LookRotation(center - p, Vector3.up), swing);
        }
    }
}
