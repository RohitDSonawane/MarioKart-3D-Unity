using UnityEngine;

namespace HarborRacer
{
    /// <summary>Runs once before the first scene loads. the design target targets 60 fps with a 0.02 fixed step.</summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
        }
    }
}
