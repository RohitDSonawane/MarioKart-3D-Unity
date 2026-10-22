using System.Collections.Generic;
using System.Linq;
using HarborRacer.Track;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HarborRacer.EditorTools
{
    /// <summary>
    /// Seals the stretches of road edge that have a drop beside them and no wall: next to each node of the AI racing lines, on both sides, it looks
    /// sideways for where the road ends and checks whether a wall already stands in between. Unprotected edges get an invisible wall collider on the Wall
    /// layer, so a kart that scrapes along the edge is held on the road instead of driving off the deck into the sea. Idempotent: rebuilds "EdgeBarriers".
    /// </summary>
    public static class EdgeBarrierBuilder
    {
        const string RootName = "EdgeBarriers";
        static readonly string[] PathNames = { "ComputerDriverPATH Toad", "ComputerDriverPATHBaby Mario" };
        const float MaxSearch = 32f, MinEdge = 3.5f, Inset = 0.45f, Height = 5f, Thickness = 0.7f;

        struct Edge { public bool found, protectedBy; public Vector3 point; public float dist; }

        [MenuItem("Tools/HarborRacer/Build Edge Barriers")]
        public static string Build()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/ToadHarbor.unity", OpenSceneMode.Single);
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            Physics.SyncTransforms();
            var root = new GameObject(RootName);
            int made = 0, edges = 0;
            var placed = new List<(Vector3 mid, Vector3 dir)>();
            foreach (var pn in PathNames)
            {
                var go = GameObject.Find("Markers/Paths/AI/" + pn);
                if (go == null) continue;
                var path = go.GetComponent<TrackPath>();
                int n = path.Count;
                for (int side = -1; side <= 1; side += 2)
                {
                    var es = new Edge[n];
                    for (int i = 0; i < n; i++) es[i] = FindEdge(path, i, side);
                    for (int i = 0; i < n; i++)
                    {
                        int j = (i + 1) % n;
                        bool a = es[i].found && !es[i].protectedBy, b = es[j].found && !es[j].protectedBy;
                        if (!a && !b) continue;
                        edges++;
                        Vector3 p1, p2;
                        if (a && b) { p1 = es[i].point; p2 = es[j].point; }
                        else
                        {
                            // only one end is open: a short barrier of 14 u centred on that node's edge point
                            var e = a ? es[i] : es[j];
                            Vector3 t = Vector3.ProjectOnPlane(path.GetNode(j).position - path.GetNode(i).position, Vector3.up).normalized;
                            p1 = e.point - t * 7f; p2 = e.point + t * 7f;
                        }
                        if ((p2 - p1).magnitude < 1f || (p2 - p1).magnitude > 220f) continue;
                        Vector3 mid = (p1 + p2) * 0.5f, dir = (p2 - p1).normalized;
                        if (placed.Any(q => (q.mid - mid).magnitude < 3f && Mathf.Abs(Vector3.Dot(q.dir, dir)) > 0.95f)) continue;     // the other line already sealed this
                        placed.Add((mid, dir));
                        var b2 = new GameObject($"EdgeBarrier_{made:000}");
                        b2.transform.SetParent(root.transform, false);
                        b2.transform.SetPositionAndRotation(mid + Vector3.up * (Height * 0.5f - 0.5f), Quaternion.LookRotation(dir, Vector3.up));
                        b2.layer = Layers.Wall;
                        var box = b2.AddComponent<BoxCollider>();
                        box.size = new Vector3(Thickness, Height, (p2 - p1).magnitude + 1.2f);
                        made++;
                    }
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return $"edge barriers: {made} walls placed over {edges} open edge segments";
        }

        static Edge FindEdge(TrackPath path, int i, int side)
        {
            int n = path.Count;
            Vector3 a = path.GetNode(i).position, b = path.GetNode((i + 1) % n).position;
            Vector3 tang = Vector3.ProjectOnPlane(b - a, Vector3.up).normalized;
            var e = new Edge();
            if (tang.sqrMagnitude < 0.5f) return e;
            Vector3 dir = Vector3.Cross(Vector3.up, tang) * side;
            if (!Physics.Raycast(a + Vector3.up * 8f, Vector3.down, out var g0, 40f, Layers.GroundMask, QueryTriggerInteraction.Ignore)) return e;
            Vector3 c = g0.point;
            for (float o = 1f; o <= MaxSearch; o += 1f)
            {
                Vector3 pos = c + dir * o;
                bool ground = Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out var gh, 14f, Layers.GroundMask, QueryTriggerInteraction.Ignore) && c.y - gh.point.y < 4f && gh.point.y - c.y < 4f;
                if (ground) continue;
                if (o < MinEdge) return e;                                   // a node on the very edge (jump lip, ramp end): not a side edge
                e.found = true; e.dist = o;
                e.point = c + dir * (o - Inset);
                // a wall anywhere between the line and the edge protects it
                e.protectedBy = Physics.Linecast(c + Vector3.up * 1.2f, pos + Vector3.up * 1.2f, out var wh, (1 << Layers.Wall) | (1 << Layers.Drivable), QueryTriggerInteraction.Ignore) && wh.normal.y < 0.7f
                                || Physics.Raycast(c + Vector3.up * 3.5f, dir, o, 1 << Layers.Wall, QueryTriggerInteraction.Ignore);
                return e;
            }
            return e;                                                        // road wider than the search: nothing to seal
        }
    }
}
