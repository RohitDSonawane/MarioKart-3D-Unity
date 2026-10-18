using HarborRacer.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HarborRacer.EditorTools
{
    /// <summary>
    /// Phase 8: turns the imported ItemBox markers into working item boxes (Zone layer, ItemBoxZone, placeholder rainbow cube).
    /// Run after MarkerImporter.Import (the importer recreates the markers).
    /// </summary>
    public static class ItemBoxBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/ToadHarbor.unity";

        [MenuItem("Tools/HarborRacer/Build Item Boxes (Toad Harbor)")]
        public static string Build()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = GameObject.Find("Markers/ItemBoxes");
            if (root == null) return "Markers/ItemBoxes missing: run MarkerImporter first.";

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "ItemBoxCube" };
            mat.SetColor("_BaseColor", new Color(0.35f, 0.75f, 1f, 1f));
            mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", new Color(0.3f, 0.6f, 1f) * 1.2f);
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Materials")) AssetDatabase.CreateFolder("Assets/_Project", "Materials");
            const string matPath = "Assets/_Project/Materials/ItemBoxCube.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing != null) { EditorUtility.CopySerialized(mat, existing); mat = existing; }
            else AssetDatabase.CreateAsset(mat, matPath);

            var rainbow = GetRainbowMaterial();
            int count = 0;
            foreach (Transform box in root.transform)
            {
                box.gameObject.layer = Layers.Zone;
                var sc = box.GetComponent<SphereCollider>();
                if (sc == null) sc = box.gameObject.AddComponent<SphereCollider>();
                sc.isTrigger = true; sc.radius = 2.2f;

                var old = box.Find("Visual");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                Transform visual = MakeBoxVisual(box, rainbow) ?? MakeCube(box, mat);

                var zone = box.GetComponent<ItemBoxZone>();
                if (zone == null) zone = box.gameObject.AddComponent<ItemBoxZone>();
                zone.visual = visual;
                count++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return $"Item boxes built: {count}";
        }

        const string PrefabPath = "Assets/_Project/Art/Items/Item Box/Itembox.prefab";
        const string GraphPath = "Assets/_Project/Art/Items/Item Box/ItemBox.shadergraph";
        const string RainbowPath = "Assets/_Project/Art/Shared/Materials/ItemBoxRainbow.mat";

        /// <summary>The rainbow ItemBox shader graph as a material (the imported prefab only carried a flat URP Lit stand-in).</summary>
        static Material GetRainbowMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(RainbowPath);
            if (m != null) return m;
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(GraphPath);
            if (shader == null) return null;
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/Shared/Materials")) AssetDatabase.CreateFolder("Assets/_Project/Art/Shared", "Materials");
            m = new Material(shader) { name = "ItemBoxRainbow" };
            AssetDatabase.CreateAsset(m, RainbowPath);
            return m;
        }

        /// <summary>Target Itembox prefab: box shell, "?" and sparkle effects, with the rainbow shader on the shell.</summary>
        static Transform MakeBoxVisual(Transform box, Material rainbow)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, box);
            go.name = "Visual";
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
            foreach (var a in go.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.Zone;
            if (rainbow != null)
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!r.name.StartsWith("ItemBox_1") && !r.name.StartsWith("ItemBoxRef")) continue;
                    r.sharedMaterial = rainbow;
                }
            return go.transform;
        }

        static Transform MakeCube(Transform box, Material mat)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Visual";
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.GetComponent<Renderer>().sharedMaterial = mat;
            cube.layer = Layers.Zone;
            cube.transform.SetParent(box, false);
            cube.transform.localPosition = Vector3.zero;
            cube.transform.localScale = Vector3.one * 2.0f;
            return cube.transform;
        }
    }
}
