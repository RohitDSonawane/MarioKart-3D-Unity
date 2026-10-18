using System;
using HarborRacer.Inputs;
using HarborRacer.Kart;
using HarborRacer.Race;
using HarborRacer.Track;
using UnityEngine;

namespace HarborRacer.Items
{
    public enum HolderState { Empty, Rolling, Holding }

    /// <summary>
    /// Per-racer item flow: Empty -> Rolling (4 s roulette) -> Holding -> use -> Empty. Also owns the timed
    /// power-ups (Star 7.5 s, Golden Mushroom 10 s, Bullet Bill 11 s). KartController calls Step once per fixed step with the
    /// latched use / back button presses, so players and AI share it. AI karts (RacerProgress.isPlayer == false) get a simple
    /// auto-use brain until Phase 9 replaces it.
    ///
    /// Controls: Use = throw / fire forward. Back = hold the item behind as a shield (single banana, shell, bomb), press Back again
    /// to throw it backward; triples fire one backward directly.
    /// </summary>
    public class ItemHolder : MonoBehaviour
    {
        [Header("Roulette")]
        public float rollSeconds = 4f;

        [Header("Timed power-ups")]
        public float starSeconds = 7.5f;
        public float goldenSeconds = 10f;
        public float bulletSeconds = 11f;
        public float bulletSpeed = 130f;
        public float bulletExitSpeed = 70f;
        public float bulletTurnDegPerSecond = 220f;
        public float mushroomBoost = 2f, tripleMushroomBoost = 2.5f, coinBoost = 0.3f;

        public HolderState State { get; private set; }
        public ItemId Current { get; private set; }
        public int UsesLeft { get; private set; }
        public bool HoldingBehind { get; private set; }
        public ItemId RollDisplay { get; private set; }
        public float RollLeft { get; private set; }
        public float StarLeft { get; private set; }
        public float GoldenLeft { get; private set; }
        public float BulletLeft { get; private set; }

        public event Action ItemChanged;
        public event Action<ItemId> ItemUsed;
        /// <summary>A throwable left the kart (true = thrown backwards); drives the driver throw animation.</summary>
        public event Action<bool> Thrown;

        KartController kart;
        RacerProgress racer;
        CoinWallet wallet;
        ItemEntourage entourage;
        System.Random rng = new System.Random();
        float rollTick, goldenCooldown;
        AIItemBrain brain;

        // bullet
        IKartInput savedInput;
        PathDriverInput bulletDriver;
        bool addedDriver;
        TrackPath savedPath;
        int savedNode;
        Transform bulletModel;
        readonly System.Collections.Generic.List<Renderer> hidden = new System.Collections.Generic.List<Renderer>();

        // star tint
        Renderer[] starRenderers;
        MaterialPropertyBlock[] starOriginal;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            kart = GetComponent<KartController>();
            racer = GetComponent<RacerProgress>();
            wallet = GetComponent<CoinWallet>();
            entourage = GetComponent<ItemEntourage>();
            if (entourage == null) entourage = gameObject.AddComponent<ItemEntourage>();
        }

        public bool IsPlayer => racer != null && racer.isPlayer;
        int Position => racer != null && racer.Position > 0 ? racer.Position : 4;
        public bool CanAct => !kart.State.Stunned && !(kart.Respawn != null && kart.Respawn.Respawning) && !(racer != null && racer.Finished) && !kart.State.Bullet;

        // ---- obtaining items ----

        /// <summary>An item box was hit: start the roulette if empty (boxes break regardless).</summary>
        public bool OnItemBox()
        {
            if (State != HolderState.Empty) return false;
            State = HolderState.Rolling;
            RollLeft = rollSeconds; rollTick = 0f;
            ItemChanged?.Invoke();
            return true;
        }

        /// <summary>Hands over an item immediately (roulette result, debug keys).</summary>
        public void Give(ItemId id)
        {
            ClearHeld();
            if (id == ItemId.None) { State = HolderState.Empty; ItemChanged?.Invoke(); return; }
            Current = id; UsesLeft = ItemInfo.Uses(id); State = HolderState.Holding; HoldingBehind = false;
            if (!IsPlayer) { brain ??= new AIItemBrain(this, kart, racer); brain.Reset(); }
            if (ItemInfo.IsGreen(id) && UsesLeft == 3) entourage.Show(ItemId.GreenShell, 3, EntourageMode.Orbit);
            else if (ItemInfo.IsRed(id) && UsesLeft == 3) entourage.Show(ItemId.RedShell, 3, EntourageMode.Orbit);
            else if (id == ItemId.TripleBanana) entourage.Show(ItemId.Banana, 3, EntourageMode.Trail);
            ItemChanged?.Invoke();
        }

        void ClearHeld()
        {
            entourage.Clear();
            HoldingBehind = false;
        }

        void Empty()
        {
            ClearHeld();
            Current = ItemId.None; UsesLeft = 0; State = HolderState.Empty;
            ItemChanged?.Invoke();
        }

        // ---- per fixed step ----

        public void Step(float dt, bool usePressed, bool backPressed)
        {
            StepPowerups(dt);

            if (State == HolderState.Rolling)
            {
                RollLeft -= dt; rollTick -= dt;
                if (rollTick <= 0f)
                {
                    float slow = 1f - Mathf.Clamp01(RollLeft / rollSeconds);
                    rollTick = 0.07f + slow * slow * 0.3f;
                    RollDisplay = (ItemId)rng.Next(1, 15);
                    if (IsPlayer) Audio.AudioService.Instance?.Play2D(Audio.AudioService.Instance?.library?.rouletteTick, 0.4f);
                }
                if (RollLeft <= 0f) FinishRoll();
                return;
            }
            if (State != HolderState.Holding) return;

            // orbiting shells strike karts they touch
            if (entourage.Mode == EntourageMode.Orbit && CanAct)
            {
                int spent = entourage.OrbitCollisions(HitType.Tumble);
                if (spent > 0) Consume(spent);
                if (State != HolderState.Holding) return;
            }

            if (!CanAct) return;

            if (!IsPlayer && !usePressed && !backPressed)
            {
                brain ??= new AIItemBrain(this, kart, racer);
                brain.Decide(dt, out usePressed, out backPressed);
            }

            if (usePressed) UseItem(false);
            else if (backPressed) UseItem(true);
        }

        void FinishRoll()
        {
            if (IsPlayer) Audio.AudioService.Instance?.Play2D(Audio.AudioService.Instance?.library?.rouletteStop, 0.7f);
            bool canBullet = kart.AntiGravity == null || kart.AntiGravity.CanUseBullet;
            Give(ItemDistribution.Roll(Position, rng, canBullet, BlueShell.IsActive));
        }

        // ---- using ----

        void UseItem(bool back)
        {
            var id = Current;
            switch (id)
            {
                case ItemId.Mushroom:
                    kart.Boost?.AddBoost(mushroomBoost, BoostSource.Mushroom); Consume(1); break;
                case ItemId.TripleMushroom:
                    kart.Boost?.AddBoost(tripleMushroomBoost, BoostSource.Mushroom); Consume(1); break;
                case ItemId.GoldenMushroom:
                    if (GoldenLeft <= 0f) GoldenLeft = goldenSeconds;
                    if (goldenCooldown <= 0f) { kart.Boost?.AddBoost(mushroomBoost, BoostSource.GoldenMushroom); goldenCooldown = 0.5f; }
                    else return;
                    break;
                case ItemId.Coin:
                    if (wallet != null) wallet.Add(1);
                    kart.Boost?.AddBoost(coinBoost, BoostSource.Mushroom, burst: false); Consume(1); break;
                case ItemId.Star: StartStar(); Consume(1); break;
                case ItemId.BulletBill:
                    if (kart.AntiGravity != null && !kart.AntiGravity.CanUseBullet) return;
                    StartBullet(); Consume(1); break;
                case ItemId.BlueShell: BlueShell.Create(kart); Consume(1); break;
                default: UseThrowable(id, back); break;
            }
            ItemUsed?.Invoke(id);
        }

        void UseThrowable(ItemId id, bool back)
        {
            bool single = ItemInfo.Uses(id) == 1;
            // Back on a single item first holds it behind as a shield; back again (or a triple) throws it backward.
            if (back && single && ItemInfo.CanHoldBehind(id) && !HoldingBehind)
            {
                HoldingBehind = true;
                entourage.Show(id, 1, EntourageMode.Hold);
                return;
            }
            Fire(id, back);
            HoldingBehind = false;
            Consume(1);
        }

        /// <summary>Spawns the projectile for an item, forward or backward.</summary>
        public void Fire(ItemId id, bool backward)
        {
            Thrown?.Invoke(backward);
            var t = kart.transform;
            Vector3 fwd = t.forward, up = t.up;
            Vector3 dir = backward ? -fwd : fwd;
            float speed = Mathf.Max(kart.ForwardSpeed, 0f);
            Vector3 muzzle = t.position + dir * 3.3f + up * 0.9f;

            if (ItemInfo.IsBanana(id))
            {
                Vector3 v = backward ? -fwd * 6f + up * 12f : fwd * (speed + 30f) + up * 35f;
                Banana.Create(kart, backward ? t.position - fwd * 3.0f + up * 0.6f : muzzle, v);
            }
            else if (ItemInfo.IsGreen(id)) GreenShell.Create(kart, muzzle, dir);
            else if (ItemInfo.IsRed(id)) RedShell.CreateRed(kart, muzzle, dir, homingEnabled: !backward);
            else if (id == ItemId.BobOmb)
            {
                Vector3 v = backward ? -fwd * 6f + up * 12f : fwd * (speed + 25f) + up * 50f;
                BobOmb.Create(kart, backward ? t.position - fwd * 3.2f + up * 0.8f : t.position + fwd * 2.5f + up * 1.6f, v);
            }
        }

        void Consume(int n)
        {
            UsesLeft -= n;
            if (UsesLeft <= 0 && !(Current == ItemId.GoldenMushroom && GoldenLeft > 0f)) { Empty(); return; }
            if (Current == ItemId.GoldenMushroom) { ItemChanged?.Invoke(); return; }
            if (entourage.Mode == EntourageMode.Orbit || entourage.Mode == EntourageMode.Trail) entourage.SetCount(UsesLeft);
            ItemChanged?.Invoke();
        }

        /// <summary>
        /// A projectile reached this kart: a held-behind item (or the trailing bananas) absorbs it if it comes from behind.
        /// Returns true if blocked; the shield item is spent.
        /// </summary>
        public bool TryBlock(ItemProjectile p)
        {
            if (State != HolderState.Holding) return false;
            bool shield = HoldingBehind || Current == ItemId.TripleBanana;
            if (!shield) return false;
            Vector3 rear = -kart.transform.forward;
            Vector3 to = (p.transform.position - kart.transform.position).normalized;
            if (Vector3.Dot(rear, to) <= 0.3f) return false;
            Consume(1);
            return true;
        }

        // ---- timed power-ups ----

        void StepPowerups(float dt)
        {
            if (goldenCooldown > 0f) goldenCooldown -= dt;
            if (GoldenLeft > 0f)
            {
                GoldenLeft -= dt;
                if (GoldenLeft <= 0f) { GoldenLeft = 0f; if (Current == ItemId.GoldenMushroom) Empty(); }
            }
            if (StarLeft > 0f)
            {
                StarLeft -= dt;
                if (StarLeft <= 0f) EndStar();
            }
            if (BulletLeft > 0f) StepBullet(dt);
        }

        public void StartStar()
        {
            StarLeft = starSeconds;
            kart.State.Star = true;
            starRenderers = kart.GetComponentsInChildren<Renderer>();
            starOriginal = new MaterialPropertyBlock[starRenderers.Length];
            for (int i = 0; i < starRenderers.Length; i++) { starOriginal[i] = new MaterialPropertyBlock(); starRenderers[i].GetPropertyBlock(starOriginal[i]); }
        }

        void EndStar()
        {
            StarLeft = 0f;
            kart.State.Star = false;
            if (starRenderers != null)
                for (int i = 0; i < starRenderers.Length; i++) if (starRenderers[i] != null) starRenderers[i].SetPropertyBlock(starOriginal[i]);
            starRenderers = null;
        }

        void LateUpdate()
        {
            if (StarLeft > 0f && starRenderers != null)
            {
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor(BaseColorId, Color.HSVToRGB(Mathf.Repeat(Time.time * 2f, 1f), 0.7f, 1f));
                foreach (var r in starRenderers) if (r != null) r.SetPropertyBlock(mpb);
            }
        }

        public void StartBullet()
        {
            BulletLeft = bulletSeconds;
            kart.State.Bullet = true;
            kart.Boost?.Cancel(); kart.Drift?.Cancel(); kart.Trick?.Cancel();
            ClearHeld();

            savedInput = kart.Input;
            bulletDriver = gameObject.GetComponent<PathDriverInput>();
            addedDriver = bulletDriver == null;
            if (addedDriver) bulletDriver = gameObject.AddComponent<PathDriverInput>();
            savedPath = bulletDriver.path; savedNode = bulletDriver.nodeIndex;     // an AI kart keeps its own racing line for afterwards
            bulletDriver.kart = kart;
            bulletDriver.path = ItemProjectile.ItemPath;
            bulletDriver.forcedSpeed = bulletSpeed;
            bulletDriver.SnapToPath();
            kart.Input = bulletDriver;

            hidden.Clear();
            foreach (var r in kart.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; hidden.Add(r); }
            bulletModel = ItemVisuals.Make(PrimitiveType.Sphere, ItemInfo.Tint(ItemId.BulletBill), new Vector3(1.6f, 1.6f, 2.8f), kart.transform);
            bulletModel.localPosition = Vector3.up * 0.1f;
        }

        void StepBullet(float dt)
        {
            BulletLeft -= dt;
            kart.CurrentSpeed = Mathf.MoveTowards(kart.CurrentSpeed, bulletSpeed, 300f * dt);

            // the bullet turns itself straight toward the path (no steering limits), otherwise 130 u/s overshoots every bend
            if (bulletDriver != null)
            {
                Vector3 flatFwd = Vector3.ProjectOnPlane(kart.transform.forward, kart.transform.up);
                Vector3 flatTo = Vector3.ProjectOnPlane(bulletDriver.AimPoint - kart.transform.position, kart.transform.up);
                if (flatTo.sqrMagnitude > 1f)
                {
                    float delta = Mathf.Clamp(Vector3.SignedAngle(flatFwd, flatTo, kart.transform.up), -bulletTurnDegPerSecond * dt, bulletTurnDegPerSecond * dt);
                    kart.Body.rotation = Quaternion.AngleAxis(delta, kart.transform.up) * kart.Body.rotation;
                }
            }

            // ram karts and sweep items out of the way
            foreach (var c in Physics.OverlapSphere(kart.transform.position, 3.4f, 1 << Layers.Kart, QueryTriggerInteraction.Ignore))
            {
                var other = ItemProjectile.KartOf(c);
                if (other == null || other == kart || other.HitReaction == null) continue;
                if (other.HitReaction.Apply(HitType.Tumble, kart.transform.position))
                    other.Push((other.transform.position - kart.transform.position).normalized * 25f, 30, 0.95f);
            }
            for (int i = ItemProjectile.All.Count - 1; i >= 0; i--)
            {
                var p = ItemProjectile.All[i];
                if (p != null && !(p is BlueShell) && (p.transform.position - kart.transform.position).sqrMagnitude < 4.5f * 4.5f) p.Kill();
            }

            bool respawning = kart.Respawn != null && kart.Respawn.Respawning;
            if (BulletLeft <= 0f || respawning) EndBullet();
        }

        void EndBullet()
        {
            BulletLeft = 0f;
            kart.State.Bullet = false;
            kart.Input = savedInput;
            if (bulletDriver != null)
            {
                bulletDriver.forcedSpeed = 0f;
                if (addedDriver) ItemVisuals.Destroy(bulletDriver);
                else { bulletDriver.path = savedPath; bulletDriver.SnapToPath(); }
                bulletDriver = null;
            }
            foreach (var r in hidden) if (r != null) r.enabled = true;
            hidden.Clear();
            if (bulletModel != null) ItemVisuals.Destroy(bulletModel.gameObject);
            kart.CurrentSpeed = bulletExitSpeed;
        }
    }
}
