using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HarborRacer.EditorTools
{
    /// <summary>
    /// Target match pass, step 1 ("the look"): copies the lighting setup of the Toad Harbor scene onto ours.
    /// Values: RenderSettings, the sun and its volume profile
    /// (URP Bloom threshold 1 / intensity 1.9, tuned to 0.8 here because our realtime sun is brighter than their baked setup; Beautify saturate 2 maps to Color Adjustments), plus the
    /// target sky (Day Skyboxes Pack 2 / 4) and sea shader graph (RealWater). Idempotent; run after UiBuilder.
    /// </summary>
    public static class SceneLookBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/ToadHarbor.unity";
        const string RefDir = "Assets/_Project/Art/Shared";
        const string ProfilePath = "Assets/_Project/Settings/ToadHarborPostFX.asset";

        [MenuItem("Tools/HarborRacer/Apply Scene Look")]
        public static string Build()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var log = new System.Text.StringBuilder();

            // sky: 6-sided day sky "4", exposure 1.11, rotation 5
            var sky = AssetDatabase.LoadAssetAtPath<Material>(RefDir + "/Sky/4.mat");
            if (sky != null) { RenderSettings.skybox = sky; log.Append("sky "); }
            else log.Append("SKY MISSING ");

            // ambient: flat 0.78 grey
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.7830189f, 0.78055656f, 0.78055656f, 1f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = 0.123f;                      // target m_ReflectionIntensity

            // fog: exponential squared, very light
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.8018868f, 0.79180014f, 0.79180014f, 1f);
            RenderSettings.fogDensity = 0.0004f;

            // sun: target light intensity 2.9, white, rotation (45, 194.3, 0)
            foreach (var l in Object.FindObjectsByType<Light>())
            {
                if (l.type != LightType.Directional) continue;
                l.intensity = 2.9f; l.color = Color.white; l.shadows = LightShadows.Soft;
                l.transform.rotation = Quaternion.Euler(45f, 194.3f, 0f);
                RenderSettings.sun = l;
                log.Append("sun ");
            }

            // post-processing volume
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            Override<Bloom>(profile, b => { b.threshold.Override(1f); b.intensity.Override(0.8f); b.scatter.Override(0.6f); });   // target intensity 1.9 floods our brighter realtime lighting
            Override<ColorAdjustments>(profile, c => { c.saturation.Override(28f); c.contrast.Override(8f); });
            Override<Tonemapping>(profile, t => t.mode.Override(TonemappingMode.Neutral));
            EditorUtility.SetDirty(profile);
            var old = GameObject.Find("PostFX");
            if (old != null) Object.DestroyImmediate(old);
            var go = new GameObject("PostFX");
            var vol = go.AddComponent<Volume>(); vol.isGlobal = true; vol.sharedProfile = profile; vol.priority = 1f;
            log.Append("postfx ");

            // sea: the RealWater shader graph material
            var water = FindWaterMaterial();
            int seas = 0;
            if (water != null)
                foreach (var r in Object.FindObjectsByType<Renderer>())
                {
                    var mats = r.sharedMaterials; bool changed = false;
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i] != null && mats[i].name == "Water_Placeholder") { mats[i] = water; changed = true; seas++; }
                    if (changed) r.sharedMaterials = mats;
                }
            log.Append($"water({seas}) ");

            log.Append($"trackShaders({ApplyTrackShaders()}) ");
            log.Append($"zLift({ApplyDepthLifts()}) ");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return "scene look applied: " + log;
        }

        /// <summary>
        /// The toy-rail meshes and start-grid strips lie exactly on top of the road / asphalt / ground meshes (coplanar), which z-fights and shows as
        /// flicker on the track. Raise them a few hundredths of a unit; applied once per object (ZLift marks it).
        /// </summary>
        static int ApplyDepthLifts()
        {
            var track = GameObject.Find("Track");
            if (track == null) return 0;
            int n = 0;
            foreach (var r in track.GetComponentsInChildren<MeshRenderer>(true))
            {
                float lift = 0f;
                if (r.name.Contains("Rail3__ct_rails")) lift = 0.05f;
                else if (r.name.Contains("CmnStartGrid2")) lift = 0.04f;
                else if (r.name.Contains("road03_seal")) lift = 0.02f;
                if (lift <= 0f || r.GetComponent<HarborRacer.Environment.ZLift>() != null) continue;
                r.transform.position += Vector3.up * lift;
                r.gameObject.AddComponent<HarborRacer.Environment.ZLift>().lift = lift;
                n++;
            }
            return n;
        }

        /// <summary>Anti-gravity strips and the start gate banner use the GravityPanel / ToadHarborStartBoard shader graphs.</summary>
        static int ApplyTrackShaders()
        {
            var gravity = AssetDatabase.LoadAssetAtPath<Material>(RefDir + "/Models/TrackShaders/Toad Harbor/GravityPanel.shadergraph");
            var board = AssetDatabase.LoadAssetAtPath<Material>(RefDir + "/Models/TrackShaders/Toad Harbor/StartLineBoard.mat");
            var track = GameObject.Find("Track");
            if (track == null) return 0;
            int n = 0;
            foreach (var r in track.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials; bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i]; if (m == null || !m.HasProperty("_BaseMap")) continue;
                    var tex = m.GetTexture("_BaseMap"); if (tex == null) continue;
                    if (gravity != null && tex.name == "ef_gravitypanell_alb") { mats[i] = gravity; changed = true; n++; }
                    else if (board != null && tex.name == "ct_startgate_alb") { mats[i] = board; changed = true; n++; }
                }
                if (changed) r.sharedMaterials = mats;
            }
            return n;
        }

        static Material FindWaterMaterial()
        {
            foreach (var g in AssetDatabase.FindAssets("Water t:Material", new[] { RefDir }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                if (m != null && m.shader != null && m.shader.name.Contains("RealWater") && m.name == "Water") return m;
            }
            return null;
        }

        static void Override<T>(VolumeProfile p, System.Action<T> set) where T : VolumeComponent
        {
            if (!p.TryGet<T>(out var c)) c = p.Add<T>(true);
            c.active = true;
            set(c);
        }
    }
}
