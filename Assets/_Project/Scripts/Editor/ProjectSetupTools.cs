using UnityEditor;
using UnityEngine;

namespace HarborRacer.EditorTools
{
    /// <summary>
    /// One-shot, idempotent project setup: layers, tags and the physics collision matrix.
    /// Run from Tools > HarborRacer > Apply Layers And Collision Matrix, or via the Unity CLI run_script command.
    /// </summary>
    public static class ProjectSetupTools
    {
        // Layer indexes 8-31 are the only user-assignable ones.
        static readonly (int index, string name)[] Layers =
        {
            (8, "Drivable"), (9, "OffRoad"), (10, "Wall"), (11, "Kart"), (12, "KartTrigger"),
            (13, "Zone"), (14, "Item"), (15, "ItemBox"), (16, "Hazard"), (17, "Scenery"), (18, "Waypoint"),
        };

        static readonly string[] Tags = { "Opponent" };

        // Layer pairs that DO collide (everything else among the project layers is ignored).
        static readonly (string a, string b)[] Collides =
        {
            ("Kart", "Drivable"), ("Kart", "OffRoad"), ("Kart", "Wall"), ("Kart", "Kart"), ("Kart", "Item"), ("Kart", "Hazard"),
            ("KartTrigger", "Zone"), ("KartTrigger", "ItemBox"),
            ("Item", "Drivable"), ("Item", "OffRoad"), ("Item", "Wall"), ("Item", "Item"), ("Item", "Hazard"), ("Item", "Zone"),
            ("Hazard", "Drivable"), ("Hazard", "OffRoad"), ("Hazard", "Wall"),
        };

        [MenuItem("Tools/HarborRacer/Apply Layers And Collision Matrix")]
        public static string Apply()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

            var layersProp = tagManager.FindProperty("layers");
            foreach (var (index, name) in Layers)
                layersProp.GetArrayElementAtIndex(index).stringValue = name;

            var tagsProp = tagManager.FindProperty("tags");
            foreach (var tag in Tags)
            {
                bool exists = false;
                for (int i = 0; i < tagsProp.arraySize; i++)
                    if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag) { exists = true; break; }
                if (!exists)
                {
                    tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                    tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
                }
            }
            tagManager.ApplyModifiedProperties();

            // Collision matrix: ignore every pair among our layers, then re-enable the allowed ones.
            foreach (var (ia, _) in Layers)
                foreach (var (ib, _) in Layers)
                    if (ia <= ib) Physics.IgnoreLayerCollision(ia, ib, true);

            foreach (var (a, b) in Collides)
                Physics.IgnoreLayerCollision(LayerMask.NameToLayer(a), LayerMask.NameToLayer(b), false);

            AssetDatabase.SaveAssets();
            return "Layers, tags and collision matrix applied.";
        }
    }
}
