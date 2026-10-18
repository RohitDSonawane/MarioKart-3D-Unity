using HarborRacer.Inputs;
using HarborRacer.Kart;
using HarborRacer.Race;
using HarborRacer.Track;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HarborRacer.EditorTools
{
    /// <summary>
    /// Phase 6 scene setup for Toad Harbor (run after the markers and the player kart are in the scene):
    /// binds checkpoints and the finish line, creates the RaceManager, and spawns the 7 opponents on the start grid.
    /// </summary>
    public static class RaceSetupBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/ToadHarbor.unity";

        // grid slot name (from the scene setup) -> AI path object name under Markers/Paths/AI.
        // Only the Toad and Baby Mario lines stay on the ground route: the other five detour over the sky glide section, which has no collision in the imported mesh.
        static readonly (string slot, string racer, string path, Color tint)[] Opponents =
        {
            ("Luigi", "Luigi", "ComputerDriverPATH Toad", new Color(0.1f, 0.65f, 0.2f)),
            ("Peach", "Peach", "ComputerDriverPATHBaby Mario", new Color(1f, 0.55f, 0.75f)),
            ("Toad", "Toad", "ComputerDriverPATH Toad", new Color(0.25f, 0.45f, 0.95f)),
            ("Waluigi", "Waluigi", "ComputerDriverPATHBaby Mario", new Color(0.45f, 0.15f, 0.65f)),
            ("Shyguy", "Shy Guy", "ComputerDriverPATH Toad", new Color(0.95f, 0.35f, 0.1f)),
            ("Baby Mario", "Baby Mario", "ComputerDriverPATHBaby Mario", new Color(0.95f, 0.75f, 0.1f)),
            ("Lemmy", "Lemmy", "ComputerDriverPATH Toad", new Color(0.2f, 0.85f, 0.85f)),
        };

        [MenuItem("Tools/HarborRacer/Build Race Setup (Toad Harbor)")]
        public static string Build()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var markers = GameObject.Find("Markers");
            if (markers == null) return "Markers missing: run MarkerImporter first.";

            // --- checkpoints ---
            var cpRoot = markers.transform.Find("Checkpoints");
            if (!cpRoot.TryGetComponent<CheckpointTrack>(out var track)) track = cpRoot.gameObject.AddComponent<CheckpointTrack>();
            track.points.Clear();
            for (int i = 0; i < cpRoot.childCount; i++)
            {
                var cp = cpRoot.GetChild(i);
                track.points.Add(cp);
                var z = cp.GetComponent<CheckpointZone>();
                if (z == null) z = cp.gameObject.AddComponent<CheckpointZone>();
                z.index = i;
            }

            // --- finish line: the first NextLapCollider is the real one; the second is a stray at the high section ---
            var lapRoot = markers.transform.Find("LapLines");
            for (int i = 0; i < lapRoot.childCount; i++)
            {
                var line = lapRoot.GetChild(i);
                if (i == 0)
                {
                    if (line.GetComponent<LapLineZone>() == null) line.gameObject.AddComponent<LapLineZone>();
                    track.finishLine = line;
                }
                else line.gameObject.SetActive(false);
            }

            // --- race manager + HUD ---
            var old = GameObject.Find("Race");
            if (old) Object.DestroyImmediate(old);
            var raceGo = new GameObject("Race");
            var rm = raceGo.AddComponent<RaceManager>();
            raceGo.AddComponent<RaceDebugHUD>();
            rm.totalLaps = 3;
            var endPath = markers.transform.Find("Paths/MarioRaceEndPath");
            rm.raceEndPath = endPath != null ? endPath.GetComponent<TrackPath>() : null;

            // --- opponents ---
            foreach (var o in GameObject.FindObjectsByType<RacerProgress>())
                if (!o.isPlayer) Object.DestroyImmediate(o.gameObject);
            var aiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KartPrefabBuilder.AiPrefabPath);
            var grid = markers.transform.Find("StartGrid");
            int spawned = 0;
            foreach (var (slotName, racerName, pathName, tint) in Opponents)
            {
                Transform slot = null;
                foreach (Transform s in grid) if (s.name.EndsWith("_" + slotName)) slot = s;
                var pathT = markers.transform.Find("Paths/AI/" + pathName);
                if (slot == null || pathT == null || aiPrefab == null) { Debug.LogWarning($"Skipping {racerName}: slot/path/prefab missing"); continue; }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(aiPrefab, scene);
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                go.name = racerName;
                go.tag = "Opponent";
                go.transform.SetPositionAndRotation(slot.position, slot.rotation);
                var prog = go.GetComponent<RacerProgress>();
                prog.racerName = racerName; prog.isPlayer = false;
                var driver = go.GetComponent<PathDriverInput>();
                driver.path = pathT.GetComponent<TrackPath>();
                driver.altPaths = new[]
                {
                    markers.transform.Find("Paths/AI/ComputerDriverPATH Toad").GetComponent<TrackPath>(),
                    markers.transform.Find("Paths/AI/ComputerDriverPATHBaby Mario").GetComponent<TrackPath>(),
                };
                driver.racerAI = true;
                driver.profile = AIProfile.ForName(racerName);
                driver.SnapToPath();
                string charId = slotName.Replace(" ", "");
                CharacterAndKartAssetBuilder.ApplyCharacterAndKartLivery(go, charId);
                var audio = go.GetComponent<HarborRacer.Audio.KartAudio>();
                if (audio != null) audio.racer = prog;
                spawned++;
            }

            // Ensure player kart has Mario livery and clean driver
            var player = GameObject.Find("Kart");
            if (player != null)
            {
                CharacterAndKartAssetBuilder.ApplyCharacterAndKartLivery(player, "Mario");
            }

            AudioVfxSetupBuilder.SetupSceneAudio(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return $"Race setup: {track.Count} checkpoints, finish line bound, {spawned} opponents, laps={rm.totalLaps}";
        }

        /// <summary>Give each AI kart its own body colour (instance-level property block on the red body renderers).</summary>
        static void Tint(GameObject kart, Color c)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", c);
            foreach (var r in kart.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && mats[i].name == "Kart_Body_Red") { r.SetPropertyBlock(block, i); }
            }
        }
    }
}
