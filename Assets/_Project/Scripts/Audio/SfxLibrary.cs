using UnityEngine;

namespace HarborRacer.Audio
{
    [CreateAssetMenu(fileName = "SfxLibrary", menuName = "HarborRacer/SfxLibrary")]
    public class SfxLibrary : ScriptableObject
    {
        [Header("Music")]
        public AudioClip courseIntro;
        public AudioClip toadHarbor;
        public AudioClip toadHarborFinalLap;
        public AudioClip finalLapStinger;
        public AudioClip starTheme;
        public AudioClip firstPlaceFinish;
        public AudioClip goodResults;
        public AudioClip badResults;

        [Header("Engine")]
        public AudioClip engineIdle;
        public AudioClip engineAccel;
        public AudioClip engineRevStart;

        [Header("Kart SFX")]
        public AudioClip driftHop;
        public AudioClip driftSqueal;
        public AudioClip driftSteer;
        public AudioClip boostBurst;
        public AudioClip jumpPanel;
        public AudioClip gliderOpen;
        public AudioClip gliderClose;
        public AudioClip gliderFlutter;
        public AudioClip landing;
        public AudioClip wallCrash;
        public AudioClip kartBump;
        public AudioClip hoverStart;
        public AudioClip hoverFinish;
        public AudioClip hoverCharge;

        [Header("Item SFX")]
        public AudioClip rouletteTick;
        public AudioClip rouletteStop;
        public AudioClip warningBeep;
        public AudioClip coinPickup;
        public AudioClip explosion;
        public AudioClip bombFuse;
        public AudioClip bulletStart;
        public AudioClip bulletLoop;
        public AudioClip bulletEnd;
        public AudioClip crateBreak;

        [Header("Race SFX")]
        public AudioClip countdown321;
        public AudioClip countdownGo;

        [Header("Mario Voices")]
        public AudioClip[] marioBoost;
        public AudioClip[] marioTrick;
        public AudioClip[] marioHurt;
        public AudioClip marioRocketStart;
        public AudioClip marioWin;
        public AudioClip marioLose;

        [Header("Ambient")]
        public AudioClip trolleyMove;
        public AudioClip seaWaves;
        public AudioClip[] toadCheers;

        public AudioClip GetRandom(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;
            return clips[Random.Range(0, clips.Length)];
        }
    }
}
