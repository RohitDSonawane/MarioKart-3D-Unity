using System;
using System.Collections.Generic;
using HarborRacer.Environment;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HarborRacer.EditorTools
{
    /// <summary>
    /// Target match pass: the 187 animated spectator Toads of the Toad Harbor scene, at their exact positions, with their
    /// own colour (pink / blue / green / yellow albedo) and animation (Toad1 = Wave, Toad2 = Jump, from the same TOAD model clips).
    /// Replaces the earlier hand-placed procedural crowd. Placement data comes from toadharbor_markers.json (ToadSpectator props).
    /// </summary>
    public static class SpectatorBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/ToadHarbor.unity";
        const string FbxPath = "Assets/_Project/Art/Track/Props/Toads/TOAD.fbx";
        const string MatDir = "Assets/_Project/Materials/Track/Props/Toads/";
        const string AnimDir = "Assets/_Project/Art/Shared/Animators";
        const string JsonPath = "Assets/_Project/Data/toadharbor_markers.json";

        [Serializable] class P { public string model; public float[] pos, rot, scale; public int ctrl; public string color; }
        [Serializable] class D { public List<P> props; }

        /// <summary>Target instance scale (1.2) is applied times this; tune if the crowd looks too big or small next to the karts.</summary>
        public static float ScaleFactor = 1f;

        [MenuItem("Tools/HarborRacer/Place Spectator Toads")]
        public static string Build()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbx == null) return "TOAD.fbx missing";
            var data = JsonUtility.FromJson<D>(System.IO.File.ReadAllText(JsonPath));

            foreach (var n in new[] { "Spectators" })
            {
                var old = GameObject.Find(n);
                if (old != null) UnityEngine.Object.DestroyImmediate(old);
            }
            var root = new GameObject("Spectators");

            var wave = GetController("ToadWave", "Wave");
            var jump = GetController("ToadJump", "Jump");
            var mats = new Dictionary<string, Material>
            {
                ["b"] = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "Mat_Toad_Blue.mat"),
                ["g"] = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "Mat_Toad_Green.mat"),
                ["p"] = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "Mat_Toad_Pink.mat"),
                ["y"] = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "Mat_Toad_Yellow.mat"),
            };

            int count = 0;
            foreach (var p in data.props)
            {
                if (p.model != "ToadSpectator") continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
                go.name = $"Toad_{count:000}_{p.color}{p.ctrl}";
                go.transform.position = new Vector3(p.pos[0], p.pos[1], p.pos[2]);
                go.transform.rotation = new Quaternion(p.rot[0], p.rot[1], p.rot[2], p.rot[3]);
                go.transform.localScale = new Vector3(p.scale[0], p.scale[1], p.scale[2]) * ScaleFactor;
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = LayerMask.NameToLayer("Scenery");
                if (mats.TryGetValue(p.color ?? "p", out var m) && m != null)
                    foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.sharedMaterial = m;
                var anim = go.GetComponent<Animator>();
                if (anim == null) anim = go.AddComponent<Animator>();
                anim.runtimeAnimatorController = p.ctrl == 2 ? jump : wave;
                go.AddComponent<LoopAnimator>();
                // 187 crowd members drawn into every shadow cascade cost far more than their tiny shadows add
                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                count++;
            }
            int snapped = SnapToVisibleGround(root.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return $"spectators placed: {count}, snapped to the ground: {snapped}";
        }

        /// <summary>
        /// Several target props (lawns, walkways, awnings) sit a little differently in our imported track, which left Toads hovering 2 to 7 u
        /// above the visible surface. Temporary mesh colliders are put on every visible mesh that has none, each Toad is dropped onto the
        /// first surface below it (within 8 u), then the temporary colliders are removed again.
        /// </summary>
        public static int SnapToVisibleGround(Transform spectators)
        {
            var temp = new List<MeshCollider>();
            foreach (var mf in UnityEngine.Object.FindObjectsByType<MeshFilter>())
            {
                if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null || mf.GetComponentInParent<Animator>() != null) continue;
                var mr = mf.GetComponent<MeshRenderer>(); if (mr == null || !mr.enabled) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; temp.Add(mc);
            }
            Physics.SyncTransforms();
            int snapped = 0;
            for (int i = 0; i < spectators.childCount; i++)
            {
                var t = spectators.GetChild(i);
                if (!Physics.Raycast(t.position + Vector3.up * 1.5f, Vector3.down, out var h, 9.5f, ~0, QueryTriggerInteraction.Ignore)) continue;
                float gap = t.position.y - h.point.y;
                if (gap > 0.3f && gap < 8f) { t.position = new Vector3(t.position.x, h.point.y, t.position.z); snapped++; }
            }
            foreach (var mc in temp) UnityEngine.Object.DestroyImmediate(mc);
            return snapped;
        }

        static AnimatorController GetController(string name, string clipName)
        {
            if (!AssetDatabase.IsValidFolder(AnimDir)) AssetDatabase.CreateFolder("Assets/_Project/Art/Shared", "Animators");
            string path = $"{AnimDir}/{name}.controller";
            var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (c != null) return c;
            AnimationClip clip = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
                if (o is AnimationClip ac && !ac.name.StartsWith("__preview__") && ac.name.EndsWith(clipName)) { clip = ac; break; }
            return clip == null ? null : AnimatorController.CreateAnimatorControllerAtPathWithClip(path, clip);
        }
    }
}
