using HarborRacer.Kart;
using HarborRacer.Track;
using UnityEngine;

namespace HarborRacer.Items
{
    /// <summary>
    /// Item box: a kart touching it breaks the box; if that kart has nothing, its roulette starts. The box comes back
    /// after 1 s with a grow animation. Lives on the Zone layer so the kart zone sensor finds it.
    /// </summary>
    public class ItemBoxZone : KartZoneBase
    {
        public float respawnSeconds = 1f;
        public float growSeconds = 0.35f;
        public Transform visual;
        public float spinDegreesPerSecond = 90f, bobHeight = 0.25f, bobSpeed = 2f;

        public bool Broken { get; private set; }
        float timer;
        Vector3 baseScale = Vector3.one, basePos;
        Collider col;

        void Awake()
        {
            col = GetComponent<Collider>();
            if (visual != null) { baseScale = visual.localScale; basePos = visual.localPosition; }
        }

        public override void OnKartEnter(KartController kart)
        {
            if (Broken) return;
            kart.Items?.OnItemBox();
            Break();
        }

        public void Break()
        {
            if (Broken) return;
            Broken = true; timer = 0f;
            if (visual != null) visual.gameObject.SetActive(false);
            if (col != null) col.enabled = false;
            if (Application.isPlaying)
            {
                Audio.AudioService.Instance?.Play3D(Audio.AudioService.Instance?.library?.rouletteTick, transform.position, 0.7f);
                HarborRacer.Hazards.DebrisBurst.Spawn(transform.position, new Color(0.4f, 0.8f, 1f), null, 0.5f);
            }
        }

        void Update() => Tick(Time.deltaTime);

        /// <summary>Respawn timer and idle animation; callable from tests.</summary>
        public void Tick(float dt)
        {
            if (visual == null) return;
            if (Broken)
            {
                timer += dt;
                if (timer >= respawnSeconds)
                {
                    Broken = false; timer = 0f;
                    visual.gameObject.SetActive(true);
                    if (col != null) col.enabled = true;
                    visual.localScale = Vector3.zero;
                }
                return;
            }
            float grow = Mathf.Clamp01((visual.localScale.x / Mathf.Max(0.0001f, baseScale.x)) + dt / growSeconds);
            visual.localScale = baseScale * grow;
            visual.Rotate(0f, spinDegreesPerSecond * dt, 0f, Space.Self);
            visual.localPosition = basePos + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
        }
    }
}
