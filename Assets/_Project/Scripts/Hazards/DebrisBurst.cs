using UnityEngine;

namespace HarborRacer.Hazards
{
    /// <summary>One-shot particle burst for broken crates (placeholder; Phase 11 uses the real debris models and sounds).</summary>
    public static class DebrisBurst
    {
        public static void Spawn(Vector3 position, Color colour, Material material, float sizeScale = 1f)
        {
            var go = new GameObject("DebrisBurst");
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1f; main.loop = false; main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 14f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.1f) ;
            main.startColor = colour;
            main.gravityModifier = 2.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission; emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(Mathf.RoundToInt(24 + sizeScale * 10f), 24, 60)) });
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 1.2f * Mathf.Max(0.5f, sizeScale);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var r = go.GetComponent<ParticleSystemRenderer>();
            if (material != null) r.sharedMaterial = material;
            ps.Play();
            if (Application.isPlaying) Object.Destroy(go, 3f);
            else Object.DestroyImmediate(go);       // edit-mode tests: no lingering objects
        }
    }
}
