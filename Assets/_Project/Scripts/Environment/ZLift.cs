using UnityEngine;

namespace HarborRacer.Environment
{
    /// <summary>Marks an overlay mesh (rails, start grid) that was raised slightly above the surface it covers to stop z-fighting. Prevents lifting twice.</summary>
    public class ZLift : MonoBehaviour
    {
        public float lift;
    }
}
