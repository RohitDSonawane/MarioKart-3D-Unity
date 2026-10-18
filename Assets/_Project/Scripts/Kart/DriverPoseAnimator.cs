using HarborRacer.Items;
using HarborRacer.Race;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HarborRacer.Kart
{
    /// <summary>
    /// Plays the character's own hand / arm animations (embedded in the character FBX, the same clips the uses) so the driver holds
    /// the wheel, leans the hands into turns, reaches for a held item and throws. Built on a PlayableGraph so no Animator Controller asset is
    /// needed: the clips are plain references set by the builder (works in a player build).
    /// Layers: base blend (idle / turn left / turn right, or the same three with an item in hand), one-shot throw clips, and the reaction
    /// clips Mario has in the (hurt, hit by item, reverse, three tricks, win, lose). Clips left empty are skipped.
    /// </summary>
    public class DriverPoseAnimator : MonoBehaviour
    {
        public Transform animRoot;                 // the character model root (the object that has the Armature child)
        public KartController kart;
        public ItemHolder holder;

        public AnimationClip idle, turnLeft, turnRight;
        public AnimationClip itemIdle, itemTurnLeft, itemTurnRight;
        public AnimationClip throwForward, throwBack;

        [Header("Reaction clips (Mario only)")]
        public AnimationClip hurt, hitItem, reverse, jumpTrick1, jumpTrick2, gliderTrick, firstPlace, loseAnim;

        [Range(1f, 30f)] public float steerSmoothing = 12f;
        [Range(1f, 30f)] public float itemBlendSpeed = 10f;

        // Reaction layers sit on top of the base blend in priority order: the first one that wants weight takes it from the rest.
        enum Slot { Finish, HitItem, Hurt, Trick, Reverse, Count }

        class Overlay
        {
            public AnimationClip clip;
            public AnimationClipPlayable playable;
            public int input;
            public float speed = 1f, t, weight;
            public bool active, hold;
            public void Start(float s) { speed = s; t = 0f; active = true; }
        }

        const int FirstOverlayInput = 3;

        PlayableGraph graph;
        AnimationMixerPlayable baseMix, top;
        AnimationClipPlayable[] basePlayables;
        AnimationClipPlayable throwFwdPlayable, throwBackPlayable;
        AnimationClip[] baseClips;
        readonly Overlay[] overlays = new Overlay[(int)Slot.Count];
        float steerS, itemW, throwT = -1f, throwLen;
        bool throwIsBack;
        bool built, started, subscribed;

        void OnEnable()
        {
            if (kart == null) kart = GetComponentInParent<KartController>();
            if (holder == null && kart != null) holder = kart.GetComponent<ItemHolder>();
            if (holder != null) holder.Thrown += OnThrown;
            if (started) Subscribe();
            Build();
        }

        // the kart's own components are only wired up by the time Start runs
        void Start() { started = true; Subscribe(); }

        void Subscribe()
        {
            if (kart == null || subscribed) return;
            subscribed = true;
            if (kart.HitReaction != null) kart.HitReaction.Hit += OnHit;
            var trick = kart.GetComponent<KartTrick>();
            if (trick != null) trick.TrickPerformed += OnTrick;
            var racer = kart.GetComponent<RacerProgress>();
            if (racer != null) racer.OnFinished += OnFinished;
        }

        void Unsubscribe()
        {
            if (kart == null || !subscribed) return;
            subscribed = false;
            if (kart.HitReaction != null) kart.HitReaction.Hit -= OnHit;
            var trick = kart.GetComponent<KartTrick>();
            if (trick != null) trick.TrickPerformed -= OnTrick;
            var racer = kart.GetComponent<RacerProgress>();
            if (racer != null) racer.OnFinished -= OnFinished;
        }

        void OnDisable()
        {
            if (holder != null) holder.Thrown -= OnThrown;
            Unsubscribe();
            if (graph.IsValid()) graph.Destroy();
            built = false;
        }

        void Build()
        {
            if (animRoot == null || idle == null) return;
            var animator = animRoot.GetComponent<Animator>();
            if (animator == null) animator = animRoot.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            graph = PlayableGraph.Create("DriverPose_" + name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "Driver", animator);

            // missing item poses fall back to the plain ones, missing turns to idle
            baseClips = new[]
            {
                idle, turnLeft != null ? turnLeft : idle, turnRight != null ? turnRight : idle,
                itemIdle != null ? itemIdle : idle,
                itemTurnLeft != null ? itemTurnLeft : (itemIdle != null ? itemIdle : idle),
                itemTurnRight != null ? itemTurnRight : (itemIdle != null ? itemIdle : idle),
            };
            baseMix = AnimationMixerPlayable.Create(graph, baseClips.Length);
            basePlayables = new AnimationClipPlayable[baseClips.Length];
            for (int i = 0; i < baseClips.Length; i++)
            {
                basePlayables[i] = AnimationClipPlayable.Create(graph, baseClips[i]);
                basePlayables[i].SetSpeed(0.0);                       // times are set by hand each frame
                graph.Connect(basePlayables[i], 0, baseMix, i);
                baseMix.SetInputWeight(i, i == 0 ? 1f : 0f);
            }

            // inputs: 0 base, 1 throw forward, 2 throw back, 3.. one per reaction slot
            top = AnimationMixerPlayable.Create(graph, FirstOverlayInput + (int)Slot.Count);
            graph.Connect(baseMix, 0, top, 0);
            top.SetInputWeight(0, 1f);
            if (throwForward != null) { throwFwdPlayable = AnimationClipPlayable.Create(graph, throwForward); throwFwdPlayable.SetSpeed(0.0); graph.Connect(throwFwdPlayable, 0, top, 1); }
            if (throwBack != null) { throwBackPlayable = AnimationClipPlayable.Create(graph, throwBack); throwBackPlayable.SetSpeed(0.0); graph.Connect(throwBackPlayable, 0, top, 2); }
            top.SetInputWeight(1, 0f); top.SetInputWeight(2, 0f);

            MakeOverlay(Slot.Finish, firstPlace);                     // swapped for loseAnim on places 6-8
            MakeOverlay(Slot.HitItem, hitItem);
            MakeOverlay(Slot.Hurt, hurt);
            MakeOverlay(Slot.Trick, jumpTrick1);                      // swapped per trick
            MakeOverlay(Slot.Reverse, reverse);

            output.SetSourcePlayable(top);
            graph.Play();
            built = true;
        }

        void MakeOverlay(Slot slot, AnimationClip clip)
        {
            var ov = new Overlay { input = FirstOverlayInput + (int)slot };
            overlays[(int)slot] = ov;
            if (clip != null) SetOverlayClip(slot, clip);
        }

        /// <summary>Puts a clip on a reaction slot's graph input (the trick slot plays three clips, the finish slot win or lose).</summary>
        void SetOverlayClip(Slot slot, AnimationClip clip)
        {
            var ov = overlays[(int)slot];
            if (clip == null || ov == null || ov.clip == clip) return;
            if (ov.clip != null) { graph.Disconnect(top, ov.input); ov.playable.Destroy(); }
            ov.clip = clip;
            ov.playable = AnimationClipPlayable.Create(graph, clip);
            ov.playable.SetSpeed(0.0);
            graph.Connect(ov.playable, 0, top, ov.input);
            top.SetInputWeight(ov.input, 0f);
        }

        void OnThrown(bool backward)
        {
            var clip = backward ? throwBack : throwForward;
            if (!built || clip == null) return;
            throwIsBack = backward; throwLen = clip.length; throwT = 0f;
        }

        void OnHit(HitType type)
        {
            if (!built || type == HitType.Bump) return;
            if (type == HitType.Tumble) overlays[(int)Slot.HitItem].Start(2.25f);   // ItemHit state speed
            overlays[(int)Slot.Hurt].Start(7f);                                     // Hurt state speed: snaps to the end pose and holds while stunned
        }

        void OnTrick(int index)
        {
            if (!built) return;
            // trick cycle: JumpTrick1 (x1.75), JumpTrick2 (x1.2), GliderTrick (x1.5)
            AnimationClip c = index == 0 ? jumpTrick1 : index == 1 ? jumpTrick2 : gliderTrick;
            if (c == null) return;
            SetOverlayClip(Slot.Trick, c);
            overlays[(int)Slot.Trick].Start(index == 0 ? 1.75f : index == 1 ? 1.2f : 1.5f);
        }

        void OnFinished()
        {
            if (!built) return;
            var racer = kart != null ? kart.GetComponent<RacerProgress>() : null;
            bool win = racer == null || racer.FinishPlace <= 5;                     // places 1-5 celebrate, 6-8 lose
            var clip = win ? firstPlace : loseAnim;
            if (clip == null) return;
            SetOverlayClip(Slot.Finish, clip);
            var ov = overlays[(int)Slot.Finish];
            ov.hold = true;                                                          // stays on the last frame
            ov.Start(win ? 1f : 0.8f);
        }

        void Update()
        {
            if (!built || kart == null) return;
            float dt = Time.deltaTime;

            float steer = kart.Input != null ? kart.Input.Steer : 0f;
            steerS = Mathf.Lerp(steerS, Mathf.Clamp(steer, -1f, 1f), 1f - Mathf.Exp(-steerSmoothing * dt));
            bool hasItem = HandItemDisplay.HandItem(holder) != ItemId.None;      // triples and items held behind leave the hands on the wheel
            itemW = Mathf.MoveTowards(itemW, hasItem ? 1f : 0f, itemBlendSpeed * dt);

            float left = Mathf.Max(0f, -steerS), right = Mathf.Max(0f, steerS), mid = 1f - Mathf.Abs(steerS);
            float plain = 1f - itemW;
            SetBase(0, mid * plain); SetBase(1, left * plain); SetBase(2, right * plain);
            SetBase(3, mid * itemW); SetBase(4, left * itemW); SetBase(5, right * itemW);

            // idle poses loop, turn poses are held at their end frame
            Pose(0, true); Pose(3, true); Pose(1, false); Pose(2, false); Pose(4, false); Pose(5, false);

            // one-shot throw on top, faded in and out
            float wF = 0f, wB = 0f;
            if (throwT >= 0f)
            {
                throwT += dt;
                float fade = Mathf.Min(Mathf.Clamp01(throwT / 0.08f), Mathf.Clamp01((throwLen - throwT) / 0.15f));
                if (throwT >= throwLen) throwT = -1f;
                else
                {
                    if (throwIsBack) { wB = fade; throwBackPlayable.SetTime(throwT); }
                    else { wF = fade; throwFwdPlayable.SetTime(throwT); }
                }
            }

            // reaction layers in priority order, each takes what is left of the weight
            float budget = 1f;
            for (int i = 0; i < overlays.Length; i++)
            {
                var ov = overlays[i];
                if (ov == null || ov.clip == null) continue;
                bool want = false;
                var slot = (Slot)i;
                if (slot == Slot.Reverse)
                {
                    want = kart.ForwardSpeed < -1f;
                    if (want) { ov.t += dt; ov.playable.SetTime(ov.t % ov.clip.length); }
                }
                else if (ov.active)
                {
                    ov.t += dt * ov.speed;
                    float len = ov.clip.length;
                    if (slot == Slot.Hurt) want = kart.State.Stunned || ov.t < len;     // holds the end pose while the kart is stunned
                    else want = ov.hold || ov.t < len;                                   // finish stays on its last frame
                    if (!want) ov.active = false;
                    ov.playable.SetTime(Mathf.Min(ov.t, len));
                }
                float target = want ? 1f : 0f;
                ov.weight = Mathf.MoveTowards(ov.weight, target, dt / (target > ov.weight ? 0.1f : 0.25f));
                float w = Mathf.Min(ov.weight, budget);
                budget -= w;
                top.SetInputWeight(ov.input, w);
            }

            float throwW = Mathf.Min(Mathf.Max(wF, wB), budget);
            float scale = Mathf.Max(wF, wB) > 0f ? throwW / Mathf.Max(wF, wB) : 0f;
            top.SetInputWeight(1, wF * scale); top.SetInputWeight(2, wB * scale);
            top.SetInputWeight(0, Mathf.Max(0f, budget - throwW));
        }

        void SetBase(int i, float w) => baseMix.SetInputWeight(i, w);

        void Pose(int i, bool loop)
        {
            var clip = baseClips[i];
            double len = clip.length;
            double t = loop && len > 0.01 ? (Time.time % len) : len;
            basePlayables[i].SetTime(t);
        }
    }
}
