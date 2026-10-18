using HarborRacer.Items;
using UnityEditor;
using UnityEngine;

namespace HarborRacer.EditorTools
{
    public static class ItemResourceBuilder
    {
        const string ResDir = "Assets/_Project/Resources/Items";

        [MenuItem("HarborRacer/Setup/Build Item Resource Prefabs")]
        public static void BuildItemResources()
        {
            EnsureDirs();

            var items = new (ItemId id, string sourcePath, Vector3 scale)[]
            {
                (ItemId.Banana, "Assets/_Project/Art/Items/Banana/ItemBanana.dae", Vector3.one * 0.7f),
                (ItemId.GreenShell, "Assets/_Project/Art/Items/Green Red Shell/ItemKoura.dae", Vector3.one * 0.85f),
                (ItemId.RedShell, "Assets/_Project/Art/Items/Green Red Shell/ItemKoura.dae", Vector3.one * 0.85f),
                (ItemId.BlueShell, "Assets/_Project/Art/Items/BlueShell/ItemTogezo.dae", Vector3.one * 1.1f),
                (ItemId.BobOmb, "Assets/_Project/Art/Items/Bo bomb/BobombItem.fbx", Vector3.one * 0.8f),
                (ItemId.BulletBill, "Assets/_Project/Art/Items/Bullet Bill/BulletBillPlayer.fbx", Vector3.one * 1.2f),
                (ItemId.Coin, "Assets/_Project/Art/Items/Coin/ItemCoin.dae", Vector3.one * 0.8f),
                (ItemId.Mushroom, "Assets/_Project/Art/Items/Mushroom/ItemKinoko.dae", Vector3.one * 0.8f),
                (ItemId.GoldenMushroom, "Assets/_Project/Art/Items/Mushroom/ItemKinoko.dae", Vector3.one * 0.8f),
                (ItemId.Star, "Assets/_Project/Art/Items/Starman/ItemStar.dae", Vector3.one * 0.8f),
            };

            foreach (var (id, path, scale) in items)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (src == null)
                {
                    Debug.LogWarning($"Source model not found for {id} at {path}");
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(src);
                instance.name = id.ToString();
                instance.transform.localScale = scale;

                // Strip colliders and animators
                foreach (var c in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                foreach (var a in instance.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);

                // Specific material tweaks
                if (id == ItemId.RedShell)
                {
                    var redMat = ItemVisuals.Mat(new Color(0.95f, 0.15f, 0.15f));
                    foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
                    {
                        var mList = new Material[r.sharedMaterials.Length];
                        for (int m = 0; m < mList.Length; m++) mList[m] = redMat;
                        r.sharedMaterials = mList;
                    }
                }
                else if (id == ItemId.GoldenMushroom)
                {
                    var goldMat = ItemVisuals.Mat(new Color(1f, 0.85f, 0.1f), true);
                    foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
                    {
                        var mList = new Material[r.sharedMaterials.Length];
                        for (int m = 0; m < mList.Length; m++) mList[m] = goldMat;
                        r.sharedMaterials = mList;
                    }
                }

                string outPath = $"{ResDir}/{id}.prefab";
                PrefabUtility.SaveAsPrefabAsset(instance, outPath);
                Object.DestroyImmediate(instance);
                Debug.Log($"Created Resource prefab for {id} at {outPath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Item Resource Prefabs built successfully!");
        }

        static void EnsureDirs()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources"))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Resources");
            }
            if (!AssetDatabase.IsValidFolder(ResDir))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Resources", "Items");
            }
        }
    }
}
