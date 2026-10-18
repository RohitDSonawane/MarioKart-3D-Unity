using HarborRacer.Kart;
using UnityEngine;

namespace HarborRacer.CameraRig
{
    /// <summary>
    /// Temporary chase camera (replaced by the full rig in Phase 10). rig at kart + up * 1.8, rotation slerps to
    /// the kart at 3/s, camera child at local (0, 0.7, -6.75). While the kart falls into the water before a respawn the
    /// camera holds still, then snaps to the kart at the checkpoint.
    /// </summary>
    public class SimpleChaseCam : MonoBehaviour
    {
        public Transform target;
        public float heightOffset = 1.8f;
        public Vector3 cameraLocalPosition = new Vector3(0f, 0.7f, -6.75f);
        public float rotateLerpPerSecond = 3f;
        Transform cam;
        KartRespawn respawn;

        void Start()
        {
            var c = GetComponentInChildren<Camera>();
            if (c == null)
            {
                var go = new GameObject("Camera") { tag = "MainCamera" };
                go.transform.SetParent(transform, false);
                c = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam = c.transform;
            cam.localPosition = cameraLocalPosition;
            cam.localRotation = Quaternion.Euler(2f, 0f, 0f);
            if (target != null)
            {
                Snap();
                respawn = target.GetComponent<KartRespawn>();
                if (respawn != null) respawn.Teleported += Snap;
            }
        }

        void OnDestroy()
        {
            if (respawn != null) respawn.Teleported -= Snap;
        }

        void Snap()
        {
            transform.position = target.position + Vector3.up * heightOffset;
            transform.rotation = target.rotation;
        }

        void LateUpdate()
        {
            if (target == null) return;
            if (respawn != null && respawn.Current == KartRespawn.Phase.Falling) return;   // hold while falling
            if (kartCtl == null) kartCtl = target.GetComponent<KartController>();
            bool gliding = kartCtl != null && kartCtl.State.Gliding;
            bool antiGrav = kartCtl != null && kartCtl.State.AntiGravity;

            // in anti-gravity / gliding the rig sits along the kart's up axis
            Vector3 up = (gliding || antiGrav) ? target.up : Vector3.up;
            transform.position = target.position + up * heightOffset;

            if (gliding && kartCtl.Glider != null)
            {
                // glide camera levels toward (glideAngleX, kart yaw, glideAngleZ) slowly
                var want = Quaternion.Euler(kartCtl.Glider.AngleX, target.eulerAngles.y, kartCtl.Glider.AngleZ);
                transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f * Time.deltaTime);
            }
            else
                transform.rotation = Quaternion.Slerp(transform.rotation, target.rotation, rotateLerpPerSecond * Time.deltaTime);
        }

        KartController kartCtl;
    }
}
