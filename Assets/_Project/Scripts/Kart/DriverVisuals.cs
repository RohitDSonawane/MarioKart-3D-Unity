using HarborRacer.Kart;
using HarborRacer.Race;
using UnityEngine;

namespace HarborRacer.Kart
{
    /// <summary>Index into <see cref="DriverVisuals.faceTextures"/>: the eye texture numbers of the character materials.</summary>
    public enum Face { Normal = 0, HalfBlink = 1, Blink = 2, Right = 3, Left = 4, Happy = 5, Hurt = 6, Sad = 7 }

    /// <summary>
    /// Driver facial expressions: blinking every 2-5 s and reaction faces (happy on a trick, hurt on a hit, left on a back-throw, sad or happy at the
    /// finish). Body animation comes from <see cref="DriverPoseAnimator"/>; this component never touches the skeleton.
    /// </summary>
    public class DriverVisuals : MonoBehaviour
    {
        public KartController kart;
        public Renderer faceRenderer;
        public Texture[] faceTextures;   // indexed by Face (the character's marioeye_alb.N textures, same order for every driver)

        Face sticky = Face.Normal;      // finish expression, held until the race restarts
        float reverseTime;
        float blinkTimer;
        float reactionTimer;
        int currentFaceIndex = 0;
        MaterialPropertyBlock propBlock;

        void Awake()
        {
            if (kart == null) kart = GetComponentInParent<KartController>();
            propBlock = new MaterialPropertyBlock();

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

            // No procedural spine lean or head turn: the character clips (DriverPoseAnimator) already lean into turns, and writing a bone's
            // localRotation from here replaced the animated pose (the AI drivers lay flat on their backs).
            UpdateFace();
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
