using System;
using UnityEngine;

namespace HarborRacer.Kart
{
    /// <summary>
    /// flag: set while inside an AntiGravity (enter) volume, cleared inside an AntiGravityFalse (exit) volume,
    /// and unchanged elsewhere, so the whole section keeps the state. While set the kart's gravity is relative to the kart,
    /// ground alignment accepts any slope, and the velocity is not clamped to the world vertical (see KartController).
    /// </summary>
    public class KartAntiGravity : MonoBehaviour
    {
        public KartController kart;

        public bool Active => kart.State.AntiGravity;
        /// <summary>Camera hints from the last enter zone.</summary>
        public bool RotateCamera { get; private set; }
        public float CameraRotX { get; private set; }
        public float CameraRotZ { get; private set; }
        public bool CanUseBullet { get; private set; } = true;

        public event Action Exited;

        float levelTimer;

        float bumpTimer = -1f;

        void Awake() { if (kart == null) kart = GetComponent<KartController>(); }

        void OnEnable() { if (kart == null) kart = GetComponent<KartController>(); if (kart != null) kart.Collided += OnCollided; }
        void OnDisable() { if (kart != null) kart.Collided -= OnCollided; bumpTimer = -1f; }

        /// <summary> (kart bump while antiGravity): 0.3 s later, if boost is under 1 s, grant a 1.25 s boost.</summary>
        void OnCollided(Collision c)
        {
            if (!kart.State.AntiGravity || bumpTimer >= 0f) return;
            var other = c.collider.GetComponentInParent<KartController>();
            if (other != null && other != kart) bumpTimer = kart.stats.antiGravityBumpDelay;
        }

        void FixedUpdate()
        {
            if (bumpTimer < 0f) return;
            bumpTimer -= Time.fixedDeltaTime;
            if (bumpTimer > 0f) return;
            bumpTimer = -1f;
            if (kart.State.AntiGravity && kart.Boost != null && kart.Boost.Remaining < 1f)
                kart.Boost.AddBoost(kart.stats.antiGravityBumpBoost, BoostSource.AntiGravity);
        }

        public void Enter(bool rotateCam, float rotX, float rotZ, bool canUseBullet)
        {
            RotateCamera = rotateCam; CameraRotX = rotX; CameraRotZ = rotZ; CanUseBullet = canUseBullet;
            if (!kart.State.AntiGravity)
            {
                kart.State.AntiGravity = true;
                levelTimer = 0f;
            }
        }

        public void Exit(bool levelOut)
        {
            if (kart.State.AntiGravity)
            {
                kart.State.AntiGravity = false;
                RotateCamera = false; CameraRotX = 2f; CameraRotZ = 0f; CanUseBullet = true;
                Exited?.Invoke();
            }
            // Ease the kart upright when the exit is flagged or when it would otherwise be stuck
            // tilted: airborne, or on a surface too steep for the normal ground alignment.
            bool cannotAlign = !kart.State.Grounded || kart.State.GroundNormal.y < kart.stats.groundNormalMinY;
            if (levelOut || cannotAlign) levelTimer = kart.stats.antiGravityExitLevelSeconds;
        }

        /// <summary>Hit, respawn: drop the state without the exit animation logic.</summary>
        public void Cancel()
        {
            if (kart.State.AntiGravity) { kart.State.AntiGravity = false; Exited?.Invoke(); }
            levelTimer = 0f;
        }

        /// <summary>After leaving a section where the road is still tilted, ease the kart back upright.</summary>
        public Quaternion AdjustRotation(Quaternion rot, float dt)
        {
            if (levelTimer <= 0f) return rot;
            levelTimer -= dt;
            Vector3 fwd = Vector3.ProjectOnPlane(rot * Vector3.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.001f) return rot;
            Quaternion upright = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            return Quaternion.Slerp(rot, upright, kart.stats.antiGravityLevelLerp * dt);
        }
    }
}
