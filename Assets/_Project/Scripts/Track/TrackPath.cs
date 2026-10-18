using System.Collections.Generic;
using UnityEngine;

namespace HarborRacer.Track
{
    public enum DriftHint { None, Left, Right }

    /// <summary>Ordered looped list of Waypoint children (AI lines, item path, race-end path, trolley route).</summary>
    public class TrackPath : MonoBehaviour
    {
        public Color gizmoColor = Color.cyan;
        public bool closed = true;

        public int Count => transform.childCount;
        public Transform GetNode(int i) => transform.GetChild(((i % Count) + Count) % Count);

        public List<Vector3> Positions()
        {
            var list = new List<Vector3>(Count);
            foreach (Transform t in transform) list.Add(t.position);
            return list;
        }

        void OnDrawGizmos()
        {
            if (Count < 2) return;
            Gizmos.color = gizmoColor;
            for (int i = 0; i < Count - 1; i++)
                Gizmos.DrawLine(GetNode(i).position, GetNode(i + 1).position);
            if (closed) Gizmos.DrawLine(GetNode(Count - 1).position, GetNode(0).position);
            for (int i = 0; i < Count; i++)
            {
                var wp = GetNode(i).GetComponent<Waypoint>();
                if (wp != null && wp.driftHint != DriftHint.None)
                {
                    Gizmos.color = wp.driftHint == DriftHint.Left ? Color.green : Color.red;
                    Gizmos.DrawSphere(GetNode(i).position, 2f);
                    Gizmos.color = gizmoColor;
                }
            }
        }
    }
}
