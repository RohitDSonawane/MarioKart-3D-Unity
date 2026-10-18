using System.Collections.Generic;
using UnityEngine;

namespace HarborRacer.Audio
{
    /// <summary>
    /// Global audio service providing 2D and pooled 3D one-shot playback, voice cooldowns, and volume management.
    /// Replaces magic effectSound indices with structured calls via SfxLibrary.
    /// </summary>
    public class AudioService : MonoBehaviour
    {
        public static AudioService Instance { get; private set; }

        public SfxLibrary library;

        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float musicVolume = 0.8f;
        [Range(0f, 1f)] public float sfxVolume = 0.9f;
        [Range(0f, 1f)] public float voiceVolume = 1f;

        const int PoolSize = 12;
        AudioSource audioSource2D;
        readonly List<AudioSource> source3DPool = new List<AudioSource>();
        readonly Dictionary<string, float> cooldowns = new Dictionary<string, float>();
        int poolIndex;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            audioSource2D = gameObject.AddComponent<AudioSource>();
            audioSource2D.playOnAwake = false;
            audioSource2D.spatialBlend = 0f;

            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject($"AudioSource_3D_{i}");
                go.transform.SetParent(transform);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 5f;
                src.maxDistance = 50f;
                source3DPool.Add(src);
            }
        }

        public void Play2D(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            audioSource2D.pitch = pitch;
            audioSource2D.PlayOneShot(clip, volume * sfxVolume * masterVolume);
        }

        public void PlayVoice2D(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            audioSource2D.pitch = 1f;
            audioSource2D.PlayOneShot(clip, volume * voiceVolume * masterVolume);
        }

        public void Play3D(AudioClip clip, Vector3 position, float volume = 1f, float minDistance = 5f, float maxDistance = 50f, float pitch = 1f)
        {
            if (clip == null || source3DPool.Count == 0) return;
            var src = source3DPool[poolIndex];
            poolIndex = (poolIndex + 1) % source3DPool.Count;

            src.transform.position = position;
            src.minDistance = minDistance;
            src.maxDistance = maxDistance;
            src.pitch = pitch;
            src.volume = volume * sfxVolume * masterVolume;
            src.clip = clip;
            src.Play();
        }

        public bool PlayVoiceWithCooldown(string key, AudioClip clip, float cooldownSeconds = 1.5f, float volume = 1f)
        {
            if (clip == null) return false;
            float now = Time.unscaledTime;
            if (cooldowns.TryGetValue(key, out float nextAllowed) && now < nextAllowed)
                return false;

            cooldowns[key] = now + cooldownSeconds;
            PlayVoice2D(clip, volume);
            return true;
        }
    }
}
