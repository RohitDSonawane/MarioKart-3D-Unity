using UnityEditor;
using UnityEngine;

namespace HarborRacer.EditorTools
{
    /// <summary>
    /// Target HUD assets (fonts, plate / flag sprites, character map icons) must live under Resources so the runtime-built HUD can load them in
    /// a player build. Moves the staged copies there and sets sprite import settings. Idempotent.
    /// </summary>
    public static class HudAssets
    {
        const string Res = "Assets/_Project/Resources/UI";

        [MenuItem("Tools/HarborRacer/Prepare HUD Assets")]
        public static string Prepare()
        {
            Folder("Assets/_Project/Resources/UI/Fonts"); Folder("Assets/_Project/Resources/UI/Map"); Folder("Assets/_Project/Resources/UI/Plates");
            int moved = 0;
            foreach (var f in new[] { "DS-DIGIT.TTF", "FOT-NewRodin Pro B.otf" }) moved += Move("Assets/_Project/Art/Shared/Fonts/" + f, Res + "/Fonts/" + f);
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Project/Art/Shared/Map" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                moved += Move(p, Res + "/Map/" + System.IO.Path.GetFileName(p));
            }
            moved += Copy("Assets/_Project/Art/UI/UIparallelogram.png", Res + "/Plates/Parallelogram.png");
            moved += Copy("Assets/_Project/Art/UI/Flag.png", Res + "/Plates/Flag.png");
            AssetDatabase.Refresh();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Res }))
            {
                var imp = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
                if (imp == null || imp.textureType == TextureImporterType.Sprite) continue;
                imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single; imp.alphaIsTransparency = true; imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }
            return $"HUD assets ready ({moved} changes)";
        }

        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        static int Move(string from, string to)
        {
            if (AssetDatabase.LoadMainAssetAtPath(from) == null || AssetDatabase.LoadMainAssetAtPath(to) != null) return 0;
            return string.IsNullOrEmpty(AssetDatabase.MoveAsset(from, to)) ? 1 : 0;
        }

        static int Copy(string from, string to)
        {
            if (AssetDatabase.LoadMainAssetAtPath(from) == null || AssetDatabase.LoadMainAssetAtPath(to) != null) return 0;
            return AssetDatabase.CopyAsset(from, to) ? 1 : 0;
        }
    }
}
