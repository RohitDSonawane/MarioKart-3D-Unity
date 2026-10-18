using UnityEditor;
using UnityEngine;

namespace HarborRacer.EditorTools
{
    /// <summary>
    /// The RedShell and GoldenMushroom item prefabs under Resources/Items had empty material slots. Unity draws an empty slot with the pink
    /// error shader, so a held or thrown red shell (or a golden mushroom) near the camera showed up as a large magenta blob. Gives them the
    /// materials that go with their textures. Idempotent.
    /// </summary>
    public static class ItemPrefabFixer
    {
        const string Dir = "Assets/_Project/Resources/Items/";

        [MenuItem("Tools/HarborRacer/Fix Item Prefab Materials")]
        public static string Fix()
        {
            int fixedSlots = 0;
            fixedSlots += Assign("RedShell.prefab", "Assets/_Project/Art/Items/Green Red Shell/New Material 1.mat");
            fixedSlots += Assign("GoldenMushroom.prefab", "Assets/_Project/Art/Items/Golden Mushroom/New Material.mat");
            AssetDatabase.SaveAssets();
            return "item prefab material slots fixed: " + fixedSlots;
        }

        static int Assign(string prefab, string matPath)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { Debug.LogWarning("ItemPrefabFixer: material missing " + matPath); return 0; }
            var root = PrefabUtility.LoadPrefabContents(Dir + prefab);
            int n = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] == null) { mats[i] = mat; changed = true; n++; }
                if (changed) r.sharedMaterials = mats;
            }
            if (n > 0) PrefabUtility.SaveAsPrefabAsset(root, Dir + prefab);
            PrefabUtility.UnloadPrefabContents(root);
            return n;
        }
    }
}
