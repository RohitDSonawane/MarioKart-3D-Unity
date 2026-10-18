using UnityEngine;

namespace HarborRacer.CameraRig
{
    /// <summary>
    /// Perlin camera shake with a few presets. Multiple shakes add up; each fades out linearly.
    /// The rig applies <see cref="Offset"/> / <see cref="Roll"/> to its shake pivot every LateUpdate.
    /// </summary>
    public class CameraShake
    {
        struct Shake { public float amp, duration, time, seed; }
        readonly System.Collections.Generic.List<Shake> shakes = new System.Collections.Generic.List<Shake>();

        public Vector3 Offset { get; private set; }
        public float Roll { get; private set; }

        public static readonly (float amp, float seconds) Hit = (0.30f, 0.4f);
        public static readonly (float amp, float seconds) Explosion = (0.80f, 0.8f);
        public static readonly (float amp, float seconds) BoostBurst = (0.10f, 0.15f);
        public static readonly (float amp, float seconds) Landing = (0.15f, 0.2f);
        public static readonly (float amp, float seconds) WallBump = (0.20f, 0.2f);

        public void Add((float amp, float seconds) preset) => Add(preset.amp, preset.seconds);

        public void Add(float amp, float seconds)
        {
            shakes.Add(new Shake { amp = amp, duration = seconds, time = 0f, seed = Random.value * 100f });
        }

        public void Tick(float dt)
        {
            Vector3 o = Vector3.zero; float roll = 0f;
            for (int i = shakes.Count - 1; i >= 0; i--)
            {
                var s = shakes[i];
                s.time += dt;
                if (s.time >= s.duration) { shakes.RemoveAt(i); continue; }
                shakes[i] = s;
                float fade = 1f - s.time / s.duration;
                float t = s.time * 38f + s.seed;
                o += new Vector3(Mathf.PerlinNoise(t, 0.5f) - 0.5f, Mathf.PerlinNoise(0.5f, t) - 0.5f, 0f) * (2f * s.amp * fade);
                roll += (Mathf.PerlinNoise(t, t) - 0.5f) * 2f * s.amp * fade * 3f;
            }
            Offset = o; Roll = roll;
        }
    }
}
