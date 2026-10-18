using HarborRacer.Race;
using HarborRacer.Track;
using UnityEngine;

namespace HarborRacer.Hazards
{
    /// <summary>
    /// one tram at the start, then a new one every 30 seconds once the race is running. Trams cycle through
    /// three colours. Runs in FixedUpdate so it is deterministic.
    /// </summary>
    public class TrolleySpawner : MonoBehaviour
    {
        public Trolley prefab;
        public TrackPath path;
        public float interval = 30f;
        public Texture2D[] colours;                 // TrolleyR / TrolleyG / TrolleyY albedo
        public bool spawnFirstAtStart = true;

        float timer;
        int colourIndex;
        bool firstDone;


        void FixedUpdate() => Tick(Time.fixedDeltaTime);

        public void Tick(float dt)
        {
            var rm = RaceManager.Instance;
            if (spawnFirstAtStart && !firstDone && rm != null && rm.State != RaceState.Waiting)
            {
                firstDone = true;
                Spawn();
            }
            if (rm == null || rm.State != RaceState.Racing && rm.State != RaceState.PlayerFinished) return;
            timer += dt;
            if (timer > interval) { timer = 0f; Spawn(); }
        }

        public Trolley Spawn()
        {
            if (prefab == null || path == null) return null;
            var t = Instantiate(prefab, transform.position, transform.rotation);
            t.path = path;
            t.nodeIndex = 1;
            t.gameObject.SetActive(true);
            if (colours != null && colours.Length > 0)
            {
                var block = new MaterialPropertyBlock();
                block.SetTexture("_BaseMap", colours[colourIndex % colours.Length]);
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                    if (r.name.Contains("Trolley")) r.SetPropertyBlock(block);
                colourIndex++;
            }
            return t;
        }
    }
}
