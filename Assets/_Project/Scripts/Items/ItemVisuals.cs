using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HarborRacer.Items
{
    /// <summary>
    /// Phase 11 item visual provider: instantiates real 3D item models from Assets/_Project/Art/Items
    /// with fallback to clean primitives.
    /// </summary>
    public static class ItemVisuals
    {
        static readonly Dictionary<Color, Material> Cache = new Dictionary<Color, Material>();

        public static void ResetStatics() { Cache.Clear(); }

        public static Material Mat(Color c, bool emissive = false)
        {
            var key = emissive ? new Color(c.r, c.g, c.b, 0.5f) : c;
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
            {
                var mats = Resources.FindObjectsOfTypeAll<Material>();
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && mats[i].shader != null && mats[i].shader.isSupported)
                    {
                        shader = mats[i].shader;
                        break;
                    }
                }
            }
            if (shader == null) shader = Shader.Find("Sprites/Default");
            m = new Material(shader) { color = c, name = "Item_" + ColorUtility.ToHtmlStringRGB(c) };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (emissive && m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 1.5f); }
            Cache[key] = m;
            return m;
        }

        public static Transform Make(PrimitiveType shape, Color c, Vector3 scale, Transform parent = null, bool emissive = false)
        {
            var go = GameObject.CreatePrimitive(shape);
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }
            go.GetComponent<Renderer>().sharedMaterial = Mat(c, emissive);
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;
            go.layer = Layers.Item;
            return go.transform;
        }

        public static Transform MakeItemModel(ItemId id, Transform parent)
        {
            var baseId = id switch
            {
                ItemId.TripleBanana => ItemId.Banana,
                ItemId.TripleGreen => ItemId.GreenShell,
                ItemId.TripleRed => ItemId.RedShell,
                ItemId.TripleMushroom => ItemId.Mushroom,
                _ => id
            };

            // 1. Try Resources (works at runtime in standalone player builds)
            var resPrefab = Resources.Load<GameObject>($"Items/{baseId}");
            if (resPrefab != null)
            {
                var go = Object.Instantiate(resPrefab, parent);
                go.name = id.ToString();
                go.layer = Layers.Item;
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                return go.transform;
            }

#if UNITY_EDITOR
            // 2. Editor fallback via AssetDatabase
            string path = ModelPath(id);
            if (!string.IsNullOrEmpty(path))
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset != null)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
                    go.name = id.ToString();
                    go.layer = Layers.Item;
                    foreach (var c in go.GetComponentsInChildren<Collider>(true))
                    {
                        if (Application.isPlaying) Object.Destroy(c);
                        else Object.DestroyImmediate(c);
                    }
                    foreach (var a in go.GetComponentsInChildren<Animator>(true))
                    {
                        if (Application.isPlaying) Object.Destroy(a);
                        else Object.DestroyImmediate(a);
                    }
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localRotation = Quaternion.identity;
                    go.transform.localScale = ModelScale(id);

                    // Material adjustments for shells
                    if (id == ItemId.RedShell || id == ItemId.TripleRed)
                    {
                        var redMat = Mat(new Color(0.9f, 0.15f, 0.15f));
                        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                        {
                            var mats = r.sharedMaterials;
                            for (int i = 0; i < mats.Length; i++) mats[i] = redMat;
                            r.sharedMaterials = mats;
                        }
                    }
                    return go.transform;
                }
            }
#endif
            // 3. Fallback to clean primitive
            var shape = id switch
            {
                ItemId.Banana or ItemId.TripleBanana => PrimitiveType.Capsule,
                _ => PrimitiveType.Sphere
            };
            var scale = id switch
            {
                ItemId.Banana or ItemId.TripleBanana => new Vector3(0.45f, 0.5f, 0.45f),
                ItemId.BlueShell => new Vector3(1.3f, 1.3f, 1.3f),
                ItemId.BobOmb => new Vector3(1.1f, 1.1f, 1.1f),
                _ => Vector3.one * 0.9f
            };
            return Make(shape, ItemInfo.Tint(id), scale, parent);
        }

        static string ModelPath(ItemId id) => id switch
        {
            ItemId.Banana or ItemId.TripleBanana => "Assets/_Project/Art/Items/Banana/ItemBanana.dae",
            ItemId.GreenShell or ItemId.TripleGreen => "Assets/_Project/Art/Items/Green Red Shell/ItemKoura.dae",
            ItemId.RedShell or ItemId.TripleRed => "Assets/_Project/Art/Items/Green Red Shell/ItemKoura.dae",
            ItemId.BlueShell => "Assets/_Project/Art/Items/BlueShell/ItemTogezo.dae",
            ItemId.BobOmb => "Assets/_Project/Art/Items/Bo bomb/BobombItem.fbx",
            ItemId.BulletBill => "Assets/_Project/Art/Items/Bullet Bill/BulletBillPlayer.fbx",
            ItemId.Coin => "Assets/_Project/Art/Items/Coin/ItemCoin.dae",
            ItemId.Mushroom or ItemId.TripleMushroom or ItemId.GoldenMushroom => "Assets/_Project/Art/Items/Mushroom/ItemKinoko.dae",
            ItemId.Star => "Assets/_Project/Art/Items/Starman/ItemStar.dae",
            _ => null
        };

        static Vector3 ModelScale(ItemId id) => id switch
        {
            ItemId.Banana or ItemId.TripleBanana => Vector3.one * 0.7f,
            ItemId.GreenShell or ItemId.TripleGreen => Vector3.one * 0.85f,
            ItemId.RedShell or ItemId.TripleRed => Vector3.one * 0.85f,
            ItemId.BlueShell => Vector3.one * 1.1f,
            ItemId.BobOmb => Vector3.one * 0.8f,
            ItemId.BulletBill => Vector3.one * 1.2f,
            ItemId.Coin => Vector3.one * 0.8f,
            ItemId.Mushroom or ItemId.TripleMushroom or ItemId.GoldenMushroom => Vector3.one * 0.8f,
            ItemId.Star => Vector3.one * 0.8f,
            _ => Vector3.one
        };

        public static void Destroy(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }
    }
}
