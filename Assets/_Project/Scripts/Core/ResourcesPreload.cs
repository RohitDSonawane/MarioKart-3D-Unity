using System.Collections.Generic;
using UnityEngine;

namespace HarborRacer
{
    /// <summary>
    /// Loads the Resources folders (item models, HUD sprites and fonts) while the game starts, instead of from disk on the frame an item is first
    /// thrown or an icon first shown. The loaded assets are kept referenced so they stay in memory.
    /// </summary>
    public static class ResourcesPreload
    {
        static readonly List<Object> held = new List<Object>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Load()
        {
            held.Clear();
            held.AddRange(Resources.LoadAll("Items"));
            held.AddRange(Resources.LoadAll("UI"));
        }
    }
}
