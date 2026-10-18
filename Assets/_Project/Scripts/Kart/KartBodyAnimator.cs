using HarborRacer.Race;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HarborRacer.Kart
{
    /// <summary>
    /// The kart body animations (KartV2.controller): the whole kart model tilts, flips and hops on hits, bumps, boost starts,
    /// glider events and anti-gravity entry. The clips are the ones, root-relative transform curves, so they are played with a
    /// PlayableGraph on the "BodyAnim" wrapper between the kart root and the spin pivot (no Animator Controller asset needed).
    /// A constant "rest" clip keeps the wrapper at identity whenever no reaction is playing.
    /// </summary>
    public class KartBodyAnimator : MonoBehaviour
    {
        public Transform animRoot;                       // the BodyAnim wrapper
        public KartController kart;

        public AnimationClip shellHit, bananaHit, collideLeft, collideRight;
        public AnimationClip startBoost, startTurbo, glideFlip1, glideFlip2, antiGravSpinLeft, antiGravSpinRight;

        enum Id { ShellHit, BananaHit, CollideLeft, CollideRight, StartBoost, StartTurbo, Flip1, Flip2, SpinLeft, SpinRight, Count }

        class Layer
        {
            public AnimationClip clip; public AnimationClipPlayable playable; public float t, speed = 1f, weight; public bool active, loop;
        }

        readonly Layer[] layers = new Layer[(int)Id.Count];
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable restPlayable;
        AnimationClip rest;
        Id current = Id.Count;                         // Count = none
        bool built, started, subscribed, wasAntiGravity;

        void OnEnable()
        {
            if (kart == null) kart = GetComponentInParent<KartController>();
            Build();
            if (started) Subscribe();
        }

        void Start() { started = true; Subscribe(); }

        void OnDisable()
        {
            Unsubscribe();
            if (graph.IsValid()) graph.Destroy();
            built = false;
        }

        void Build()
        {
            if (animRoot == null) return;
            var animator = animRoot.GetComponent<Animator>();
            if (animator == null) animator = animRoot.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            graph = PlayableGraph.Create("KartBody_" + name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "Body", animator);

            mixer = AnimationMixerPlayable.Create(graph, 1 + (int)Id.Count);
            rest = MakeRestClip();
            restPlayable = AnimationClipPlayable.Create(graph, rest);
            restPlayable.SetSpeed(0.0);
            graph.Connect(restPlayable, 0, mixer, 0);
            mixer.SetInputWeight(0, 1f);

            var clips = new[] { shellHit, bananaHit, collideLeft, collideRight, startBoost, startTurbo, glideFlip1, glideFlip2, antiGravSpinLeft, antiGravSpinRight };
            for (int i = 0; i < clips.Length; i++)
            {
                var l = new Layer { clip = clips[i], loop = (Id)i == Id.StartTurbo };
                layers[i] = l;
                if (l.clip == null) continue;
                l.playable = AnimationClipPlayable.Create(graph, l.clip);
                l.playable.SetSpeed(0.0);
                graph.Connect(l.playable, 0, mixer, 1 + i);
                mixer.SetInputWeight(1 + i, 0f);
            }
            output.SetSourcePlayable(mixer);
            graph.Play();
            built = true;
        }

        static AnimationClip MakeRestClip()
        {
            var c = new AnimationClip { name = "KartBodyRest" };
            foreach (var p in new[] { "localPosition.x", "localPosition.y", "localPosition.z", "localRotation.x", "localRotation.y", "localRotation.z" })
                c.SetCurve("", typeof(Transform), p, AnimationCurve.Constant(0f, 0.1f, 0f));
            c.SetCurve("", typeof(Transform), "localRotation.w", AnimationCurve.Constant(0f, 0.1f, 1f));
            return c;
        }

        void Subscribe()
        {
            if (kart == null || subscribed) return;
            subscribed = true;
            if (kart.HitReaction != null)
            {
                kart.HitReaction.Hit += OnHit;
                // the clips replace the procedural spin / flip
                if (shellHit != null && bananaHit != null) kart.HitReaction.proceduralSpin = false;
            }
            if (kart.Boost != null) kart.Boost.BoostGranted += OnBoost;
            if (kart.Glider != null) { kart.Glider.Opened += OnGliderOpened; kart.Glider.GlideTrick += OnGlideTrick; }
        }

        void Unsubscribe()
        {
            if (kart == null || !subscribed) return;
            subscribed = false;
            if (kart.HitReaction != null) { kart.HitReaction.Hit -= OnHit; kart.HitReaction.proceduralSpin = true; }
            if (kart.Boost != null) kart.Boost.BoostGranted -= OnBoost;
            if (kart.Glider != null) { kart.Glider.Opened -= OnGliderOpened; kart.Glider.GlideTrick -= OnGlideTrick; }
        }

        void OnHit(HitType type)
        {
            switch (type)
            {
                case HitType.SpinOut: Play(Id.BananaHit); break;
                case HitType.Tumble:
                case HitType.Explosion: Play(Id.ShellHit); break;
                case HitType.Bump: Play(kart.HitReaction.LastBumpFromLeft ? Id.CollideRight : Id.CollideLeft); break;   // leans away from the hit
            }
        }

        void OnBoost(BoostSource s, float seconds, bool burst) { if (burst) Play(Id.StartBoost); }
        void OnGliderOpened() => Play(Id.Flip1);
        void OnGlideTrick(int index) => Play(index == 0 ? Id.Flip1 : Id.Flip2);

        static bool IsHit(Id id) => id == Id.ShellHit || id == Id.BananaHit;

        void Play(Id id)
        {
            if (!built) return;
            var l = layers[(int)id];
            if (l == null || l.clip == null) return;
            if (current != Id.Count && IsHit(current) && !IsHit(id) && layers[(int)current].active) return;   // a hit is not interrupted by cosmetics
            if (current != id && current != Id.Count) layers[(int)current].active = false;
            current = id;
            l.t = 0f; l.active = true;
        }

        void Update()
        {
            if (!built || kart == null) return;
            float dt = Time.deltaTime;

            // anti-gravity entry
            bool ag = kart.State.AntiGravity;
            if (ag && !wasAntiGravity)
                Play(kart.AntiGravity != null && kart.AntiGravity.CameraRotZ < 0f ? Id.SpinLeft : Id.SpinRight);
            wasAntiGravity = ag;

            // rocket start charge: the kart shakes while the accelerator is held during the countdown
            var rm = RaceManager.Instance;
            var rocket = kart.GetComponent<RocketStart>();
            bool charging = rm != null && rm.State == RaceState.Countdown && rocket != null && rocket.HeldSeconds > 0f;
            var turbo = layers[(int)Id.StartTurbo];
            if (turbo != null && turbo.clip != null)
            {
                if (charging && !turbo.active && current == Id.Count) { current = Id.StartTurbo; turbo.active = true; turbo.t = 0f; }
                else if (!charging && turbo.active) turbo.active = false;
            }

            float budget = 1f;
            for (int i = 0; i < layers.Length; i++)
            {
                var l = layers[i];
                if (l == null || l.clip == null) continue;
                bool want = l.active;
                if (l.active)
                {
                    l.t += dt * l.speed;
                    float len = l.clip.length;
                    if (l.loop) l.t %= len;
                    else if (l.t >= len) { l.active = false; want = false; if (current == (Id)i) current = Id.Count; }
                    l.playable.SetTime(Mathf.Min(l.t, len));
                }
                float target = want ? 1f : 0f;
                l.weight = Mathf.MoveTowards(l.weight, target, dt / (target > l.weight ? 0.08f : 0.2f));
                float w = Mathf.Min(l.weight, budget);
                budget -= w;
                mixer.SetInputWeight(1 + i, w);
            }
            mixer.SetInputWeight(0, Mathf.Max(0f, budget));
        }
    }
}
