using HarborRacer.Kart;
using HarborRacer.Track;
using UnityEngine;

namespace HarborRacer.Hazards
{
    /// <summary>
    /// (crash boxes and DK barrels): hitting one breaks it and, unless the kart is boosting, drops the kart's speed
    /// to 5. Broken crates are gone for the rest of the race. Items break them too (Phase 8).
    /// The trigger sits on the Zone layer so the kart's zone sensor finds it; the visual child is hidden on break.
    /// </summary>
    public class CrateZone : KartZoneBase
    {
        public GameObject visual;
        public Material debrisMaterial;
        public Color debrisColour = new Color(0.7f, 0.45f, 0.2f);
        public float slowSpeed = 5f;

        public bool Broken { get; private set; }

        public override void OnKartEnter(KartController kart)
        {
            if (Broken) return;
            bool boosting = kart.Boost != null && kart.Boost.Boosting;
            if (kart.CurrentSpeed > slowSpeed && !boosting) kart.CurrentSpeed = slowSpeed;   // ref
            Break();
        }

        public void Break()
        {
            if (Broken) return;
            Broken = true;
            HarborRacer.Audio.AudioService.Instance?.Play3D(HarborRacer.Audio.AudioService.Instance?.library?.crateBreak, transform.position, 0.8f);
            DebrisBurst.Spawn(transform.position + Vector3.up * 2f, debrisColour, debrisMaterial, transform.lossyScale.x);
            if (visual != null) visual.SetActive(false);
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }
    }
}
