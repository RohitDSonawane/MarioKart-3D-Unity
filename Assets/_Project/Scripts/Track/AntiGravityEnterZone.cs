using HarborRacer.Kart;
using UnityEngine;

namespace HarborRacer.Track
{
    /// <summary>Ref AntiGravity tag: while inside, the kart is in anti-gravity (camera hints from CameraRotateAntigravity).</summary>
    public class AntiGravityEnterZone : KartZoneBase
    {
        public bool rotateCamera;
        public float cameraRotX;
        public float cameraRotZ;
        public bool canUseBullet = true;

        public override void OnKartStay(KartController k) => k.AntiGravity?.Enter(rotateCamera, cameraRotX, cameraRotZ, canUseBullet);
    }
}
