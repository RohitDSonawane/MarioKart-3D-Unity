using UnityEditor;
using UnityEditor.SceneManagement;
public static class Reload2 { public static string Run() { EditorSceneManager.OpenScene("Assets/_Project/Scenes/ToadHarbor.unity", OpenSceneMode.Single); return "reloaded"; } }
