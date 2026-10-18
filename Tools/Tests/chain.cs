using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HarborRacer.EditorTools;

public static class Chain
{
    public static string Run()
    {
        var sb = new StringBuilder();
        var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/ToadHarbor.unity", OpenSceneMode.Single);
        foreach (var go in scene.GetRootGameObjects())
            if (go.name.StartsWith("Carried_") || go.name == "Trolley(Clone)") { sb.AppendLine("removed stray " + go.name); Object.DestroyImmediate(go); }
        sb.AppendLine("ItemPrefabs: " + ItemPrefabFixer.Fix());
        sb.AppendLine("HudAssets: " + HudAssets.Prepare());
        sb.AppendLine("MarkerImporter: " + MarkerImporter.Import());
        sb.AppendLine("BuildPrefab: " + KartPrefabBuilder.BuildPrefab());
        sb.AppendLine("PlaceInScenes: " + KartPrefabBuilder.PlaceInScenes());
        sb.AppendLine("RaceSetup: " + RaceSetupBuilder.Build());
        sb.AppendLine("Hazards: " + HazardBuilder.Build());
        sb.AppendLine("ItemBoxes: " + ItemBoxBuilder.Build());
        sb.AppendLine("Ui: " + UiBuilder.Build());
        sb.AppendLine("Look: " + SceneLookBuilder.Build());
        sb.AppendLine("Toads: " + SpectatorBuilder.Build());
        sb.AppendLine("Ambient: " + AmbientAudioBuilder.Build());
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        sb.AppendLine("CHAIN DONE");
        Debug.Log("CHAIN DONE");
        return sb.ToString();
    }
}
