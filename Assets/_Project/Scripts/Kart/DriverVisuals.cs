using HarborRacer.Kart;
using HarborRacer.Race;
using UnityEngine;

namespace HarborRacer.Kart
{
    /// <summary>
    /// Procedural driver animation and facial expressions (Phase 11):
    /// - Steering lean & drift lean (spine rotation)
    /// - Head look-at nearest racer (within 20u)
    /// - Face blinking (every 2-5 s) and reaction face swaps (trick/happy, hit/hurt)
    /// - Hop/trick bounce
    /// </summary>
    /// <summary>Index into <see cref="DriverVisuals.faceTextures"/>: the eye texture numbers of the character materials.</summary>
    public enum Face { Normal = 0, HalfBlink = 1, Blink = 2, Right = 3, Left = 4, Happy = 5, Hurt = 6, Sad = 7 }

    public class DriverVisuals : MonoBehaviour
    {
        public KartController kart;
        public Transform spineBone;
        public Transform headBone;
        public Renderer faceRenderer;
        public Texture[] faceTextures;   // indexed by Face (the character's marioeye_alb.N textures, same order for every driver)

        [Header("Tuning")]
        public float maxSteerLeanAngle = 18f;
        public float driftLeanAngle = 22f;
        public float leanLerpSpeed = 8f;
        public float maxHeadYaw = 60f;

        Face sticky = Face.Normal;      // finish expression, held until the race restarts
        float reverseTime;
        float currentLean;
        float blinkTimer;
        float reactionTimer;
        int currentFaceIndex = 0;
        Transform nearestOpponent;
        MaterialPropertyBlock propBlock;

        void Awake()
        {
            if (kart == null) kart = GetComponentInParent<KartController>();
            propBlock = new MaterialPropertyBlock();

            // Auto-locate spine and head if not assigned
            if (spineBone == null)
            {
                foreach (var t in GetComponentsInChildren<Transform>())
                {
                    string n = t.name.ToLower();
                    if (n.Contains("spine") || n.Contains("chest") || n.Contains("waist"))
                    {
                        spineBone = t;
                        break;
                    }
                }
            }

            if (headBone == null)
            {
                foreach (var t in GetComponentsInChildren<Transform>())
                {
                    string n = t.name.ToLower();
                    if (n.Contains("head"))
                    {
                        headBone = t;
                        break;
                    }
                }
            }

            // Auto-locate face / eye renderer if not assigned
            if (faceRenderer == null)
            {
                foreach (var r in GetComponentsInChildren<Renderer>())
                {
                    string n = r.name.ToLower();
                    if (n.Contains("eye") || n.Contains("face"))
                    {
                        faceRenderer = r;
                        break;
                    }
                }
            }
        }

        void Start()
        {
            if (kart != null)
            {
                if (kart.HitReaction != null) kart.HitReaction.Hit += OnHit;
                var trick = kart.GetComponent<KartTrick>();
                if (trick != null) trick.TrickPerformed += OnTrick;
                var holder = kart.GetComponent<Items.ItemHolder>();
                if (holder != null) holder.Thrown += OnThrown;
                var racer = kart.GetComponent<RacerProgress>();
                if (racer != null) racer.OnFinished += OnFinished;
            }
            blinkTimer = Random.Range(2f, 4f);
        }

        void OnDestroy()
        {
            if (kart != null)
            {
                if (kart.HitReaction != null) kart.HitReaction.Hit -= OnHit;
                var trick = kart.GetComponent<KartTrick>();
                if (trick != null) trick.TrickPerformed -= OnTrick;
                var holder = kart.GetComponent<Items.ItemHolder>();
                if (holder != null) holder.Thrown -= OnThrown;
                var racer = kart.GetComponent<RacerProgress>();
                if (racer != null) racer.OnFinished -= OnFinished;
            }
        }

        void OnHit(HitType type)
        {
            if (type != HitType.Bump) SetFace(Face.Hurt, kart.HitReaction != null ? kart.HitReaction.stunSeconds : 1.5f);
        }

        void OnTrick(int index) => SetFace(Face.Happy, 0.8f);

        // a backward throw looks over the left shoulder, then blinks
        void OnThrown(bool backward) { if (backward) SetFace(Face.Left, 0.7f); }

        // places 1-5 are happy (blinking now and then), the rest are sad
        void OnFinished()
        {
            var racer = kart.GetComponent<RacerProgress>();
            sticky = racer != null && racer.FinishPlace <= 5 ? Face.Happy : Face.Sad;
        }

        public void SetFace(Face face, float duration) => SetFace((int)face, duration);

        public void SetFace(int index, float duration)
        {
            if (sticky != Face.Normal) return;
            currentFaceIndex = index;
            reactionTimer = duration;
            ApplyFace(currentFaceIndex);
        }

        void LateUpdate()
        {
            if (kart == null) return;

            // 1. Procedural Spine Lean
            float steerInput = 0f;
            var playerInput = kart.GetComponent<Inputs.PlayerKartInput>();
            if (playerInput != null) steerInput = playerInput.Steer;
            else
            {
                var aiInput = kart.GetComponent<Inputs.PathDriverInput>();
                if (aiInput != null) steerInput = aiInput.Steer;
            }
            float driftDir = kart.Drift != null && kart.Drift.IsDrifting ? kart.Drift.DriftDir : 0f;
            float targetLean = (-steerInput * maxSteerLeanAngle) + (-driftDir * driftLeanAngle);

            currentLean = Mathf.Lerp(currentLean, targetLean, leanLerpSpeed * Time.deltaTime);
            if (spineBone != null)
            {
                spineBone.localRotation = Quaternion.Euler(0f, 0f, currentLean);
            }

            // 2. Head Look-At Nearby Opponent
            UpdateHeadLook();

            // 3. Facial Expression & Blinking
            UpdateFace();
        }

        void UpdateHeadLook()
        {
            if (headBone == null) return;

            // Find nearest opponent within 20u
            float minDist = 20f;
            nearestOpponent = null;

            var racers = RaceManager.Instance != null ? RaceManager.Instance.Racers : null;
            if (racers != null)
            {
                for (int i = 0; i < racers.Count; i++)
                {
                    var r = racers[i];
                    if (r == null || r.transform == kart.transform) continue;
                    float d = Vector3.Distance(kart.transform.position, r.transform.position);
                    if (d < minDist)
                    {
                        minDist = d;
                        nearestOpponent = r.transform;
                    }
                }
            }

            if (nearestOpponent != null && kart.ForwardSpeed > 0f)
            {
                Vector3 toOpponent = nearestOpponent.position - kart.transform.position;
                Vector3 localDir = kart.transform.InverseTransformDirection(toOpponent);
                float yaw = Mathf.Clamp(Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg, -maxHeadYaw, maxHeadYaw);
                headBone.localRotation = Quaternion.Slerp(headBone.localRotation, Quaternion.Euler(0f, yaw, 0f), 5f * Time.deltaTime);
            }
            else
            {
                headBone.localRotation = Quaternion.Slerp(headBone.localRotation, Quaternion.identity, 5f * Time.deltaTime);
            }
        }

        void UpdateFace()
        {
            if (faceRenderer == null || faceTextures == null || faceTextures.Length == 0) return;

            if (sticky != Face.Normal)
            {
                reverseTime += Time.deltaTime;
                ApplyFace(sticky == Face.Happy && reverseTime % 4f < 0.2f ? (int)Face.Blink : (int)sticky);
                return;
            }

            // rolling backwards, the eyes flick left and blink
            if (kart.ForwardSpeed < -1f && reactionTimer <= 0f)
            {
                reverseTime += Time.deltaTime;
                ApplyFace(reverseTime % 2f > 1.9f ? (int)Face.Blink : (int)Face.Left);
                return;
            }
            reverseTime = 0f;

            if (reactionTimer > 0f)
            {
                reactionTimer -= Time.deltaTime;
                if (reactionTimer <= 0f)
                {
                    currentFaceIndex = 0; // return to normal
                    ApplyFace(currentFaceIndex);
                }
                return;
            }

            // Blinking timer
            blinkTimer -= Time.deltaTime;
            if (blinkTimer <= 0f)
            {
                if (currentFaceIndex == 0)
                {
                    currentFaceIndex = 2; // blink
                    ApplyFace(currentFaceIndex);
                    blinkTimer = 0.12f;
                }
                else
                {
                    currentFaceIndex = 0; // normal
                    ApplyFace(currentFaceIndex);
                    blinkTimer = Random.Range(2.5f, 5f);
                }
            }
        }

        void ApplyFace(int idx)
        {
            if (faceRenderer == null || faceTextures == null || idx < 0 || idx >= faceTextures.Length) return;
            var tex = faceTextures[idx];
            if (tex == null) return;

            faceRenderer.GetPropertyBlock(propBlock);
            propBlock.SetTexture("_BaseMap", tex);
            faceRenderer.SetPropertyBlock(propBlock);
        }
    }
}
