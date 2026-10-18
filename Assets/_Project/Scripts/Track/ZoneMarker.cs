using System;
using System.Collections.Generic;
using UnityEngine;

namespace HarborRacer.Track
{
    public enum ZoneKind
    {
        StartSlot, Checkpoint, LapLine, BoostPad, JumpPanel, GliderRamp, GliderLaunch, TrickZone,
        AntiGravityEnter, AntiGravityExit, CancelDownForce, DownForce, OutOfBounds, Water,
        Fence, ExtraFloor, ShellPlatform, ItemBox, Crate, SlowDownAI, Explosion, TrolleySpawn,
    }

    [Serializable]
    public struct Tuning
    {
        public string key;
        public float value;
    }

    /// <summary>
    /// Data-only marker for a gameplay zone, imported from the scene setup.
    /// Phases 4-8 replace/augment these with behaviour components that read the same values.
    /// </summary>
    public class ZoneMarker : MonoBehaviour
    {
        public ZoneKind kind;
        public int index;
        public List<Tuning> tuning = new List<Tuning>();

        public float Get(string key, float fallback = 0f)
        {
            foreach (var t in tuning) if (t.key == key) return t.value;
            return fallback;
        }

        public static Color ColorFor(ZoneKind k)
        {
            switch (k)
            {
                case ZoneKind.Checkpoint: return new Color(1f, 1f, 0.2f, 0.9f);
                case ZoneKind.LapLine: return new Color(1f, 1f, 1f, 1f);
                case ZoneKind.JumpPanel: return new Color(1f, 0.55f, 0.1f, 0.9f);
                case ZoneKind.GliderRamp: case ZoneKind.GliderLaunch: return new Color(0.3f, 0.6f, 1f, 0.9f);
                case ZoneKind.TrickZone: return new Color(1f, 0.3f, 0.9f, 0.9f);
                case ZoneKind.AntiGravityEnter: return new Color(0.2f, 1f, 1f, 0.9f);
                case ZoneKind.AntiGravityExit: return new Color(0.1f, 0.5f, 1f, 0.9f);
                case ZoneKind.OutOfBounds: case ZoneKind.Water: return new Color(1f, 0.15f, 0.15f, 0.8f);
                case ZoneKind.ItemBox: return new Color(0.4f, 1f, 0.4f, 0.9f);
                case ZoneKind.Crate: return new Color(0.7f, 0.45f, 0.2f, 0.9f);
                case ZoneKind.Fence: return new Color(0.6f, 0.6f, 0.6f, 0.6f);
                default: return new Color(0.8f, 0.8f, 0.8f, 0.7f);
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = ColorFor(kind);
            Gizmos.matrix = transform.localToWorldMatrix;
            if (TryGetComponent<BoxCollider>(out var b)) Gizmos.DrawWireCube(b.center, b.size);
            else if (TryGetComponent<SphereCollider>(out var s)) Gizmos.DrawWireSphere(s.center, s.radius);
            else Gizmos.DrawWireCube(Vector3.zero, Vector3.one * 2f);
        }
    }
}
