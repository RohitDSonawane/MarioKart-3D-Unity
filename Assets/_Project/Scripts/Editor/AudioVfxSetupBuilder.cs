using System.IO;
using System.Linq;
using HarborRacer.Audio;
using HarborRacer.Kart;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarborRacer.EditorTools
{
    public static class AudioVfxSetupBuilder
    {
        public const string SfxLibraryPath = "Assets/_Project/Audio/SfxLibrary.asset";

        static readonly string[] AllCharacters = { "Mario", "Luigi", "Peach", "Toad", "Waluigi", "Shyguy", "BabyMario", "Lemmy" };

        [MenuItem("Tools/HarborRacer/Build Audio & SFX Library")]
        public static string BuildSfxLibrary()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SfxLibraryPath));
            var lib = AssetDatabase.LoadAssetAtPath<SfxLibrary>(SfxLibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<SfxLibrary>();
                AssetDatabase.CreateAsset(lib, SfxLibraryPath);
            }

            // Music
            lib.courseIntro = LoadClip("Assets/_Project/Audio/Music/CourseIntro.mp3");
            lib.toadHarbor = LoadClip("Assets/_Project/Audio/Music/ToadHarbor.mp3");
            lib.toadHarborFinalLap = LoadClip("Assets/_Project/Audio/Music/ToadHarborFinalLap.mp3");
            lib.finalLapStinger = LoadClip("Assets/_Project/Audio/Music/FinalLapStinger.mp3");
            lib.starTheme = LoadClip("Assets/_Project/Audio/Music/StarTheme.wav");
            lib.firstPlaceFinish = LoadClip("Assets/_Project/Audio/Music/FirstPlaceFinish.mp3");
            lib.goodResults = LoadClip("Assets/_Project/Audio/Music/GoodResults.mp3");
            lib.badResults = LoadClip("Assets/_Project/Audio/Music/BadResults.mp3");

            // Engine
            lib.engineIdle = LoadClip("Assets/_Project/Audio/SFX/Engine/EngineIdle.wav");
            lib.engineAccel = LoadClip("Assets/_Project/Audio/SFX/Engine/EngineAccel.wav");
            lib.engineRevStart = LoadClip("Assets/_Project/Audio/SFX/Engine/EngineRevStart.wav");

            // Kart SFX
            lib.driftHop = LoadClip("Assets/_Project/Audio/SFX/Kart/DriftHop.wav");
            lib.driftSqueal = LoadClip("Assets/_Project/Audio/SFX/Kart/DriftSqueal.wav");
            lib.driftSteer = LoadClip("Assets/_Project/Audio/SFX/Kart/DriftSteer.wav");
            lib.boostBurst = LoadClip("Assets/_Project/Audio/SFX/Kart/BoostBurst.wav");
            lib.jumpPanel = LoadClip("Assets/_Project/Audio/SFX/Kart/JumpPanel.wav");
            lib.gliderOpen = LoadClip("Assets/_Project/Audio/SFX/Kart/GliderOpen.wav");
            lib.gliderClose = LoadClip("Assets/_Project/Audio/SFX/Kart/GliderClose.wav");
            lib.gliderFlutter = LoadClip("Assets/_Project/Audio/SFX/Kart/GliderFlutter.wav");
            lib.landing = LoadClip("Assets/_Project/Audio/SFX/Kart/Landing.wav");
            lib.wallCrash = LoadClip("Assets/_Project/Audio/SFX/Kart/WallCrash.wav");
            lib.kartBump = LoadClip("Assets/_Project/Audio/SFX/Kart/KartBump.wav");
            lib.hoverStart = LoadClip("Assets/_Project/Audio/SFX/Kart/HoverStart.wav");
            lib.hoverFinish = LoadClip("Assets/_Project/Audio/SFX/Kart/HoverFinish.wav");
            lib.hoverCharge = LoadClip("Assets/_Project/Audio/SFX/Kart/HoverCharge.wav");

            // Items
            lib.rouletteTick = LoadClip("Assets/_Project/Audio/SFX/Items/RouletteTick.wav");
            lib.rouletteStop = LoadClip("Assets/_Project/Audio/SFX/Items/RouletteStop.wav");
            lib.warningBeep = LoadClip("Assets/_Project/Audio/SFX/Items/WarningBeep.wav");
            lib.coinPickup = LoadClip("Assets/_Project/Audio/SFX/Items/CoinPickup.wav");
            lib.explosion = LoadClip("Assets/_Project/Audio/SFX/Items/Explosion.mp3");
            lib.bombFuse = LoadClip("Assets/_Project/Audio/SFX/Items/BombFuse.mp3");
            lib.bulletStart = LoadClip("Assets/_Project/Audio/SFX/Items/BulletStart.wav");
            lib.bulletLoop = LoadClip("Assets/_Project/Audio/SFX/Items/BulletLoop.wav");
            lib.bulletEnd = LoadClip("Assets/_Project/Audio/SFX/Items/BulletEnd.wav");
            lib.crateBreak = LoadClip("Assets/_Project/Audio/SFX/Items/CrateBreak.wav");

            // Race
            lib.countdown321 = LoadClip("Assets/_Project/Audio/SFX/Race/Countdown321.wav");
            lib.countdownGo = LoadClip("Assets/_Project/Audio/SFX/Race/CountdownGo.wav");

            // Mario Voices
            lib.marioBoost = new[]
            {
                LoadClip("Assets/_Project/Audio/SFX/Voices/Mario/Boost1.wav"),
                LoadClip("Assets/_Project/Audio/SFX/Voices/Mario/Boost2.wav"),
                LoadClip("Assets/_Project/Audio/SFX/Voices/Mario/Boost3.wav"),
                LoadClip("Assets/_Project/Audio/SFX/Voices/Mario/Boost4.wav"),
                LoadClip("Assets/_Project/Audio/SFX/Voices/Mario/Boost5.wav"),
                LoadClip("Assets/_Project/Audio/SFX/Voices/Mario/Boost6.wav"),
            };
            lib.marioTrick = new[]
            {
                LoadClip("Assets/_Project/Audio/SFX/Voices/Mario/Trick1.wav"),
                LoadClip("Assets/_Project/Audio/SFX/Voices/Mario/Trick2.wav"),
                LoadClip("Assets/_Project/Audio/SFX/Voices/Mario/Trick3.wav"),
            };
            lib.marioHurt = new[]
            {
                LoadClip("Assets/_Project/Audio/SFX/Voices/Mario/Hurt1.wav"),
                LoadClip("Assets/_Project/Audio/SFX/Voices/Mario/Hurt2.wav"),
                LoadClip("Assets/_Project/Audio/SFX/Voices/Mario/Hurt3.wav"),
            };
            lib.marioRocketStart = LoadClip("Assets/_Project/Audio/SFX/Voices/Mario/RocketStart.wav");
            lib.marioWin = LoadClip("Assets/_Project/Audio/SFX/Voices/Mario/Win.wav");
            lib.marioLose = LoadClip("Assets/_Project/Audio/SFX/Voices/Mario/Lose.wav");

            // Ambient
            lib.trolleyMove = LoadClip("Assets/_Project/Audio/SFX/Ambient/TrolleyMove.wav");
            lib.seaWaves = LoadClip("Assets/_Project/Audio/SFX/Ambient/SeaWaves.wav");
            lib.toadCheers = new[]
            {
                LoadClip("Assets/_Project/Audio/SFX/Ambient/ToadCheer1.wav"),
                LoadClip("Assets/_Project/Audio/SFX/Ambient/ToadCheer2.wav"),
                LoadClip("Assets/_Project/Audio/SFX/Ambient/ToadCheer3.wav"),
            };

            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            return $"SfxLibrary built at {SfxLibraryPath}";
        }

        [MenuItem("Tools/HarborRacer/Build Character Materials")]
        public static string BuildCharacterMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            int created = 0;

            foreach (var charName in AllCharacters)
            {
                string charDir = $"Assets/_Project/Art/Characters/{charName}";
                string texDir = $"{charDir}/Textures";
                string matDir = $"{charDir}/Materials";
                if (!Directory.Exists(texDir)) continue;

                Directory.CreateDirectory(matDir);

                // Find primary albedo texture
                Texture2D mainTex = null;
                foreach (var file in Directory.GetFiles(texDir))
                {
                    if (file.EndsWith(".meta")) continue;
                    string n = Path.GetFileName(file).ToLower();
                    if (n.Contains("alb") && !n.Contains("eye") && !n.Contains("pupil"))
                    {
                        mainTex = AssetDatabase.LoadAssetAtPath<Texture2D>(file);
                        break;
                    }
                }
                if (mainTex == null)
                {
                    // Fallback to any png
                    foreach (var file in Directory.GetFiles(texDir))
                    {
                        if (file.EndsWith(".meta")) continue;
                        if (file.ToLower().EndsWith(".png"))
                        {
                            mainTex = AssetDatabase.LoadAssetAtPath<Texture2D>(file);
                            break;
                        }
                    }
                }

                string matPath = $"{matDir}/{charName}_Body.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(shader) { name = $"{charName}_Body" };
                    AssetDatabase.CreateAsset(mat, matPath);
                    created++;
                }
                if (mainTex != null)
                {
                    mat.SetTexture("_BaseMap", mainTex);
                }
                mat.SetFloat("_Smoothness", 0.35f);
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
            return $"Character materials updated (new: {created})";
        }

        /// <summary>
        /// Hands on the wheel: wires the character FBX's own pose clips into a DriverPoseAnimator (matched by name, the names differ per character).
        /// </summary>
        static void AddPoseAnimator(GameObject driverRoot, GameObject model, string fbxPath)
        {
            var clips = new System.Collections.Generic.List<AnimationClip>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (o is AnimationClip c && !c.name.StartsWith("__preview__")) clips.Add(c);
            string Short(AnimationClip c) { int i = c.name.IndexOf('|'); return (i >= 0 ? c.name.Substring(i + 1) : c.name).ToLowerInvariant(); }
            AnimationClip Find(System.Func<string, bool> test) => clips.Find(c => test(Short(c)));
            bool Item(string n) => n.Contains("item");
            bool Left(string n) => n.Contains("left");
            bool Right(string n) => n.Contains("right");

            var pa = driverRoot.AddComponent<DriverPoseAnimator>();
            pa.animRoot = model.transform;
            pa.idle = Find(n => !Item(n) && !n.Contains("with") && (n.EndsWith("idle") || n == "normal"));
            pa.turnLeft = Find(n => n.EndsWith("turnleft") && !Item(n) && !n.Contains("with") && !n.Contains("boost"));
            pa.turnRight = Find(n => n.EndsWith("turnright") && !Item(n) && !n.Contains("with") && !n.Contains("boost"));
            pa.itemIdle = Find(n => n.EndsWith("itemidle") || n.EndsWith("itemnormal") || n.EndsWith("normalitem") || n == "normalwithitem");
            pa.itemTurnLeft = Find(n => Left(n) && n.Contains("turn") && (Item(n) || n.Contains("withitem")) && !n.Contains("boost"));
            pa.itemTurnRight = Find(n => Right(n) && n.Contains("turn") && (Item(n) || n.Contains("withitem")) && !n.Contains("boost"));
            pa.throwForward = Find(n => n.Contains("throwforward"));
            pa.throwBack = Find(n => n.Contains("throwback"));
            // reaction clips exist in Mario's FBX only (the never made them for the other drivers)
            pa.hurt = Find(n => n == "hitbyopponent");
            pa.hitItem = Find(n => n == "hitopponentitem");
            pa.reverse = Find(n => n == "reverse");
            pa.jumpTrick1 = Find(n => n == "jumptrick1");
            pa.jumpTrick2 = Find(n => n == "jumptrick2");
            pa.gliderTrick = Find(n => n == "glidertrick");
            pa.firstPlace = Find(n => n == "firstplace");
            pa.loseAnim = Find(n => n == "loseanim");
            if (pa.idle == null) Debug.LogWarning("DriverPoseAnimator: no idle clip found in " + fbxPath);
        }

        public static GameObject MountDriver(Transform visual, string charName)
        {
            // Remove existing driver if any
            var old = visual.Find("Driver");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            string fbxPath = $"Assets/_Project/Art/Characters/{charName}/{charName}.fbx";
            var fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxAsset == null)
            {
                Debug.LogWarning($"Driver FBX not found: {fbxPath}");
                return null;
            }

            var driverRoot = new GameObject("Driver");
            driverRoot.transform.SetParent(visual, false);
            // Center in the kart seat
            driverRoot.transform.localPosition = new Vector3(0f, 0.15f, -0.05f);
            driverRoot.transform.localRotation = Quaternion.identity;
            driverRoot.transform.localScale = Vector3.one * 0.95f;

            var model = Object.Instantiate(fbxAsset, driverRoot.transform);
            model.name = charName;

            // 1. Purge embedded duplicate kart meshes and items from character model
            var toDestroy = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in model.GetComponentsInChildren<Transform>(true))
            {
                if (child == model.transform) continue;
                string n = child.name;
                if (n.StartsWith("BodyK", System.StringComparison.OrdinalIgnoreCase) ||
                    (n.StartsWith("Item", System.StringComparison.OrdinalIgnoreCase) && n != "ItemBone") ||
                    n == "Banana" || n == "Bullet" || n == "Coin" || n == "GoldenMushroom" ||
                    n == "GreenShell" || n == "Mushroom" || n == "RedShell")
                {
                    toDestroy.Add(child.gameObject);
                }
            }
            foreach (var go in toDestroy)
            {
                if (go != null) Object.DestroyImmediate(go);
            }

            // 2. Apply body and eye materials
            string bodyMatPath = $"Assets/_Project/Art/Characters/{charName}/Materials/{charName}_Body.mat";
            string eyeMatPath = $"Assets/_Project/Art/Characters/{charName}/Materials/{charName}_Eye.mat";
            var bodyMat = AssetDatabase.LoadAssetAtPath<Material>(bodyMatPath);
            var eyeMat = AssetDatabase.LoadAssetAtPath<Material>(eyeMatPath);

            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                string rName = r.gameObject.name;
                Material targetMat = bodyMat;
                if (rName.IndexOf("Eye", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    rName.IndexOf("Pupil", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (eyeMat != null) targetMat = eyeMat;
                }
                if (targetMat != null)
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = targetMat;
                    r.sharedMaterials = mats;
                }
            }

            // 3. Add DriverVisuals and assign animated eye textures
            var dv = driverRoot.AddComponent<DriverVisuals>();
            AddPoseAnimator(driverRoot, model, fbxPath);
            driverRoot.AddComponent<HarborRacer.Items.HandItemDisplay>();      // held item in the hand (the ItemBone is kept above)
            string texDir = $"Assets/_Project/Art/Characters/{charName}/Textures";
            if (Directory.Exists(texDir))
            {
                var eyeTextures = new System.Collections.Generic.List<Texture>();
                for (int i = 0; i <= 10; i++)
                {
                    string[] matches = Directory.GetFiles(texDir, $"*eye*alb.{i}.png", SearchOption.TopDirectoryOnly);
                    // keep the slot even when a character lacks the texture: the index is the expression (Face enum)
                    Texture2D tex = matches.Length > 0 ? AssetDatabase.LoadAssetAtPath<Texture2D>(matches[0].Replace("\\", "/")) : null;
                    eyeTextures.Add(tex);
                }
                if (eyeTextures.Exists(x => x != null))
                {
                    dv.faceTextures = eyeTextures.ToArray();
                }
            }

            SetLayerRecursive(driverRoot, visual.gameObject.layer);
            return driverRoot;
        }

        public static void SetupSceneAudio(Scene scene)
        {
            var oldAudio = GameObject.Find("Audio");
            if (oldAudio != null) Object.DestroyImmediate(oldAudio);

            var audioRoot = new GameObject("Audio");
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(audioRoot, scene);
            }

            var sfxLib = AssetDatabase.LoadAssetAtPath<SfxLibrary>(SfxLibraryPath);

            var svc = audioRoot.AddComponent<AudioService>();
            svc.library = sfxLib;

            var music = audioRoot.AddComponent<MusicController>();
            music.library = sfxLib;

            // Ambient sea sound near the harbor
            if (sfxLib != null && sfxLib.seaWaves != null)
            {
                var seaGo = new GameObject("Ambient_Sea");
                seaGo.transform.SetParent(audioRoot.transform, false);
                seaGo.transform.position = new Vector3(300f, -110f, 200f);
                var seaSrc = seaGo.AddComponent<AudioSource>();
                seaSrc.clip = sfxLib.seaWaves;
                seaSrc.loop = true;
                seaSrc.spatialBlend = 1f;
                seaSrc.rolloffMode = AudioRolloffMode.Linear;
                seaSrc.minDistance = 30f;
                seaSrc.maxDistance = 250f;
                seaSrc.volume = 0.6f;
                seaSrc.playOnAwake = true;
                seaSrc.Play();
            }

            // Ambient Toad cheers near start grid
            if (sfxLib != null && sfxLib.toadCheers != null && sfxLib.toadCheers.Length > 0 && sfxLib.toadCheers[0] != null)
            {
                var cheerGo = new GameObject("Ambient_ToadCheer");
                cheerGo.transform.SetParent(audioRoot.transform, false);
                cheerGo.transform.position = new Vector3(191f, -108f, 323f);
                var cheerSrc = cheerGo.AddComponent<AudioSource>();
                cheerSrc.clip = sfxLib.toadCheers[0];
                cheerSrc.loop = true;
                cheerSrc.spatialBlend = 1f;
                cheerSrc.rolloffMode = AudioRolloffMode.Linear;
                cheerSrc.minDistance = 15f;
                cheerSrc.maxDistance = 120f;
                cheerSrc.volume = 0.5f;
                cheerSrc.playOnAwake = true;
                cheerSrc.Play();
            }
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        static AudioClip LoadClip(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) Debug.LogWarning($"AudioClip not found at {path}");
            return clip;
        }

        [MenuItem("Tools/HarborRacer/Verify Phase 11")]
        public static string VerifyPhase11()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/ToadHarbor.unity", OpenSceneMode.Single);
            var audioService = Object.FindAnyObjectByType<AudioService>();
            var musicCtrl = Object.FindAnyObjectByType<MusicController>();
            var sfxLib = AssetDatabase.LoadAssetAtPath<SfxLibrary>(SfxLibraryPath);
            
            var player = GameObject.Find("Kart");
            var kAudio = player != null ? player.GetComponent<KartAudio>() : null;
            var kEffects = player != null ? player.GetComponent<KartEffects>() : null;
            var dVisuals = player != null ? player.GetComponentInChildren<DriverVisuals>() : null;

            var aiControllers = Object.FindObjectsByType<HarborRacer.Inputs.PathDriverInput>();
            int aiWithAudio = 0;
            int aiWithDriver = 0;
            foreach (var ai in aiControllers)
            {
                if (ai.GetComponent<KartAudio>() != null) aiWithAudio++;
                if (ai.GetComponentInChildren<DriverVisuals>() != null) aiWithDriver++;
            }

            var trolleys = Object.FindObjectsByType<HarborRacer.Hazards.Trolley>();
            int trolleyAudio = 0;
            foreach (var tr in trolleys)
            {
                if (tr.GetComponent<AudioSource>() != null) trolleyAudio++;
            }

            string roots = string.Join(", ", scene.GetRootGameObjects().Select(g => g.name));
            string kartComps = player != null ? string.Join(", ", player.GetComponents<Component>().Select(c => c.GetType().Name)) : "none";

            string report = $"[Phase11Report] Scene={scene.name} AudioService={(audioService!=null)} MusicController={(musicCtrl!=null)} " +
                            $"SfxLib={(sfxLib!=null)} PlayerKart={(player!=null)} PlayerAudio={(kAudio!=null)} " +
                            $"PlayerEffects={(kEffects!=null)} PlayerDriver={(dVisuals!=null)} " +
                            $"AI_Total={aiControllers.Length} AI_Audio={aiWithAudio} AI_Driver={aiWithDriver} " +
                            $"Trolleys={trolleys.Length} TrolleyAudio={trolleyAudio}\n" +
                            $"Roots: {roots}\n" +
                            $"KartComps: {kartComps}";
            File.WriteAllText("Temp/Phase11Report.txt", report);
            Debug.Log(report);
            return report;
        }
    }
}
