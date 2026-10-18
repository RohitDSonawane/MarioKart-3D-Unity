using System;
using System.IO;
using HarborRacer.Kart;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HarborRacer.EditorTools
{
    public static class CharacterAndKartAssetBuilder
    {
        public struct CharacterInfo
        {
            public string id;
            public string refDir;
            public string bodyTexName;
            public string eyeTexName;
            public string kartLiveryCode;
            public string emblemName;

            public CharacterInfo(string id, string refDir, string bodyTexName, string eyeTexName, string kartLiveryCode, string emblemName)
            {
                this.id = id;
                this.refDir = refDir;
                this.bodyTexName = bodyTexName;
                this.eyeTexName = eyeTexName;
                this.kartLiveryCode = kartLiveryCode;
                this.emblemName = emblemName;
            }
        }

        public static readonly CharacterInfo[] Characters =
        {
            new CharacterInfo("Mario", "ImportSource/Models/CharacterTexturesAndAnim/MarioStuff/Mario", "mario_alb.png", "marioeye_alb.0.png", "mro", "Mario.png"),
            new CharacterInfo("Luigi", "ImportSource/Models/CharacterTexturesAndAnim/LuigiStuff/Luigi", "luigi_alb.png", "luigieye_alb.0.png", "lig", "Luigi.png"),
            new CharacterInfo("Peach", "ImportSource/Models/CharacterTexturesAndAnim/Peach/TEx", "peach_alb.png", "peacheye_alb.0.png", "pch", "Peach.png"),
            new CharacterInfo("Toad", "ImportSource/Models/CharacterTexturesAndAnim/Toad/Toad", "kinopio_alb.png", "kinopioeye_alb.0.png", "kno", "Toad.png"),
            new CharacterInfo("Waluigi", "ImportSource/Models/CharacterTexturesAndAnim/WaluigiTex/Texture", "Waluigi_Alb.png", "WaluigiEye_Alb.0.png", "wlg", "Waluigi.png"),
            new CharacterInfo("Shyguy", "ImportSource/Models/CharacterTexturesAndAnim/Shy Guy Stuff", "heyho_alb.png", "heyhoeye_alb.png", "hyh00", "Shy Guy.png"),
            new CharacterInfo("BabyMario", "ImportSource/Models/CharacterTexturesAndAnim/Baby Mario/Tex", "bbmario_alb.png", "bbmarioeye_alb.0.png", "mrob", "Baby Mario.png"),
            new CharacterInfo("Lemmy", "ImportSource/Models/CharacterTexturesAndAnim/Lemmy/Tex", "lemmy_alb.png", "lemmyeye_alb.0.png", "lmy", "Lemmy.png")
        };

        const string RefKartMaterialsDir = "ImportSource/Models/Kart/Materials";
        const string RefEmblemsDir = "ImportSource/Models/Kart/Emblems";

        public const string ProjectKartTexturesDir = "Assets/_Project/Art/Kart/Body/Textures";
        public const string ProjectKartEmblemsDir = "Assets/_Project/Art/Kart/Body/Emblems";
        public const string ProjectKartMaterialsDir = "Assets/_Project/Art/Kart/Body/Materials";

        public static CharacterInfo? FindCharacter(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string clean = name.Replace(" ", "").ToLowerInvariant();
            foreach (var c in Characters)
            {
                if (c.id.ToLowerInvariant() == clean) return c;
            }
            return null;
        }

        [MenuItem("Tools/HarborRacer/Build All Character & Kart Assets")]
        public static string BuildAllAssets()
        {
            var sb = new System.Text.StringBuilder();

            // 1. Copy target assets into project
            int copied = CopyAssets();
            sb.AppendLine($"Copied {copied} textures/emblems from the import source.");

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 2. Configure textures
            ConfigureTextureImporters();

            // 3. Create materials for characters
            int charMats = BuildCharacterMaterials();
            sb.AppendLine($"Created/updated character materials: {charMats}");

            // 4. Create materials for karts (livery + emblems)
            int kartMats = BuildKartMaterials();
            sb.AppendLine($"Created/updated kart materials: {kartMats}");

            AssetDatabase.SaveAssets();

            // 5. Update scene karts in ToadHarbor
            string sceneResult = UpdateSceneKarts("Assets/_Project/Scenes/ToadHarbor.unity");
            sb.AppendLine(sceneResult);

            return sb.ToString();
        }

        public static int CopyAssets()
        {
            int count = 0;
            Directory.CreateDirectory(ProjectKartTexturesDir);
            Directory.CreateDirectory(ProjectKartEmblemsDir);
            Directory.CreateDirectory(ProjectKartMaterialsDir);

            // Copy Kart common textures
            string[] kartCommon = { "bodyk_std_nrm.png", "bodyk_std_a_spm.png", "bodyk_std_emm.0.png" };
            foreach (var fn in kartCommon)
            {
                string src = Path.Combine(RefKartMaterialsDir, fn);
                string dst = Path.Combine(ProjectKartTexturesDir, fn);
                if (File.Exists(src))
                {
                    File.Copy(src, dst, true);
                    count++;
                }
            }

            // Copy character textures and kart liveries/emblems
            foreach (var c in Characters)
            {
                // Character textures
                string charTexDst = $"Assets/_Project/Art/Characters/{c.id}/Textures";
                Directory.CreateDirectory(charTexDst);

                if (Directory.Exists(c.refDir))
                {
                    foreach (var file in Directory.GetFiles(c.refDir, "*.png"))
                    {
                        string destFile = Path.Combine(charTexDst, Path.GetFileName(file));
                        File.Copy(file, destFile, true);
                        count++;
                    }
                }

                // Kart livery
                string liveryName = $"bodyk_std_{c.kartLiveryCode}_alb.png";
                string liverySrc = Path.Combine(RefKartMaterialsDir, liveryName);
                if (File.Exists(liverySrc))
                {
                    File.Copy(liverySrc, Path.Combine(ProjectKartTexturesDir, liveryName), true);
                    count++;
                }

                // Kart emblem
                string emblemSrc = Path.Combine(RefEmblemsDir, c.emblemName);
                if (File.Exists(emblemSrc))
                {
                    File.Copy(emblemSrc, Path.Combine(ProjectKartEmblemsDir, c.emblemName), true);
                    count++;
                }
            }

            return count;
        }

        public static void ConfigureTextureImporters()
        {
            // Configure normal maps
            foreach (var c in Characters)
            {
                string charTexDst = $"Assets/_Project/Art/Characters/{c.id}/Textures";
                if (!Directory.Exists(charTexDst)) continue;

                foreach (var file in Directory.GetFiles(charTexDst, "*nrm*.png"))
                {
                    string assetPath = file.Replace("\\", "/");
                    var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (importer != null && importer.textureType != TextureImporterType.NormalMap)
                    {
                        importer.textureType = TextureImporterType.NormalMap;
                        importer.SaveAndReimport();
                    }
                }
            }

            // Kart normal map
            string kartNrm = $"{ProjectKartTexturesDir}/bodyk_std_nrm.png";
            var kartNrmImp = AssetImporter.GetAtPath(kartNrm) as TextureImporter;
            if (kartNrmImp != null && kartNrmImp.textureType != TextureImporterType.NormalMap)
            {
                kartNrmImp.textureType = TextureImporterType.NormalMap;
                kartNrmImp.SaveAndReimport();
            }

            // Emblem textures: ensure alpha is transparency and clamp wrap mode
            if (Directory.Exists(ProjectKartEmblemsDir))
            {
                foreach (var file in Directory.GetFiles(ProjectKartEmblemsDir, "*.png"))
                {
                    string assetPath = file.Replace("\\", "/");
                    var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (importer != null)
                    {
                        importer.alphaIsTransparency = true;
                        importer.wrapMode = TextureWrapMode.Clamp;
                        importer.SaveAndReimport();
                    }
                }
            }
        }

        public static int BuildCharacterMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            int count = 0;

            foreach (var c in Characters)
            {
                string charDir = $"Assets/_Project/Art/Characters/{c.id}";
                string texDir = $"{charDir}/Textures";
                string matDir = $"{charDir}/Materials";
                Directory.CreateDirectory(matDir);

                // Body material
                string bodyMatPath = $"{matDir}/{c.id}_Body.mat";
                var bodyMat = AssetDatabase.LoadAssetAtPath<Material>(bodyMatPath);
                if (bodyMat == null)
                {
                    bodyMat = new Material(shader) { name = $"{c.id}_Body" };
                    AssetDatabase.CreateAsset(bodyMat, bodyMatPath);
                    count++;
                }

                var bodyTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/{c.bodyTexName}");
                if (bodyTex != null)
                {
                    bodyMat.SetTexture("_BaseMap", bodyTex);
                }
                bodyMat.SetColor("_BaseColor", Color.white);

                // Normal map for body if exists
                string bodyNrmName = c.bodyTexName.ToLower().Replace("alb", "nrm");
                var bodyNrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/{bodyNrmName}");
                if (bodyNrm != null)
                {
                    bodyMat.SetTexture("_BumpMap", bodyNrm);
                    bodyMat.EnableKeyword("_NORMALMAP");
                }

                bodyMat.SetFloat("_Smoothness", 0.35f);
                EditorUtility.SetDirty(bodyMat);

                // Eye material
                string eyeMatPath = $"{matDir}/{c.id}_Eye.mat";
                var eyeMat = AssetDatabase.LoadAssetAtPath<Material>(eyeMatPath);
                if (eyeMat == null)
                {
                    eyeMat = new Material(shader) { name = $"{c.id}_Eye" };
                    AssetDatabase.CreateAsset(eyeMat, eyeMatPath);
                    count++;
                }

                var eyeTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/{c.eyeTexName}");
                if (eyeTex != null)
                {
                    eyeMat.SetTexture("_BaseMap", eyeTex);
                }
                eyeMat.SetColor("_BaseColor", Color.white);
                eyeMat.SetFloat("_Smoothness", 0.5f);
                EditorUtility.SetDirty(eyeMat);
            }

            return count;
        }

        public static int BuildKartMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            int count = 0;

            var kartNrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ProjectKartTexturesDir}/bodyk_std_nrm.png");
            var kartSpm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ProjectKartTexturesDir}/bodyk_std_a_spm.png");

            foreach (var c in Characters)
            {
                // 1. Kart Livery Material
                string kartMatPath = $"{ProjectKartMaterialsDir}/Mat_Kart_{c.id}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(kartMatPath);
                if (mat == null)
                {
                    mat = new Material(shader) { name = $"Mat_Kart_{c.id}" };
                    AssetDatabase.CreateAsset(mat, kartMatPath);
                    count++;
                }

                string liveryTexPath = $"{ProjectKartTexturesDir}/bodyk_std_{c.kartLiveryCode}_alb.png";
                var liveryTex = AssetDatabase.LoadAssetAtPath<Texture2D>(liveryTexPath);
                if (liveryTex != null)
                {
                    mat.SetTexture("_BaseMap", liveryTex);
                }
                mat.SetColor("_BaseColor", Color.white);
                if (kartNrm != null)
                {
                    mat.SetTexture("_BumpMap", kartNrm);
                    mat.EnableKeyword("_NORMALMAP");
                }
                if (kartSpm != null)
                {
                    mat.SetTexture("_MetallicGlossMap", kartSpm);
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
                mat.SetFloat("_Smoothness", 0.75f);
                mat.SetFloat("_Metallic", 0.35f);
                EditorUtility.SetDirty(mat);

                // 2. Kart Emblem Material (Transparent decal on hood)
                string emblemMatPath = $"{ProjectKartMaterialsDir}/Mat_Emblem_{c.id}.mat";
                var emblemMat = AssetDatabase.LoadAssetAtPath<Material>(emblemMatPath);
                if (emblemMat == null)
                {
                    emblemMat = new Material(shader) { name = $"Mat_Emblem_{c.id}" };
                    AssetDatabase.CreateAsset(emblemMat, emblemMatPath);
                    count++;
                }

                // Configure transparent surface
                emblemMat.SetFloat("_Surface", 1f); // 1 = Transparent
                emblemMat.SetFloat("_Blend", 0f);   // 0 = Alpha
                emblemMat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                emblemMat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                emblemMat.SetFloat("_ZWrite", 0f);
                emblemMat.renderQueue = (int)RenderQueue.Transparent;
                emblemMat.SetOverrideTag("RenderType", "Transparent");
                emblemMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                emblemMat.SetColor("_BaseColor", Color.white);

                string emblemTexPath = $"{ProjectKartEmblemsDir}/{c.emblemName}";
                var emblemTex = AssetDatabase.LoadAssetAtPath<Texture2D>(emblemTexPath);
                if (emblemTex != null)
                {
                    emblemMat.SetTexture("_BaseMap", emblemTex);
                    emblemMat.SetTextureOffset("_BaseMap", new Vector2(0f, 0.89f));
                    emblemMat.SetTextureScale("_BaseMap", new Vector2(1f, 1f));
                }
                emblemMat.SetFloat("_Smoothness", 0.6f);
                EditorUtility.SetDirty(emblemMat);
            }

            return count;
        }

        public static string UpdateSceneKarts(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid()) return $"Scene {scenePath} could not be opened.";

            var greyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Kart/Body/Materials/Kart_Body_Grey.mat");

            // 1. Player Kart (Mario)
            var playerKart = GameObject.Find("Kart");
            if (playerKart != null)
            {
                ApplyCharacterAndKartLivery(playerKart, "Mario", greyMat);
            }

            // 2. Opponent Karts
            var opponents = GameObject.FindObjectsByType<HarborRacer.Race.RacerProgress>();
            foreach (var opp in opponents)
            {
                if (opp.isPlayer) continue;
                var info = FindCharacter(opp.racerName);
                string charId = info.HasValue ? info.Value.id : opp.racerName.Replace(" ", "");
                ApplyCharacterAndKartLivery(opp.gameObject, charId, greyMat);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return $"Successfully updated scene karts in {scenePath}";
        }

        public static void ApplyCharacterAndKartLivery(GameObject kartGo, string charId, Material greyMat = null)
        {
            if (greyMat == null)
            {
                greyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Kart/Body/Materials/Kart_Body_Grey.mat");
            }

            var info = FindCharacter(charId);
            string resolvedId = info.HasValue ? info.Value.id : charId;

            var kartMat = AssetDatabase.LoadAssetAtPath<Material>($"{ProjectKartMaterialsDir}/Mat_Kart_{resolvedId}.mat");
            var emblemMat = AssetDatabase.LoadAssetAtPath<Material>($"{ProjectKartMaterialsDir}/Mat_Emblem_{resolvedId}.mat");

            var visual = kartGo.transform.Find("BodyAnim/SpinPivot/Visual");
            if (visual == null) visual = kartGo.transform.Find("SpinPivot/Visual");
            if (visual == null) visual = kartGo.transform.Find("Visual");
            if (visual == null) return;

            // 1. Kart Body renderers under Visual/Body
            var bodyTransform = visual.Find("Body");
            if (bodyTransform != null)
            {
                foreach (var r in bodyTransform.GetComponentsInChildren<Renderer>(true))
                {
                    string n = r.gameObject.name;
                    Material target = kartMat;
                    if (n.Contains(".001"))
                    {
                        // Emblem plate
                        target = emblemMat;
                    }
                    else if (n.Contains("Layer_3"))
                    {
                        // Engine / underside
                        target = greyMat;
                    }

                    if (target != null)
                    {
                        var mats = r.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++) mats[i] = target;
                        r.sharedMaterials = mats;
                    }
                }
            }

            // 2. Driver under Visual/Driver
            AudioVfxSetupBuilder.MountDriver(visual, resolvedId);
        }
    }
}
