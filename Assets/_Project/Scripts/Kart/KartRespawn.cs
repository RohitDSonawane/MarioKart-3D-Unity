using System;
using System.Collections.Generic;
using HarborRacer.Track;
using UnityEngine;

namespace HarborRacer.Kart
{
    public enum RespawnReason { OutOfBounds, Water }

    /// <summary>
    /// behaviour: water waits 0.5 s then teleports; out-of-bounds teleports immediately; the kart is
    /// kinematic for 0.5 s at the checkpoint, then control returns. Target = nearest checkpoint marker to the last safe
    /// ground position (Phase 6 replaces this with the last checkpoint actually passed).
    /// </summary>
    public class KartRespawn : MonoBehaviour
    {
        public enum Phase { None, Falling, Hover }

        public KartController kart;
        public float aiRespawnSpeed = 60f;
        public Phase Current { get; private set; }
        public bool Respawning => Current != Phase.None;
        public event Action Teleported;

        Vector3 lastSafePos;
        Quaternion lastSafeRot;
        bool haveSafe;
        int safeCounter;
        float timer;
        bool lockedByUs;
        static List<Transform> checkpoints;

        void Awake()
        {
            if (kart == null) kart = GetComponent<KartController>();
            lastSafePos = transform.position; lastSafeRot = transform.rotation;
        }

        public void Begin(RespawnReason reason)
        {
            if (Respawning) return;
            kart.Boost?.Cancel();
            kart.Drift?.Cancel();
            kart.Trick?.Cancel();
            kart.Glider?.Cancel();
            kart.AntiGravity?.Cancel();
            kart.State.OnJumpPanel = false;
            kart.State.TrickAvailable = false;
            kart.CurrentSpeed = 0f;
            if (!kart.controlsLocked) { kart.controlsLocked = true; lockedByUs = true; }

            if (reason == RespawnReason.Water)
            {
                SetPhase(Phase.Falling);
                timer = kart.stats.waterFallSeconds;
            }
            else Teleport();
        }

        /// <summary>Called first thing in every KartController fixed step.</summary>
        public void Step(float dt)
        {
            if (!Respawning)
            {
                // remember where we last stood on solid ground (every 10 steps)
                if (kart.State.Grounded && !kart.State.OnJumpPanel && ++safeCounter >= 10)
                {
                    safeCounter = 0; haveSafe = true;
                    lastSafePos = transform.position; lastSafeRot = transform.rotation;
                }
                return;
            }
            timer -= dt;
            if (Current == Phase.Falling && timer <= 0f) Teleport();
            else if (Current == Phase.Hover && timer <= 0f) Finish();
        }

        void Teleport()
        {
            var rb = kart.Body;
            ResolveTarget(out Vector3 pos, out Quaternion rot);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            transform.SetPositionAndRotation(pos, rot);
            rb.position = pos; rb.rotation = rot;
            kart.CurrentSpeed = 0f;
            kart.ForwardSpeed = 0f;
            kart.Zones?.Clear();
            Physics.SyncTransforms();
            SetPhase(Phase.Hover);
            timer = kart.stats.respawnHoverSeconds;
            Teleported?.Invoke();
        }

        void Finish()
        {
            kart.Body.isKinematic = false;
            // an AI kart rolls away at speed: from a standstill it reached the next jump too slowly, fell again and looped
            var racer = GetComponent<HarborRacer.Race.RacerProgress>();
            bool ai = racer != null && !racer.isPlayer;
            kart.Body.linearVelocity = ai ? transform.forward * aiRespawnSpeed : Vector3.zero;
            if (ai) kart.CurrentSpeed = aiRespawnSpeed;
            if (lockedByUs) { kart.controlsLocked = false; lockedByUs = false; }
            SetPhase(Phase.None);
        }

        void SetPhase(Phase p) { Current = p; }

        void ResolveTarget(out Vector3 pos, out Quaternion rot)
        {
            Vector3 from = haveSafe ? lastSafePos : transform.position;
            Transform best = null; float bestD = float.MaxValue;
            foreach (var cp in Checkpoints())
            {
                float d = (cp.position - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = cp; }
            }

            // In a race the respawn point is the last checkpoint the racer passed (Phase 6); otherwise the nearest one.
            var progress = GetComponent<HarborRacer.Race.RacerProgress>();
            if (progress != null && progress.LastCheckpoint != null) best = progress.LastCheckpoint;

            Vector3 basePos = best != null ? best.position : lastSafePos;
            Vector3 fwd = best != null ? best.forward : lastSafeRot * Vector3.forward;
            Vector3 up = Vector3.up;
            // checkpoint boxes are centred above the road: probe down from just above the box centre
            if (Physics.Raycast(basePos + Vector3.up * 2f, Vector3.down, out var h, 80f, Layers.GroundMask, QueryTriggerInteraction.Ignore))
            {
                basePos = h.point; up = h.normal;
            }
            fwd = Vector3.ProjectOnPlane(fwd, up);
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.ProjectOnPlane(Vector3.forward, up);
            rot = Quaternion.LookRotation(fwd.normalized, up);
            pos = basePos + up * kart.stats.respawnHoverHeight;
        }

        static IEnumerable<Transform> Checkpoints()
        {
            if (checkpoints == null || checkpoints.Count == 0 || checkpoints[0] == null)
            {
                checkpoints = new List<Transform>();
                foreach (var m in FindObjectsByType<ZoneMarker>())
                    if (m.kind == ZoneKind.Checkpoint) checkpoints.Add(m.transform);
            }
            return checkpoints;
        }

        public static void ResetCache() => checkpoints = null;
    }
}
