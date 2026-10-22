using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Static render/physics cost census of the open scene: renderers, triangles, materials, shadows, lights, colliders, quality settings.</summary>
public static class PerfScene
{
    public static string Run()
    {
        var sb = new StringBuilder();
        var rs = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
        long tris = 0; var mats = new System.Collections.Generic.HashSet<Material>(); int shadowCasters = 0, skinned = 0, particles = 0, noStatic = 0; int slots = 0;
        var big = new System.Collections.Generic.List<(string, long)>();
        foreach (var r in rs)
        {
            if (r is SkinnedMeshRenderer) skinned++;
            if (r is ParticleSystemRenderer) particles++;
            if (r.shadowCastingMode != ShadowCastingMode.Off) shadowCasters++;
            foreach (var m in r.sharedMaterials) { slots++; if (m) mats.Add(m); }
            var mf = r.GetComponent<MeshFilter>();
            long t = 0;
            if (mf && mf.sharedMesh) { var me = mf.sharedMesh; for (int i = 0; i < me.subMeshCount; i++) t += me.GetIndexCount(i) / 3; }
            else if (r is SkinnedMeshRenderer s && s.sharedMesh) t = s.sharedMesh.triangles.Length / 3;
            tris += t; big.Add((r.name, t));
            if (!r.gameObject.isStatic) noStatic++;
        }
        sb.AppendLine($"renderers {rs.Length} (skinned {skinned}, particle {particles}), material slots {slots}, unique materials {mats.Count}, shadow casters {shadowCasters}, non-static {noStatic}, tris {tris:N0}");
        sb.AppendLine("shaders: " + string.Join(", ", mats.Select(m => m.shader.name).GroupBy(n => n).OrderByDescending(g => g.Count()).Take(8).Select(g => g.Key + " x" + g.Count())));
        sb.AppendLine("heaviest meshes: " + string.Join(" | ", big.OrderByDescending(b => b.Item2).Take(8).Select(b => b.Item1 + " " + b.Item2)));
        var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
        sb.AppendLine("lights " + lights.Length + ": " + string.Join(", ", lights.Take(8).Select(l => l.type + "/" + l.shadows)));
        var mcs = Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Exclude);
        long ctris = 0; int nonConvex = 0; foreach (var c in mcs) { if (c.sharedMesh) { ctris += c.sharedMesh.triangles.Length / 3; if (!c.convex) nonConvex++; } }
        sb.AppendLine($"mesh colliders {mcs.Length} (non-convex {nonConvex}), collider tris {ctris:N0}; all colliders {Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude).Length}; rigidbodies {Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude).Length}");
        sb.AppendLine($"audio sources {Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude).Length}, animators {Object.FindObjectsByType<Animator>(FindObjectsInactive.Exclude).Length}, particle systems {Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude).Length}, canvases {Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).Length}, reflection probes {Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Exclude).Length}");
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        sb.AppendLine($"quality '{QualitySettings.names[QualitySettings.GetQualityLevel()]}' vsync {QualitySettings.vSyncCount} target {Application.targetFrameRate} shadowDist {QualitySettings.shadowDistance} cascades {QualitySettings.shadowCascades} AA {QualitySettings.antiAliasing} lodBias {QualitySettings.lodBias} pixelLights {QualitySettings.pixelLightCount}");
        if (urp) sb.AppendLine($"URP: renderScale {urp.renderScale} hdr {urp.supportsHDR} msaa {urp.msaaSampleCount} shadowRes {urp.mainLightShadowmapResolution} shadows {urp.supportsMainLightShadows} depthTex {urp.supportsCameraDepthTexture} opaqueTex {urp.supportsCameraOpaqueTexture} srpBatcher {urp.useSRPBatcher} additionalLights {urp.additionalLightsRenderingMode}");
        sb.AppendLine($"fixedDelta {Time.fixedDeltaTime} maxDelta {Time.maximumDeltaTime} simMode {Physics.simulationMode} solverIter {Physics.defaultSolverIterations} autoSync {Physics.autoSyncTransforms}");
        var vols = Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude);
        foreach (var v in vols) if (v.sharedProfile) sb.AppendLine("volume: " + string.Join(", ", v.sharedProfile.components.Select(c => c.GetType().Name)));
        return sb.ToString();
    }
}
