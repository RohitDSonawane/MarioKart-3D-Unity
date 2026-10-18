using UnityEngine;

namespace HarborRacer.Kart
{
    /// <summary>
    /// Cosmetic wheel spin and front-wheel steering, plus the anti-gravity wheel flip
    /// </summary>
    public class KartVisuals : MonoBehaviour
    {
        public KartController kart;
        public Inputs.PlayerKartInput playerInput;       // optional; AI karts set steer via SetSteer
        public Transform[] frontSteerPivots;             // yaw pivots (FL, FR)
        public Transform[] spinningWheels;               // all four wheel meshes (spin around local X); parents are the pivots
        public float wheelRadius = 0.27f;
        public float maxSteerAngle = 20f;
        public float steerLerpPerSecond = 10f;
        public float antiGravityFlipAngle = 90f;
        public float antiGravityFlipSpeed = 5f;          // blend units per second

        float steerAngle;
        float spin;
        float externalSteer;
        float flipBlend;

        public void SetSteer(float s) => externalSteer = s;

        void LateUpdate()
        {
            if (kart == null) return;
            float steer = playerInput != null ? playerInput.Steer : externalSteer;
            if (kart.controlsLocked) steer = 0f;
            steerAngle = Mathf.Lerp(steerAngle, steer * maxSteerAngle, steerLerpPerSecond * Time.deltaTime);
            flipBlend = Mathf.MoveTowards(flipBlend, kart.State.AntiGravity ? 1f : 0f, antiGravityFlipSpeed * Time.deltaTime);

            foreach (var w in spinningWheels)
            {
                if (!w) continue;
                var pivot = w.parent;
                bool front = System.Array.IndexOf(frontSteerPivots, pivot) >= 0;
                float side = pivot.localPosition.x < 0f ? 1f : -1f;                     // left wheels roll one way, right the other
                Quaternion yaw = Quaternion.Euler(0f, front ? steerAngle : 0f, 0f);
                Quaternion flip = Quaternion.Euler(0f, 0f, side * antiGravityFlipAngle * flipBlend);
                pivot.localRotation = yaw * flip;
            }

            spin += kart.ForwardSpeed * Time.deltaTime / Mathf.Max(0.01f, wheelRadius) * Mathf.Rad2Deg;
            spin %= 360f;
            foreach (var w in spinningWheels)
                if (w) w.localRotation = Quaternion.Euler(spin, 0f, 0f);
        }
    }
}
