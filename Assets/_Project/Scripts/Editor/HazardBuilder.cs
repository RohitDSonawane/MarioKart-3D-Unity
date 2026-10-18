using System.Collections.Generic;
using System.IO;
using HarborRacer.Hazards;
using HarborRacer.Track;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HarborRacer.EditorTools
{
    /// <summary>
    /// Phase 7 setup for Toad Harbor (run after MarkerImporter / race setup): trolley prefab + spawner + the pre-placed trolleys,
    /// breakable crates and barrels (as Zone triggers with visuals), and the 9 loose traffic cones.
    /// </summary>
    public static class HazardBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/ToadHarbor.unity";
        const string PropDir = "Assets/_Project/Art/Track/Props/";
        const string TrolleyPrefabPath = "Assets/_Project/Prefabs/Hazards/Trolley.prefab";
        const string JsonPath = "Assets/_Project/Data/toadharbor_markers.json";

        [System.Serializable] class PropD { public string model, name; public bool active = true; public float[] pos, rot, scale; }
        [System.Serializable] class ConeD { public string name; public float[] pos, rot, scale; }
        [System.Serializable] class DataD { public List<PropD> props; public List<ConeD> trafficCones; }

        [MenuItem("Tools/HarborRacer/Build Hazards (Toad Harbor)")]
        public static string Build()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var markers = GameObject.Find("Markers");
            if (markers == null) return "Markers missing: run MarkerImporter first.";
            var data = JsonUtility.FromJson<DataD>(File.ReadAllText(JsonPath));

            var old = GameObject.Find("Hazards");
            if (old) Object.DestroyImmediate(old);
            var root = new GameObject("Hazards").transform;

            var trolleyPrefab = BuildTrolleyPrefab();
            var trolleyPath = markers.transform.Find("Paths/TrolleyPath")?.GetComponent<TrackPath>();
            int trolleys = BuildTrolleys(root, trolleyPrefab, trolleyPath, data, markers.transform);
            int crates = BuildCrates(markers.transform);
            int cones = BuildCones(root, data);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return $"hazards: trolleys placed={trolleys} (+spawner), crates/barrels={crates}, cones={cones}";
        }

        // ------------------------------------------------------------------ trolley

        static Trolley BuildTrolleyPrefab()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TrolleyPrefabPath));
            var root = new GameObject("Trolley") { layer = LayerMask.NameToLayer("Hazard") };
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 2000f; rb.linearDamping = 0f; rb.angularDamping = 5f;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 8.6f, 0.6f); box.size = new Vector3(14.3f, 17.4f, 39.6f);
            box.sharedMaterial = FrictionlessMaterial();     // a heavy tram with default friction loses most of its forward speed each step
            var trolley = root.AddComponent<Trolley>();

            var sfxLib = AssetDatabase.LoadAssetAtPath<HarborRacer.Audio.SfxLibrary>("Assets/_Project/Audio/SfxLibrary.asset");
            var trolleySound = sfxLib != null && sfxLib.trolleyMove != null
                ? sfxLib.trolleyMove
                : AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SFX/Ambient/TrolleyMove.wav");
            if (trolleySound != null)
            {
                var src = root.AddComponent<AudioSource>();
                src.clip = trolleySound;
                src.loop = true;
                src.playOnAwake = true;
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 8f;
                src.maxDistance = 60f;
                src.volume = 0.7f;
            }

            var body = Instantiate(PropDir + "Trolley/Trolley.dae", root.transform, "TrolleyBody");
            if (body) body.transform.localScale = Vector3.one * 0.19f;
            var driver = Instantiate(PropDir + "Trolley/T_Nokonoko.dae", root.transform, "Driver");
            if (driver) { driver.transform.localPosition = new Vector3(-0.06f, 6.0f, 17.0f); driver.transform.localScale = Vector3.one * 0.19f; }
            foreach (var a in root.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t != root.transform) t.gameObject.layer = root.layer;

            var saved = PrefabUtility.SaveAsPrefabAsset(root, TrolleyPrefabPath);
            Object.DestroyImmediate(root);
            return saved.GetComponent<Trolley>();
        }

        static PhysicsMaterial FrictionlessMaterial()
        {
            const string path = PropDir + "Trolley/Frictionless.physicMaterial";
            var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (m == null)
            {
                m = new PhysicsMaterial("Frictionless");
                AssetDatabase.CreateAsset(m, path);
            }
            m.dynamicFriction = 0f; m.staticFriction = 0f; m.bounciness = 0f;
            m.frictionCombine = PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(m);
            return m;
        }

        static int BuildTrolleys(Transform parent, Trolley prefab, TrackPath path, DataD data, Transform markers)
        {
            if (path == null || prefab == null) return 0;

            // spawner at the TrolleySystem location (start of the path)
            var spawnerGo = new GameObject("TrolleySpawner");
            spawnerGo.transform.SetParent(parent, false);
            spawnerGo.transform.position = path.GetNode(0).position;
            spawnerGo.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(path.GetNode(1).position - path.GetNode(0).position, Vector3.up).normalized, Vector3.up);
            var spawner = spawnerGo.AddComponent<TrolleySpawner>();
            spawner.prefab = prefab; spawner.path = path; spawner.interval = 30f;
            spawner.colours = new[]
            {
                AssetDatabase.LoadAssetAtPath<Texture2D>(PropDir + "Trolley/TrolleyR_Alb.png"),
                AssetDatabase.LoadAssetAtPath<Texture2D>(PropDir + "Trolley/TrolleyG_Alb.png"),
                AssetDatabase.LoadAssetAtPath<Texture2D>(PropDir + "Trolley/TrolleyY_Alb.png"),
            };

            // the pre-placed trolleys of the scene, snapped onto the path
            int count = 0;
            if (data.props != null)
                foreach (var p in data.props)
                {
                    if (p.model != "Trolley" || !p.active) continue;
                    var pos = new Vector3(p.pos[0], p.pos[1], p.pos[2]);
                    int nearest = 0; float best = float.MaxValue;
                    for (int i = 0; i < path.Count - 1; i++)
                    {
                        float d = (path.GetNode(i).position - pos).sqrMagnitude;
                        if (d < best) { best = d; nearest = i; }
                    }
                    Vector3 dir = Vector3.ProjectOnPlane(path.GetNode(nearest + 1).position - path.GetNode(nearest).position, Vector3.up).normalized;
                    var t = (Trolley)PrefabUtility.InstantiatePrefab(prefab, parent);
                    t.name = $"Trolley_{count:00}";
                    t.transform.SetPositionAndRotation(path.GetNode(nearest).position, Quaternion.LookRotation(dir, Vector3.up));
                    t.path = path; t.nodeIndex = nearest + 1;
                    if (t.GetComponent<AudioSource>() == null)
                    {
                        var sfxLib = AssetDatabase.LoadAssetAtPath<HarborRacer.Audio.SfxLibrary>("Assets/_Project/Audio/SfxLibrary.asset");
                        var sound = sfxLib != null && sfxLib.trolleyMove != null
                            ? sfxLib.trolleyMove
                            : AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SFX/Ambient/TrolleyMove.wav");
                        if (sound != null)
                        {
                            var src = t.gameObject.AddComponent<AudioSource>();
                            src.clip = sound;
                            src.loop = true;
                            src.playOnAwake = true;
                            src.spatialBlend = 1f;
                            src.rolloffMode = AudioRolloffMode.Linear;
                            src.minDistance = 8f;
                            src.maxDistance = 60f;
                            src.volume = 0.7f;
                        }
                    }
                    count++;
                }
            return count;
        }

        // ------------------------------------------------------------------ crates / barrels

        [MenuItem("Tools/HarborRacer/Align Crates To Ground")]
        public static string AlignCratesToGround()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var crateZones = Object.FindObjectsByType<CrateZone>();
            int count = 0;
            foreach (var cz in crateZones)
            {
                var go = cz.gameObject;
                var vis = cz.visual != null ? cz.visual : go.transform.Find("Visual")?.gameObject;
                if (vis == null) continue;
                SnapCrateToGround(go, vis);
                var box = go.GetComponent<BoxCollider>();
                if (box == null) box = go.AddComponent<BoxCollider>();
                FitBox(box, go.transform, vis);
                box.isTrigger = true;
                go.layer = LayerMask.NameToLayer("Zone");
                count++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return $"Aligned {count} crates and barrels flush with ground.";
        }

        static int BuildCrates(Transform markers)
        {
            var debris = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/VFX/Kart_Additive.mat");
            var crateModel = AssetDatabase.LoadAssetAtPath<GameObject>(PropDir + "Crate/CrashBox.dae");
            var barrelModel = AssetDatabase.LoadAssetAtPath<GameObject>(PropDir + "DK Barrel/DK Barrel/DKBarrel.fbx");
            int n = 0;
            foreach (var m in markers.GetComponentsInChildren<ZoneMarker>(true))
            {
                if (m.kind != ZoneKind.Crate) continue;
                var go = m.gameObject;
                // markers are renamed Crate_NN by the importer; barrels are the ones with the big (6.5) instance scale
                bool barrel = go.transform.lossyScale.x > 3f;
                var model = barrel ? barrelModel : crateModel;
                if (model == null) continue;

                var oldVisual = go.transform.Find("Visual");
                if (oldVisual != null) Object.DestroyImmediate(oldVisual.gameObject);
                var vis = (GameObject)PrefabUtility.InstantiatePrefab(model, go.transform);
                vis.name = "Visual";
                vis.transform.localPosition = Vector3.zero; vis.transform.localRotation = Quaternion.identity; vis.transform.localScale = Vector3.one;
                foreach (var a in vis.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
                foreach (var t in vis.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = LayerMask.NameToLayer("Scenery");

                // Align with ground: snap root so visual bottom sits flush with road surface
                SnapCrateToGround(go, vis);

                // collider: fitted precisely around the visual renderers
                var box = go.GetComponent<BoxCollider>();
                if (box == null) box = go.AddComponent<BoxCollider>();
                FitBox(box, go.transform, vis);
                box.isTrigger = true;
                go.layer = LayerMask.NameToLayer("Zone");

                if (!go.TryGetComponent<CrateZone>(out var zone)) zone = go.AddComponent<CrateZone>();
                zone.visual = vis; zone.debrisMaterial = debris;
                zone.debrisColour = barrel ? new Color(0.75f, 0.4f, 0.15f) : new Color(0.75f, 0.55f, 0.25f);
                n++;
            }
            return n;
        }

        static void SnapCrateToGround(GameObject go, GameObject vis)
        {
            var rends = vis.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return;
            if (Physics.Raycast(go.transform.position + Vector3.up * 10f, Vector3.down, out var hit, 30f, Layers.GroundMask, QueryTriggerInteraction.Ignore))
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                float deltaY = hit.point.y - b.min.y;
                go.transform.position += Vector3.up * deltaY;
                Physics.SyncTransforms();
            }
        }

        /// <summary>Fit a box collider (in go-local space) around all renderers of a visual child.</summary>
        static void FitBox(BoxCollider box, Transform space, GameObject visual)
        {
            Bounds b = default; bool first = true;
            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
                foreach (var corner in Corners(r.bounds))
                {
                    var local = space.InverseTransformPoint(corner);
                    if (first) { b = new Bounds(local, Vector3.zero); first = false; } else b.Encapsulate(local);
                }
            box.center = b.center; box.size = b.size;
        }

        static IEnumerable<Vector3> Corners(Bounds b)
        {
            for (int i = 0; i < 8; i++)
                yield return b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
        }

        // ------------------------------------------------------------------ cones

        static int BuildCones(Transform parent, DataD data)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(PropDir + "Traffic Cone/Traffic Cone/Pylon.fbx");
            if (model == null || data.trafficCones == null) return 0;
            var coneRoot = new GameObject("TrafficCones").transform; coneRoot.SetParent(parent, false);
            int n = 0;
            foreach (var c in data.trafficCones)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model, coneRoot);
                go.name = $"Cone_{n:00}";
                go.transform.position = new Vector3(c.pos[0], c.pos[1], c.pos[2]);
                go.transform.rotation = new Quaternion(c.rot[0], c.rot[1], c.rot[2], c.rot[3]);
                go.transform.localScale = new Vector3(c.scale[0], c.scale[1], c.scale[2]);
                foreach (var a in go.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);

                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 1f; rb.linearDamping = 0.05f; rb.angularDamping = 0.2f;
                go.AddComponent<PylonBody>();

                // one box collider around the whole cone
                var box = go.AddComponent<BoxCollider>();
                FitBox(box, go.transform, go);
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = LayerMask.NameToLayer("Hazard");
                n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ helpers

        static GameObject Instantiate(string assetPath, Transform parent, string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (asset == null) { Debug.LogWarning("Missing model: " + assetPath); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.name = name;
            return go;
        }
    }
}
