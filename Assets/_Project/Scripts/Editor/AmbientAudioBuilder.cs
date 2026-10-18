using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HarborRacer.Track;

namespace HarborRacer.EditorTools
{
    /// <summary>
    /// Target ambient bed for Toad Harbor: the three shore wave loops as 3D emitters wherever the AI racing line runs beside open water,
    /// the outer-sea loop and the two city murmur loops (L / R) as quiet stereo beds. Idempotent: rebuilds the "AmbientBed" object.
    /// </summary>
    public static class AmbientAudioBuilder
    {
        const string RefDir = "ImportSource/SOunds/Toad Harbor/";
        const string Dest = "Assets/_Project/Audio/SFX/Ambient/";
        static readonly (string src, string dst)[] Files =
        {
            ("sea_shore_1_EQExcitRev.ny.32.c4.wav", "Shore1.wav"), ("sea_shore_2ExcitRev.ny.32.c4.wav", "Shore2.wav"), ("sea_shore_3_EQExcitRev.ny.32.c4.wav", "Shore3.wav"),
            ("SE_ENV_OUTER_SEA.ny.32.cn4.wav", "OuterSea.wav"), ("Demo_City_L.ny.32.wav", "CityL.wav"), ("Demo_City_R.ny.32.wav", "CityR.wav"),
        };

        [MenuItem("Tools/HarborRacer/Build Ambient Audio")]
        public static string Build()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            int copied = 0;
            foreach (var f in Files)
            {
                string from = Path.Combine(projectRoot, RefDir + f.src), to = Path.Combine(projectRoot, Dest + f.dst);
                if (File.Exists(from) && !File.Exists(to)) { File.Copy(from, to); copied++; }
            }
            AssetDatabase.Refresh();
            foreach (var f in Files)
            {
                var imp = AssetImporter.GetAtPath(Dest + f.dst) as AudioImporter;
                if (imp == null) continue;
                var s = imp.defaultSampleSettings;
                s.loadType = f.dst.StartsWith("Shore") ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.CompressedInMemory;
                s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = 0.5f;
                imp.defaultSampleSettings = s;
                imp.forceToMono = f.dst.StartsWith("Shore");              // 3D emitters are mono
                imp.SaveAndReimport();
            }

            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/ToadHarbor.unity", OpenSceneMode.Single);
            var old = GameObject.Find("AmbientBed");
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("AmbientBed");
            var audioRoot = GameObject.Find("Audio");
            if (audioRoot != null) root.transform.SetParent(audioRoot.transform, false);

            AudioClip Clip(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>(Dest + n);

            // quiet stereo beds
            Bed(root.transform, "City_L", Clip("CityL.wav"), -0.7f, 0.18f);
            Bed(root.transform, "City_R", Clip("CityR.wav"), 0.7f, 0.18f);
            Bed(root.transform, "OuterSea", Clip("OuterSea.wav"), 0f, 0.14f);

            // shore emitters beside open water along the racing line
            var pathGo = GameObject.Find("Markers/Paths/AI/ComputerDriverPATH Toad");
            int shores = 0;
            if (pathGo != null)
            {
                var path = pathGo.GetComponent<TrackPath>();
                Physics.SyncTransforms();
                var shoreClips = new[] { Clip("Shore1.wav"), Clip("Shore2.wav"), Clip("Shore3.wav") };
                var placed = new List<Vector3>();
                var candidates = new List<(Vector3 pos, float d)>();
                // shoreline points of the low harbor: no ground at the point, ground within 24 u, and the racing line within 110 u
                for (float x = -150f; x <= 750f; x += 16f)
                    for (float z = -450f; z <= 450f; z += 16f)
                    {
                        Vector3 q = new Vector3(x, -100f, z);
                        if (Physics.Raycast(q, Vector3.down, 40f, Layers.GroundMask, QueryTriggerInteraction.Ignore)) continue;           // land
                        bool shore = false;
                        foreach (var dv in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                            if (Physics.Raycast(q + dv * 22f, Vector3.down, 40f, Layers.GroundMask, QueryTriggerInteraction.Ignore)) { shore = true; break; }
                        if (!shore) continue;
                        float best = float.MaxValue;
                        for (int i = 0; i < path.Count; i++) { var np = path.GetNode(i).position; if (np.y > -60f) continue; best = Mathf.Min(best, (np - q).magnitude); }
                        if (best < 110f) candidates.Add((new Vector3(x, -110f, z), best));
                    }
                foreach (var c in candidates.OrderBy(c => c.d))
                {
                    if (shores >= 10) break;
                    bool near = false; foreach (var q in placed) if ((q - c.pos).sqrMagnitude < 110f * 110f) near = true;
                    if (near) continue;
                    placed.Add(c.pos);
                    var clip = shoreClips[shores % shoreClips.Length];
                    if (clip == null) continue;
                    var go = new GameObject("Shore_" + shores); go.transform.SetParent(root.transform, false); go.transform.position = c.pos;
                    var src = go.AddComponent<AudioSource>();
                    src.clip = clip; src.loop = true; src.playOnAwake = true; src.spatialBlend = 1f; src.rolloffMode = AudioRolloffMode.Linear;
                    src.minDistance = 12f; src.maxDistance = 110f; src.volume = 0.5f; src.dopplerLevel = 0f;
                    src.time = Random.value * clip.length;
                    shores++;
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "ambient bed built: clips copied " + copied + ", shore emitters " + shores + ", city L/R + outer sea beds";
        }

        static void Bed(Transform parent, string name, AudioClip clip, float pan, float volume)
        {
            if (clip == null) return;
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip; src.loop = true; src.playOnAwake = true; src.spatialBlend = 0f; src.panStereo = pan; src.volume = volume;
        }
    }
}
