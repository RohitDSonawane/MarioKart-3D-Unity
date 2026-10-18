using HarborRacer.Kart;
using UnityEngine;

namespace HarborRacer.Kart
{
    /// <summary>
    /// Phase 11 full VFX suite: multi-tier drift sparks (blue #3CA0FF, orange #FF8A1E, purple #C24BFF),
    /// boost flame bursts and streams, landing puffs, off-road dirt trails, trick sparkles,
    /// anti-gravity wheel spin rings, and star aura.
    /// </summary>
    public class KartEffects : MonoBehaviour
    {
        public KartController kart;
        public Material additiveMaterial;
        public Transform sparkLeft, sparkRight;       // rear wheel anchors
        public Transform exhaustLeft, exhaustRight;   // rear exhaust anchors (local +Z points backwards)

        static readonly Color Tier1 = new Color(0.235f, 0.627f, 1f, 1f);     // #3CA0FF (blue)
        static readonly Color Tier2 = new Color(1f, 0.541f, 0.118f, 1f);      // #FF8A1E (orange)
        static readonly Color Tier3 = new Color(0.761f, 0.294f, 1f, 1f);     // #C24BFF (purple)
        static readonly Color DirtColor = new Color(0.6f, 0.45f, 0.25f, 0.8f);
        static readonly Color StarColor1 = new Color(1f, 0.9f, 0.2f, 1f);
        static readonly Color StarColor2 = new Color(0.2f, 0.9f, 1f, 1f);
        static readonly Color AntiGravColor = new Color(0.1f, 0.7f, 1f, 0.9f);

        ParticleSystem sparkL, sparkR;
        ParticleSystem flameL, flameR;
        ParticleSystem driftDustL, driftDustR;
        ParticleSystem dirtL, dirtR;
        ParticleSystem landingPuff;
        ParticleSystem trickSparkles;
        ParticleSystem starAura;
        ParticleSystem antiGravL, antiGravR;

        int shownTier = -1;
        bool wasGrounded = true;

        void Awake()
        {
            if (kart == null) kart = GetComponent<KartController>();

            sparkL = MakeSparks(sparkLeft, "SparksL");
            sparkR = MakeSparks(sparkRight, "SparksR");
            flameL = MakeFlame(exhaustLeft, "FlameL");
            flameR = MakeFlame(exhaustRight, "FlameR");

            driftDustL = MakeDust(sparkLeft, "DriftDustL", new Color(0.9f, 0.9f, 0.9f, 0.5f));
            driftDustR = MakeDust(sparkRight, "DriftDustR", new Color(0.9f, 0.9f, 0.9f, 0.5f));

            dirtL = MakeDust(sparkLeft, "DirtL", DirtColor);
            dirtR = MakeDust(sparkRight, "DirtR", DirtColor);

            landingPuff = MakeLandingPuff(transform, "LandingPuff");
            trickSparkles = MakeSparkles(transform, "TrickSparkles");
            starAura = MakeStarAura(transform, "StarAura");

            antiGravL = MakeAntiGravRing(sparkLeft, "AntiGravL");
            antiGravR = MakeAntiGravRing(sparkRight, "AntiGravR");
        }

        void OnEnable()
        {
            if (kart == null) kart = GetComponent<KartController>();
            if (kart != null)
            {
                if (kart.Boost != null) kart.Boost.BoostGranted += OnBoostGranted;
                var trick = GetComponent<KartTrick>();
                if (trick != null)
                {
                    trick.TrickPerformed += OnTrickPerformed;
                    trick.TrickLanded += OnTrickLanded;
                }
            }
        }

        void OnDisable()
        {
            if (kart != null)
            {
                if (kart.Boost != null) kart.Boost.BoostGranted -= OnBoostGranted;
                var trick = GetComponent<KartTrick>();
                if (trick != null)
                {
                    trick.TrickPerformed -= OnTrickPerformed;
                    trick.TrickLanded -= OnTrickLanded;
                }
            }
        }

        void OnBoostGranted(BoostSource src, float seconds, bool burst)
        {
            if (!burst) return;
            flameL?.Emit(22);
            flameR?.Emit(22);
        }

        void OnTrickPerformed(int idx)
        {
            trickSparkles?.Emit(30);
        }

        void OnTrickLanded()
        {
            landingPuff?.Emit(20);
        }

        void LateUpdate()
        {
            if (kart == null) return;
            var d = kart.Drift;
            var st = kart.State;
            bool boosting = kart.Boost != null && kart.Boost.Boosting;

            // 1. Drift sparks & dust
            bool sparks = d != null && d.IsDrifting && d.Landed && d.Tier > 0;
            int tier = sparks ? d.Tier : 0;
            if (tier != shownTier)
            {
                shownTier = tier;
                Color c = tier == 3 ? Tier3 : tier == 2 ? Tier2 : Tier1;
                SetColor(sparkL, c); SetColor(sparkR, c);
            }
            SetEmitting(sparkL, sparks);
            SetEmitting(sparkR, sparks);

            bool driftDust = d != null && d.IsDrifting && d.Landed && d.DriftTime < 1.2f;
            SetEmitting(driftDustL, driftDust);
            SetEmitting(driftDustR, driftDust);

            // 2. Boost flames
            SetEmitting(flameL, boosting);
            SetEmitting(flameR, boosting);

            // 3. Off-road dirt
            bool onDirt = st != null && st.OnDirt && st.Grounded && Mathf.Abs(kart.ForwardSpeed) > 5f;
            SetEmitting(dirtL, onDirt);
            SetEmitting(dirtR, onDirt);

            // 4. Landing puff detection
            if (!wasGrounded && st != null && st.Grounded)
            {
                landingPuff?.Emit(15);
            }
            wasGrounded = st != null && st.Grounded;

            // 5. Star aura
            bool isStar = st != null && st.Star;
            SetEmitting(starAura, isStar);

            // 6. Anti-gravity wheel sparks
            bool isAntiGrav = st != null && st.AntiGravity;
            SetEmitting(antiGravL, isAntiGrav);
            SetEmitting(antiGravR, isAntiGrav);
        }

        // ---- Particle System Factories ----

        static void SetEmitting(ParticleSystem ps, bool on)
        {
            if (ps == null) return;
            var e = ps.emission;
            if (e.enabled != on) e.enabled = on;
            if (on && !ps.isPlaying) ps.Play();
        }

        static void SetColor(ParticleSystem ps, Color c)
        {
            if (ps == null) return;
            var m = ps.main;
            m.startColor = new ParticleSystem.MinMaxGradient(c);
        }

        ParticleSystem MakeSparks(Transform anchor, string name)
        {
            if (anchor == null) return null;
            var ps = NewSystem(anchor, name);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            main.gravityModifier = 0.5f;
            main.startColor = new ParticleSystem.MinMaxGradient(Tier1);
            var emission = ps.emission; emission.rateOverTime = 70f; emission.enabled = false;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 25f; shape.radius = 0.06f;
            ps.transform.localRotation = Quaternion.Euler(-60f, 180f, 0f);
            FadeOut(ps);
            return ps;
        }

        ParticleSystem MakeFlame(Transform anchor, string name)
        {
            if (anchor == null) return null;
            var ps = NewSystem(anchor, name);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 11f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.5f, 0.1f, 1f), new Color(1f, 0.9f, 0.3f, 1f));
            var emission = ps.emission; emission.rateOverTime = 80f; emission.enabled = false;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 8f; shape.radius = 0.05f;
            var sol = ps.sizeOverLifetime; sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(1f, 0f)));
            FadeOut(ps);
            return ps;
        }

        ParticleSystem MakeDust(Transform anchor, string name, Color dustCol)
        {
            if (anchor == null) return null;
            var ps = NewSystem(anchor, name);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.startColor = new ParticleSystem.MinMaxGradient(dustCol);
            var emission = ps.emission; emission.rateOverTime = 40f; emission.enabled = false;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.15f;
            var sol = ps.sizeOverLifetime; sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(1f, 1f)));
            FadeOut(ps);
            return ps;
        }

        ParticleSystem MakeLandingPuff(Transform parent, string name)
        {
            var ps = NewSystem(parent, name);
            ps.transform.localPosition = new Vector3(0f, -0.6f, 0f);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.95f, 0.95f, 0.95f, 0.6f));
            main.loop = false;
            var emission = ps.emission; emission.rateOverTime = 0f; emission.enabled = false;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 0.8f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            FadeOut(ps);
            return ps;
        }

        ParticleSystem MakeSparkles(Transform parent, string name)
        {
            var ps = NewSystem(parent, name);
            ps.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.4f, 1f), new Color(0.4f, 0.9f, 1f, 1f));
            main.loop = false;
            var emission = ps.emission; emission.rateOverTime = 0f; emission.enabled = false;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 1.2f;
            FadeOut(ps);
            return ps;
        }

        ParticleSystem MakeStarAura(Transform parent, string name)
        {
            var ps = NewSystem(parent, name);
            ps.transform.localPosition = new Vector3(0f, 0f, 0f);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            main.startColor = new ParticleSystem.MinMaxGradient(StarColor1, StarColor2);
            var emission = ps.emission; emission.rateOverTime = 45f; emission.enabled = false;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 1.5f;
            FadeOut(ps);
            return ps;
        }

        ParticleSystem MakeAntiGravRing(Transform anchor, string name)
        {
            if (anchor == null) return null;
            var ps = NewSystem(anchor, name);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startColor = new ParticleSystem.MinMaxGradient(AntiGravColor);
            var emission = ps.emission; emission.rateOverTime = 30f; emission.enabled = false;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 0.3f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            FadeOut(ps);
            return ps;
        }

        ParticleSystem NewSystem(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 250;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = additiveMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        static void FadeOut(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }
    }
}
