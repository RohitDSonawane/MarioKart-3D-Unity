using System.IO;
using HarborRacer.CameraRig;
using HarborRacer.Inputs;
using HarborRacer.Kart;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HarborRacer.EditorTools
{
    /// <summary>
    /// Builds Prefabs/Kart/Kart.prefab from the player layout and drops a drivable kart + chase camera
    /// into the Toad Harbor scene. Idempotent.
    /// </summary>
    public static class KartPrefabBuilder
    {
        const string BodyFbx = "Assets/_Project/Art/Kart/Body/StandardKart.fbx";
        const string MatDir = "Assets/_Project/Art/Kart/Body/Materials";
        const string StatsPath = "Assets/_Project/ScriptableObjects/KartStats/DefaultKartStats.asset";
        const string PrefabPath = "Assets/_Project/Prefabs/Kart/Kart.prefab";
        public const string AiPrefabPath = "Assets/_Project/Prefabs/Kart/KartAI.prefab";

        [MenuItem("Tools/HarborRacer/Build Kart Prefab")]
        public static string BuildPrefab()
        {
            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory(Path.GetDirectoryName(StatsPath));
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));

            // ---- stats asset ----
            var stats = AssetDatabase.LoadAssetAtPath<KartStats>(StatsPath);
            if (stats == null)
            {
                stats = ScriptableObject.CreateInstance<KartStats>();
                AssetDatabase.CreateAsset(stats, StatsPath);
            }

            // ---- body materials + remap onto the FBX ----
            var red = Lit("Kart_Body_Red", new Color(0.86f, 0.12f, 0.1f), 0.75f, 0.35f);
            var grey = Lit("Kart_Body_Grey", new Color(0.82f, 0.82f, 0.85f), 0.6f, 0.5f);
            var tireMat = Lit("Kart_Tire", new Color(0.07f, 0.07f, 0.08f), 0.2f, 0f);
            var rimMat = Lit("Kart_Rim", new Color(0.75f, 0.75f, 0.8f), 0.85f, 0.8f);
            if (AssetImporter.GetAtPath(BodyFbx) is ModelImporter mi)
            {
                mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "m_Body"), red);
                mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "m_Body.002"), grey);
                mi.SaveAndReimport();
            }

            // ---- hierarchy ----
            var root = new GameObject("Kart");
            root.layer = LayerMask.NameToLayer("Kart");
            root.transform.localScale = Vector3.one * 0.8f;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 50f;
            rb.linearDamping = 0f;
            rb.angularDamping = 2000f;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var col = root.AddComponent<SphereCollider>();
            col.radius = 1.6427898f;
            col.center = new Vector3(0f, 0.40987968f, -0.09f);

            var probe = Child(root.transform, "GroundProbe", new Vector3(-0.01f, -0.38f, 0.27f));

            // SpinPivot sits at the kart's visual centre; KartHitReaction spins it. Visual (hop, drift yaw) is its child.
            // BodyAnim (identity) carries the body clips (KartBodyAnimator) and is the parent of SpinPivot.
            var bodyAnim = Child(root.transform, "BodyAnim", Vector3.zero);
            var spinPivot = Child(bodyAnim, "SpinPivot", new Vector3(0f, 0f, -0.5f));
            var visual = Child(spinPivot, "Visual", new Vector3(0f, -0.83f, 0f));
            visual.localScale = Vector3.one * 1.5f;
            var bodyAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BodyFbx);
            var body = (GameObject)PrefabUtility.InstantiatePrefab(bodyAsset, visual);
            body.name = "Body";
            body.layer = root.layer;

            // wheels (positions from the Tires container, in Visual space)
            var wheelsRoot = Child(visual, "Wheels", Vector3.zero);
            var pivots = new Transform[2];
            var spinners = new Transform[4];
            (string name, Vector3 pos, bool front)[] wheels =
            {
                ("FL", new Vector3(-0.58f, 0.0f, 0.464f), true),
                ("FR", new Vector3(0.58f, 0.0f, 0.469f), true),
                ("BL", new Vector3(-0.54f, 0.0f, -0.621f), false),
                ("BR", new Vector3(0.555f, 0.0f, -0.621f), false),
            };
            for (int i = 0; i < 4; i++)
            {
                var pivot = Child(wheelsRoot, "Wheel_" + wheels[i].name, wheels[i].pos);
                if (wheels[i].front) pivots[i < 2 ? i : 0] = pivot;
                var spin = Child(pivot, "Spin", Vector3.zero);
                spinners[i] = spin;
                var tire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tire.name = "Tire";
                tire.transform.SetParent(spin, false);
                tire.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                tire.transform.localScale = new Vector3(0.54f, 0.14f, 0.54f);
                tire.GetComponent<Renderer>().sharedMaterial = tireMat;
                Object.DestroyImmediate(tire.GetComponent<Collider>());
                var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rim.name = "Rim";
                rim.transform.SetParent(spin, false);
                rim.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                rim.transform.localScale = new Vector3(0.3f, 0.15f, 0.3f);
                rim.GetComponent<Renderer>().sharedMaterial = rimMat;
                Object.DestroyImmediate(rim.GetComponent<Collider>());
            }
            SetLayerRecursive(visual.gameObject, root.layer);

            // spawn anchors
            Child(root.transform, "ShellSpawnFront", new Vector3(-0.01f, -0.22f, 3.79f));
            Child(root.transform, "ShellSpawnBack", new Vector3(0.01f, -0.22f, -4.01f)).localRotation = Quaternion.Euler(0, 180, 0);
            Child(root.transform, "BananaSpawn", new Vector3(-0.01f, 1.35f, 2.47f)).localRotation = Quaternion.Euler(0, 180, 0);
            Child(root.transform, "CoinSpawn", new Vector3(0f, 0.67f, -0.29f));

            // components
            var input = root.AddComponent<PlayerKartInput>();
            var kart = root.AddComponent<KartController>();
            kart.stats = stats;
            kart.groundProbe = probe;
            var boost = root.AddComponent<KartBoost>();
            var drift = root.AddComponent<KartDrift>();
            drift.kart = kart; drift.boost = boost; drift.visual = visual;
            kart.TurnStrength = stats.turnStrength;
            root.AddComponent<KartTrick>().kart = kart;
            var hitReaction = root.AddComponent<KartHitReaction>();
            hitReaction.kart = kart; hitReaction.spinPivot = spinPivot;
            AddBodyAnimator(root, bodyAnim, kart);
            root.AddComponent<KartZoneSensor>().kart = kart;
            root.AddComponent<KartRespawn>().kart = kart;
            root.AddComponent<KartAntiGravity>().kart = kart;
            root.AddComponent<HarborRacer.Race.RacerProgress>();
            root.AddComponent<HarborRacer.Race.RocketStart>().kart = kart;
            root.AddComponent<HarborRacer.Items.CoinWallet>();
            root.AddComponent<HarborRacer.Items.ItemEntourage>();
            root.AddComponent<HarborRacer.Items.ItemHolder>();

            // glider wings (closed = scaled to zero; KartGlider opens them)
            var glider = root.AddComponent<KartGlider>();
            glider.kart = kart;
            glider.gliderVisual = BuildGliderVisual(visual, root.layer);

            // particle anchors (root space): rear wheels for drift sparks, rear exhaust for boost flames
            var fx = root.AddComponent<KartEffects>();
            fx.kart = kart;
            fx.additiveMaterial = GetOrCreateAdditiveMaterial();
            fx.sparkLeft = Child(root.transform, "SparkAnchorL", new Vector3(-0.85f, -1.15f, -1.45f));
            fx.sparkRight = Child(root.transform, "SparkAnchorR", new Vector3(0.85f, -1.15f, -1.45f));
            fx.exhaustLeft = Child(root.transform, "ExhaustAnchorL", new Vector3(-0.35f, -0.45f, -1.95f));
            fx.exhaustRight = Child(root.transform, "ExhaustAnchorR", new Vector3(0.35f, -0.45f, -1.95f));
            fx.exhaustLeft.localRotation = Quaternion.Euler(0f, 180f, 0f);   // local +Z points backwards
            fx.exhaustRight.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var vis = root.AddComponent<KartVisuals>();
            vis.kart = kart;
            vis.playerInput = input;
            vis.frontSteerPivots = new[] { FindWheel(wheelsRoot, "FL"), FindWheel(wheelsRoot, "FR") };
            vis.spinningWheels = spinners;

            // Audio & VFX setup
            var sfxLib = AssetDatabase.LoadAssetAtPath<HarborRacer.Audio.SfxLibrary>(AudioVfxSetupBuilder.SfxLibraryPath);
            var audio = root.AddComponent<HarborRacer.Audio.KartAudio>();
            audio.kart = kart;
            audio.library = sfxLib;
            audio.racer = root.GetComponent<HarborRacer.Race.RacerProgress>();

            // Apply Mario livery and driver for player kart
            CharacterAndKartAssetBuilder.ApplyCharacterAndKartLivery(root, "Mario");

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);

            // AI variant: same kart, driven by a path follower instead of the keyboard; remove player driver (added per opponent)
            var driver = visual.Find("Driver");
            if (driver != null) Object.DestroyImmediate(driver.gameObject);
            Object.DestroyImmediate(root.GetComponent<PlayerKartInput>());
            vis.playerInput = null;
            var aiInput = root.AddComponent<HarborRacer.Inputs.PathDriverInput>();
            aiInput.kart = kart;
            PrefabUtility.SaveAsPrefabAsset(root, AiPrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return $"Kart prefab saved: {AssetDatabase.GetAssetPath(prefab)}";
        }

        const string BodyClipDir = "Assets/_Project/Art/Kart/Anims/";

        static void AddBodyAnimator(GameObject root, Transform bodyAnim, KartController kart)
        {
            AnimationClip Clip(string n) => AssetDatabase.LoadAssetAtPath<AnimationClip>(BodyClipDir + n + ".anim");
            var ba = root.AddComponent<KartBodyAnimator>();
            ba.kart = kart; ba.animRoot = bodyAnim;
            ba.shellHit = Clip("ShellHit"); ba.bananaHit = Clip("BananaHit");
            ba.collideLeft = Clip("NormalCollide"); ba.collideRight = Clip("NormalCollideRight");
            ba.startBoost = Clip("StartBoost"); ba.startTurbo = Clip("KartStartTurbo");
            ba.glideFlip1 = Clip("GliderFlip1"); ba.glideFlip2 = Clip("GliderFlip2");
            ba.antiGravSpinLeft = Clip("AnigravitySpinLeft"); ba.antiGravSpinRight = Clip("AnigravitySpinRight");
        }

        [MenuItem("Tools/HarborRacer/Place Kart In Scenes")]
        public static string PlaceInScenes()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return "Kart prefab missing; run Build Kart Prefab first.";
            var sb = new System.Text.StringBuilder();

            // Toad Harbor: slot 0 (Mario) from the imported markers
            var th = EditorSceneManager.OpenScene("Assets/_Project/Scenes/ToadHarbor.unity", OpenSceneMode.Single);
            var slot = GameObject.Find("Markers/StartGrid/Slot_0_Mario");
            Vector3 p = slot ? slot.transform.position : new Vector3(191.1f, -108.7f, 323.2f);
            Quaternion r = slot ? slot.transform.rotation : Quaternion.identity;
            sb.AppendLine(Place(th, p, r));
            EditorSceneManager.SaveScene(th);
            return sb.ToString();
        }

        static string Place(UnityEngine.SceneManagement.Scene scene, Vector3 pos, Quaternion rot)
        {
            // remove previous player/camera
            foreach (var n in new[] { "Kart", "CameraRig", "Main Camera", "KartDebug" })
            {
                var old = GameObject.Find(n);
                if (old) Object.DestroyImmediate(old);
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var kart = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            kart.name = "Kart";
            kart.tag = "Player";
            var progress = kart.GetComponent<HarborRacer.Race.RacerProgress>();
            progress.racerName = "Mario"; progress.isPlayer = true;
            kart.transform.SetPositionAndRotation(pos, rot);

            var rig = new GameObject("CameraRig");
            var chase = rig.AddComponent<KartCameraRig>();
            chase.target = kart.transform;

            var dbg = new GameObject("KartDebug");
            dbg.AddComponent<KartDebugHUD>().kart = kart.GetComponent<KartController>();

            // Scene Audio Setup (AudioService, MusicController, ambient sources)
            AudioVfxSetupBuilder.SetupSceneAudio(scene);
            return $"{scene.name}: kart placed at {pos}";
        }

        // ---- helpers ----
        const string GliderFbx = "Assets/_Project/Art/Kart/Glider/Glider.fbx";
        const string VfxDir = "Assets/_Project/Art/VFX";

        static Transform BuildGliderVisual(Transform visual, int layer)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(GliderFbx);
            if (asset == null) return null;
            var wing = Lit("Glider_Wing", new Color(0.9f, 0.12f, 0.1f), 0.5f, 0f);
            var frame = Lit("Glider_Frame", new Color(0.85f, 0.85f, 0.9f), 0.6f, 0.6f);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, visual);
            go.name = "Glider";
            // the model is ~30 units wide; scale to ~2.7x the kart width and perch it above and behind the seat
            go.transform.localScale = Vector3.one * 0.1f;
            go.transform.localPosition = new Vector3(0f, 0.55f, -0.25f);
            go.transform.localRotation = Quaternion.identity;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = mats[i] != null && mats[i].name.Contains("Wing") ? wing : frame;
                r.sharedMaterials = mats;
            }
            foreach (var a in go.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            SetLayerRecursive(go, layer);
            return go.transform;
        }

        static Material GetOrCreateAdditiveMaterial()
        {
            Directory.CreateDirectory(VfxDir);
            string texPath = VfxDir + "/SoftDot.png";
            if (!File.Exists(texPath))
            {
                var t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f;
                        float a = Mathf.Clamp01(1f - d); a *= a;
                        t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                File.WriteAllBytes(texPath, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(texPath);
            }
            string matPath = VfxDir + "/Kart_Additive.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m == null)
            {
                var shader = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Sprites/Default");
                m = new Material(shader) { name = "Kart_Additive" };
                AssetDatabase.CreateAsset(m, matPath);
            }
            m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Transform FindWheel(Transform wheelsRoot, string n) => wheelsRoot.Find("Wheel_" + n);

        static Transform Child(Transform parent, string name, Vector3 localPos)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            return g.transform;
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        static Material Lit(string name, Color c, float smooth, float metal)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
