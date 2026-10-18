using HarborRacer.Kart;
using UnityEngine;

namespace HarborRacer.Items
{
    /// <summary>
    /// Shows the single item a driver is holding in the hand (the item sits on the "ItemBone" at the end of the arm, the hand reaches for it
    /// when the roulette ends and it disappears about a quarter of a second after the throw). Triple items orbit / trail instead, and an item
    /// held behind is shown by <see cref="ItemEntourage"/>, so the hand goes back to the wheel in those cases.
    /// The model follows the bone's position every LateUpdate (after the animation) but keeps the kart's orientation, so the bone's own axes do not matter.
    /// </summary>
    public class HandItemDisplay : MonoBehaviour
    {
        public KartController kart;
        public ItemHolder holder;
        public Transform socket;                       // the ItemBone; found by name when empty
        public float hideDelayAfterThrow = 0.25f;      // hand item hidden 0.25 s after the throw starts
        public Vector3 offset = new Vector3(0f, 0.05f, 0.1f);   // in kart space, from the bone
        public float handSize = 0.5f;                  // longest side of a held item in world units (the item prefabs are about 6 units wide)

        Transform shown;
        ItemId shownId;
        float hideTimer;

        /// <summary>The item the hand should hold right now, or None.</summary>
        public static ItemId HandItem(ItemHolder h)
        {
            if (h == null || h.State != HolderState.Holding || h.Current == ItemId.None) return ItemId.None;
            if (h.HoldingBehind || ItemInfo.Uses(h.Current) != 1 || h.Current == ItemId.BulletBill) return ItemId.None;
            return h.Current;
        }

        void OnEnable()
        {
            if (kart == null) kart = GetComponentInParent<KartController>();
            if (holder == null && kart != null) holder = kart.GetComponent<ItemHolder>();
            if (holder != null) holder.Thrown += OnThrown;
            if (socket == null) socket = FindDeep(transform, "ItemBone") ?? FindDeep(transform, "HandR");   // only Mario has the ItemBone
        }

        void OnDisable()
        {
            if (holder != null) holder.Thrown -= OnThrown;
            Clear();
        }

        void OnThrown(bool backward) => hideTimer = hideDelayAfterThrow;

        void Update()
        {
            if (hideTimer > 0f) hideTimer -= Time.deltaTime;
            ItemId want = HandItem(holder);
            if (want == ItemId.None && hideTimer > 0f) return;           // still showing the item mid-throw
            if (want == shownId) return;
            Clear();
            if (want == ItemId.None || socket == null) return;
            shown = ItemVisuals.MakeItemModel(want, null);
            if (shown == null) return;
            shownId = want;
            FitToHand(shown, want);
            Place();
        }

        void LateUpdate()
        {
            if (shown != null) Place();
        }

        void FitToHand(Transform t, ItemId id)
        {
            var b = new Bounds(t.position, Vector3.zero);
            foreach (var r in t.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            float longest = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (longest < 0.01f) return;
            float target = id == ItemId.Coin ? 0.4f : id == ItemId.BlueShell ? 0.6f : id == ItemId.Banana ? 0.6f : handSize;
            t.localScale *= target / longest;
        }

        void Place()
        {
            var k = kart != null ? kart.transform : transform;
            shown.position = socket.position + k.TransformDirection(offset);
            shown.rotation = k.rotation;
        }

        void Clear()
        {
            if (shown != null) ItemVisuals.Destroy(shown.gameObject);
            shown = null; shownId = ItemId.None;
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }
    }
}
