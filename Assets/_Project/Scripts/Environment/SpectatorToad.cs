using UnityEngine;

namespace HarborRacer.Environment
{
    /// <summary>
    /// Animates spectator Toads along the track with lively cheering, hopping, and waving.
    /// Uses procedural micro-animation with random phase offsets to keep CPU overhead near zero
    /// while giving the crowds varied, energetic movement.
    /// </summary>
    public class SpectatorToad : MonoBehaviour
    {
        [SerializeField] float cheerSpeed = 4f;
        [SerializeField] float hopHeight = 0.25f;
        [SerializeField] float swayAngle = 8f;

        Vector3 initialPos;
        Quaternion initialRot;
        float phase;

        void Start()
        {
            initialPos = transform.localPosition;
            initialRot = transform.localRotation;
            phase = Random.Range(0f, Mathf.PI * 2f);
            cheerSpeed = Random.Range(3.5f, 5.5f);
            hopHeight = Random.Range(0.15f, 0.3f);
        }

        void Update()
        {
            float t = Time.time * cheerSpeed + phase;
            float hop = Mathf.Abs(Mathf.Sin(t)) * hopHeight;
            float sway = Mathf.Sin(t * 0.5f) * swayAngle;

            transform.localPosition = initialPos + Vector3.up * hop;
            transform.localRotation = initialRot * Quaternion.Euler(0f, 0f, sway);
        }
    }
}
