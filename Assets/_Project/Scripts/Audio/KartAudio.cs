using HarborRacer.Items;
using HarborRacer.Kart;
using HarborRacer.Race;
using UnityEngine;

namespace HarborRacer.Audio
{
    /// <summary>
    /// Per-kart audio model: dual engine loops (idle crossfaded with accel, pitch modulated by speed and boost),
    /// 3D spatial settings for AI karts, drift squeal, boost burst, glider, tricks, collisions and voices.
    /// </summary>
    public class KartAudio : MonoBehaviour
    {
        public SfxLibrary library;
        public KartController kart;
        public RacerProgress racer;

        AudioSource idleSource;
        AudioSource accelSource;
        AudioSource driftSource;
        AudioSource sfxSource;
        AudioSource voiceSource;

        bool isPlayer;
        float lastCoinCount;
        float lastSpeed;
        bool wasGliding;

        void Awake()
        {
            if (kart == null) kart = GetComponent<KartController>();
            if (racer == null) racer = GetComponent<RacerProgress>();
            isPlayer = racer != null ? racer.isPlayer : (CompareTag("Player") || GetComponent<Inputs.PlayerKartInput>() != null);

            idleSource = CreateAudioSource("Engine_Idle", true);
            accelSource = CreateAudioSource("Engine_Accel", true);
            driftSource = CreateAudioSource("Drift_Squeal", true);
            sfxSource = CreateAudioSource("Kart_SFX", false);
            voiceSource = CreateAudioSource("Kart_Voice", false);
        }

        AudioSource CreateAudioSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = isPlayer ? 0f : 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 5f;
            src.maxDistance = 50f;
            return src;
        }

        void Start()
        {
            if (library == null && AudioService.Instance != null)
                library = AudioService.Instance.library;

            if (library != null)
            {
                if (library.engineIdle != null)
                {
                    idleSource.clip = library.engineIdle;
                    idleSource.Play();
                }
                if (library.engineAccel != null)
                {
                    accelSource.clip = library.engineAccel;
                    accelSource.Play();
                }
                if (library.driftSqueal != null)
                {
                    driftSource.clip = library.driftSqueal;
                }
            }

            // Hook events
            if (kart != null)
            {
                if (kart.Boost != null)
                {
                    kart.Boost.BoostGranted += OnBoostGranted;
                }
                if (kart.Drift != null)
                {
                    kart.Drift.DriftStarted += OnDriftStarted;
                    kart.Drift.TierChanged += OnDriftTierChanged;
                    kart.Drift.DriftReleased += OnDriftReleased;
                }
                if (kart.Glider != null)
                {
                    kart.Glider.Opened += OnGliderOpened;
                    kart.Glider.Closed += OnGliderClosed;
                }
                if (kart.HitReaction != null)
                {
                    kart.HitReaction.Hit += OnHit;
                }
                var trick = GetComponent<KartTrick>();
                if (trick != null)
                {
                    trick.TrickPerformed += OnTrickPerformed;
                    trick.TrickLanded += OnTrickLanded;
                }
                var wallet = GetComponent<CoinWallet>();
                if (wallet != null)
                {
                    wallet.Changed += OnCoinChanged;
                    lastCoinCount = wallet.Count;
                }
            }
        }

        void OnDestroy()
        {
            if (kart != null)
            {
                if (kart.Boost != null) kart.Boost.BoostGranted -= OnBoostGranted;
                if (kart.Drift != null)
                {
                    kart.Drift.DriftStarted -= OnDriftStarted;
                    kart.Drift.TierChanged -= OnDriftTierChanged;
                    kart.Drift.DriftReleased -= OnDriftReleased;
                }
                if (kart.Glider != null)
                {
                    kart.Glider.Opened -= OnGliderOpened;
                    kart.Glider.Closed -= OnGliderClosed;
                }
                if (kart.HitReaction != null) kart.HitReaction.Hit -= OnHit;
                var trick = GetComponent<KartTrick>();
                if (trick != null)
                {
                    trick.TrickPerformed -= OnTrickPerformed;
                    trick.TrickLanded -= OnTrickLanded;
                }
                var wallet = GetComponent<CoinWallet>();
                if (wallet != null) wallet.Changed -= OnCoinChanged;
            }
        }

        void Update()
        {
            if (kart == null) return;
            float speed = Mathf.Abs(kart.ForwardSpeed);
            float maxSpeed = kart.stats != null ? kart.stats.maxSpeed : 85f;
            float speedRatio = Mathf.Clamp01(speed / Mathf.Max(1f, maxSpeed));
            bool isBoosting = kart.Boost != null && kart.Boost.Boosting;

            // Audio volumes
            float masterVol = AudioService.Instance != null ? AudioService.Instance.masterVolume : 1f;
            float sfxVol = AudioService.Instance != null ? AudioService.Instance.sfxVolume : 0.8f;

            // Engine Crossfade: idle fades out above 10 speed; accel fades in
            float idleVol = Mathf.Clamp01(1f - (speed / 15f)) * 0.7f * sfxVol * masterVol;
            float accelVol = Mathf.Clamp01((speed - 5f) / 20f) * 0.6f * sfxVol * masterVol;

            idleSource.volume = Mathf.Lerp(idleSource.volume, idleVol, 8f * Time.deltaTime);
            accelSource.volume = Mathf.Lerp(accelSource.volume, accelVol, 8f * Time.deltaTime);

            // Engine Pitch: 0.8 base + 1.0 * speedRatio (+0.2 when boosting)
            float targetPitch = 0.8f + 1.0f * speedRatio + (isBoosting ? 0.25f : 0f);
            accelSource.pitch = Mathf.Lerp(accelSource.pitch, targetPitch, 6f * Time.deltaTime);

            // Drift sound
            var d = kart.Drift;
            bool drifting = d != null && d.IsDrifting && d.Landed;
            if (drifting && !driftSource.isPlaying)
            {
                driftSource.volume = 0.5f * sfxVol * masterVol;
                driftSource.Play();
            }
            else if (!drifting && driftSource.isPlaying)
            {
                driftSource.Stop();
            }

            // Glider landing detection
            if (wasGliding && !kart.State.Gliding && kart.State.Grounded)
            {
                PlayClip(library?.landing, 0.6f);
            }
            wasGliding = kart.State.Gliding;
            lastSpeed = speed;
        }

        void OnBoostGranted(BoostSource source, float seconds, bool burst)
        {
            if (!burst || library == null) return;
            PlayClip(library.boostBurst, 0.8f);

            // Play voice on boost (with cooldown to prevent spam)
            if (isPlayer && library.marioBoost != null && library.marioBoost.Length > 0)
            {
                var voice = library.GetRandom(library.marioBoost);
                PlayVoice(voice, 1f, 1.2f);
            }
        }

        void OnDriftStarted(int dir)
        {
            if (library != null && library.driftHop != null)
            {
                PlayClip(library.driftHop, 0.5f);
            }
        }

        void OnDriftTierChanged(int tier)
        {
            // Mini-turbo charge dings
            if (library != null && library.hoverCharge != null)
            {
                float pitch = 0.9f + tier * 0.2f;
                sfxSource.pitch = pitch;
                sfxSource.PlayOneShot(library.hoverCharge, 0.6f);
            }
        }

        void OnDriftReleased(int tier)
        {
            if (tier > 0 && library != null && library.boostBurst != null)
            {
                PlayClip(library.boostBurst, 0.7f);
            }
        }

        void OnGliderOpened()
        {
            PlayClip(library?.gliderOpen, 0.8f);
        }

        void OnGliderClosed()
        {
            PlayClip(library?.gliderClose, 0.7f);
        }

        void OnTrickPerformed(int index)
        {
            PlayClip(library?.hoverCharge, 0.7f);
            if (isPlayer && library?.marioTrick != null && library.marioTrick.Length > 0)
            {
                PlayVoice(library.GetRandom(library.marioTrick), 1f, 1.5f);
            }
        }

        void OnTrickLanded()
        {
            PlayClip(library?.landing, 0.7f);
        }

        void OnHit(HitType type)
        {
            if (library == null) return;
            if (type == HitType.Bump)
            {
                PlayClip(library.kartBump, 0.6f);
            }
            else
            {
                PlayClip(library.wallCrash, 0.8f);
                if (isPlayer && library.marioHurt != null && library.marioHurt.Length > 0)
                {
                    PlayVoice(library.GetRandom(library.marioHurt), 1f, 1.5f);
                }
            }
        }

        void OnCoinChanged(int count)
        {
            if (count > lastCoinCount && library != null && library.coinPickup != null)
            {
                PlayClip(library.coinPickup, 0.7f);
            }
            lastCoinCount = count;
        }

        void PlayClip(AudioClip clip, float vol)
        {
            if (clip == null) return;
            sfxSource.pitch = 1f;
            sfxSource.PlayOneShot(clip, vol);
        }

        void PlayVoice(AudioClip clip, float vol, float cooldown = 1.5f)
        {
            if (clip == null) return;
            if (isPlayer)
            {
                AudioService.Instance?.PlayVoiceWithCooldown("MarioVoice", clip, cooldown, vol);
            }
            else
            {
                voiceSource.pitch = 1f;
                voiceSource.PlayOneShot(clip, vol);
            }
        }
    }
}
