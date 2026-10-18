using HarborRacer.Kart;
using HarborRacer.Track;
using UnityEngine;

namespace HarborRacer.Hazards
{
    /// <summary>
    /// A tram that rolls along the TrolleyPath: speed 13, destroyed at the last node. A kart touching it at
    /// more than 35 speed is tumbled and nudged to the side; slower contact is just a physical bump.
    /// Karts that are invulnerable (star / bullet) pass unharmed.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Trolley : MonoBehaviour
    {
        public TrackPath path;
        public int nodeIndex;
        public float speed = 13f;
        public float reachRadius = 8f;
        public float hitMinKartSpeed = 35f;       // REALCURRENTSPEED > 35
        public Vector3 hitBoxCenter = new Vector3(0f, 8.6f, 0.6f);
        public Vector3 hitBoxSize = new Vector3(16f, 18f, 41f);
        public float downAcceleration = 400f;     // 20000 * 0.02
        public float groundAlignPerSecond = 3f;

        Rigidbody rb;
        float turnSmooth, turnVel;
        readonly Collider[] buf = new Collider[16];

        public float hopHeight = 10f, hopUpSeconds = 0.3f, hopDownSeconds = 1.6f;      // up 10, back down over about 1.6 s
        float hopT = -1f;
        Transform hopVisual;
        Vector3 hopBase;

        public bool Finished { get; private set; }
        public bool Hopping => hopT >= 0f;

        void Awake() { rb = GetComponent<Rigidbody>(); }

        void FixedUpdate() => Tick(Time.fixedDeltaTime);

        /// <summary>Advance one physics step (public so tests can drive it).</summary>
        public void Tick(float dt)
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (path == null || path.Count == 0 || Finished) return;

            Vector3 pos = transform.position;
            Vector3 target = path.GetNode(nodeIndex).position;
            if ((target - pos).magnitude < reachRadius)
            {
                nodeIndex++;
                if (nodeIndex >= path.Count - 1) { Finished = true; Destroy(gameObject); return; }
                target = path.GetNode(nodeIndex).position;
            }

            // y = SmoothDamp(y, dir), Rotate(0, y / 20)
            Quaternion rot = rb.rotation;
            Vector3 to = target - pos;
            float dir = Vector3.Dot(Vector3.Cross(rot * Vector3.forward, to), rot * Vector3.up);
            turnSmooth = Mathf.SmoothDamp(turnSmooth, dir, ref turnVel, 2.5f * dt, Mathf.Infinity, dt);
            rot *= Quaternion.Euler(0f, turnSmooth / 20f, 0f);

            // follow the ground slope
            if (Physics.Raycast(pos + transform.up * 2f, -transform.up, out var h, 12f, Layers.GroundMask, QueryTriggerInteraction.Ignore) && h.normal.y > 0.5f)
                rot = Quaternion.Slerp(rot, Quaternion.FromToRotation(rot * Vector3.up, h.normal) * rot, groundAlignPerSecond * dt);
            rb.rotation = rot;

            Vector3 v = (rot * Vector3.forward) * speed;
            v.y = rb.linearVelocity.y;
            rb.linearVelocity = v;
            rb.AddForce(Vector3.down * downAcceleration, ForceMode.Acceleration);

            HitKarts();
            StepHop(dt);
        }

        /// <summary>: a starred / bullet kart makes the trolley jump up and settle (visual only, the body keeps driving).</summary>
        void StepHop(float dt)
        {
            if (hopT < 0f) return;
            if (hopVisual == null) { hopT = -1f; return; }
            hopT += dt;
            float h;
            if (hopT < hopUpSeconds) h = hopHeight * (hopT / hopUpSeconds);
            else
            {
                float k = (hopT - hopUpSeconds) / hopDownSeconds;
                if (k >= 1f) { hopVisual.localPosition = hopBase; hopT = -1f; return; }
                h = hopHeight * (1f - k) * (1f - k);
            }
            hopVisual.localPosition = hopBase + Vector3.up * h / Mathf.Max(0.0001f, transform.lossyScale.y);
        }

        void StartHop()
        {
            if (hopT >= 0f || transform.childCount == 0) return;
            hopVisual = transform.GetChild(0);
            hopBase = hopVisual.localPosition;
            hopT = 0f;
        }

        void HitKarts()
        {
            Vector3 center = transform.TransformPoint(hitBoxCenter);
            int n = Physics.OverlapBoxNonAlloc(center, hitBoxSize * 0.5f, buf, transform.rotation, 1 << Layers.Kart, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var kart = buf[i].GetComponentInParent<KartController>();
                if (kart == null || kart.HitReaction == null) continue;
                if (kart.ForwardSpeed <= hitMinKartSpeed) continue;
                if (kart.State.Star || kart.State.Bullet) { StartHop(); continue; }
                kart.HitReaction.Apply(HitType.Tumble, transform.position);
            }
        }
    }
}
