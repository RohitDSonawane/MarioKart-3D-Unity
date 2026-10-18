using HarborRacer.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HarborRacer.EditorTools
{
    /// <summary>Phase 10: adds the race HUD (and pause menu, results) to the Toad Harbor scene. Run after KartPrefabBuilder.PlaceInScenes.</summary>
    public static class UiBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/ToadHarbor.unity";

        [MenuItem("Tools/HarborRacer/Build Race HUD (Toad Harbor)")]
        public static string Build()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var old = GameObject.Find("RaceUI");
            if (old != null) Object.DestroyImmediate(old);
            var go = new GameObject("RaceUI");
            go.AddComponent<RaceHud>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "Race HUD added";
        }
    }
}
