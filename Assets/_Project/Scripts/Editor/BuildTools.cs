using System;
using System.IO;
using HarborRacer.Inputs;
using HarborRacer.Kart;
using HarborRacer.Race;
using HarborRacer.Track;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HarborRacer.EditorTools
{
    public static class BuildTools
    {
        const string MainScenePath = "Assets/_Project/Scenes/ToadHarbor.unity";
        const string BuildOutputPath = "Builds/HarborRacer_Windows/HarborRacer.exe";

        public static bool CheckPlayMode()
        {
            bool playing = EditorApplication.isPlaying;
            File.WriteAllText("Temp/PlayModeStatus.txt", playing.ToString());
            Debug.Log("[BuildTools] EditorApplication.isPlaying = " + playing);
            return playing;
        }

        [MenuItem("Tools/HarborRacer/Build Windows Player")]
        public static string BuildStandalone()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BuildOutputPath));

            // Ensure ToadHarbor is in Build Settings
            var sceneList = new[] { new EditorBuildSettingsScene(MainScenePath, true) };
            EditorBuildSettings.scenes = sceneList;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { MainScenePath },
                locationPathName = BuildOutputPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };

            Debug.Log($"[BuildTools] Starting Windows Build to {BuildOutputPath}...");
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            string reportText = $"[BuildReport] Result={summary.result} TotalTime={summary.totalTime} " +
                               $"TotalSize={summary.totalSize} Errors={summary.totalErrors} Warnings={summary.totalWarnings}";
            Debug.Log(reportText);

            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/BuildReport.txt", reportText);

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[BuildTools] Build succeeded! Executable at {BuildOutputPath}");
                return reportText;
            }
            else
            {
                string err = $"Build failed with {summary.totalErrors} errors.";
                Debug.LogError(err);
                return err;
            }
        }

        [MenuItem("Tools/HarborRacer/Run Race Simulation Test")]
        public static string RunRaceSimulationTest()
        {
            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            var rm = UnityEngine.Object.FindAnyObjectByType<RaceManager>();
            if (rm == null) return "RaceManager not found in scene.";

            var karts = UnityEngine.Object.FindObjectsByType<KartController>();
            if (karts.Length == 0) return "No karts found in scene.";

            // Save original simulation mode
            var origSimMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;

            // Make player kart drive autonomously using path follower like AI for the test
            var playerKart = GameObject.Find("Kart");
            PathDriverInput testPlayerDriver = null;
            if (playerKart != null)
            {
                var toadPath = GameObject.Find("Markers/Paths/AI/ComputerDriverPATH Toad")?.GetComponent<TrackPath>();
                testPlayerDriver = playerKart.AddComponent<PathDriverInput>();
                testPlayerDriver.kart = playerKart.GetComponent<KartController>();
                testPlayerDriver.path = toadPath;
                testPlayerDriver.racerAI = true;
                testPlayerDriver.SnapToPath();
            }

            int lapsCompleted = 0;
            int totalSteps = 1000; // 20 simulated seconds
            float dt = 0.02f;

            try
            {
                for (int step = 0; step < totalSteps; step++)
                {
                    // Tick inputs and physics
                    foreach (var k in karts)
                    {
                        if (k == null) continue;
                        var driver = k.GetComponent<PathDriverInput>();
                        if (driver != null) driver.Tick(dt);
                    }

                    Physics.Simulate(dt);

                    foreach (var r in UnityEngine.Object.FindObjectsByType<RacerProgress>())
                    {
                        if (r.Lap > 1) lapsCompleted++;
                    }
                }
            }
            finally
            {
                Physics.simulationMode = origSimMode;
                if (testPlayerDriver != null) UnityEngine.Object.DestroyImmediate(testPlayerDriver);
                // Reload scene clean without saving to avoid leaking test state
                EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            }

            string testReport = $"[SimulationTest] Simulated {totalSteps} steps (20s) across {karts.Length} karts. LapsCompletedCounter={lapsCompleted}. No crashes or null references.";
            Debug.Log(testReport);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/SimulationTestReport.txt", testReport);
            return testReport;
        }
    }
}
