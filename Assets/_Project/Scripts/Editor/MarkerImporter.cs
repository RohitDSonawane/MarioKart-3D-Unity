using System;
using System.Collections.Generic;
using System.IO;
using HarborRacer.Track;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HarborRacer.EditorTools
{
    /// <summary>
    /// Builds the "Markers" hierarchy in ToadHarbor.unity from Assets/_Project/Data/toadharbor_markers.json
    /// (pre-extracted marker data for the track). Idempotent.
    /// </summary>
    public static class MarkerImporter
    {
        const string JsonPath = "Assets/_Project/Data/toadharbor_markers.json";
        const string ScenePath = "Assets/_Project/Scenes/ToadHarbor.unity";

        // ---- JSON DTOs (JsonUtility) ----
        [Serializable] class BoxD { public float[] center; public float[] size; public bool trigger; }
        [Serializable] class TuneD { public string k; public float v; }
        [Serializable]
        class ItemD
        {
            public string name, tag;
            public float[] pos, rot, scale;
            public BoxD box;
            public int layer;
            public bool active = true;
            public List<TuneD> tune;
        }
        [Serializable] class NodeD { public string name, tag; public float[] pos; }
        [Serializable] class PathD { public string name; public List<NodeD> nodes; }
        [Serializable] class ZoneGroupD { public string tag; public List<ItemD> items; }
        [Serializable] class PropD { public string model, name; public bool active = true; public float[] pos, rot, scale; }
        [Serializable]
        class DataD
        {
            public List<ItemD> startGrid, checkpoints, lapColliders, itemBoxes, extraFloors, extraFenceColliders,
                oobTriggers, antiGravityTriggers, antiGravityExits, shellPlatforms, singles;
            public List<PathD> aiPaths, itemPaths;
            public List<ZoneGroupD> zones;
            public List<PropD> props;
            public List<ItemD> jumpPanelEmissions;
        }

        static readonly Dictionary<string, ZoneKind> TagToKind = new Dictionary<string, ZoneKind>
        {
            { "JumpPanel", ZoneKind.JumpPanel }, { "GliderPanel", ZoneKind.GliderRamp }, { "GliderPanelFly", ZoneKind.GliderLaunch },
            { "TrickCollider", ZoneKind.TrickZone }, { "AntiGravity", ZoneKind.AntiGravityEnter }, { "AntiGravityFalse", ZoneKind.AntiGravityExit },
            { "CancelDownForce", ZoneKind.CancelDownForce }, { "OutOfBounds", ZoneKind.OutOfBounds }, { "Water", ZoneKind.Water },
            { "ColliderInAir", ZoneKind.DownForce }, { "fence", ZoneKind.Fence }, { "ShellPlatforms", ZoneKind.ShellPlatform },
            { "SlowDownComputer", ZoneKind.SlowDownAI }, { "Crate", ZoneKind.Crate }, { "Explosion", ZoneKind.Explosion },
        };

        [MenuItem("Tools/HarborRacer/Import Markers")]
        public static string Import()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var data = JsonUtility.FromJson<DataD>(File.ReadAllText(JsonPath));

            var old = GameObject.Find("Markers");
            if (old) UnityEngine.Object.DestroyImmediate(old);
            var root = new GameObject("Markers");

            int zoneCount = 0;

            // Start grid
            var grid = Child(root, "StartGrid");
            if (data.startGrid != null)
                for (int i = 0; i < data.startGrid.Count; i++)
                {
                    var d = data.startGrid[i];
                    var g = Child(grid, $"Slot_{i}_{d.name}");
                    Place(g, d, applyScale: false);
                    var m = g.AddComponent<ZoneMarker>(); m.kind = ZoneKind.StartSlot; m.index = i;
                    zoneCount++;
                }

            // Checkpoints (ordered) + lap lines
            var cps = Child(root, "Checkpoints");
            if (data.checkpoints != null)
                for (int i = 0; i < data.checkpoints.Count; i++)
                {
                    var g = Child(cps, $"CP_{i:00}");
                    var d = data.checkpoints[i];
                    Place(g, d); AddBox(g, d, trigger: true, layerName: "Zone");
                    var m = g.AddComponent<ZoneMarker>(); m.kind = ZoneKind.Checkpoint; m.index = i;
                    zoneCount++;
                }
            var laps = Child(root, "LapLines");
            if (data.lapColliders != null)
                for (int i = 0; i < data.lapColliders.Count; i++)
                {
                    var g = Child(laps, $"LapLine_{i}");
                    var d = data.lapColliders[i];
                    Place(g, d); AddBox(g, d, trigger: true, layerName: "Zone");
                    g.SetActive(d.active);
                    var m = g.AddComponent<ZoneMarker>(); m.kind = ZoneKind.LapLine; m.index = i;
                    zoneCount++;
                }

            // Paths
            Physics.SyncTransforms();
            var paths = Child(root, "Paths");
            var ai = Child(paths, "AI");
            if (data.aiPaths != null) foreach (var p in data.aiPaths) BuildPath(ai, p, Color.cyan);
            if (data.itemPaths != null)
                foreach (var p in data.itemPaths)
                    BuildPath(paths, p, p.name == "TrolleyPath" ? Color.yellow : p.name == "MarioRaceEndPath" ? Color.magenta : Color.green);

            // The harbor boat deck is missing from the imported mesh; the AI lines and the player's route run across it, so bridge it with an invisible road slab
            var patch = Child(Child(root, "Patches"), "HarborDeckFill");
            patch.transform.position = new Vector3(593f, -110.9f, 20f); patch.layer = Layers.Drivable;
            var pbox = patch.AddComponent<BoxCollider>(); pbox.size = new Vector3(66f, 3f, 240f);

            // Item boxes
            var boxes = Child(root, "ItemBoxes");
            if (data.itemBoxes != null)
                for (int i = 0; i < data.itemBoxes.Count; i++)
                {
                    var g = Child(boxes, $"ItemBox_{i:00}");
                    Place(g, data.itemBoxes[i], applyScale: false);
                    var sc = g.AddComponent<SphereCollider>(); sc.isTrigger = true; sc.radius = 1.6f;
                    g.layer = LayerMask.NameToLayer("ItemBox");
                    var m = g.AddComponent<ZoneMarker>(); m.kind = ZoneKind.ItemBox; m.index = i;
                    zoneCount++;
                }
            // the scene has 22 Itembox prefab instances; 4 sit outside the "Itemboxes" group
            if (data.props != null)
            {
                int extra = 0;
                foreach (var p in data.props)
                {
                    if (p.model != "ItemBoxPrefab") continue;
                    var pos = V3(p.pos);
                    bool known = false;
                    foreach (Transform ex in boxes.transform) if (Vector3.Distance(ex.position, pos) < 3f) { known = true; break; }
                    if (known) continue;
                    var g = Child(boxes, $"ItemBox_extra{extra:00}");
                    g.transform.position = pos; g.transform.rotation = Quaternion.identity;
                    var sc = g.AddComponent<SphereCollider>(); sc.isTrigger = true; sc.radius = 1.6f;
                    g.layer = LayerMask.NameToLayer("ItemBox");
                    var m = g.AddComponent<ZoneMarker>(); m.kind = ZoneKind.ItemBox; m.index = 100 + extra;
                    extra++; zoneCount++;
                }
            }

            // Tagged zones
            var forcePanels = new List<ItemD>();
            var zones = Child(root, "Zones");
            if (data.zones != null)
                foreach (var zg in data.zones)
                {
                    if (zg.items == null || zg.items.Count == 0 || !TagToKind.TryGetValue(zg.tag, out var kind)) continue;
                    var parent = Child(zones, zg.tag);
                    for (int i = 0; i < zg.items.Count; i++)
                    {
                        var d = zg.items[i];
                        // The's trigger-type jump panels are thin plates that float above our road (their parent chain
                        // contains un-resolvable prefab nodes). Collect their force values and rebuild the triggers from the panel
                        // decals instead (BuildJumpPanels below).
                        if (kind == ZoneKind.JumpPanel && d.box != null && d.box.size != null && d.box.trigger)
                        {
                            forcePanels.Add(d);
                            continue;
                        }
                        var g = Child(parent, $"{zg.tag}_{i:00}");
                        Place(g, d);
                        // Jump-panel-tagged boxes that are NOT triggers are the's invisible ExtraFloors: solid ground.
                        bool invisibleFloor = kind == ZoneKind.JumpPanel && d.box != null && d.box.size != null && !d.box.trigger;
                        var effectiveKind = invisibleFloor ? ZoneKind.ExtraFloor : kind;
                        bool solid = kind == ZoneKind.Fence || invisibleFloor;
                        bool hasCollider = d.box != null && d.box.size != null;
                        if (hasCollider)
                        {
                            // Fences are solid walls, invisible floors are drivable; crates/shell platforms keep markers only
                            // (later phases); everything else is a trigger on the Zone layer.
                            string layer = invisibleFloor ? "Drivable" : solid ? "Wall" : "Zone";
                            if (kind == ZoneKind.Crate || kind == ZoneKind.ShellPlatform) layer = kind == ZoneKind.Crate ? "Hazard" : "Scenery";
                            AddBox(g, d, trigger: !solid && kind != ZoneKind.Crate && kind != ZoneKind.ShellPlatform, layerName: layer);
                            if (kind == ZoneKind.ShellPlatform) Destroy(g.GetComponent<BoxCollider>());
                            if (kind == ZoneKind.Water) ThickenDownward(g.GetComponent<BoxCollider>(), 20f);
                        }
                        g.SetActive(d.active);
                        var m = g.AddComponent<ZoneMarker>(); m.kind = effectiveKind; m.index = i;
                        if (d.tune != null) foreach (var t in d.tune) m.tuning.Add(new Tuning { key = t.k, value = t.v });
                        if (hasCollider && !solid) ZoneBinder.Bind(g, m);
                        zoneCount++;
                    }
                }

            // Structural props from the scene setup: the boat and ramp carry jump panels / glide launches.
            int propCount = BuildProps(root, data);
            int jumpPanels = BuildJumpPanels(root, zones.gameObject, data, forcePanels);

            // Note: ExtraFloors / ExtraFenceColliders in the scene are tagged (JumpPanel / fence) and already covered by "Zones".

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return $"markers imported: zones={zoneCount}, jumpPanels={jumpPanels}, props={propCount}, aiPaths={data.aiPaths?.Count}, itemPaths={data.itemPaths?.Count}";
        }

        /// <summary>
        /// Jump panels from the panel decals ("JumpPanelEmissions"): they sit exactly on the ramps. Decals closer than 14 u are
        /// one panel; decals next to a glider dash panel belong to the glider ramp and are skipped. Each panel becomes a trigger box
        /// lying on the road along the nearest racing line, with the up/down forces of the nearest original panel.
        /// </summary>
        static int BuildJumpPanels(GameObject root, GameObject zonesRoot, DataD data, List<ItemD> forcePanels)
        {
            if (data.jumpPanelEmissions == null || data.jumpPanelEmissions.Count == 0) return 0;
            Physics.SyncTransforms();

            var gliders = new List<Vector3>();
            if (data.zones != null)
                foreach (var zg in data.zones)
                    if (zg.tag == "GliderPanel" && zg.items != null)
                        foreach (var it in zg.items) gliders.Add(V3(it.pos));

            // cluster decals
            var clusters = new List<List<Vector3>>();
            foreach (var e in data.jumpPanelEmissions)
            {
                var p = V3(e.pos);
                bool placed = false;
                foreach (var c in clusters)
                    if (Vector3.Distance(c[0], p) < 14f) { c.Add(p); placed = true; break; }
                if (!placed) clusters.Add(new List<Vector3> { p });
            }

            // racing lines
            var lines = new List<Transform>();
            var paths = root.transform.Find("Paths");
            if (paths != null)
                foreach (Transform t in paths)
                {
                    if (t.name == "AI") foreach (Transform a in t) lines.Add(a);
                    else if (t.name != "TrolleyPath") lines.Add(t);
                }

            var parent = Child(zonesRoot, "JumpPanels");
            int count = 0;
            foreach (var c in clusters)
            {
                Vector3 centroid = Vector3.zero;
                foreach (var p in c) centroid += p;
                centroid /= c.Count;

                bool glider = false;
                foreach (var gpos in gliders) if (Vector3.Distance(gpos, centroid) < 10f) { glider = true; break; }
                if (glider) continue;

                // nearest racing-line segment: direction of travel across the panel
                float best = float.MaxValue; Vector3 dir = Vector3.forward; Vector3 onLine = centroid;
                foreach (var line in lines)
                    for (int i = 0; i < line.childCount - 1; i++)
                    {
                        Vector3 a = line.GetChild(i).position, b = line.GetChild(i + 1).position;
                        Vector3 ab = b - a; float len2 = ab.sqrMagnitude; if (len2 < 0.01f) continue;
                        float t = Mathf.Clamp01(Vector3.Dot(centroid - a, ab) / len2);
                        Vector3 q = a + ab * t;
                        float d = Vector3.Distance(q, centroid);
                        if (d < best) { best = d; dir = Vector3.ProjectOnPlane(ab, Vector3.up).normalized; onLine = q; }
                    }
                if (best > 45f) continue;                         // not on any racing line: leave it out

                // forces from the nearest original panel
                float up = -140000f, down = 230000f, bestF = 80f;
                foreach (var f in forcePanels)
                {
                    float d = Vector3.Distance(V3(f.pos), centroid);
                    if (d >= bestF || f.tune == null) continue;
                    float fu = float.NaN, fd = float.NaN;
                    foreach (var t in f.tune) { if (t.k == "JumpPanelScript.upforce") fu = t.v; if (t.k == "JumpPanelScript.downforce") fd = t.v; }
                    if (!float.IsNaN(fu) && !float.IsNaN(fd)) { up = fu; down = fd; bestF = d; }
                }

                // sit the trigger on the road surface under the decal; a panel with no drivable surface right under it
                // (the sky glide section is not part of the imported track mesh) can never fire, so it is left out
                Vector3 ground = centroid;
                if (!Physics.Raycast(centroid + Vector3.up * 4f, Vector3.down, out var h, 30f, Layers.GroundMask, QueryTriggerInteraction.Ignore)
                    || centroid.y - h.point.y > 6f || h.collider.gameObject.layer != Layers.Drivable)
                {
                    Debug.Log($"JumpPanel at {centroid} skipped: no drivable surface under the decal");
                    continue;
                }
                bool isPierBoatRamp = Vector3.Distance(centroid, new Vector3(587f, -108f, 80f)) < 25f;
                if (isPierBoatRamp)
                {
                    dir = new Vector3(0f, 0f, -1f);
                    up = -80000f; down = 230000f;
                }
                ground = h.point;

                var g = Child(parent, $"JumpPanel_{count:00}");
                g.transform.position = ground + Vector3.up * 2f;
                g.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                g.layer = LayerMask.NameToLayer("Zone");
                var box = g.AddComponent<BoxCollider>();
                box.size = isPierBoatRamp ? new Vector3(14f, 6f, 8f) : new Vector3(26f, 6f, 8f);
                box.isTrigger = true;
                var m = g.AddComponent<ZoneMarker>(); m.kind = ZoneKind.JumpPanel; m.index = count;
                m.tuning.Add(new Tuning { key = "JumpPanelScript.upforce", value = up });
                m.tuning.Add(new Tuning { key = "JumpPanelScript.downforce", value = down });
                float aimed = isPierBoatRamp ? 28f : AimLaunch(paths, ground + Vector3.up * 1.2f, dir);
                if (aimed > 0f) m.tuning.Add(new Tuning { key = "JumpPanelScript.launch", value = aimed });
                ZoneBinder.Bind(g, m);
                count++;
            }

            // Harbor boat ramp: the jump panel on the deck of CityBoat_01 launching back to the road
            var boatPanel = Child(parent, $"JumpPanel_{count:00}");
            boatPanel.transform.position = new Vector3(589.25f, -107.5f, -10.25f);
            boatPanel.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, -1f), Vector3.up);
            boatPanel.layer = LayerMask.NameToLayer("Zone");
            var boatBox = boatPanel.AddComponent<BoxCollider>();
            boatBox.size = new Vector3(14f, 6f, 8f);
            boatBox.isTrigger = true;
            var boatM = boatPanel.AddComponent<ZoneMarker>();
            boatM.kind = ZoneKind.JumpPanel;
            boatM.index = count;
            boatM.tuning.Add(new Tuning { key = "JumpPanelScript.upforce", value = -80000f });
            boatM.tuning.Add(new Tuning { key = "JumpPanelScript.downforce", value = 230000f });
            boatM.tuning.Add(new Tuning { key = "JumpPanelScript.launch", value = 20f });
            ZoneBinder.Bind(boatPanel, boatM);
            count++;

            return count;
        }

        /// <summary>
        /// Walks the Toad racing line forward from the panel. Where the line leaves the ground (a jump) and touches down again, returns the
        /// upward launch speed whose arc lands there (ballistic: v0 = dy/T + g*T/2); 0 means keep the default hop.
        /// </summary>
        static float AimLaunch(Transform paths, Vector3 takeoff, Vector3 dir)
        {
            var line = paths != null ? paths.Find("AI/ComputerDriverPATH Toad") : null;
            if (line == null || line.childCount < 4) return 0f;
            int n = line.childCount, start = 0; float best = float.MaxValue;
            for (int i = 0; i < n; i++) { float d = Vector3.Distance(line.GetChild(i).position, takeoff); if (d < best) { best = d; start = i; } }
            if (best > 60f) return 0f;
            bool air = false;
            for (int k = -1; k < 8; k++)
            {
                Vector3 a = line.GetChild(((start + k) % n + n) % n).position, b = line.GetChild(((start + k + 1) % n + n) % n).position;
                float len = Vector3.Distance(a, b);
                for (float d = 0f; d < len; d += 4f)
                {
                    Vector3 p = Vector3.Lerp(a, b, d / len);
                    if (Vector3.Dot(p - takeoff, dir) < 10f) continue;                      // on / behind the ramp
                    bool hit = Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var h, 80f, Layers.GroundMask, QueryTriggerInteraction.Ignore) && h.collider.gameObject.layer == Layers.Drivable;
                    float height = hit ? p.y - h.point.y : 999f;
                    if (height > 12f) { air = true; continue; }
                    if (!air || height > 7f) continue;
                    float dist = Vector3.ProjectOnPlane(h.point - takeoff, Vector3.up).magnitude;
                    const float hSpeed = 100f, g = 72f;
                    float T = Mathf.Max(0.3f, dist / hSpeed);
                    float v0 = (h.point.y + 1f - takeoff.y) / T + g * T * 0.5f;
                    Debug.Log($"JumpPanel at {takeoff} aims at road {dist:F0}u away (dy {h.point.y - takeoff.y:F1}): launch {v0:F0}");
                    return Mathf.Clamp(v0, 22f, 80f);
                }
            }
            return 0f;
        }

        const string PropDir = "Assets/_Project/Art/Track/Props/";

        // model key -> (asset path, collider layer or null for visual-only, substring filter for which meshes get colliders)
        static readonly Dictionary<string, (string path, string layer, string meshFilter)> PropModels = new Dictionary<string, (string, string, string)>
        {
            { "CityBoat", (PropDir + "CityBoat/CityBoat.dae", "Drivable", "") },
            { "Yacht", (PropDir + "Yacht/Yacht.dae", "Wall", "") },
            { "JumpPanelModel", (PropDir + "JumpPanelReg/CiJump.dae", "Drivable", "ct_wood03") },
            { "LargeRamp", ("Assets/_Project/Art/Track/Props/LargeRamp.fbx", "Drivable", "") },     // the glider ramp: the mesh is baked off its pivot, which is why the transform puts it right on the ramp zone
            { "PalmTree", ("Assets/_Project/Art/Track/Props/PalmTree.fbx", null, "") },
            { "Fire", ("Assets/_Project/Art/Shared/Fire/Fire.prefab", null, "") },
            { "FireBall", ("Assets/_Project/Art/Shared/Fire/FIREBALL.prefab", null, "") },
        };

        static int BuildProps(GameObject root, DataD data)
        {
            if (data.props == null) return 0;
            var parent = Child(root, "Props");
            int count = 0;
            foreach (var p in data.props)
            {
                if (!PropModels.TryGetValue(p.model, out var info)) continue;     // palm trees etc.: visual-only, later
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(info.path);
                if (asset == null) { Debug.LogWarning("Prop model missing: " + info.path); continue; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent.transform);
                go.name = $"{p.model}_{count:00}";
                go.transform.position = V3(p.pos);
                go.transform.rotation = p.rot != null && p.rot.Length == 4 ? new Quaternion(p.rot[0], p.rot[1], p.rot[2], p.rot[3]) : Quaternion.identity;
                go.transform.localScale = V3(p.scale);
                go.SetActive(p.active);

                if (p.model == "LargeRamp")
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = GliderRampMaterial();

                if (p.model == "PalmTree") { PalmSway.Attach(go, count); count++; continue; }
                if (p.model == "Fire" || p.model == "FireBall")
                {
                    foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = LayerMask.NameToLayer("Scenery");
                    // the prefabs carry a mesh renderer with no material (magenta in URP); the flame is the particle system
                    foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true)) UnityEngine.Object.DestroyImmediate(mr);
                    foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true)) UnityEngine.Object.DestroyImmediate(mf);
                    count++; continue;
                }   // visual only: keeps the wave clip

                // static props: drop the boat-bob / animator so colliders stay put
                foreach (var anim in go.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(anim);

                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    TryCollider(smr.gameObject, smr.sharedMesh, info.layer, info.meshFilter);
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                    TryCollider(mf.gameObject, mf.sharedMesh, info.layer, info.meshFilter);
                count++;
            }
            return count;
        }

        static Material GliderRampMaterial()
        {
            const string path = PropDir + "GliderRamp.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "GliderRamp" };
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", new Color(0.2f, 0.5f, 0.95f));
            m.SetFloat("_Smoothness", 0.5f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(0.05f, 0.2f, 0.6f));
            EditorUtility.SetDirty(m);
            return m;
        }

        static void TryCollider(GameObject g, Mesh mesh, string layer, string nameFilter)
        {
            if (mesh == null || layer == null) { g.layer = LayerMask.NameToLayer("Scenery"); return; }
            if (!string.IsNullOrEmpty(nameFilter) && !g.name.Contains(nameFilter)) { g.layer = LayerMask.NameToLayer("Scenery"); return; }
            if (g.name.Contains("ef_dashboard")) { g.layer = LayerMask.NameToLayer("Scenery"); return; }
            g.layer = LayerMask.NameToLayer(layer);
            var mc = g.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
        }

        /// <summary>The racing lines cross spots where the imported mesh has no road (e.g. the harbor boat deck); pull such nodes onto the nearest drivable surface.</summary>
        static Vector3 SnapToRoad(Vector3 pos)
        {
            bool Road(Vector3 q, out RaycastHit h) =>
                Physics.Raycast(q + Vector3.up * 6f, Vector3.down, out h, 40f, Layers.GroundMask, QueryTriggerInteraction.Ignore) && h.collider.gameObject.layer == Layers.Drivable;
            if (Road(pos, out _)) return pos;
            if (!Physics.Raycast(pos + Vector3.up * 6f, Vector3.down, out _, 40f, Layers.GroundMask, QueryTriggerInteraction.Ignore) && !Physics.CheckSphere(pos, 40f, Layers.GroundMask, QueryTriggerInteraction.Ignore))
                return pos; // sky section: nothing around to snap to
            for (float r = 4f; r <= 48f; r += 4f)
                for (int a = 0; a < 16; a++)
                {
                    var q = pos + Quaternion.Euler(0f, a * 22.5f, 0f) * Vector3.forward * r;
                    if (Road(q, out var h)) { Debug.Log($"Path node {pos} snapped {r:F0}u onto the road"); return new Vector3(h.point.x, h.point.y + 2.4f, h.point.z); }
                }
            return pos;
        }

        static void BuildPath(GameObject parent, PathD p, Color color)
        {
            var go = Child(parent, p.name);
            var tp = go.AddComponent<TrackPath>(); tp.gizmoColor = color;
            go.layer = LayerMask.NameToLayer("Waypoint");
            for (int i = 0; i < p.nodes.Count; i++)
            {
                var n = p.nodes[i];
                var wg = Child(go, $"WP_{i:000}");
                wg.transform.position = p.name == "TrolleyPath" ? V3(n.pos) : SnapToRoad(V3(n.pos));
                wg.layer = LayerMask.NameToLayer("Waypoint");
                var wp = wg.AddComponent<Waypoint>();
                wp.driftHint = n.tag == "DriftLeft" ? DriftHint.Left : n.tag == "DriftRight" ? DriftHint.Right : DriftHint.None;
            }
            // trolley route is a one-way line, not a loop
            tp.closed = p.name != "TrolleyPath";
        }

        static GameObject Child(GameObject parent, string name)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent.transform, false);
            return g;
        }

        static void Place(GameObject g, ItemD d, bool applyScale = true)
        {
            g.transform.position = V3(d.pos);
            g.transform.rotation = d.rot != null && d.rot.Length == 4 ? new Quaternion(d.rot[0], d.rot[1], d.rot[2], d.rot[3]) : Quaternion.identity;
            if (applyScale && d.scale != null && d.scale.Length == 3) g.transform.localScale = V3(d.scale);
        }

        static void AddBox(GameObject g, ItemD d, bool trigger, string layerName)
        {
            if (d.box == null || d.box.size == null) return;
            var b = g.AddComponent<BoxCollider>();
            b.center = V3(d.box.center); b.size = V3(d.box.size); b.isTrigger = trigger;
            g.layer = LayerMask.NameToLayer(layerName);
        }

        /// <summary>Make a thin trigger slab at least <paramref name="worldDepth"/> deep, keeping its top face where it is, so fast falls cannot skip it.</summary>
        public static void ThickenDownward(BoxCollider bc, float worldDepth)
        {
            if (bc == null) return;
            float sy = Mathf.Max(0.0001f, Mathf.Abs(bc.transform.lossyScale.y));
            float want = worldDepth / sy;
            if (bc.size.y >= want) return;
            float extra = want - bc.size.y;
            bc.size = new Vector3(bc.size.x, want, bc.size.z);
            bc.center += Vector3.down * extra * 0.5f;
        }

        static void Destroy(UnityEngine.Object o) { if (o) UnityEngine.Object.DestroyImmediate(o); }
        static Vector3 V3(float[] a) => a == null || a.Length < 3 ? Vector3.zero : new Vector3(a[0], a[1], a[2]);
    }
}
