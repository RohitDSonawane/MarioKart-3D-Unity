using HarborRacer.Items;
using HarborRacer.Race;
using UnityEngine;

namespace HarborRacer.Audio
{
    /// <summary>
    /// Manages the music state machine: Course Intro -> Race Theme -> Final Lap (stinger + fast track) ->
    /// Star override (pause race music, loop star theme, resume) -> Finish jingle -> Results.
    /// </summary>
    public class MusicController : MonoBehaviour
    {
        public static MusicController Instance { get; private set; }

        public SfxLibrary library;
        public AudioSource courseSource;
        public AudioSource overlaySource;

        [Range(0f, 1f)] public float volume = 0.8f;

        bool isFinalLap;
        bool isStarActive;
        float savedTrackTime;
        ItemHolder playerItemHolder;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (courseSource == null)
            {
                courseSource = gameObject.AddComponent<AudioSource>();
                courseSource.playOnAwake = false;
                courseSource.loop = true;
                courseSource.spatialBlend = 0f;
            }

            if (overlaySource == null)
            {
                overlaySource = gameObject.AddComponent<AudioSource>();
                overlaySource.playOnAwake = false;
                overlaySource.loop = false;
                overlaySource.spatialBlend = 0f;
            }
        }

        void Start()
        {
            if (RaceManager.Instance != null)
            {
                RaceManager.Instance.StateChanged += OnRaceStateChanged;
                RaceManager.Instance.FinalLapStarted += OnFinalLapStarted;
                RaceManager.Instance.CountdownTick += OnCountdownTick;
            }

            // Find player item holder for star theme override
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerItemHolder = playerObj.GetComponent<ItemHolder>();
            }

            // Play course intro or start course music if waiting/intro
            PlayCourseMusic(false);
        }

        void OnDestroy()
        {
            if (RaceManager.Instance != null)
            {
                RaceManager.Instance.StateChanged -= OnRaceStateChanged;
                RaceManager.Instance.FinalLapStarted -= OnFinalLapStarted;
                RaceManager.Instance.CountdownTick -= OnCountdownTick;
            }
        }

        void Update()
        {
            if (AudioService.Instance != null)
            {
                float targetVol = volume * AudioService.Instance.musicVolume * AudioService.Instance.masterVolume;
                if (!isStarActive) courseSource.volume = targetVol;
                overlaySource.volume = targetVol;
            }

            // Check star override on player
            if (playerItemHolder == null)
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null) playerItemHolder = playerObj.GetComponent<ItemHolder>();
            }

            if (playerItemHolder != null)
            {
                bool hasStar = playerItemHolder.StarLeft > 0f;
                if (hasStar && !isStarActive)
                {
                    StartStarOverride();
                }
                else if (!hasStar && isStarActive)
                {
                    EndStarOverride();
                }
            }
        }

        void OnCountdownTick(int tick)
        {
            if (library == null) return;
            if (tick > 0 && library.countdown321 != null)
            {
                AudioService.Instance?.Play2D(library.countdown321, 1f);
            }
            else if (tick == 0 && library.countdownGo != null)
            {
                AudioService.Instance?.Play2D(library.countdownGo, 1f);
            }
        }

        void OnRaceStateChanged(RaceState state)
        {
            if (state == RaceState.PlayerFinished || state == RaceState.Results)
            {
                PlayFinishMusic();
            }
        }

        void OnFinalLapStarted()
        {
            if (isFinalLap) return;
            isFinalLap = true;

            // Play final lap stinger then switch to fast theme
            if (library != null && library.finalLapStinger != null)
            {
                overlaySource.PlayOneShot(library.finalLapStinger, volume);
            }

            PlayCourseMusic(true);
        }

        public void PlayCourseMusic(bool fast)
        {
            if (library == null) return;
            var clip = fast ? (library.toadHarborFinalLap ?? library.toadHarbor) : library.toadHarbor;
            if (clip == null) return;

            courseSource.clip = clip;
            courseSource.loop = true;
            courseSource.Play();
        }

        void StartStarOverride()
        {
            isStarActive = true;
            if (library != null && library.starTheme != null)
            {
                savedTrackTime = courseSource.time;
                courseSource.Pause();
                overlaySource.clip = library.starTheme;
                overlaySource.loop = true;
                overlaySource.Play();
            }
        }

        void EndStarOverride()
        {
            isStarActive = false;
            overlaySource.Stop();
            overlaySource.loop = false;
            courseSource.time = savedTrackTime;
            courseSource.UnPause();
        }

        void PlayFinishMusic()
        {
            courseSource.Stop();
            overlaySource.Stop();

            if (library == null) return;
            var player = RaceManager.Instance != null ? RaceManager.Instance.Player : null;
            bool won = player != null && (player.FinishPlace == 1 || player.Position == 1);

            AudioClip finishClip = won ? library.firstPlaceFinish : library.goodResults;
            if (finishClip != null)
            {
                overlaySource.clip = finishClip;
                overlaySource.loop = false;
                overlaySource.Play();
            }

            // Mario win / lose voice
            if (player != null && player.isPlayer)
            {
                AudioClip voice = won ? library.marioWin : library.marioLose;
                if (voice != null)
                {
                    AudioService.Instance?.PlayVoice2D(voice, 1f);
                }
            }
        }
    }
}
