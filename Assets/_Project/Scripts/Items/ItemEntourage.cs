using System.Collections.Generic;
using HarborRacer.Kart;
using UnityEngine;

namespace HarborRacer.Items
{
    public enum EntourageMode { None, Orbit, Trail, Hold }

    /// <summary>
    /// Visuals for the items a kart is carrying: orbiting triple shells, trailing triple bananas and a single item held behind.
    /// Orbiting shells also hit karts they touch. Positions are computed from the kart transform every
    /// LateUpdate (play mode) or on demand via Place (tests).
    /// </summary>
    public class ItemEntourage : MonoBehaviour
    {
        public float orbitRadius = 2.2f, orbitDegreesPerSecond = 360f, orbitHeight = 0.9f;
        public float trailDistance = 2.6f, trailSpacing = 1.1f;

        public EntourageMode Mode { get; private set; }
        public int Count => items.Count;

        readonly List<Transform> items = new List<Transform>();
        KartController kart;
        ItemId id;
        float angle;
        static readonly Collider[] Buf = new Collider[8];

        void Awake() { kart = GetComponent<KartController>(); }

        public void Show(ItemId item, int count, EntourageMode mode)
        {
            Clear();
            id = item; Mode = mode;
            for (int i = 0; i < count; i++) items.Add(MakeVisual(item));
            Place();
        }

        public void SetCount(int n)
        {
            while (items.Count > n) { ItemVisuals.Destroy(items[items.Count - 1].gameObject); items.RemoveAt(items.Count - 1); }
            if (items.Count == 0) Mode = EntourageMode.None;
        }

        public void Clear()
        {
            foreach (var t in items) if (t != null) ItemVisuals.Destroy(t.gameObject);
            items.Clear();
            Mode = EntourageMode.None;
        }

        Transform MakeVisual(ItemId item)
        {
            var root = new GameObject("Carried_" + item);
            root.layer = Layers.Item;
            ItemVisuals.MakeItemModel(item, root.transform);
            return root.transform;
        }

        void LateUpdate()
        {
            if (items.Count == 0) return;
            angle += orbitDegreesPerSecond * Time.deltaTime;
            Place();
        }

        public void Place()
        {
            if (kart == null) kart = GetComponent<KartController>();
            var t = transform;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null) continue;
                items[i].position = SlotPosition(i);
                items[i].rotation = t.rotation;
            }
        }

        Vector3 SlotPosition(int i)
        {
            var t = transform;
            switch (Mode)
            {
                case EntourageMode.Orbit:
                {
                    float a = (angle + 360f * i / Mathf.Max(1, items.Count)) * Mathf.Deg2Rad;
                    return t.position + t.up * orbitHeight + t.right * Mathf.Sin(a) * orbitRadius + t.forward * Mathf.Cos(a) * orbitRadius;
                }
                case EntourageMode.Trail:
                    return t.position - t.forward * (trailDistance + i * trailSpacing) + t.up * 0.5f;
                case EntourageMode.Hold:
                    return t.position - t.forward * trailDistance + t.up * 0.55f;
            }
            return t.position;
        }

        /// <summary>Orbiting items strike karts they touch; returns how many items were spent.</summary>
        public int OrbitCollisions(HitType hit)
        {
            if (Mode != EntourageMode.Orbit) return 0;
            int spent = 0;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                int n = Physics.OverlapSphereNonAlloc(SlotPosition(i), 0.9f, Buf, 1 << Layers.Kart, QueryTriggerInteraction.Ignore);
                for (int c = 0; c < n; c++)
                {
                    var other = ItemProjectile.KartOf(Buf[c]);
                    if (other == null || other == kart) continue;
                    bool star = other.State.Star || other.State.Bullet;
                    bool landed = !star && other.HitReaction != null && other.HitReaction.Apply(hit, SlotPosition(i));
                    if (landed || star) { spent++; break; }
                }
            }
            return spent;
        }
    }
}
