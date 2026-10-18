using UnityEngine;

namespace HarborRacer.Hazards
{
    /// <summary>
    /// a loose traffic cone. It is a plain dynamic Rigidbody that karts knock flying (the kart's velocity is
    /// rewritten every step, so cones never slow a kart); the only extra is a constant down acceleration of 100 u/s2.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PylonBody : MonoBehaviour
    {
        public float downAcceleration = 100f;      // AddForce(down * 5000 * dt, Acceleration)
        Rigidbody rb;

        void Awake() { rb = GetComponent<Rigidbody>(); }

        void FixedUpdate()
        {
            if (rb != null && !rb.isKinematic) rb.AddForce(Vector3.down * downAcceleration, ForceMode.Acceleration);
        }
    }
}
